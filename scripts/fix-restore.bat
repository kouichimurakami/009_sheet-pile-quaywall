@echo off
rem NU1105 (プロジェクト情報が見つかりません) や CS0006 (メタデータファイルが
rem 見つかりません) が Visual Studio に出たときの復旧スクリプト。
rem
rem 原因: GitHub の ZIP をダウンロードして新しいフォルダに展開するたびに、
rem obj / .vs フォルダに前のフォルダ名を含む古いキャッシュが残り、
rem NuGet の依存関係解決やプロジェクト間のビルド成果物の参照が壊れることがある。
rem
rem 使い方: このファイルをダブルクリックするだけ。
rem 実行後は Visual Studio を再起動し、
rem src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj を開き直すこと。

setlocal
set ROOT=%~dp0..
echo 対象フォルダ: %ROOT%
echo.

echo [1/4] .vs キャッシュを削除しています...
if exist "%ROOT%\.vs" rmdir /s /q "%ROOT%\.vs"

echo [2/4] obj / bin キャッシュを削除しています...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Get-ChildItem -Path '%ROOT%' -Recurse -Directory -Include bin,obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"

echo [3/4] NuGet復元を実行しています...
dotnet restore "%ROOT%\src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj"

echo [4/4] ビルドを実行しています(Core→Pluginの順)...
dotnet build "%ROOT%\src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj" -c Debug

echo.
echo 完了しました。Visual Studio を再起動し、
echo src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj を開き直してください。
pause
