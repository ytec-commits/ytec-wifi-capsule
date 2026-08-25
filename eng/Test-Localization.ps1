[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localizationRoot = Join-Path $projectRoot 'src\Ytec.WifiCapsule.App\Localization'
$japanesePath = Join-Path $localizationRoot 'Strings.ja.xaml'
$englishPath = Join-Path $localizationRoot 'Strings.en.xaml'
$xamlNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'

function Get-LocalizedStrings([string] $path) {
    [xml] $document = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $namespaceManager = New-Object Xml.XmlNamespaceManager($document.NameTable)
    $namespaceManager.AddNamespace('x', $xamlNamespace)
    $strings = @{}
    foreach ($node in $document.SelectNodes('//*[@x:Key]', $namespaceManager)) {
        $key = $node.GetAttribute('Key', $xamlNamespace)
        if ([string]::IsNullOrWhiteSpace($key)) {
            throw "An empty localization key was found: $path"
        }

        if ($strings.ContainsKey($key)) {
            throw "Duplicate localization key: $key ($path)"
        }

        if ([string]::IsNullOrWhiteSpace($node.InnerText)) {
            throw "Empty localization value: $key ($path)"
        }

        $strings[$key] = $node.InnerText
    }

    return $strings
}

$japanese = Get-LocalizedStrings $japanesePath
$english = Get-LocalizedStrings $englishPath
$missingInEnglish = @($japanese.Keys | Where-Object { -not $english.ContainsKey($_) })
$missingInJapanese = @($english.Keys | Where-Object { -not $japanese.ContainsKey($_) })
if ($missingInEnglish.Count -gt 0 -or $missingInJapanese.Count -gt 0) {
    throw (
        "Japanese and English localization keys differ. Missing in English: {0}; missing in Japanese: {1}" -f
        ($missingInEnglish -join ', '),
        ($missingInJapanese -join ', ')
    )
}

foreach ($entry in $english.GetEnumerator()) {
    if ($entry.Key -ne 'LanguageSwitch' -and $entry.Value -match '[\u3041-\u30ff\u3400-\u9fff]') {
        throw "Unexpected Japanese text in the English resource: $($entry.Key)"
    }
}

$uiFiles = @(
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App\App.xaml.cs'
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App\MainWindow.xaml'
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App\MainWindow.xaml.cs'
)
$referencedKeys = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($path in $uiFiles) {
    $source = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($source, 'DynamicResource\s+([A-Za-z0-9]+)')) {
        [void] $referencedKeys.Add($match.Groups[1].Value)
    }

    foreach ($match in [regex]::Matches(
        $source,
        'UiLanguage\.(?:Text|Format)\(\s*"([A-Za-z0-9]+)"',
        [Text.RegularExpressions.RegexOptions]::Singleline
    )) {
        [void] $referencedKeys.Add($match.Groups[1].Value)
    }
}

$missingKeys = @($referencedKeys | Where-Object { -not $japanese.ContainsKey($_) })
if ($missingKeys.Count -gt 0) {
    throw "A referenced localization key is missing: $($missingKeys -join ', ')"
}

$languageSource = Get-Content -LiteralPath (
    Join-Path $projectRoot 'src\Ytec.WifiCapsule.App\UiLanguage.cs'
) -Raw -Encoding UTF8
if (
    $languageSource -notmatch 'CurrentUICulture' -or
    $languageSource -notmatch '--lang=' -or
    $languageSource -notmatch 'Strings\.\{Code\}\.xaml'
) {
    throw 'Automatic or explicit UI language selection is incomplete.'
}

Write-Output "Localization policy: PASS ($($japanese.Count) keys, Japanese/English)"
