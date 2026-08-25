[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ZipPath,

    [string] $ExpectedVersion = '1.1.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$zipPathValue = [IO.Path]::GetFullPath($ZipPath)
if (-not (Test-Path -LiteralPath $zipPathValue -PathType Leaf)) {
    throw "ZIPが見つかりません: $zipPathValue"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPathValue)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    foreach ($entryName in $entryNames) {
        if (
            $entryName.StartsWith('/') -or
            $entryName.Contains(':') -or
            $entryName.Contains('\') -or
            $entryName -match '(^|/)\.\.(/|$)'
        ) {
            throw "ZIPに安全でないパスがあります: $entryName"
        }
    }

    $requiredEntries = @(
        'YtecWifiCapsule.exe',
        'YtecWifiCapsule.exe.config',
        'Ytec.WifiCapsule.Core.dll',
        'Ytec.WifiCapsule.Windows.dll',
        'Newtonsoft.Json.dll',
        '操作マニュアル/index.html',
        '操作マニュアル/app-icon.png',
        '操作マニュアル/screenshots/main-backup.png',
        '操作マニュアル/screenshots/main-restore.png',
        '操作マニュアル/Y-TEC Wi-Fi Capsule 操作マニュアル.pdf',
        'User Manual/index.html',
        'User Manual/app-icon.png',
        'User Manual/screenshots/main-backup.png',
        'User Manual/screenshots/main-restore.png',
        'User Manual/Y-TEC Wi-Fi Capsule User Manual.pdf',
        'お読みください.txt',
        'Read Me.txt',
        'LICENSE.txt',
        'NOTICE',
        'SOURCE.txt',
        'PRIVACY.md',
        'CODE_SIGNING_POLICY.md',
        'CHANGELOG.md',
        'THIRD-PARTY-NOTICES.txt',
        'SHA256SUMS.txt'
    )
    foreach ($requiredEntry in $requiredEntries) {
        if ($requiredEntry -notin $entryNames) {
            throw "ZIPに必要なファイルがありません: $requiredEntry"
        }
    }

    $forbiddenEntries = @(
        $entryNames | Where-Object {
            $_ -match '\.pdb$' -or
            $_ -match 'Ytec\.WifiCapsule\.Tests' -or
            $_ -match 'capture|ui-test|\.partial$|secrets?'
        }
    )
    if ($forbiddenEntries.Count -gt 0) {
        throw "ZIPに配布禁止ファイルがあります: $($forbiddenEntries -join ', ')"
    }
}
finally {
    $archive.Dispose()
}

$verificationRoot = Join-Path (
    [IO.Path]::GetDirectoryName($zipPathValue)
) 'verification'
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
$extractDirectory = Join-Path $verificationRoot (
    "$([IO.Path]::GetFileNameWithoutExtension($zipPathValue))-" +
    [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
)
if (Test-Path -LiteralPath $extractDirectory) {
    throw "検証先が既に存在します: $extractDirectory"
}
[IO.Compression.ZipFile]::ExtractToDirectory($zipPathValue, $extractDirectory)

$extractPrefix = $extractDirectory.TrimEnd('\') + '\'
$hashFile = Join-Path $extractDirectory 'SHA256SUMS.txt'
$hashLines = Get-Content -LiteralPath $hashFile -Encoding UTF8
foreach ($line in $hashLines) {
    if ($line -notmatch '^([0-9A-F]{64}) \*(.+)$') {
        throw "SHA256SUMS.txtの形式が不正です: $line"
    }
    $expectedHash = $Matches[1]
    $relativePath = $Matches[2].Replace('/', '\')
    $target = [IO.Path]::GetFullPath(
        (Join-Path $extractDirectory $relativePath)
    )
    if (-not $target.StartsWith(
            $extractPrefix,
            [StringComparison]::OrdinalIgnoreCase
        )) {
        throw "ハッシュ対象が展開先の外側を指しています: $relativePath"
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw "ハッシュ対象がありません: $relativePath"
    }
    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) {
        throw "ファイルハッシュが一致しません: $relativePath"
    }
}

$exePath = Join-Path $extractDirectory 'YtecWifiCapsule.exe'
$fileVersion = (
    [Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
).FileVersion
if ($fileVersion -ne "$ExpectedVersion.0") {
    throw "EXEのバージョンが一致しません: $fileVersion"
}

$signature = Get-AuthenticodeSignature -LiteralPath $exePath
if ($signature.Status -ne 'NotSigned') {
    throw "未署名正式版として想定外の署名状態です: $($signature.Status)"
}

$windowsAssembly = [Reflection.Assembly]::LoadFrom(
    (Join-Path $extractDirectory 'Ytec.WifiCapsule.Windows.dll')
)
if (
    'Ytec.WifiCapsule.Windows.OfficialApplicationKey' -notin
    $windowsAssembly.GetManifestResourceNames()
) {
    throw '配布ZIPのWindows DLLに公式アプリ鍵がありません。'
}

$readme = Get-Content -LiteralPath (
    Join-Path $extractDirectory 'お読みください.txt'
) -Raw -Encoding UTF8
foreach ($textFileName in @(
        'お読みください.txt',
        'Read Me.txt',
        'LICENSE.txt'
    )) {
    $textFilePath = Join-Path $extractDirectory $textFileName
    $textFileBytes = [IO.File]::ReadAllBytes($textFilePath)
    if (
        $textFileBytes.Length -lt 3 -or
        $textFileBytes[0] -ne 0xEF -or
        $textFileBytes[1] -ne 0xBB -or
        $textFileBytes[2] -ne 0xBF
    ) {
        throw "Windows 7向けテキストがUTF-8 BOM付きではありません: $textFileName"
    }
    $textFileBody = [Text.Encoding]::UTF8.GetString(
        $textFileBytes,
        3,
        $textFileBytes.Length - 3
    )
    if ($textFileBody -match '(?<!\r)\n') {
        throw "Windows 7向けテキストにLFのみの改行があります: $textFileName"
    }
}
if (
    $readme -notmatch 'Windows 7 SP1' -or
    $readme -notmatch '32ビット' -or
    $readme -notmatch '管理者' -or
    $readme -notmatch '未署名' -or
    $readme -notmatch 'SHA-256'
) {
    throw '配布用の重要事項が不足しています。'
}

$license = Get-Content -LiteralPath (
    Join-Path $extractDirectory 'LICENSE.txt'
) -Raw -Encoding UTF8
if (
    $license -notmatch 'Apache License' -or
    $license -notmatch 'Version 2\.0'
) {
    throw 'Apache-2.0ライセンスが配布物に含まれていません。'
}

$sourceNotice = Get-Content -LiteralPath (
    Join-Path $extractDirectory 'SOURCE.txt'
) -Raw -Encoding UTF8
if (
    $sourceNotice -notmatch 'github\.com/ytec-commits/ytec-wifi-capsule' -or
    $sourceNotice -notmatch '公式ビルド' -or
    $sourceNotice -notmatch '公開ソース'
) {
    throw 'ソースコードと公式ビルドの説明が不足しています。'
}

$manualHtml = Get-Content -LiteralPath (
    Join-Path $extractDirectory '操作マニュアル\index.html'
) -Raw -Encoding UTF8
if (
    $manualHtml -notmatch '必要なWi-Fi設定だけ' -or
    $manualHtml -notmatch 'Windows 7 SP1' -or
    $manualHtml -notmatch '32ビット / 64ビット' -or
    $manualHtml -notmatch '管理者権限' -or
    $manualHtml -notmatch 'AES-256-CBC' -or
    $manualHtml -notmatch 'HMAC-SHA-256' -or
    $manualHtml -notmatch '内蔵鍵方式' -or
    $manualHtml -notmatch '公開ソース' -or
    $manualHtml -notmatch '公式ビルド' -or
    $manualHtml -notmatch '同名の保存済みWi-Fi設定も上書きする'
) {
    throw '操作マニュアルの重要事項が不足しています。'
}

$manualPdf = Join-Path (
    $extractDirectory
) '操作マニュアル\Y-TEC Wi-Fi Capsule 操作マニュアル.pdf'
if ((Get-Item -LiteralPath $manualPdf).Length -lt 500000) {
    throw '操作マニュアルPDFのサイズが想定より小さすぎます。'
}
$pdfHeader = [IO.File]::ReadAllBytes($manualPdf)[0..4]
if ([Text.Encoding]::ASCII.GetString($pdfHeader) -ne '%PDF-') {
    throw '操作マニュアルPDFのヘッダーが不正です。'
}

$englishManualHtml = Get-Content -LiteralPath (
    Join-Path $extractDirectory 'User Manual\index.html'
) -Raw -Encoding UTF8
if (
    $englishManualHtml -notmatch 'Back Up Wi-Fi Profiles' -or
    $englishManualHtml -notmatch 'Windows 7 SP1' -or
    $englishManualHtml -notmatch '32-bit or 64-bit' -or
    $englishManualHtml -notmatch 'Administrator privileges' -or
    $englishManualHtml -notmatch 'AES-256-CBC' -or
    $englishManualHtml -notmatch 'HMAC-SHA-256' -or
    $englishManualHtml -notmatch 'public-source build' -or
    $englishManualHtml -notmatch 'Official Y-TEC builds' -or
    $englishManualHtml -notmatch 'Overwrite saved Wi-Fi profiles'
) {
    throw '英語版操作マニュアルの重要事項が不足しています。'
}

$englishManualPdf = Join-Path (
    $extractDirectory
) 'User Manual\Y-TEC Wi-Fi Capsule User Manual.pdf'
if ((Get-Item -LiteralPath $englishManualPdf).Length -lt 500000) {
    throw '英語版操作マニュアルPDFのサイズが想定より小さすぎます。'
}
$englishPdfHeader = [IO.File]::ReadAllBytes($englishManualPdf)[0..4]
if ([Text.Encoding]::ASCII.GetString($englishPdfHeader) -ne '%PDF-') {
    throw '英語版操作マニュアルPDFのヘッダーが不正です。'
}

$zipHashPath = "$zipPathValue.sha256"
if (Test-Path -LiteralPath $zipHashPath -PathType Leaf) {
    $zipHashLine = Get-Content -LiteralPath $zipHashPath -Raw -Encoding UTF8
    if ($zipHashLine -notmatch '^([0-9A-F]{64}) \*(.+)\r?\n$') {
        throw 'ZIPのSHA-256ファイル形式が不正です。'
    }
    $actualZipHash = (
        Get-FileHash -LiteralPath $zipPathValue -Algorithm SHA256
    ).Hash
    if ($actualZipHash -ne $Matches[1]) {
        throw 'ZIPのSHA-256が一致しません。'
    }
}

[pscustomobject]@{
    ZipPath = $zipPathValue
    ExtractDirectory = $extractDirectory
    Version = $fileVersion
    Signature = $signature.Status.ToString()
    EntryCount = $entryNames.Count
    VerifiedHashes = $hashLines.Count
}
