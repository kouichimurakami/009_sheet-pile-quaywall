#!/usr/bin/env python3
"""アライズ設計計算書 xlsx から Dynamo 入力用の中間 JSON を生成する。

セル参照と 009 パラメータの対応は docs/references/アライズ計算書-009パラメータ対応表.md
による。計算書から決まらない 3 項目 (tie_rod.rod_d / span.definition / site.wall_length)
は null または "要確認" で出力し、validation.errors に記録する。人が JSON を編集して
埋めたあと --check で再検証する。

validation が扱うのは「Dynamo ノード側で検査できない項目」だけである。径・肉厚・全長の
範囲、タイロッド取付間隔が矢板ピッチの整数倍であること等は SpqwGeometryNodes の各ノードが
Core の Validate() で検査するため、ここでは重複して検査しない。

使い方:
    python3 scripts/build-design-input.py <xlsx> --out <json>   # 生成
    python3 scripts/build-design-input.py --check <json>        # 編集後の再検証

終了コード: 0 = 検証エラーなし / 1 = 検証エラーあり / 2 = 入出力エラー
"""

import argparse
import importlib.util
import json
import math
import os
import pathlib
import re
import sys

# 誤差許容 1 mm (CLAUDE.PRIVATE.md §6-5)
TOL_M = 0.001

NUMBER = re.compile(r"[-+]?\d+(?:\.\d+)?")


