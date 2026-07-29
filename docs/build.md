# ビルド方法

[← README に戻る](../README.md)

> 文中の `§N.M` は、分割前の README 一括版のセクション番号を引き継いだ相互参照です。対応表: §1・§2・§8・§10 は [README](../README.md) 本体、§3 → [commands.md](commands.md)、§4 → [dynamo-nodes.md](dynamo-nodes.md)、§5 → [parameters.md](parameters.md)、§6 → [calculations.md](calculations.md)、§7 → [build.md](build.md)、§9 → [known-issues.md](known-issues.md)。


**3 プロジェクト構成**(2026-07-29、Dynamo ノードを Plugin から分離): Core 層は AutoCAD 非依存のため WSL / Linux でもビルド・テストできる。Plugin 層(AutoCAD コマンド)は AutoCAD 本体 DLL が必要、Dynamo 層(Zero Touch Node)は `DynamoServices.dll` のみが必要で、いずれも無い環境ではスタブで構文検証まで行う。

```bash
# Core + テスト(AutoCAD 不要。674 件が green であること)
dotnet test tests/SheetPileQuayWall.Core.Tests -c Release

# Plugin(AutoCAD コマンド)の構文検証(AutoCAD 不要。スタブとリンクする。配布不可)
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release -p:UseAutoCadStubs=true

# Dynamo(Zero Touch Node)の構文検証(AutoCAD/Dynamo 不要。スタブとリンクする。配布不可)
dotnet build src/SheetPileQuayWall.Dynamo/SheetPileQuayWall.Dynamo.csproj -c Release -p:UseAutoCadStubs=true

# Plugin の実機ビルド(AutoCAD 必須)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-dll-versions.ps1   # exit 0 を確認
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release

# Dynamo の実機ビルド(Civil 3D 同梱の Dynamo 3.3 必須)
dotnet build src/SheetPileQuayWall.Dynamo/SheetPileQuayWall.Dynamo.csproj -c Release
```

AutoCAD / Civil 3D が既定パス以外にある場合は `-p:AcadRoot="..."` を指定する。Dynamo プロジェクトは `$(AcadRoot)\C3D\Dynamo\Core\DynamoServices.dll` を既定の参照元とする(`-p:DynamoRoot="..."` で個別に上書きも可)。

### プロジェクトの置き場所を固定する(推奨。フォルダ乱立の根本対策)

社内ルールで `git clone` / `git pull` が使えず GitHub の ZIP を都度ダウンロードする運用のため、そのままだと展開のたびに「新しいフォルダー (N)」が増え続け、後述の NU1105 / CS0006 が繰り返し発生する。

[`scripts/update-project.bat`](scripts/update-project.bat) を**プロジェクトフォルダの外**(デスクトップなど)へ 1 回だけコピーしておくと、以後は新しい ZIP をこのファイルへ**ドラッグ&ドロップするだけ**で、同じ固定フォルダ(`update-project.bat` と同じ場所の `009_sheet-pile-quaywall`)への上書き展開・キャッシュ削除・`dotnet restore`・`dotnet build` までを自動実行する。フォルダが増えないため NU1105 自体が起きなくなる。

> プロジェクトフォルダの**中**に置くと、更新のたびに自分自身が上書き対象に巻き込まれるため、必ず外に置くこと。

### Visual Studio で NU1105(プロジェクト情報が見つかりません)/ CS0006(メタデータファイルが見つかりません)が出た場合

`update-project.bat` を使わず手動で ZIP を展開している場合、`obj` / `.vs` フォルダへ前のフォルダ名を含む古いキャッシュが残り、NuGet の依存関係解決やプロジェクト間のビルド成果物の参照が壊れることがある。[`scripts/fix-restore.bat`](scripts/fix-restore.bat) をダブルクリックすると、`.vs` / `obj` / `bin` の削除、`dotnet restore`、`dotnet build`(Core → Plugin の順)までを自動実行する。実行後は Visual Studio を再起動し、`SheetPileQuayWall.Plugin.csproj` を開き直すこと。

> `-p:Platform=x64` を手動ビルドコマンドに付けないこと。`Core.csproj` には `<Platforms>` の指定が無く、`Platform` を強制すると出力先が `bin\x64\...` という Visual Studio が期待しない場所になり、CS0006 の原因になる(実機で確認済み)。

### AutoCAD / Civil 3D への登録(NETLOAD)

ビルドした Plugin DLL は自動ロード設定を持たないため、起動のたびに手動でロードする。

1. AutoCAD 2025 または Civil 3D 2025 を起動する
2. コマンドラインに `NETLOAD` と入力し Enter
3. ファイル選択ダイアログで実機ビルドの出力 `src\SheetPileQuayWall.Plugin\bin\Release\net8.0-windows\SheetPileQuayWall.Plugin.dll` を選択する
4. ロード完了後、§3 の全 19 コマンド(`SPQW_FRONTWALL_Create` 等)がコマンドラインから実行可能になる

> スタブビルド(`-p:UseAutoCadStubs=true`)の出力は `AutoCadStubs.dll` を含むため NETLOAD しないこと(配布不可。§7 冒頭参照)。

### Dynamo への登録(Import Library)

Dynamo ノードは AutoCAD コマンドとは**別の DLL**(`SheetPileQuayWall.Dynamo.dll`)にあり、Dynamo 側の設定に永続登録されないため、グラフを開くたびに手動でインポートする。

