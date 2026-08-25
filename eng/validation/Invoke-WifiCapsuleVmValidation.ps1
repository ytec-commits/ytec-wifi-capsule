[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^YWB-Win(?:7SP1|8|81|10-22H2|11-25H2)-x(?:86|64)-Clean$')]
    [string] $VmName,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9._-]{0,31}$')]
    [string] $GuestUser = 'YwbTest',

    [ValidateRange(1, 30)]
    [int] $TimeoutMinutes = 10,

    [string] $PayloadRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$labRoot = 'D:\Y-TEC-Workspace\business-apps\ytec-windows-backup'
$vboxManage = 'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe'
$passwordFile = Join-Path (
    Join-Path $labRoot '.validation\vm-secrets'
) "$VmName.password.txt"
if ([string]::IsNullOrWhiteSpace($PayloadRoot)) {
    $PayloadRoot = Join-Path $projectRoot 'artifacts\vm-payload-ready'
}
$PayloadRoot = [IO.Path]::GetFullPath($PayloadRoot)
$projectPrefix = $projectRoot.TrimEnd('\') + '\'
if (-not $PayloadRoot.StartsWith(
        $projectPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )) {
    throw 'VM検証ペイロードはプロジェクト内を指定してください。'
}
$runner = Join-Path $PSScriptRoot 'guest\Run-WifiCapsuleAcceptance.cmd'
$dotNetCaptureHelper = Join-Path (
    $labRoot
) 'eng\validation\guest\Capture-DotNetRelease.cmd'
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$guestRoot = "C:\YWCValidation-$runStamp"
$evidenceRoot = Join-Path (
    $projectRoot
) ".validation\evidence\vm-results\$VmName\$runStamp"
$includeX64 = if ($VmName -like '*-x64-*') { '1' } else { '0' }

foreach ($requiredFile in @(
        $vboxManage,
        $passwordFile,
        $runner,
        $dotNetCaptureHelper,
        (Join-Path $PayloadRoot 'x86\Ytec.WifiCapsule.Tests.exe'),
        (Join-Path $PayloadRoot 'x86\YtecWifiCapsule.exe'),
        (Join-Path $PayloadRoot 'x64\Ytec.WifiCapsule.Tests.exe'),
        (Join-Path $PayloadRoot 'x64\YtecWifiCapsule.exe')
    )) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "必要なVM検証ファイルがありません: $requiredFile"
    }
}

$machineInfo = & $vboxManage showvminfo $VmName --machinereadable
if ($LASTEXITCODE -ne 0) {
    throw "VM情報を取得できませんでした: $VmName"
}
if (-not ($machineInfo -match '^VMState="running"$')) {
    throw "VMが起動していません: $VmName"
}
$enabledNic = @(
    $machineInfo | Where-Object {
        $_ -match '^nic[1-4]="' -and $_ -notmatch '="none"$'
    }
)
if ($enabledNic.Count -ne 0) {
    throw "NICが無効ではありません: $VmName"
}

function Close-GuestSessions {
    $null = & $vboxManage guestcontrol $VmName closesession --all 2>&1
}