def load_extractor():
    """同じ scripts/ にある抽出スクリプトをモジュールとして読み込む。"""
    path = pathlib.Path(__file__).with_name("extract-xlsx-text.py")
    spec = importlib.util.spec_from_file_location("xlsx_text", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def numbers(text):
    return [float(v) for v in NUMBER.findall(text)]


def last_number(cells, ref):
    values = numbers(cells[ref])
    if not values:
        raise ValueError("セル %s に数値がありません: %r" % (ref, cells[ref]))
    return values[-1]


def joint_code(text):
    """継手の記載 '(L-T)型[L-75x75x9]' を 009 の継手コードへ変換する。"""
    match = re.search(r"\[L-(\d+)x", text)
    if match:
        return "LT" + match.group(1)
    if "(P-P)" in text:
        return "PP"
    if "(P-T)" in text:
        return "PT"
    raise ValueError("継手形式を判別できません: %r" % text)


def joint_effective_width(outer_d_m, code):
    """有効幅 B [m]。src/SheetPileQuayWall.Core/FrontWall/JointGeometry.cs と同じ式。
    照合専用のため、式を持たない LT100 と PP/PT は None を返す。"""
    r = outer_d_m / 2.0
    if code == "LT65":
        return r + 0.076 + math.sqrt(r * r - 0.080 * 0.080)
    if code == "LT75":
        return r + 0.0855 + math.sqrt(r * r - 0.090 * 0.090)
    return None


def build(xlsx_path):
    sheets = load_extractor().extract(xlsx_path)
    if len(sheets) < 2:
        raise ValueError("シートが 2 枚ありません: %s" % xlsx_path)
    s0 = {c["ref"]: c["text"] for r in sheets[0]["rows"] for c in r["cells"]}
    s1 = {c["ref"]: c["text"] for r in sheets[1]["rows"] for c in r["cells"]}

    front = s0["A106"].split()   # NO 外径(mm) 厚さ(mm) 継手
    anchor = s0["A166"].split()  # NO 外径(mm) 厚さ(mm) I(cm4) Z(cm3)
    waling = s0["A129"].split()  # H B t1 t2 I Z (H〜t2 は mm)

    return {
        "source": {
            "xlsx": os.path.basename(xlsx_path),
            "mapping": "docs/references/アライズ計算書-009パラメータ対応表.md",
            "generator": "scripts/build-design-input.py",
            "unit": "長さは全てメートル、角度は度 (CLAUDE.PRIVATE.md §2.1)",
        },
        "front_wall": {
            "outer_d": float(front[1]) / 1000.0,
            "wall_t": float(front[2]) / 1000.0,
            "joint": joint_code(front[3]),
            "grade": s0["A92"].split()[-1],
            "head_z": last_number(s0, "A64"),
            "tip_z": float(s0["Q274"]),
            "pile_pitch": last_number(s1, "A295") / 100.0,  # A295 のみ cm
        },
        "tie_rod": {
            "rod_d": None,  # 計算書はタイブル型番 F270T のみで径の記載が無い
            "tie_elev": last_number(s0, "A65"),
            "tie_spacing": last_number(s0, "A116"),
            "hwl": last_number(s0, "A49"),
            "waling_h": float(waling[0]) / 1000.0,
            "anchor_reaction": numbers(s1["A581"])[0],
        },
        "anchor_pile": {
            "outer_d": float(anchor[1]) / 1000.0,
            "wall_t": float(anchor[2]) / 1000.0,
            "length": last_number(s1, "A1157"),
            "tip_z": last_number(s1, "A1163"),
            "head_to_tie": last_number(s0, "A67"),
            "incl_deg": 0.0 if "直杭" in s0["A14"] else None,
            "closed_tip": False,
        },
        "span": {
            "center_to_center": last_number(s1, "A2029"),
            "definition": "要確認",
        },
        "site": {
            "wall_length": None,
            "origin_x": 0.0,
            "origin_y": 0.0,
        },
    }


def derive(doc):
    fw, tr, ap = doc["front_wall"], doc["tie_rod"], doc["anchor_pile"]
    sp, site = doc["span"], doc["site"]

    span_009 = None
    if sp["definition"] == "center":
        span_009 = round(sp["center_to_center"] + ap["outer_d"] / 2.0, 3)
    elif sp["definition"] == "land_face":
        span_009 = sp["center_to_center"]

    pile_count = None
    tie_count = None
    if site["wall_length"]:
        pile_count = math.ceil(site["wall_length"] / fw["pile_pitch"] - TOL_M)
        tie_count = int(site["wall_length"] // tr["tie_spacing"])

    return {
        "front_wall_length": round(fw["head_z"] - fw["tip_z"], 3),
        "anchor_head_z": round(ap["tip_z"] + ap["length"], 3),
        "span_009": span_009,
        "pile_count": pile_count,
        "tie_count": tie_count,
    }


def validate(doc, derived):
    """Dynamo ノードが検査できない項目だけを検査する。"""
    fw, tr, ap = doc["front_wall"], doc["tie_rod"], doc["anchor_pile"]
    sp, site = doc["span"], doc["site"]
    errors = []
    warnings = []

    # 1. 計算書から決まらない 3 項目。埋まるまでモデルを生成してはならない。
    if tr["rod_d"] is None:
        errors.append(
            "tie_rod.rod_d が未設定です。タイブル F270T の呼び径をメーカーカタログで"
            "確認し、009 のカタログ規格径 (φ25〜φ90) をメートルで記入してください。")
    if sp["definition"] not in ("center", "land_face"):
        errors.append(
            'span.definition が "要確認" のままです。計算書の間距離定義図を確認し、'
            '"center" (前壁中心〜控え杭中心) または "land_face" '
            "(前壁中心〜控え杭陸側定着面) を記入してください。誤ると控え杭位置が "
            "%.3f m ずれます。" % (ap["outer_d"] / 2.0))
    if site["wall_length"] is None:
        errors.append(
            "site.wall_length が未設定です。計算書は 1 断面の計算のため施設延長を"
            "含みません。現場条件から記入してください。")
    if ap["incl_deg"] is None:
        errors.append("anchor_pile.incl_deg を判別できませんでした。控え工形式を確認してください。")

    # 2. 部材間にまたがる標高の整合 (ノード側では検査されない)
    #    前壁の全長は head_z − tip_z の派生量であり独立した比較対象が無いため検査しない。
    if tr["tie_elev"] >= fw["head_z"] - TOL_M:
        errors.append(
            "タイロッド軸心標高 %.3f m が前壁の杭上端標高 %.3f m 以上です。"
            "タイ材は矢板天端より下に取り付きます。"
            % (tr["tie_elev"], fw["head_z"]))
    gap = derived["anchor_head_z"] - tr["tie_elev"]
    if abs(gap - ap["head_to_tie"]) > TOL_M:
        errors.append(
            "控え杭天端 %.3f m − タイロッド軸心標高 %.3f m = %.3f m が、"
            "控え工〜タイ材までの長さ %.3f m と一致しません。"
            % (derived["anchor_head_z"], tr["tie_elev"], gap, ap["head_to_tie"]))

    # 3. 計算書のピッチと 009 の継手式の照合 (どのノードも行わない)
    computed = joint_effective_width(fw["outer_d"], fw["joint"])
    if computed is None:
        warnings.append(
            "継手 %s は 009 に有効幅の算定式が無いため、pile_pitch %.5f m を照合できません。"
            % (fw["joint"], fw["pile_pitch"]))
    elif abs(computed - fw["pile_pitch"]) > TOL_M:
        warnings.append(
            "計算書の pile_pitch %.5f m が 009 の継手式による有効幅 %.5f m と "
            "%.1f mm 違います (継手 %s、外径 %.3f m)。"
            % (fw["pile_pitch"], computed, abs(computed - fw["pile_pitch"]) * 1000.0,
               fw["joint"], fw["outer_d"]))

    return {"errors": errors, "warnings": warnings}


def refresh(doc):
    """入力値から派生量と検証結果を計算し直す。"""
    doc["derived"] = derive(doc)
    doc["validation"] = validate(doc, doc["derived"])
    return doc


def report(doc):
    v = doc["validation"]
    for message in v["warnings"]:
        print("警告: " + message)
    for message in v["errors"]:
        print("エラー: " + message)
    print("検証: エラー %d 件 / 警告 %d 件" % (len(v["errors"]), len(v["warnings"])))
    return 1 if v["errors"] else 0


def write(doc, path):
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(doc, ensure_ascii=False, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser(
        description="設計計算書 xlsx から Dynamo 入力用の中間 JSON を生成する")
    parser.add_argument("xlsx", nargs="?", help="入力 xlsx ファイル")
    parser.add_argument("--out", help="出力 JSON")
    parser.add_argument("--check", metavar="JSON",
                        help="既存 JSON の派生量・検証を再計算して上書きする")
    parser.add_argument("--overwrite", action="store_true",
                        help="--out の既存ファイルを上書きする (手編集を失うため既定は拒否)")
    args = parser.parse_args()

    sys.stdout.reconfigure(encoding="utf-8")

    if args.check:
        with open(args.check, encoding="utf-8") as f:
            doc = json.load(f)
        refresh(doc)
        write(doc, args.check)
        return report(doc)

    if not args.xlsx or not args.out:
        parser.error("生成には <xlsx> と --out の両方が必要です")
    if os.path.exists(args.out) and not args.overwrite:
        print("エラー: %s は既に存在します。手編集を上書きしないため中止しました。"
              "再生成するには --overwrite を付けてください。" % args.out)
        return 2

    doc = refresh(build(args.xlsx))
    write(doc, args.out)
    print("生成: %s" % args.out)
    return report(doc)


if __name__ == "__main__":
    sys.exit(main())
