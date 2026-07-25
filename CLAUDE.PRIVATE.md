# CLAUDE.md


# 1.プロジェクト概要
## 目的

**鋼管矢板式岸壁**(前壁鋼管矢板 + タイロッド + 控え杭)を、単一の DLL で完結してパラメトリック 3D モデル生成・積算(施工歩掛計算)できるようにする。

本プロジェクトは以下 3 リポジトリの後継・統合版として作成する。**009 単独で(006 / 007 / 008 を参照・依存せずに)ビルド・実行できること**が必須要件。

| 由来 | 内容 | 採否 |
|---|---|---|
| `006_steel-pipe-pile` | 前壁鋼管矢板(簡易継手・傾斜角・XData 挿入点追随)+ 控え杭(タイロッド軸線整列) | 検討中(§ アーキテクチャ確定待ち) |
| `007_steel-pipe-sheet-pile` | 前壁鋼管矢板(実形状継手 LT65/75/100・PP・PT、断面性能、打設歩掛積算) | 検討中(同上) |
| `008_tairod` | タイロッド(Core/Plugin 分割、172 テストで検証済みの計算層) | 検討中(同上) |

どの実装を土台にするか、プロジェクト構成(単一プロジェクト or Core/Plugin 分割)は未確定。詳細は README.md の提案セクションを参照。

## ターゲット環境

C# / .NET 8.0、net8.0-windows、x64
AutoCAD 2025 .NET, Civil3D 2025, Dynamo Zero Touch Node

## 参照ドキュメント

ルートにある参照文書:

| ファイル | 用途 |
|---|---|
| `港湾土木請負工事積算基準 令和7年度改訂版.md` | 港湾工事積算基準の OCR テキスト |
| `港湾工事工種体系ツリー.md` | 港湾工事の工種体系(レベル0〜レベル6)定義 |
| `体系階層(レベル)の定義.md` | 工事工種体系のレベル定義説明 |


## AutoCAD 2025 と Civil 3D 2025 の C# 開発資料の整理

https://aps.autodesk.com/ja/developer/overview/autocad-api

https://blog.autodesk.io/autocad-2025-dotnet8-migration/

https://www.autodesk.com/jp/support/technical/article/caas/tsarticles/tsarticles/JPN/ts/1txCL4leYa6fEP0lW2pBCD.html

https://help.autodesk.com/view/CIV3D/2025/JPN/

https://help.autodesk.com/view/CIV3D/2025/JPN/?guid=GUID-ECDDB244-F5B9-4FFF-AF02-86CB951540F4


---

## 2. C# コーディング規約

### 2.1 必須ルール

- **`using` ディレクティブを使わない**。型は完全修飾名で書く(例:`Autodesk.AutoCAD.DatabaseServices.Solid3d`)。
- 参照 DLL は `<Private>False</Private>`(Copy Local = False)必須 — AutoCAD 本体 DLL を配布物に同梱しない。
- mm 単位を絶対に混入しない。**単位はメートル統一**。
- Dynamo Zero Touch Node の戻り値は `[MultiReturn]` で辞書キーに日本語を用いる(将来拡張時)。
- 英語パラメータ名と日本語説明の対応は `Dictionary<string, string>` で内部に保持する。

### 2.2 座標系

| 軸 | 方向 | 備考 |
|---|---|---|
| X | 陸側 → +X、海側 → −X | 法線直角方向(施設延長直角方向) |
| Y | 施設延長方向 | 法線平行方向 |
| Z | 鉛直上向き(+Z が上) | **Z = 0 を鋼管矢板上部工の法線標高 D.L.(基本水準面)に統一**(前壁・タイロッド・控え杭すべて共通。決定日 2026-07-25、詳細は `docs/implementation-plan.md` §2 参照) |

原点は D.L. 上の任意点。各構造物の標高パラメータ(杭先端標高・タイロッド軸心標高等)はすべて D.L. 基準の数値でそのまま Z 座標になる。平面位置(X, Y)は UCS でクリック取得後 WCS へ変換する(008_tairod の手順を踏襲)か、他部材からの整列計算で自動算出する。

### 2.3 3D モデル作成方針