function Invoke-GuestRun {
    param(
        [Parameter(Mandatory)]
        [string] $Executable,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    foreach ($attempt in 1..8) {
        $null = & $vboxManage guestcontrol $VmName run `
            "--exe=$Executable" `
            "--username=$GuestUser" `
            "--passwordfile=$passwordFile" `
            --wait-stdout `
            --wait-stderr `
            -- `
            @Arguments 2>&1
        if ($LASTEXITCODE -eq 0) {
            return
        }
        Close-GuestSessions
        Start-Sleep -Seconds 5
    }
    throw "VM内コマンドを完了できませんでした: $Executable"
}

function Invoke-GuestControlRetry {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $Operation
    )

    foreach ($attempt in 1..8) {
        $null = & $vboxManage guestcontrol $VmName @Arguments 2>&1
        if ($LASTEXITCODE -eq 0) {
            return
        }
        Start-Sleep -Seconds 5
    }
    throw "VirtualBox GuestControlが失敗しました: $Operation"
}

function Copy-GuestFile {
    param(
        [Parameter(Mandatory)]
        [string] $GuestPath,

        [Parameter(Mandatory)]
        [string] $HostPath,

        [switch] $AllowMissing
    )

    $null = & $vboxManage guestcontrol $VmName copyfrom `
        "--username=$GuestUser" `
        "--passwordfile=$passwordFile" `
        $GuestPath `
        $HostPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        if ($AllowMissing) {
            return $false
        }
        throw "VMからファイルを取得できませんでした: $GuestPath"
    }
    return $true
}

Close-GuestSessions
Start-Sleep -Seconds 3
foreach ($directory in @(
        "$guestRoot\x86",
        "$guestRoot\x64",
        "$guestRoot\results"
    )) {
    Invoke-GuestControlRetry `
        -Operation "mkdir $directory" `
        -Arguments @(
            'mkdir',
            '--parents',
            "--username=$GuestUser",
            "--passwordfile=$passwordFile",
            $directory
        )
}

$architectures = @('x86')
if ($includeX64 -eq '1') {
    $architectures += 'x64'
}
foreach ($architecture in $architectures) {
    $architectureRoot = Join-Path $PayloadRoot $architecture
    foreach ($file in (
        Get-ChildItem -LiteralPath $architectureRoot -File |
            Where-Object { $_.Extension -ne '.pdb' }
    )) {
        Invoke-GuestControlRetry `
            -Operation "copyto $architecture\$($file.Name)" `
            -Arguments @(
                'copyto',
                "--username=$GuestUser",
                "--passwordfile=$passwordFile",
                $file.FullName,
                "$guestRoot\$architecture\$($file.Name)"
            )
    }
}
Invoke-GuestControlRetry `
    -Operation 'copyto acceptance runner' `
    -Arguments @(
        'copyto',
        "--username=$GuestUser",
        "--passwordfile=$passwordFile",
        $runner,
        "$guestRoot\Run-WifiCapsuleAcceptance.cmd"
    )

New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
$dotNetHostPath = Join-Path $evidenceRoot 'dotnet-release.txt'
$dotNetCopied = Copy-GuestFile `
    -GuestPath 'C:\YWBValidation\dotnet-after.txt' `
    -HostPath $dotNetHostPath `
    -AllowMissing
if (-not $dotNetCopied) {
    Invoke-GuestControlRetry `
        -Operation 'copy .NET capture helper' `
        -Arguments @(
            'copyto',
            "--username=$GuestUser",
            "--passwordfile=$passwordFile",
            $dotNetCaptureHelper,
            "$guestRoot\Capture-DotNetRelease.cmd"
        )
    Invoke-GuestRun `
        -Executable 'C:\Windows\System32\cmd.exe' `
        -Arguments @(
            '/d',
            '/c',
            "$guestRoot\Capture-DotNetRelease.cmd",
            "$guestRoot\dotnet-release.txt"
        )
    foreach ($attempt in 1..30) {
        Start-Sleep -Seconds 2
        if (Copy-GuestFile `
                -GuestPath "$guestRoot\dotnet-release.txt" `
                -HostPath $dotNetHostPath `
                -AllowMissing) {
            $dotNetCopied = $true
            break
        }
    }
}
if (-not $dotNetCopied) {
    throw '.NET FrameworkのRelease値をVMから取得できませんでした。'
}
$dotNetText = Get-Content -LiteralPath $dotNetHostPath -Raw
if ($dotNetText -notmatch 'Release\s+REG_DWORD\s+0x([0-9a-fA-F]+)') {
    throw '.NET FrameworkのRelease値を読み取れませんでした。'
}
$dotNetRelease = [Convert]::ToInt32($Matches[1], 16)
if ($dotNetRelease -lt 394254) {
    throw ".NET Framework 4.6.1を確認できません: Release=$dotNetRelease"
}

Close-GuestSessions
Start-Sleep -Seconds 2
$null = & $vboxManage guestcontrol $VmName start `
    '--exe=C:\Windows\System32\cmd.exe' `
    "--username=$GuestUser" `
    "--passwordfile=$passwordFile" `
    --quiet `
    -- `
    /d `
    /c `
    "$guestRoot\Run-WifiCapsuleAcceptance.cmd" `
    $guestRoot `
    $includeX64 2>&1
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1073740940) {
    throw "VM内の受入試験を開始できませんでした: $VmName"
}

$doneHostPath = Join-Path $evidenceRoot 'done.txt'
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$done = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    if (Copy-GuestFile `
            -GuestPath "$guestRoot\results\done.txt" `
            -HostPath $doneHostPath `
            -AllowMissing) {
        $done = $true
        break
    }
}
if (-not $done) {
    throw "VM内の受入試験が時間内に完了しませんでした: $VmName"
}
if ((Get-Content -LiteralPath $doneHostPath -Raw).Trim() -ne 'PASS') {
    throw "VM内の受入試験が失敗しました: $VmName"
}

foreach ($fileName in @(
        'tests-x86.txt',
        'native-wifi-x86.txt',
        'ui-x86.txt'
    )) {
    $null = Copy-GuestFile `
        -GuestPath "$guestRoot\results\$fileName" `
        -HostPath (Join-Path $evidenceRoot $fileName)
}
if ($includeX64 -eq '1') {
    foreach ($fileName in @(
            'tests-x64.txt',
            'native-wifi-x64.txt',
            'ui-x64.txt'
        )) {
        $null = Copy-GuestFile `
            -GuestPath "$guestRoot\results\$fileName" `
            -HostPath (Join-Path $evidenceRoot $fileName)
    }
}
foreach ($architecture in @('x86', 'x64')) {
    if ($architecture -eq 'x64' -and $includeX64 -ne '1') {
        continue
    }
    foreach ($captureName in @('main-backup.png', 'main-restore.png')) {
        $null = Copy-GuestFile `
            -GuestPath "$guestRoot\$architecture\captures\$captureName" `
            -HostPath (
                Join-Path $evidenceRoot "$architecture-$captureName"
            )
    }
}

$screenshotPath = Join-Path $evidenceRoot 'screen-after-tests.png'
$null = & $vboxManage controlvm $VmName screenshotpng $screenshotPath 2>&1

$x86Output = Get-Content -LiteralPath (
    Join-Path $evidenceRoot 'tests-x86.txt'
) -Raw
if ($x86Output -notmatch '(?m)^24/24 tests passed\.\r?$') {
    throw "x86回帰試験の合格記録を確認できません: $VmName"
}
$x86Native = Get-Content -LiteralPath (
    Join-Path $evidenceRoot 'native-wifi-x86.txt'
) -Raw
if ($x86Native -notmatch 'NATIVE_WIFI_OK process=32-bit') {
    throw "x86 Native Wi-Fi API確認に失敗しました: $VmName"
}

$x64Summary = 'N/A (32-bit OS)'
if ($includeX64 -eq '1') {
    $x64Output = Get-Content -LiteralPath (
        Join-Path $evidenceRoot 'tests-x64.txt'
    ) -Raw
    if ($x64Output -notmatch '(?m)^24/24 tests passed\.\r?$') {
        throw "x64回帰試験の合格記録を確認できません: $VmName"
    }
    $x64Native = Get-Content -LiteralPath (
        Join-Path $evidenceRoot 'native-wifi-x64.txt'
    ) -Raw
    if ($x64Native -notmatch 'NATIVE_WIFI_OK process=64-bit') {
        throw "x64 Native Wi-Fi API確認に失敗しました: $VmName"
    }
    $x64Summary = '24/24 PASS'
}

[pscustomobject]@{
    VmName = $VmName
    DotNetRelease = $dotNetRelease
    X86Tests = '24/24 PASS'
    X64Tests = $x64Summary
    NativeWifi = 'PASS'
    UiCapture = 'PASS'
    EvidenceDirectory = $evidenceRoot
    CompletedUtc = [DateTimeOffset]::UtcNow
} | Format-List
