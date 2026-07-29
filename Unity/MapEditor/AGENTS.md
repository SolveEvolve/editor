# MapEditor Unity MCP

Coplay MCP for Unity is pinned in `Packages/manifest.json`.

## Dedicated endpoint

This project uses the local endpoint `http://127.0.0.1:8092/mcp`, registered in Codex as `mapEditorUnityMCP`.

- Do not reconfigure this project to use ports 8080 or 8091; those are already assigned to other Unity projects.
- Confirm the connected Unity instance reports this exact project path before making any edit.
- Keep the endpoint loopback-only; do not enable LAN binding or remote HTTP without an explicit request.

## Agent operating rules

- Begin every Unity task by reading editor state, active scene, hierarchy, and console output.
- Do not use saving, package changes, builds, or Play Mode merely to test connectivity.
- Preserve a pre-existing dirty scene. The mutation smoke check refuses to run on one.
- Use `tools/UnityMcpSmoke.ps1` for read-only protocol and project-identity verification. Its optional mutation probe must only be used on a clean scene and always stops Play Mode during cleanup.
- Do not run concurrent MCP mutations against this Unity project.

## First-time setup

1. Open MapEditor in Unity and allow Package Manager to resolve the pinned Coplay MCP package.
2. In **Tools > MCP for Unity**, select HTTP Local and set the base URL to `http://127.0.0.1:8092`; start the local server.
3. Run `powershell.exe -ExecutionPolicy Bypass -File .\tools\RegisterCodexUnityMcp.ps1`.
4. Start a new Codex task so it loads the `mapEditorUnityMCP` tools, then run `powershell.exe -ExecutionPolicy Bypass -File .\tools\UnityMcpSmoke.ps1`.
