[CmdletBinding()]
param(
    [string] $Version = '1.1.0',

    [Parameter(Mandatory)]
    [string] $OfficialKeyFile,

    [string] $OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$officialKeyPath = [IO.Path]::GetFullPath($OfficialKeyFile)
if (-not (Test-Path -LiteralPath $officialKeyPath -PathType Leaf)) {
    throw "公式アプリ鍵ファイルが見つかりません: $officialKeyPath"
}
if ((Get-Item -LiteralPath $officialKeyPath).Length -ne 32) {
    throw '公式アプリ鍵ファイルは32バイトである必要があります。'
}
$projectPrefix = $projectRoot.TrimEnd('\') + '\'
if ($officialKeyPath.StartsWith(
        $projectPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw '公式アプリ鍵の平文ファイルはプロジェクト外へ置いてください。'
}
$defaultOutputRoot = Join-Path $projectRoot 'artifacts\release'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = $defaultOutputRoot
}
$outputRootPath = [IO.Path]::GetFullPath($OutputRoot)
if (
    -not $outputRootPath.Equals(
        $projectRoot,
        [StringComparison]::OrdinalIgnoreCase
    ) -and
    -not $outputRootPath.StartsWith(
        $projectPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )
) {
    throw '配布物の出力先はプロジェクト内を指定してください。'
}

$releaseName = "Y-TEC-Wi-Fi-Capsule-$Version-portable-unsigned"
$stageDirectory = Join-Path $outputRootPath $releaseName
$zipPath = Join-Path $outputRootPath "$releaseName.zip"
$zipHashPath = "$zipPath.sha256"
foreach ($target in @($stageDirectory, $zipPath, $zipHashPath)) {
    if (Test-Path -LiteralPath $target) {
        throw "既存の配布物を上書きしません。退避してから再実行してください: $target"
    }
}

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw ".NET SDKが見つかりません: $dotnet"
}

$manualPdf = Join-Path (
    Join-Path $projectRoot 'docs\manual'
) 'Y-TEC Wi-Fi Capsule 操作マニュアル.pdf'
$englishManualPdf = Join-Path (
    Join-Path $projectRoot 'docs\manual\en'
) 'Y-TEC Wi-Fi Capsule User Manual.pdf'
$manualFiles = [ordered]@{
    '操作マニュアル\index.html' = (
        Join-Path $projectRoot 'docs\manual\index.html'
    )
    '操作マニュアル\app-icon.png' = (
        Join-Path $projectRoot 'docs\manual\app-icon.png'
    )
    '操作マニュアル\screenshots\main-backup.png' = (
        Join-Path $projectRoot 'docs\manual\screenshots\main-backup.png'
    )
    '操作マニュアル\screenshots\main-restore.png' = (
        Join-Path $projectRoot 'docs\manual\screenshots\main-restore.png'
    )
    '操作マニュアル\Y-TEC Wi-Fi Capsule 操作マニュアル.pdf' = $manualPdf
    'User Manual\index.html' = (
        Join-Path $projectRoot 'docs\manual\en\index.html'
    )
    'User Manual\app-icon.png' = (
        Join-Path $projectRoot 'docs\manual\en\app-icon.png'
    )
    'User Manual\screenshots\main-backup.png' = (
        Join-Path $projectRoot 'docs\manual\en\screenshots\main-backup.png'
    )
    'User Manual\screenshots\main-restore.png' = (
        Join-Path $projectRoot 'docs\manual\en\screenshots\main-restore.png'
    )
    'User Manual\Y-TEC Wi-Fi Capsule User Manual.pdf' = $englishManualPdf
}
foreach ($sourcePath in $manualFiles.Values) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "操作マニュアルの構成ファイルがありません: $sourcePath"
    }
}

$buildDirectory = Join-Path (
    Join-Path $outputRootPath 'build'
) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null

