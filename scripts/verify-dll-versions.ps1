<#
.SYNOPSIS
    参照DLLバージョン検証 (CLAUDE.PRIVATE.md §9)

.DESCRIPTION
    コード生成・ビルドの前に、実行環境 (AutoCAD 2025 / Civil 3D 2025 / Dynamo 3.3) の
    DLL バージョンを実測し、期待バージョンマトリクスと突き合わせる。
    バージョン不一致は TypeLoadException / MissingMethodException をランタイムで
    引き起こし、ビルド成功では検出できないため事前検証が必須。

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\verify-dll-versions.ps1

.NOTES
    終了コード  0 = 全て一致 / 1 = 不一致あり / 2 = インストール未検出
#>

[CmdletBinding()]
param(
    [string]$AcadRoot = 'C:\Program Files\Autodesk\AutoCAD 2025'
)

$ErrorActionPreference = 'Stop'

$C3dRoot    = Join-Path $AcadRoot 'C3D'
$DynamoRoot = Join-Path $C3dRoot  'Dynamo\Core'

# 期待バージョンマトリクス (CLAUDE.PRIVATE.md §9.2 Step 3)
$Expected = @(
    @{ Name = 'AcCoreMgd.dll';        Dir = $AcadRoot;    Major = '25'   }
    @{ Name = 'AcDbMgd.dll';          Dir = $AcadRoot;    Major = '25'   }
    @{ Name = 'AcMgd.dll';            Dir = $AcadRoot;    Major = '25'   }
    @{ Name = 'AeccDbMgd.dll';        Dir = $C3dRoot;     Major = $null  }  # Civil 3D 独自体系
    @{ Name = 'AeccDbRoadwayMgd.dll'; Dir = $C3dRoot;     Major = $null  }
    @{ Name = 'ProtoGeometry.dll';    Dir = $DynamoRoot;  Major = '3.3'  }
    @{ Name = 'DynamoServices.dll';   Dir = $DynamoRoot;  Major = '3.3'  }
    @{ Name = 'DSCoreNodes.dll';      Dir = $DynamoRoot;  Major = '3.3'  }
)

Write-Host '=== 参照DLLバージョン検証 ===' -ForegroundColor Cyan
Write-Host ("検証日      : {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Write-Host ("AutoCAD ルート: {0}" -f $AcadRoot)
Write-Host ''

if (-not (Test-Path $AcadRoot)) {
    Write-Host "[NG] AutoCAD 2025 が見つかりません: $AcadRoot" -ForegroundColor Red
    Write-Host ''
    Write-Host '対処:' -ForegroundColor Yellow
    Write-Host '  1) Civil 3D 2025 をインストールした環境で本スクリプトを再実行する'
    Write-Host '  2) 別パスにインストール済みの場合は -AcadRoot で指定する'
    Write-Host '  3) スタブでビルドする場合は stubs/build_stubs.csproj を使用し、'
    Write-Host '     実機検証まで「未検証」であることをソース先頭コメントに明記する'
    exit 2
}

$rows     = @()
$mismatch = 0

foreach ($e in $Expected) {
    $path = Join-Path $e.Dir $e.Name

    if (-not (Test-Path $path)) {
        $rows += [pscustomobject]@{
            DLL = $e.Name; FileVersion = '(未検出)'; Expected = $e.Major; Result = 'NG'
        }
        $mismatch++
        continue
    }

    $ver    = (Get-Item $path).VersionInfo.FileVersion
    $result = 'OK'

    if ($null -ne $e.Major -and $ver -notlike "$($e.Major).*") {
        $result = 'NG'
        $mismatch++
    }
    elseif ($null -eq $e.Major) {
        $result = 'INFO'   # 期待値未定義。実測値を記録するのみ
    }

    $rows += [pscustomobject]@{
        DLL = $e.Name; FileVersion = $ver; Expected = $(if ($e.Major) { "$($e.Major).x" } else { '(実測記録)' }); Result = $result
    }
}

$rows | Format-Table -AutoSize

if ($mismatch -gt 0) {
    Write-Host "[NG] 不一致 $mismatch 件。コード生成を中止してください。" -ForegroundColor Red
    Write-Host '推奨対処: .csproj の HintPath 修正 / 別環境への切替 / バージョン統一' -ForegroundColor Yellow
    exit 1
}

Write-Host '[OK] 全て期待バージョンと一致しました。' -ForegroundColor Green
exit 0
