using UnityEngine;
using UnityEditor;
using System.IO;

namespace MCPForUnity.Editor
{
    [InitializeOnLoad]
    public static class MCPInstaller
    {
        static MCPInstaller()
        {
            // Initial setup check
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string hiddenDir = Path.Combine(projectRoot, ".mcp-unity");
            
            if (!Directory.Exists(hiddenDir))
            {
                try
                {
                    Directory.CreateDirectory(hiddenDir);
                    // Hide on Windows
                    File.SetAttributes(hiddenDir, File.GetAttributes(hiddenDir) | FileAttributes.Hidden);
                    Debug.Log("[MCP Installer] Created .mcp-unity directory for configuration and logs.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MCP Installer] Failed to create .mcp-unity directory: {e.Message}");
                }
            }
        }

        [MenuItem("System/Install/Verify Installation")]
        public static void VerifyInstallation()
        {
            Debug.Log("[MCP Installer] Verifying installation...");
            
            // simple checks
            bool packageExists = File.Exists("Packages/com.coplaydev.unity-mcp/package.json");
            Debug.Log($"[MCP Installer] Package.json found: {packageExists}");
            
            if (packageExists)
            {
                EditorUtility.DisplayDialog("MCP Installation", "MCP for Unity is installed correctly.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("MCP Installation", "Error: package.json not found in Packages/com.coplaydev.unity-mcp", "OK");
            }
        }
    }
}