- **断面ポリライン → Region → Extrude** の順で生成。単純形状(円柱等)は `CreateFrustum` 直接生成も許容する(008 の逸脱事例を参照)。
- 最終的に **Solid3d 1 個** に集約する(複数ソリッドのままにしない)。集約は `BooleanOperation.BoolUnite`(複数外形の合成)または `BoolSubtract`(外形から内空を抜く)を用途に応じて選択。
- パラメータは XData RegAppId(プロジェクト固有名)に保存し、後から `Action` コマンドで再生成できる構造にする。
- ソリッドの生成・修正後は、同じ日本語名のレイヤーを作成して、同レイヤーに分類する。
- ユーザーがソリッドにカラー設定できる機能を組み込む。

### 2.4 AutoCAD コマンド命名パターン

| コマンド | 役割 |
|---|---|
| `<STRUCT>_Create` | パラメータ入力 → Solid3d 生成 → XData 記録 |
| `<STRUCT>_Action` | 既存エンティティ選択 → パラメータ再入力 → 同位置再生成 |
| `<STRUCT>_Query`  | XData 読取 → 諸元・派生量・積算数量を出力 |

### 2.5 既存コード体系の保護

- 追加・修正は既存コマンド体系を壊さない。
- 変更箇所はチャット出力で明示する(diff 的に「ここを足した／ここを置換した」と言う)。

---

## 3. パラメータ設計

- 何をパラメータとするかは **複数案を提示してから推奨案を示す**(A/B/C のうち推奨は B、など)。黙って選ばない。
- パラメータ表は「英語名 / 日本語名 / 単位 / デフォルト値 / 範囲」の 5 列で書く。
- 派生量は信頼度ラベル(**確定** / **概算** / **推定**)を付与する。

---

## 4. OCR / JSON ワークフロー

推奨ワークフロー:
1. 仕様書をスキャン → PNG/JPEG で保存
2. Python スクリプトで Claude Vision → JSON 出力
3. JSON を確認・手修正(品質チェック)
4. JSON から C# / Python / Command を一括生成
5. Claude Code CLI でビルドエラー修正
6. Dynamo で動作確認

### OCR JSON フォーマット

```json
{
  "contents": [
    [
      {
        "boundingBox": [[x,y],[x,y],[x,y],[x,y]],
        "id": 0,
        "isVertical": "true",
        "text": "テキスト内容",
        "isTextline": "true",
        "confidence": 0.96
      }
    ]
  ]
}
```

`isVertical` と `boundingBox` の位置関係から読み順を推定する。`confidence` が低い要素は手動確認が必要。

---

## 5. ビルド

WSL 環境に .NET SDK を未インストールの場合Windows 側で Visual Studio 2026 を使ってビルドする。**コード変更時は構文レベルのレビューに留める**。

```bash
# スタブが必要な場合(AutoCAD API 未インストール環境)
cd stubs && dotnet build build_stubs.csproj -c Release

# プラグイン本体
cd src/<ProjectName> && dotnet build -c Release
```

---

## 6. コード生成出力フォーマット(固定順)

C# コード等を生成する際は以下の順で出力する:

1.  複数の計画案を提案する。
2.  計画案からコード開発に必要なテストをすべて提案する
2. 「ビルドに必要な参照アセンブリ一覧」と「AutoCAD / Civil3D / Dynamo Zero Touch Nodeで実行するコマンド名と説明」を全て表示
3.  入力パラメータ表(英語名 / 日本語名 / 単位 / デフォルト値 / 範囲)
4.  派生量(自動算出)の表(英語名 / 計算式 / デフォルト値)
5.  パラメータ整合性チェック(不一致時はエラー停止、再生成しない。誤差許容:1 mm = 0.001 m)
    例:`toe_length + stem_thickness + heel_length == footing_width` が成立すること
6.  完全なコード
7.  注意点
8.  .gitignore 影響の有無

---

## 7. README.md の記載内容

1. 概略図
2. 参照アセンブリ
3. AutoCad Civil3D コマンド
4. Civil3D Dynamo ノード一覧 グラフ配線イメージ デフォルト値 
5. 入力パラメータ
6. 計算値(自動算出)
7. ビルド方法
8. 規約・制約
9. 既存の README.md が存在する場合、変更点を示す

---

## 8. 禁止事項

