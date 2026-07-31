#!/usr/bin/env python3
"""xlsx からセルテキストを抽出する(標準ライブラリのみ)。

docs/references/アライズ鋼管矢板岸壁.xlsx は ZIP エントリ名の区切りが
'\\' の非標準 xlsx で、openpyxl / pandas はそのままでは開けない
(KeyError: 'xl/sharedStrings.xml')。ここではエントリ名を '/' に正規化
してから直接パースするため、標準・非標準どちらの xlsx も読める。

使い方:
    python3 scripts/extract-xlsx-text.py <xlsx> [--format text|json] [--out <file>]
"""

import argparse
import json
import sys
import xml.etree.ElementTree as ET
import zipfile

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
RNS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG_RNS = "{http://schemas.openxmlformats.org/package/2006/relationships}"


def read_entries(path):
    """ZIP エントリ名を '/' 区切りに正規化した dict を返す。"""
    with zipfile.ZipFile(path) as z:
        return {i.filename.replace("\\", "/"): z.read(i.filename) for i in z.infolist()}


def rich_text(node):
    """<si> / <is> のテキストを連結する(<rPh> のふりがなは除外)。"""
    parts = []
    direct = node.find(NS + "t")
    if direct is not None:
        parts.append(direct.text or "")
    for run in node.findall(NS + "r"):
        parts.append(run.findtext(NS + "t") or "")
    return "".join(parts)


def read_shared_strings(entries):
    data = entries.get("xl/sharedStrings.xml")
    if data is None:
        return []
    return [rich_text(si) for si in ET.fromstring(data).findall(NS + "si")]


def sheet_list(entries):
    """(シート名, エントリ名) をブック内の並び順で返す。"""
    workbook = ET.fromstring(entries["xl/workbook.xml"])
    rels = ET.fromstring(entries["xl/_rels/workbook.xml.rels"])
    targets = {r.get("Id"): r.get("Target") for r in rels.findall(PKG_RNS + "Relationship")}
    sheets = []
    for sheet in workbook.find(NS + "sheets"):
        target = targets[sheet.get(RNS + "id")].lstrip("/")
        if not target.startswith("xl/"):
            target = "xl/" + target
        sheets.append((sheet.get("name"), target))
    return sheets


def cell_text(cell, shared):
    kind = cell.get("t")
    if kind == "inlineStr":
        inline = cell.find(NS + "is")
        return rich_text(inline) if inline is not None else ""
    value = cell.findtext(NS + "v")
    if value is None:
        return ""
    if kind == "s":
        index = int(value)
        return shared[index] if index < len(shared) else ""
    return value


def read_rows(data, shared):
    """空セル・空行を除いた行データを返す。"""
    rows = []
    for row in ET.fromstring(data).iter(NS + "row"):
        cells = []
        for cell in row.findall(NS + "c"):
            text = cell_text(cell, shared).strip()
            if text:
                cells.append({"ref": cell.get("r"), "text": text})
        if cells:
            rows.append({"row": int(row.get("r")), "cells": cells})
    return rows


def extract(path):
    entries = read_entries(path)
    shared = read_shared_strings(entries)
    return [
        {"name": name, "rows": read_rows(entries[target], shared)}
        for name, target in sheet_list(entries)
    ]


def to_text(sheets):
    lines = []
    for sheet in sheets:
        lines.append("########## SHEET: " + sheet["name"])
        for row in sheet["rows"]:
            joined = " | ".join(c["text"] for c in row["cells"])
            lines.append("[%d] %s" % (row["row"], joined))
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="xlsx からセルテキストを抽出する")
    parser.add_argument("xlsx", help="入力 xlsx ファイル")
    parser.add_argument("--format", choices=["text", "json"], default="text", help="出力形式")
    parser.add_argument("--out", help="出力先ファイル(省略時は標準出力)")
    args = parser.parse_args()

    sheets = extract(args.xlsx)
    if args.format == "json":
        body = json.dumps({"source": args.xlsx, "sheets": sheets}, ensure_ascii=False, indent=2)
    else:
        body = to_text(sheets)

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(body + "\n")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(body)


if __name__ == "__main__":
    main()
