[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$outputPathValue = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputPathValue) {
    throw "既存ファイルを上書きしません: $outputPathValue"
}

$parent = [IO.Path]::GetDirectoryName($outputPathValue)
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw "出力先フォルダーがありません: $parent"
}

$key = New-Object byte[] 32
$generator = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $generator.GetBytes($key)
    [IO.File]::WriteAllBytes($outputPathValue, $key)
}
finally {
    $generator.Dispose()
    [Array]::Clear($key, 0, $key.Length)
}

Write-Output '32バイトのカスタムアプリ鍵を作成しました。値は表示しません。'
Write-Output "保管先: $outputPathValue"