1. Civil 3D 2025 で `DYNAMO` コマンドを実行し Dynamo を起動する
2. 左側のライブラリペイン下部の「Import Library...」から、実機ビルドの出力 `src\SheetPileQuayWall.Dynamo\bin\Release\net8.0-windows\SheetPileQuayWall.Dynamo.dll` を選択する(**`SheetPileQuayWall.Plugin.dll` ではない**。§4.1 参照)
3. インポート後、ノード検索に `SpqwNodes.` と入力すると §4.2〜4.10 の 7 ノードが、`SpqwGeometryNodes.` と入力すると §4.11 の 3 ノード(実験的・未検証)が一覧表示される(§4.1 共通仕様)

AutoCAD コマンド(NETLOAD、`SheetPileQuayWall.Plugin.dll`)と Dynamo ノード(Import Library、`SheetPileQuayWall.Dynamo.dll`)は別 DLL・独立した登録操作であり、使う方だけ実行すればよい。**本手順は実機 AutoCAD / Civil 3D 環境で未検証**(§9.5「実機動作確認」参照)。

### プロジェクト構成

```
009_sheet-pile-quaywall/
├── src/
│   ├── SheetPileQuayWall.Core/          AutoCAD 非依存の計算層(BCL のみ参照)
│   │   ├── Point3.cs / PileGeometry.cs / FrontWallRef.cs
│   │   ├── CrossMemberValidator.cs      部材間整合チェック(§8 の 4 組)
│   │   ├── QuayWallEstimate.cs          施設 1 件分の数量集計
│   │   ├── FrontWall/                   007 移植 8 + 新規 6
│   │   │                                 PieceAssignment / FrontWallPlacement / JointMass
│   │   │                                 WallLayout(施設全長→本数の壁一括レイアウト)
│   │   │                                 DriveEstimate(打撃)/ VibroEstimate(バイブロ単独)
│   │   │                                 VibroJetEstimate(ジェット併用)
│   │   │                                 DriveEquipment(打撃工法の杭打機・杭打船選定)
│   │   ├── TieRod/                      008 移植 5 + 新規 2(TieRodPlacement /
│   │   │                                 TieRodPitch「矢板何本ごと」→取付間隔・組数)
│   │   ├── AnchorPile/                  006@6d6d8cf 由来(書き直し)4 + 新規 1
│   │   │                                 (AnchorDriveEstimate。4節3-4.6 陸上打設)
│   │   ├── Import/                      帳票 CSV 取り込み(CsvTable・各部材の Importer・
│   │   │                                 SpecTextParser・QuantityReconciliation)6 ファイル
│   │   └── Geotech/                      柱状図から加重平均N値・一軸圧縮強度を算出
│   │                                     (BoringLog.cs。CsvTable・JetLayerType を再利用)
│   ├── SheetPileQuayWall.Plugin/        AutoCAD 依存層(AcCoreMgd/AcDbMgd/AcMgd を参照)
│   │   ├── Commands/                    SPQW_* 19 コマンド(8 ファイル。ImportCommands が
│   │   │                                 帳票取り込み 4 コマンド、AnchorDriveEstimateCommand
│   │   │                                 が控え杭の打撃工法・陸上打設を担当)
│   │   ├── XData/                       XDataStore(キー=値 + 1011)+ 3 部材のレコード
│   │   └── DrawingHelper.cs / Prompt.cs / SolidBuilder.cs
│   └── SheetPileQuayWall.Dynamo/        Dynamo Zero Touch Node 層(2026-07-29 新設。
│       │                                 AutoCAD 本体 DLL は参照しない)
│       ├── SpqwNodes.cs                  DynamoServices.dll のみ参照。7 ノード(断面性能・
│       │                                 数量集計・柱状図解析・打設歩掛積算 4 系統)。すべて
│       │                                 AutoCAD 非依存の純計算(Core 呼び出しのみ)
│       └── SpqwGeometryNodes.cs          ProtoGeometry.dll 参照(実験的・未検証)。3 ノード
│                                         (控え杭・前壁本体円筒・タイロッドのソリッド生成。
│                                         XData 無し)
├── stubs/                               AutoCAD/Dynamo API スタブ(構文検証専用。配布禁止)
├── tests/SheetPileQuayWall.Core.Tests/  xUnit 674 ケース + fixtures/
├── scripts/
│   ├── port-from-legacy.sh              007/008 からの冪等移植
│   └── verify-dll-versions.ps1          参照 DLL バージョン実測(CLAUDE.PRIVATE.md §9)
└── docs/
    ├── implementation-plan.md           設計決定 1〜11・フェーズ計画・実機検証項目(§13.5)
    ├── features.html                    機能概要(図表中心)
    └── samples/                          帳票 CSV 5 種 + 柱状図 CSV(§5.5・§5.6)
```

### レガシー 3 リポジトリからの移植

Core の一部は 006/007/008 から移植したもので、`scripts/port-from-legacy.sh` が再現する。移植元は `git show <commit>:<path>` で取り出すためレガシー側の作業ツリー状態に影響されず、冪等(再実行後に `git diff` が空なら同期済み)。

| 移植元 | 移植先 namespace |
|---|---|
| `SteelPipeSheetPile.Data`(007@`b12b188`) | `SheetPileQuayWall.Core.FrontWall` |
| `TaiRod.Core`(008@`ff3a986`) | `SheetPileQuayWall.Core.TieRod` |
| `006@6d6d8cf` の継手判定・整列計算 | 手作業で抽出・書き直し(スクリプト対象外) |
