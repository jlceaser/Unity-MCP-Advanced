using System;
using System.Collections.Generic;
using UnityEngine;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// ScriptableObject that caches tool metadata at build time.
    /// This eliminates runtime reflection for tool discovery.
    /// </summary>
    public class MCPToolMetadataCache : ScriptableObject
    {
        private const string AssetPath = "Packages/com.jlceaser.unity-mcp-vibe/Editor/Resources/MCPToolMetadataCache.asset";

        [SerializeField]
        private List<CachedToolEntry> _tools = new List<CachedToolEntry>();

        [SerializeField]
        private string _generatedAt;

        [SerializeField]
        private string _unityVersion;

        [SerializeField]
        private int _toolCount;

        public IReadOnlyList<CachedToolEntry> Tools => _tools;
        public string GeneratedAt => _generatedAt;
        public string UnityVersion => _unityVersion;
        public int ToolCount => _toolCount;

        public void SetTools(List<CachedToolEntry> tools)
        {
            _tools = tools ?? new List<CachedToolEntry>();
            _toolCount = _tools.Count;
            _generatedAt = DateTime.UtcNow.ToString("o");
            _unityVersion = Application.unityVersion;
        }

        public static string GetAssetPath() => AssetPath;

        /// <summary>
        /// Cached tool entry - serializable version of tool metadata
        /// </summary>
        [Serializable]
        public class CachedToolEntry
        {
            public string Name;
            public string Description;
            public string ClassName;
            public string Namespace;
            public string AssemblyName;
            public string FullTypeName;
            public bool AutoRegister = true;
            public bool RequiresPolling;
            public string PollAction;
            public bool StructuredOutput;
            public List<CachedParameter> Parameters = new List<CachedParameter>();
        }

        [Serializable]
        public class CachedParameter
        {
            public string Name;
            public string Description;
            public string Type;
            public bool Required;
            public string DefaultValue;
        }
    }
}
