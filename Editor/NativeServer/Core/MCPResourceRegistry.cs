#nullable disable
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using MCPForUnity.Editor.NativeServer.Protocol;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Resource handler delegate
    /// </summary>
    public delegate Task<MCPResourceContent> MCPResourceHandler(string uri);

    /// <summary>
    /// Synchronous resource handler delegate
    /// </summary>
    public delegate MCPResourceContent MCPSyncResourceHandler(string uri);

    /// <summary>
    /// Resource registration info
    /// </summary>
    public class MCPResourceRegistration
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string MimeType { get; set; }
        public MCPResourceHandler Handler { get; set; }
        public bool IsDynamic { get; set; }
        public DateTime RegisteredAt { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// High-performance resource registry for MCP.
    /// Provides access to Unity project resources, editor state, and more.
    /// </summary>
    public class MCPResourceRegistry
    {
        private readonly ConcurrentDictionary<string, MCPResourceRegistration> _resources;
        private readonly ConcurrentDictionary<string, MCPResourceHandler> _dynamicHandlers;
        private readonly DateTime _registryStartTime;

        public int ResourceCount => _resources.Count;
        public IEnumerable<string> ResourceUris => _resources.Keys;

        public event Action<string> OnResourceRegistered;
        public event Action<string> OnResourceAccessed;

        public MCPResourceRegistry()
        {
            _resources = new ConcurrentDictionary<string, MCPResourceRegistration>(StringComparer.OrdinalIgnoreCase);
            _dynamicHandlers = new ConcurrentDictionary<string, MCPResourceHandler>(StringComparer.OrdinalIgnoreCase);
            _registryStartTime = DateTime.UtcNow;

            // Register built-in resources
            RegisterBuiltInResources();
        }

        #region Registration

        /// <summary>
        /// Register a static resource
        /// </summary>
        public void RegisterResource(string uri, string name, string description, MCPResourceHandler handler, string mimeType = "application/json")
        {
            var registration = new MCPResourceRegistration
            {
                Uri = uri,
                Name = name,
                Description = description,
                Handler = handler,
                MimeType = mimeType,
                IsDynamic = false,
                RegisteredAt = DateTime.UtcNow
            };

            _resources[uri] = registration;
            OnResourceRegistered?.Invoke(uri);
        }

        /// <summary>
        /// Register a sync resource handler
        /// </summary>
        public void RegisterResource(string uri, string name, string description, MCPSyncResourceHandler syncHandler, string mimeType = "application/json")
        {
            MCPResourceHandler asyncHandler = (u) => Task.FromResult(syncHandler(u));
            RegisterResource(uri, name, description, asyncHandler, mimeType);
        }

        /// <summary>
        /// Register a dynamic resource pattern (e.g., "unity://asset/*")
        /// </summary>
        public void RegisterDynamicHandler(string pattern, MCPResourceHandler handler)
        {
            _dynamicHandlers[pattern] = handler;
        }

        /// <summary>
        /// Unregister a resource
        /// </summary>
        public bool UnregisterResource(string uri)
        {
            return _resources.TryRemove(uri, out _);
        }

        #endregion

        #region Access

        /// <summary>
        /// Read a resource by URI
        /// </summary>
        public async Task<MCPResourcesReadResult> ReadResource(string uri)
        {
            OnResourceAccessed?.Invoke(uri);

            // Try exact match first
            if (_resources.TryGetValue(uri, out var registration))
            {
                registration.AccessCount++;
                var content = await registration.Handler(uri);
                return new MCPResourcesReadResult
                {
                    Contents = new List<MCPResourceContent> { content }
                };
            }

            // Try dynamic handlers
            foreach (var kvp in _dynamicHandlers)
            {
                if (MatchesPattern(uri, kvp.Key))
                {
                    var content = await kvp.Value(uri);
                    return new MCPResourcesReadResult
                    {
                        Contents = new List<MCPResourceContent> { content }
                    };
                }
            }

            // Not found
            return new MCPResourcesReadResult
            {
                Contents = new List<MCPResourceContent>
                {
                    new MCPResourceContent
                    {
                        Uri = uri,
                        MimeType = "text/plain",
                        Text = $"Resource not found: {uri}"
                    }
                }
            };
        }

        private bool MatchesPattern(string uri, string pattern)
        {
            if (pattern.EndsWith("*"))
            {
                string prefix = pattern.Substring(0, pattern.Length - 1);
                return uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            return uri.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region List Resources

        /// <summary>
        /// Get all resources as MCP resource definitions
        /// </summary>
        public List<MCPResource> GetAllResources()
        {
            return _resources.Values.Select(r => new MCPResource
            {
                Uri = r.Uri,
                Name = r.Name,
                Description = r.Description,
                MimeType = r.MimeType
            }).ToList();
        }

        #endregion

        #region Built-in Resources

        private void RegisterBuiltInResources()
        {
            // Editor State
            RegisterResource("unity://editor-state", "Editor State", "Current Unity Editor state", (uri) =>
            {
                var state = new
                {
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    timeSinceStartup = EditorApplication.timeSinceStartup,
                    applicationPath = EditorApplication.applicationPath,
                    unityVersion = Application.unityVersion
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(state, Formatting.Indented)
                });
            });

            // Project Info
            RegisterResource("unity://project-info", "Project Info", "Unity project information", (uri) =>
            {
                var info = new
                {
                    productName = PlayerSettings.productName,
                    companyName = PlayerSettings.companyName,
                    version = PlayerSettings.bundleVersion,
                    projectPath = Application.dataPath.Replace("/Assets", ""),
                    assetsPath = Application.dataPath,
                    platform = Application.platform.ToString(),
                    buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    unityVersion = Application.unityVersion
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(info, Formatting.Indented)
                });
            });

            // Active Scene
            RegisterResource("unity://active-scene", "Active Scene", "Current active scene information", (uri) =>
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var sceneInfo = new
                {
                    name = scene.name,
                    path = scene.path,
                    buildIndex = scene.buildIndex,
                    isDirty = scene.isDirty,
                    isLoaded = scene.isLoaded,
                    rootCount = scene.rootCount,
                    totalObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(sceneInfo, Formatting.Indented)
                });
            });

            // Scene Hierarchy
            RegisterResource("unity://scene-hierarchy", "Scene Hierarchy", "Current scene hierarchy", (uri) =>
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var rootObjects = scene.GetRootGameObjects();

                var hierarchy = rootObjects.Select(go => GetGameObjectHierarchy(go, 0)).ToList();

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(hierarchy, Formatting.Indented)
                });
            });

            // Selection
            RegisterResource("unity://selection", "Selection", "Currently selected objects", (uri) =>
            {
                var selection = Selection.gameObjects.Select(go => new
                {
                    name = go.name,
                    path = GetGameObjectPath(go),
                    active = go.activeSelf,
                    layer = LayerMask.LayerToName(go.layer),
                    tag = go.tag,
                    components = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray()
                }).ToList();

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(selection, Formatting.Indented)
                });
            });

            // Custom Tools
            RegisterResource("unity://custom-tools", "Custom Tools", "List of registered custom tools", (uri) =>
            {
                // This will be populated by the server controller
                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = "{\"tools\": []}"
                });
            });

            // Console Logs
            RegisterResource("unity://console", "Console", "Recent console log entries", (uri) =>
            {
                // Returns placeholder - actual implementation would need reflection
                var consolePlaceholder = new
                {
                    message = "Console access requires additional setup",
                    suggestion = "Use read_console tool instead"
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(consolePlaceholder, Formatting.Indented)
                });
            });

            // Server Stats
            RegisterResource("unity://server-stats", "Server Stats", "MCP server statistics", (uri) =>
            {
                var stats = new
                {
                    server = "Unity-MCP-Vibe-Native",
                    version = "2.0.0",
                    uptime = (DateTime.UtcNow - _registryStartTime).TotalSeconds,
                    resourceCount = _resources.Count,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(stats, Formatting.Indented)
                });
            });

            // Tags and Layers
            RegisterResource("unity://tags-layers", "Tags & Layers", "Project tags and layers", (uri) =>
            {
                var tagsLayers = new
                {
                    tags = UnityEditorInternal.InternalEditorUtility.tags,
                    layers = Enumerable.Range(0, 32)
                        .Select(i => new { index = i, name = LayerMask.LayerToName(i) })
                        .Where(l => !string.IsNullOrEmpty(l.name))
                        .ToList()
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(tagsLayers, Formatting.Indented)
                });
            });

            // Build Settings
            RegisterResource("unity://build-settings", "Build Settings", "Current build settings", (uri) =>
            {
                var settings = new
                {
                    activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                    development = EditorUserBuildSettings.development,
                    scenes = EditorBuildSettings.scenes.Select(s => new { s.path, s.enabled }).ToList()
                };

                return Task.FromResult(new MCPResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonConvert.SerializeObject(settings, Formatting.Indented)
                });
            });

            Debug.Log($"[MCPResourceRegistry] Registered {_resources.Count} built-in resources");
        }

        #endregion

        #region Helpers

        private object GetGameObjectHierarchy(GameObject go, int depth)
        {
            if (depth > 10) return new { name = go.name, truncated = true };

            return new
            {
                name = go.name,
                active = go.activeSelf,
                components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                children = Enumerable.Range(0, go.transform.childCount)
                    .Select(i => GetGameObjectHierarchy(go.transform.GetChild(i).gameObject, depth + 1))
                    .ToList()
            };
        }

        private string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform current = go.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        #endregion
    }
}
