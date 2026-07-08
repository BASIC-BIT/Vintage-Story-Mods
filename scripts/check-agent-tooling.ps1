$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Errors = [System.Collections.Generic.List[string]]::new()

function Add-CheckError {
    param([string]$Message)
    $Errors.Add($Message)
}

function Get-RepoPath {
    param([string]$RelativePath)
    return Join-Path $RepoRoot $RelativePath
}

function Test-RepoPath {
    param([string]$RelativePath)
    return Test-Path -LiteralPath (Get-RepoPath $RelativePath)
}

function Read-RepoFile {
    param([string]$RelativePath)
    $Path = Get-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    return Get-Content -LiteralPath $Path -Raw
}

function Get-ChildDirectoryNames {
    param([string]$RelativePath)
    $Path = Get-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    return Get-ChildItem -LiteralPath $Path -Directory |
        Select-Object -ExpandProperty Name |
        Sort-Object
}

function Get-OpenCodeMcpNames {
    $Candidates = @("opencode.json", ".opencode/opencode.json")
    $Names = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($Candidate in $Candidates) {
        if (-not (Test-RepoPath $Candidate)) {
            continue
        }

        try {
            $Json = Read-RepoFile $Candidate | ConvertFrom-Json
        } catch {
            Add-CheckError "$Candidate must be valid JSON: $($_.Exception.Message)"
            continue
        }

        if ($null -eq $Json.mcp) {
            continue
        }

        foreach ($Property in $Json.mcp.PSObject.Properties) {
            [void]$Names.Add($Property.Name)
        }
    }

    return @($Names | Sort-Object)
}

function Get-CodexMcpNames {
    $Config = Read-RepoFile ".codex/config.toml"
    $Matches = [regex]::Matches($Config, "(?m)^\[mcp_servers\.([^\]]+)\]")
    $Names = foreach ($Match in $Matches) {
        $Match.Groups[1].Value
    }

    return @($Names | Sort-Object)
}

if (-not (Test-RepoPath "AGENTS.md")) {
    Add-CheckError "AGENTS.md must exist."
}

if (-not (Test-RepoPath "docs/agentic/codex.md")) {
    Add-CheckError "docs/agentic/codex.md must exist."
}

if (-not (Test-RepoPath ".opencode/skills")) {
    Add-CheckError ".opencode/skills must exist."
}

if (-not (Test-RepoPath ".codex/skills")) {
    Add-CheckError ".codex/skills must exist."
}

$Agents = Read-RepoFile "AGENTS.md"
$CodexDocs = Read-RepoFile "docs/agentic/codex.md"
$OpenCodeSkillNames = Get-ChildDirectoryNames ".opencode/skills"
$CodexSkillNames = Get-ChildDirectoryNames ".codex/skills"

if ($OpenCodeSkillNames.Count -eq 0) {
    Add-CheckError ".opencode/skills must contain at least one skill."
}

if ($Agents -notlike "*.codex/skills/*") {
    Add-CheckError "AGENTS.md must mention the Codex wrapper skill location."
}

foreach ($SkillName in $OpenCodeSkillNames) {
    $SourcePath = ".opencode/skills/$SkillName/SKILL.md"
    $WrapperPath = ".codex/skills/$SkillName/SKILL.md"
    $Wrapper = Read-RepoFile $WrapperPath

    if (-not (Test-RepoPath $SourcePath)) {
        Add-CheckError "$SourcePath must exist."
    }

    if (-not (Test-RepoPath $WrapperPath)) {
        Add-CheckError "$WrapperPath must exist."
        continue
    }

    if ($CodexSkillNames -notcontains $SkillName) {
        Add-CheckError ".codex/skills must include $SkillName."
    }

    if (-not $Wrapper.StartsWith("---`n")) {
        Add-CheckError "$WrapperPath must start with frontmatter."
    }

    if ($Wrapper -notmatch "(?m)^name:\s+$([regex]::Escape($SkillName))\s*$") {
        Add-CheckError "$WrapperPath must declare name: $SkillName."
    }

    if ($Wrapper -notmatch "(?m)^description:\s+\S") {
        Add-CheckError "$WrapperPath must include a description."
    }

    if ($Wrapper -match "(?m)^compatibility:") {
        Add-CheckError "$WrapperPath must not use OpenCode-only compatibility frontmatter."
    }

    if (-not $Wrapper.Contains($SourcePath)) {
        Add-CheckError "$WrapperPath must point to $SourcePath."
    }

    if (-not $CodexDocs.Contains("- ``$SkillName``")) {
        Add-CheckError "docs/agentic/codex.md must list $SkillName."
    }
}

$OpenCodeMcpNames = Get-OpenCodeMcpNames
$CodexMcpNames = Get-CodexMcpNames

foreach ($McpName in $OpenCodeMcpNames) {
    if ($CodexMcpNames -notcontains $McpName) {
        Add-CheckError ".codex/config.toml must define [mcp_servers.$McpName] to mirror opencode.json."
    }
}

foreach ($McpName in $CodexMcpNames) {
    if (-not $CodexDocs.Contains("- ``$McpName``")) {
        Add-CheckError "docs/agentic/codex.md must list MCP $McpName."
    }
}

if ($OpenCodeMcpNames.Count -eq 0 -and $CodexMcpNames.Count -eq 0) {
    if (-not $CodexDocs.Contains("No project-scoped OpenCode MCP servers are committed")) {
        Add-CheckError "docs/agentic/codex.md must state that no project-scoped OpenCode MCP servers are committed."
    }
}

if ($Errors.Count -gt 0) {
    Write-Error "Agent tooling check failed:`n- $($Errors -join "`n- ")"
    exit 1
}

Write-Host "Agent tooling check passed."