$appProject = Join-Path (
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App'
) 'Ytec.WifiCapsule.App.csproj'
& $dotnet restore $appProject `
    -p:PlatformTarget=AnyCPU `
    -p:Prefer32Bit=false `
    -p:YtecWifiCapsuleOfficialKeyFile=$officialKeyPath
if ($LASTEXITCODE -ne 0) {
    throw 'AnyCPU配布ビルド用の復元に失敗しました。'
}
& $dotnet build $appProject `
    -t:Rebuild `
    -c Release `
    --no-restore `
    -p:PlatformTarget=AnyCPU `
    -p:Prefer32Bit=false `
    -p:YtecWifiCapsuleOfficialKeyFile=$officialKeyPath `
    -p:UiTestBuild=false `
    -o $buildDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'AnyCPU配布ビルドに失敗しました。'
}

$buildFiles = @(
    'YtecWifiCapsule.exe',
    'YtecWifiCapsule.exe.config',
    'Ytec.WifiCapsule.Core.dll',
    'Ytec.WifiCapsule.Windows.dll',
    'Newtonsoft.Json.dll'
)
foreach ($relativePath in $buildFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildDirectory $relativePath))) {
        throw "配布に必要なビルドファイルがありません: $relativePath"
    }
}

$windowsAssembly = [Reflection.Assembly]::LoadFrom(
    (Join-Path $buildDirectory 'Ytec.WifiCapsule.Windows.dll')
)
if (
    'Ytec.WifiCapsule.Windows.OfficialApplicationKey' -notin
    $windowsAssembly.GetManifestResourceNames()
) {
    throw '配布ビルドへ公式アプリ鍵が埋め込まれていません。'
}

$fileVersion = (
    [Diagnostics.FileVersionInfo]::GetVersionInfo(
        (Join-Path $buildDirectory 'YtecWifiCapsule.exe')
    )
).FileVersion
if ($fileVersion -ne "$Version.0") {
    throw "EXEのバージョンが一致しません: $fileVersion"
}

$signature = Get-AuthenticodeSignature -LiteralPath (
    Join-Path $buildDirectory 'YtecWifiCapsule.exe'
)
if ($signature.Status -ne 'NotSigned') {
    throw "未署名正式版として想定外の署名状態です: $($signature.Status)"
}

New-Item -ItemType Directory -Path $stageDirectory | Out-Null
New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory '操作マニュアル\screenshots'
) | Out-Null
New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory 'User Manual\screenshots'
) | Out-Null

foreach ($relativePath in $buildFiles) {
    Copy-Item -LiteralPath (
        Join-Path $buildDirectory $relativePath
    ) -Destination (Join-Path $stageDirectory $relativePath)
}
foreach ($entry in $manualFiles.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (
        Join-Path $stageDirectory $entry.Key
    )
}
foreach ($fileName in @(
        'お読みください.txt',
        'Read Me.txt',
        'LICENSE.txt',
        'NOTICE',
        'SOURCE.txt',
        'PRIVACY.md',
        'CODE_SIGNING_POLICY.md',
        'CHANGELOG.md',
        'THIRD-PARTY-NOTICES.txt'
    )) {
    Copy-Item -LiteralPath (
        Join-Path $projectRoot $fileName
    ) -Destination (Join-Path $stageDirectory $fileName)
}

$hashLines = Get-ChildItem -LiteralPath $stageDirectory -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring(
            $stageDirectory.Length
        ).TrimStart('\').Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash *$relativePath"
    }
[IO.File]::WriteAllLines(
    (Join-Path $stageDirectory 'SHA256SUMS.txt'),
    $hashLines,
    [Text.UTF8Encoding]::new($false)
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open(
    $zipPath,
    [IO.Compression.ZipArchiveMode]::Create
)
try {
    foreach ($file in (
        Get-ChildItem -LiteralPath $stageDirectory -Recurse -File |
            Sort-Object FullName
    )) {
        $entryName = $file.FullName.Substring(
            $stageDirectory.Length
        ).TrimStart('\').Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText(
    $zipHashPath,
    "$zipHash *$([IO.Path]::GetFileName($zipPath))`r`n",
    [Text.UTF8Encoding]::new($false)
)

[pscustomobject]@{
    Version = $Version
    StageDirectory = $stageDirectory
    ZipPath = $zipPath
    ZipSha256 = $zipHash
    Signature = $signature.Status.ToString()
    FileCount = (
        Get-ChildItem -LiteralPath $stageDirectory -Recurse -File
    ).Count
}
