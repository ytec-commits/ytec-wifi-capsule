[CmdletBinding()]
param(
    [string] $OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'artifacts\vm-payload-ready'
}
$outputRootPath = [IO.Path]::GetFullPath($OutputRoot)
$projectPrefix = $projectRoot.TrimEnd('\') + '\'
if (-not $outputRootPath.StartsWith(
        $projectPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw 'VM検証ペイロードの出力先はプロジェクト内を指定してください。'
}
if (Test-Path -LiteralPath $outputRootPath) {
    throw "既存のVM検証ペイロードを上書きしません: $outputRootPath"
}

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$solution = Join-Path $projectRoot 'YtecWifiCapsule.slnx'
$appProject = Join-Path (
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App'
) 'Ytec.WifiCapsule.App.csproj'
$testProject = Join-Path (
    Join-Path $projectRoot 'tests\Ytec.WifiCapsule.Tests'
) 'Ytec.WifiCapsule.Tests.csproj'

foreach ($architecture in @('x86', 'x64')) {
    $architectureRoot = Join-Path $outputRootPath $architecture
    & $dotnet restore $solution `
        "-p:PlatformTarget=$architecture" `
        -p:Prefer32Bit=false
    if ($LASTEXITCODE -ne 0) {
        throw "$architecture VM検証用の復元に失敗しました。"
    }

    & $dotnet build $testProject `
        -t:Rebuild `
        -c Release `
        --no-restore `
        "-p:PlatformTarget=$architecture" `
        -p:Prefer32Bit=false `
        -o $architectureRoot
    if ($LASTEXITCODE -ne 0) {
        throw "$architecture テストペイロードのビルドに失敗しました。"
    }

    & $dotnet build $appProject `
        -t:Rebuild `
        -c Release `
        --no-restore `
        "-p:PlatformTarget=$architecture" `
        -p:Prefer32Bit=false `
        -p:UiTestBuild=true `
        -p:UiTestCaptureWide=true `
        -o $architectureRoot
    if ($LASTEXITCODE -ne 0) {
        throw "$architecture UIペイロードのビルドに失敗しました。"
    }
}

foreach ($requiredFile in @(
        'x86\Ytec.WifiCapsule.Tests.exe',
        'x86\YtecWifiCapsule.exe',
        'x64\Ytec.WifiCapsule.Tests.exe',
        'x64\YtecWifiCapsule.exe'
    )) {
    if (-not (Test-Path -LiteralPath (
                Join-Path $outputRootPath $requiredFile
            ) -PathType Leaf)) {
        throw "VM検証ペイロードが不足しています: $requiredFile"
    }
}

[pscustomobject]@{
    OutputRoot = $outputRootPath
    X86Files = (
        Get-ChildItem -LiteralPath (
            Join-Path $outputRootPath 'x86'
        ) -Recurse -File
    ).Count
    X64Files = (
        Get-ChildItem -LiteralPath (
            Join-Path $outputRootPath 'x64'
        ) -Recurse -File
    ).Count
}
