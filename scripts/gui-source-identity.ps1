[CmdletBinding()]
param([string]$Repo = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath $Repo).Path
$paths = @('mods-dll/thebasics/src', 'mods-dll/thebasics/assets', 'mods-dll/thebasics/modinfo.json',
    'mods-dll/thebasics/thebasics.csproj', 'mods-dll/thebasics/Properties', 'tools/GuiPreview',
    'scripts/preview-gui.ps1', 'scripts/gui-source-identity.ps1', 'scripts/gui-capture-report.py')
$entries = [Collections.Generic.SortedDictionary[string,string]]::new([StringComparer]::Ordinal)
foreach ($relative in $paths) {
    $path = Join-Path $Repo $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $files = if (Test-Path -LiteralPath $path -PathType Container) { Get-ChildItem -LiteralPath $path -Recurse -File } else { Get-Item -LiteralPath $path }
    foreach ($file in $files) {
        $name = [IO.Path]::GetRelativePath($Repo, $file.FullName).Replace('\', '/')
        if ($name -match '/(bin|obj)/') { continue }
        $entries[$name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
if ($entries.Count -eq 0) { throw 'No GUI source files found.' }
$text = [Text.StringBuilder]::new()
foreach ($entry in $entries.GetEnumerator()) { [void]$text.Append($entry.Key).Append([char]0).Append($entry.Value).Append("`n") }
[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text.ToString()))).ToLowerInvariant()
