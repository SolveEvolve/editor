[CmdletBinding()]
param(
    [string]$Name = "mapEditorUnityMCP",
    [string]$Endpoint = "http://127.0.0.1:8092/mcp"
)

$ErrorActionPreference = "Stop"

if ($null -eq (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw "The Codex CLI was not found on PATH. Restart the terminal after installing Codex, then rerun this command."
}

$configPath = Join-Path $env:USERPROFILE '.codex\config.toml'
$escapedName = [regex]::Escape($Name)
if ((Test-Path -LiteralPath $configPath) -and (Select-String -Path $configPath -Pattern "^\[mcp_servers\.$escapedName\]$" -Quiet)) {
    throw "A Codex MCP server named '$Name' already exists. Refusing to overwrite it; inspect it with 'codex mcp get $Name'."
}

& codex mcp add $Name --url $Endpoint
if ($LASTEXITCODE -ne 0) { throw "Codex did not register the MapEditor Unity MCP endpoint." }

Write-Host "Registered '$Name' at $Endpoint. Start a new Codex task before using its Unity tools."
