param([string]$OutputPath)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $projectDir 'README.html' }
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'readme-template.html') -Raw -Encoding utf8
$documents = [ordered]@{ EN='README.md'; ZH='README.zh-CN.md'; JA='README.ja.md' }
foreach ($entry in $documents.GetEnumerator()) {
    # Remote build badges are omitted so the local document loads offline.
    $markdown = Get-Content -LiteralPath (Join-Path $projectDir $entry.Value) -Raw -Encoding utf8
    $markdown = [regex]::Replace($markdown, '(?m)^\[!\[.*\r?\n?', '')
    $html = (ConvertFrom-Markdown -InputObject $markdown).Html
    $html = $html.Replace('href="README.md"', 'href="?lang=en"').
        Replace('href="README.zh-CN.md"', 'href="?lang=zh"').
        Replace('href="README.ja.md"', 'href="?lang=ja"')
    $template = $template.Replace('{{' + $entry.Key + '}}', $html)
}
$script = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'readme-language.js') -Raw -Encoding utf8
$template = $template.Replace('{{LANGUAGE_SCRIPT}}', $script)
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedOutput) -Force | Out-Null
[IO.File]::WriteAllText($resolvedOutput, $template, [Text.UTF8Encoding]::new($false))
Write-Host "Generated: $resolvedOutput"
