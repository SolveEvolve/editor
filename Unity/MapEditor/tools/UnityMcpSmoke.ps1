[CmdletBinding()]
param(
    [string]$Endpoint = "http://127.0.0.1:8092/mcp",
    [string]$ExpectedProjectPath,
    [switch]$ExerciseControls
)

$ErrorActionPreference = "Stop"
$headers = @{ Accept = "application/json, text/event-stream" }
$nextId = 1

if ([string]::IsNullOrWhiteSpace($ExpectedProjectPath)) {
    $ExpectedProjectPath = Split-Path -Parent $PSScriptRoot
}

function Invoke-McpRequest([string]$Method, [hashtable]$Params) {
    $id = $script:nextId
    $script:nextId++
    $body = @{ jsonrpc = "2.0"; id = $id; method = $Method; params = $Params } | ConvertTo-Json -Depth 12 -Compress
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Endpoint -Method Post -Headers $script:headers -ContentType "application/json" -Body $body
    $payloads = @($response.Content -split "`n" | Where-Object { $_ -like "data:*" } | ForEach-Object { $_.Substring(5).Trim() | ConvertFrom-Json })
    $reply = $payloads | Where-Object { $_.id -eq $id } | Select-Object -First 1
    if ($null -eq $reply) { throw "No JSON-RPC response for $Method." }
    if ($null -ne $reply.error) { throw "MCP error: $($reply.error.message)" }
    return @{ Result = $reply.result; Response = $response }
}

function Invoke-McpTool([string]$Name, [hashtable]$Arguments = @{}) {
    $reply = Invoke-McpRequest "tools/call" @{ name = $Name; arguments = $Arguments }
    if ($reply.Result.isError) { throw "Unity tool $Name failed: $($reply.Result.content[0].text)" }
    return $reply.Result.structuredContent
}

function Read-McpResource([string]$Uri) {
    $reply = Invoke-McpRequest "resources/read" @{ uri = $Uri }
    return $reply.Result.contents[0].text | ConvertFrom-Json
}

$init = Invoke-McpRequest "initialize" @{ protocolVersion = "2025-06-18"; capabilities = @{}; clientInfo = @{ name = "MapEditor Unity MCP Smoke"; version = "1.0" } }
$sessionId = $init.Response.Headers["Mcp-Session-Id"]
if ([string]::IsNullOrWhiteSpace($sessionId)) { throw "MCP server did not return a session id." }
$script:headers["Mcp-Session-Id"] = $sessionId
$notification = @{ jsonrpc = "2.0"; method = "notifications/initialized"; params = @{} } | ConvertTo-Json -Compress
$null = Invoke-WebRequest -UseBasicParsing -Uri $Endpoint -Method Post -Headers $script:headers -ContentType "application/json" -Body $notification

$tools = Invoke-McpRequest "tools/list" @{}
$instances = Read-McpResource "mcpforunity://instances"
$expectedProjectName = Split-Path -Leaf ([IO.Path]::GetFullPath($ExpectedProjectPath).TrimEnd('\\'))
$knownNames = @($instances.instances | ForEach-Object { $_.name })
if ($knownNames -notcontains $expectedProjectName) {
    throw "Expected Unity project '$expectedProjectName' was not among endpoint instances: $($knownNames -join ', ')"
}

$activeScene = Invoke-McpTool "manage_scene" @{ action = "get_active" }
$editorState = Read-McpResource "mcpforunity://editor/state"
$console = Invoke-McpTool "read_console" @{ action = "get"; types = @("error", "warning"); count = 20; format = "detailed"; include_stacktrace = $false }
$control = $null

if ($ExerciseControls) {
    if ($activeScene.data.isDirty) { throw "Refusing mutation smoke test: the active scene is dirty." }
    $probeName = "MapEditor MCP Smoke Probe"
    $playing = $false
    try {
        $created = Invoke-McpTool "manage_gameobject" @{ action = "create"; name = $probeName; primitive_type = "Cube"; position = @(0, 1000, 0) }
        $deleted = Invoke-McpTool "manage_gameobject" @{ action = "delete"; target = $probeName; search_method = "by_name" }
        $null = Invoke-McpTool "manage_editor" @{ action = "undo" }
        $afterUndo = Invoke-McpTool "manage_scene" @{ action = "get_active" }
        if ($afterUndo.data.isDirty) { throw "Undo did not restore the clean scene." }
        $null = Invoke-McpTool "manage_editor" @{ action = "play" }; $playing = $true; Start-Sleep -Seconds 2
        $null = Invoke-McpTool "manage_editor" @{ action = "stop" }; $playing = $false
        $control = @{ created = $created.success; deleted = $deleted.success; scene_clean_after_undo = (-not $afterUndo.data.isDirty); play_started_and_stopped = $true }
    }
    finally {
        if ($playing) { $null = Invoke-McpTool "manage_editor" @{ action = "stop" } }
    }
}

[ordered]@{ ok = $true; endpoint = $Endpoint; tool_count = $tools.Result.tools.Count; instances = $instances.instances; active_scene = $activeScene.data.path; active_scene_dirty = $activeScene.data.isDirty; editor_playing = $editorState.data.editor.play_mode.is_playing; console_warnings_errors = $console.data; controls = $control } | ConvertTo-Json -Depth 20