- `using` ディレクティブを書く
- mm 単位の値を混入する
- Z 軸下向きの座標を使う
- 3D ソリッドを複数のまま残す(`BoolUnite` / `BoolSubtract` 忘れによる集約抜け)
- AutoCAD 本体 DLL を出力先にコピーする(`<Private>False</Private>` を省く)
- **006 / 007 / 008 のプロジェクト参照・アセンブリ参照を追加する**(009 は単独ビルド・単独実行が要件。共通ロジックは 009 内にコードとして移植する。将来 NuGet パッケージ化して共有する場合は改めて協議する)

## 9. 参照DLLバージョン確認ルール(コード生成前に必須実行)

### 9.1 原則
コードを書き始める前に、**必ず** `.csproj` の参照アセンブリと実行環境(Civil 3D 2025 / Dynamo 3.3)のDLLバージョンを突き合わせる。一致が確認できるまでコードを生成しない。バージョン不一致は `TypeLoadException` / `MissingMethodException` をランタイムで引き起こし、ビルド成功では検出できないため事前検証が必須。

### 9.2 実行手順(コード生成前に必ずこの順で実施)

**Step 1: .csproj から期待バージョンを抽出**
- `<Reference>` の `HintPath` と `SpecificVersion` を読む
- `CopyLocal=false` であることを確認(AutoCAD系DLLは必ず false)

**Step 2: 実環境のDLLバージョンを PowerShell で実測**
```powershell
# AutoCAD / Civil 3D 系
Get-ChildItem "C:\Program Files\Autodesk\AutoCAD 2025\Ac*Mgd.dll",
              "C:\Program Files\Autodesk\AutoCAD 2025\C3D\Aecc*Mgd.dll" |
  Select-Object Name,
                @{N='FileVersion';E={$_.VersionInfo.FileVersion}},
                @{N='ProductVersion';E={$_.VersionInfo.ProductVersion}}

# Dynamo 3.3 系(Civil 3D 同梱版)
Get-ChildItem "C:\Program Files\Autodesk\AutoCAD 2025\C3D\Dynamo\Core\*.dll" |
  Where-Object { $_.Name -match "^(Dynamo|ProtoGeometry|DSCore)" } |
  Select-Object Name, @{N='Version';E={$_.VersionInfo.FileVersion}}
```

WSL2 から実行する場合は `powershell.exe -Command "..."` を経由する。

**Step 3: 期待バージョンマトリクスと突き合わせ**

| DLL | メジャー期待値 | 備考 |
|---|---|---|
| AcCoreMgd.dll / AcDbMgd.dll / AcMgd.dll | 25.x | AutoCAD 2025 系 |
| AeccDbMgd.dll / AeccDbRoadwayMgd.dll など Aecc*Mgd | Civil 3D 2025 同梱版 | バージョン番号体系が AutoCAD と異なる点に注意 |
| ProtoGeometry.dll | 3.3.x | Dynamo 3.3 |
| DynamoServices.dll / DynamoCoreWpf.dll | 3.3.x | 同上 |
| DSCoreNodes.dll | 3.3.x | Zero Touch Node 用 |

**Step 4: 不一致が出た場合の動作**
コードを生成せず以下を出力して停止する:
1. 検出された実バージョン一覧
2. .csproj が期待しているバージョン
3. 推奨対処(HintPath 修正 / 別環境への切替 / バージョン統一)

ユーザーの判断を待つこと。**推測で書き進めない。**

### 9.3 出力時の必須記載
コード生成時は以下をチャット冒頭とソースファイル先頭コメントの両方に記載する:

```csharp
// === 参照DLLバージョン検証済み ===
// AcCoreMgd.dll     : 25.x.x.x  (C:\Program Files\Autodesk\AutoCAD 2025\)
// AcDbMgd.dll       : 25.x.x.x  (同上)
// AeccDbMgd.dll     : XX.x.x.x  (C:\Program Files\Autodesk\AutoCAD 2025\C3D\)
// ProtoGeometry.dll : 3.3.x.x   (...\C3D\Dynamo\Core\)
// 検証日: YYYY-MM-DD
// 検証コマンド: scripts/verify-dll-versions.ps1
```

未検証のDLLがある場合は `未検証` と明記し、その状態でビルドした場合のリスクを併記する。

### 9.4 検証スクリプトの恒久化
プロジェクトルートに `scripts/verify-dll-versions.ps1` を配置し、上記Step 2のコマンドを保存する。CI/ローカル両方で再実行可能にする。pre-commit hook で実行を強制すると更に堅牢。
