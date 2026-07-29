using UnityEditor;
using UnityEngine;

namespace MapEditor
{
    [InitializeOnLoad]
    internal static class MapEditorMcpBootstrap
    {
        static MapEditorMcpBootstrap()
        {
            Debug.Log("MapEditor MCP is configured for the dedicated loopback endpoint http://127.0.0.1:8092/mcp.");
        }
    }
}
