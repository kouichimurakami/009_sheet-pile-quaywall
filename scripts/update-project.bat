@echo off
rem プロジェクトを「常に同じ場所」に更新するスクリプト。
rem
rem 背景: 社内ルールで git clone/pull が使えず、GitHub の ZIP を毎回ダウンロード
rem する運用のため、展開のたびに「新しいフォルダー (N)」が増え続け、
rem NU1105 / CS0006 が繰り返し発生していた。このスクリプトは、ダウンロードした
rem ZIP をいつも同じ固定フォルダへ展開・上書きし、キャッシュ削除とビルドまで
rem 自動で行うことで、この問題を構造的に防ぐ。
rem
rem 【重要】初回セットアップ: このファイルは、デスクトップなど
rem プロジェクトフォルダの「外」に置いて使うこと。プロジェクトフォルダの
rem 中に置くと、更新のたびに自分自身が上書き対象に巻き込まれてしまう。
rem
rem 使い方: ダウンロードした ZIP ファイルを、この update-project.bat の
rem アイコンにドラッグ&ドロップする。以後もこれを繰り返すだけでよい。
rem 固定フォルダの場所は、このファイルと同じディレクトリの
rem 009_sheet-pile-quaywall フォルダになる。

setlocal
if "%~1"=="" (
    echo 使い方: ダウンロードした ZIP ファイルを、この update-project.bat に
    echo ドラッグ^&ドロップしてください。
    pause
    exit /b 1
)

set ZIPFILE=%~1
set TARGET=%~dp0009_sheet-pile-quaywall
set TEMPDIR=%TEMP%\009_sheet-pile-quaywall-extract

echo ZIP     : %ZIPFILE%
echo 展開先  : %TARGET%
echo.

echo [1/5] 一時フォルダへ展開しています...
if exist "%TEMPDIR%" rmdir /s /q "%TEMPDIR%"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Expand-Archive -Path '%ZIPFILE%' -DestinationPath '%TEMPDIR%' -Force"
if errorlevel 1 (
    echo 展開に失敗しました。ZIP ファイルを確認してください。
    pause
    exit /b 1
)

set EXTRACTED=
for /d %%D in ("%TEMPDIR%\*") do set EXTRACTED=%%D
if "%EXTRACTED%"=="" (
    echo 展開されたフォルダが見つかりませんでした。
    pause
    exit /b 1
)

if not exist "%TARGET%" (
    echo [2/5] 新規セットアップ: %TARGET% を作成します...
    move "%EXTRACTED%" "%TARGET%" >nul
) else (
    echo [2/5] 既存フォルダを上書き更新しています...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "Copy-Item -Path '%EXTRACTED%\*' -Destination '%TARGET%' -Recurse -Force"
)

rmdir /s /q "%TEMPDIR%" 2>nul

echo [3/5] .vs キャッシュを削除しています...
if exist "%TARGET%\.vs" rmdir /s /q "%TARGET%\.vs"

echo [4/5] obj / bin キャッシュを削除しています...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Get-ChildItem -Path '%TARGET%' -Recurse -Directory -Include bin,obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"

echo [5/5] NuGet復元とビルドを実行しています...
dotnet restore "%TARGET%\src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj"
dotnet restore "%TARGET%\src\SheetPileQuayWall.Dynamo\SheetPileQuayWall.Dynamo.csproj"
dotnet build "%TARGET%\src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj" -c Debug
dotnet build "%TARGET%\src\SheetPileQuayWall.Dynamo\SheetPileQuayWall.Dynamo.csproj" -c Debug

echo.
echo 完了しました。プロジェクトの場所は今後も常にここです:
echo   %TARGET%
echo Visual Studio では次のファイルを開いてください(AutoCAD コマンド用):
echo   %TARGET%\src\SheetPileQuayWall.Plugin\SheetPileQuayWall.Plugin.csproj
echo Dynamo ノード用のプロジェクトはこちらです:
echo   %TARGET%\src\SheetPileQuayWall.Dynamo\SheetPileQuayWall.Dynamo.csproj
echo.
echo 次回コードを更新する ときも、新しい ZIP をこの update-project.bat に
echo ドラッグ^&ドロップするだけで完了します。
pause
