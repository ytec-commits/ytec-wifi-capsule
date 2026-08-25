[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    $requiredFiles = @(
        'LICENSE.txt',
        'NOTICE',
        'README.md',
        'README.en.md',
        'SECURITY.md',
        'PRIVACY.md',
        'CONTRIBUTING.md',
        'CODE_SIGNING_POLICY.md',
        'THIRD-PARTY-NOTICES.txt'
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $relativePath -PathType Leaf)) {
            throw "公開リポジトリの必須ファイルがありません: $relativePath"
        }
    }

    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Git追跡ファイル一覧を取得できませんでした。'
    }
    $forbidden = @(
        $tracked | Where-Object {
            ($_ -match '(?i)(^|/)(\.validation|artifacts|output|tmp)/') -or ($_ -match '(?i)\.(ywcwifi|pfx|p12|snk|key|bin)$') -or ($_ -match '(?i)(official|private).*(key|secret)')
        }
    )
    if ($forbidden.Count -gt 0) {
        throw "公開禁止候補が追跡されています: $($forbidden -join ', ')"
    }

    $keySource = Get-Content -LiteralPath (
        'src\Ytec.WifiCapsule.Windows\ApplicationWifiKey.cs'
    ) -Raw -Encoding UTF8
    if (
        $keySource -match 'KeyShareA|KeyShareB' -or
        ([regex]::Matches($keySource, '0x[0-9A-Fa-f]{2}')).Count -gt 8 -or
        $keySource -notmatch 'public development key'
    ) {
        throw '公開ソースのアプリ鍵境界が不正です。'
    }

    Write-Output 'Public-source policy: PASS'
}
finally {
    Pop-Location
}
