#nullable disable
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Core
{
    /// <summary>
    /// MCP Auto-Compile System - Works WITHOUT Unity Focus!
    ///
    /// This system solves the critical issue where Unity only detects file changes
    /// when it has focus. We use multiple techniques to ensure compilation happens
    /// even when Unity is in the background.
    ///
    /// Techniques used:
    /// 1. FileSystemWatcher to detect changes immediately
    /// 2. Background thread to wake Unity editor
    /// 3. Multiple Unity API calls to force refresh
    /// 4. Aggressive polling as fallback
    /// </summary>
    [InitializeOnLoad]
    public static class MCPAutoCompileSystem
    {
        private static FileSystemWatcher _watcher;
        private static readonly object _lock = new object();
        private static bool _pendingRefresh = false;
        private static DateTime _lastChangeTime = DateTime.MinValue;
        private static DateTime _lastRefreshTime = DateTime.MinValue;
        private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxWaitTime = TimeSpan.FromSeconds(5);

        private static System.Timers.Timer _backgroundTimer;
        private static bool _isInitialized = false;

        // Track what files changed
        private static readonly Queue<string> _changedFiles = new Queue<string>();
        private const int MaxTrackedChanges = 50;

        // Settings
        public static bool Enabled
        {
            get => EditorPrefs.GetBool("MCP_AutoCompile_Enabled", true);
            set => EditorPrefs.SetBool("MCP_AutoCompile_Enabled", value);
        }

        public static bool VerboseLogging
        {
            get => EditorPrefs.GetBool("MCP_AutoCompile_Verbose", false);
            set => EditorPrefs.SetBool("MCP_AutoCompile_Verbose", value);
        }

        static MCPAutoCompileSystem()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // Setup FileSystemWatcher
            SetupFileWatcher();

            // Setup background timer for aggressive polling
            SetupBackgroundTimer();

            // Register for editor events
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting += Cleanup;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // Enable auto-refresh in Unity settings
            EnableAutoRefresh();

            Debug.Log("[MCP AutoCompile] System initialized - Focus-free compilation enabled!");
        }

        private static void SetupFileWatcher()
        {
            try
            {
                string assetsPath = Application.dataPath;

                _watcher = new FileSystemWatcher(assetsPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.cs"
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
                _watcher.Renamed += OnFileRenamed;

                _watcher.EnableRaisingEvents = true;

                if (VerboseLogging)
                    Debug.Log($"[MCP AutoCompile] FileSystemWatcher active on: {assetsPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP AutoCompile] Failed to setup FileSystemWatcher: {ex.Message}");
            }
        }

        private static void SetupBackgroundTimer()
        {
            _backgroundTimer = new System.Timers.Timer(100); // 100ms interval
            _backgroundTimer.Elapsed += (s, e) =>
            {
                if (!Enabled) return;

                lock (_lock)
                {
                    if (_pendingRefresh)
                    {
                        // Check if enough time has passed since last change
                        var timeSinceChange = DateTime.Now - _lastChangeTime;
                        var timeSinceRefresh = DateTime.Now - _lastRefreshTime;

                        if (timeSinceChange >= RefreshCooldown && timeSinceRefresh >= RefreshCooldown)
                        {
                            _pendingRefresh = false;
                            _lastRefreshTime = DateTime.Now;

                            // Schedule refresh on main thread
                            EditorApplication.delayCall += () =>
                            {
                                ForceRefreshAndCompile();
                            };
                        }
                    }
                }

                // Always try to wake Unity
                TryWakeUnity();
            };
            _backgroundTimer.AutoReset = true;
            _backgroundTimer.Start();
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!Enabled) return;
            if (!e.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return;
            if (e.FullPath.Contains("~") || e.FullPath.Contains(".tmp")) return;

            lock (_lock)
            {
                _pendingRefresh = true;
                _lastChangeTime = DateTime.Now;

                // Track changed file
                if (_changedFiles.Count >= MaxTrackedChanges)
                    _changedFiles.Dequeue();
                _changedFiles.Enqueue(e.FullPath);
            }

            if (VerboseLogging)
                Debug.Log($"[MCP AutoCompile] File changed: {Path.GetFileName(e.FullPath)}");

            // Immediately try to wake Unity
            TryWakeUnity();
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileChanged(sender, e);
        }

        private static void TryWakeUnity()
        {
            try
            {
                // Method 1: Queue player loop update (most effective)
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { }

            try
            {
                // Method 2: Empty delay call to trigger update
                EditorApplication.delayCall += () => { };
            }
            catch { }
        }

        private static void OnEditorUpdate()
        {
            if (!Enabled) return;

            // Check for pending refresh
            lock (_lock)
            {
                if (_pendingRefresh)
                {
                    var timeSinceChange = DateTime.Now - _lastChangeTime;

                    // Force refresh after max wait time even if changes keep coming
                    if (timeSinceChange >= MaxWaitTime)
                    {
                        _pendingRefresh = false;
                        ForceRefreshAndCompile();
                    }
                }
            }
        }

        /// <summary>
        /// Force Unity to refresh and recompile scripts
        /// </summary>
        public static void ForceRefreshAndCompile()
        {
            if (EditorApplication.isCompiling)
            {
                if (VerboseLogging)
                    Debug.Log("[MCP AutoCompile] Already compiling, skipping refresh");
                return;
            }

            try
            {
                if (VerboseLogging)
                    Debug.Log("[MCP AutoCompile] Forcing refresh and compile...");

                // Method 1: Request script reload
                EditorUtility.RequestScriptReload();

                // Method 2: Refresh asset database
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                _lastRefreshTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP AutoCompile] Refresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enable Unity's auto-refresh settings
        /// </summary>
        private static void EnableAutoRefresh()
        {
            // Enable auto-refresh
            EditorPrefs.SetBool("kAutoRefresh", true);

            // Set script compilation during play to "Recompile And Continue Playing"
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);

            // Disable safe mode that might block compilation
            EditorPrefs.SetBool("ScriptsSafeMode", false);
        }

        private static void OnCompilationStarted(object obj)
        {
            if (VerboseLogging)
                Debug.Log("[MCP AutoCompile] Compilation started");
        }

        private static void OnCompilationFinished(object obj)
        {
            if (VerboseLogging)
                Debug.Log("[MCP AutoCompile] Compilation finished");
        }

        /// <summary>
        /// Get list of recently changed files
        /// </summary>
        public static string[] GetRecentlyChangedFiles()
        {
            lock (_lock)
            {
                return _changedFiles.ToArray();
            }
        }

        /// <summary>
        /// Get current status
        /// </summary>
        public static object GetStatus()
        {
            return new
            {
                enabled = Enabled,
                isCompiling = EditorApplication.isCompiling,
                pendingRefresh = _pendingRefresh,
                watcherActive = _watcher?.EnableRaisingEvents ?? false,
                backgroundTimerActive = _backgroundTimer?.Enabled ?? false,
                recentChanges = GetRecentlyChangedFiles().Length,
                lastChangeTime = _lastChangeTime.ToString("HH:mm:ss"),
                lastRefreshTime = _lastRefreshTime.ToString("HH:mm:ss")
            };
        }

        /// <summary>
        /// Restart the auto-compile system
        /// </summary>
        public static void Restart()
        {
            Cleanup();
            _isInitialized = false;
            Initialize();
        }

        private static void Cleanup()
        {
            try
            {
                _backgroundTimer?.Stop();
                _backgroundTimer?.Dispose();
                _backgroundTimer = null;

                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// MCP tool for auto-compile control
    /// </summary>
    [McpForUnityTool(
        name: "auto_compile",
        Description = "Control auto-compile system. Actions: status, enable, disable, force_refresh, force_compile, restart")]
    public static class MCPAutoCompileTool
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "status";

            switch (action)
            {
                case "status":
                    return new SuccessResponse("Auto-Compile Status", MCPAutoCompileSystem.GetStatus());

                case "enable":
                    MCPAutoCompileSystem.Enabled = true;
                    return new SuccessResponse("Auto-compile enabled");

                case "disable":
                    MCPAutoCompileSystem.Enabled = false;
                    return new SuccessResponse("Auto-compile disabled");

                case "force_refresh":
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return new SuccessResponse("Asset database refreshed");

                case "force_compile":
                case "compile":
                case "recompile":
                    MCPAutoCompileSystem.ForceRefreshAndCompile();
                    return new SuccessResponse("Compilation requested");

                case "restart":
                    MCPAutoCompileSystem.Restart();
                    return new SuccessResponse("Auto-compile system restarted");

                case "verbose_on":
                    MCPAutoCompileSystem.VerboseLogging = true;
                    return new SuccessResponse("Verbose logging enabled");

                case "verbose_off":
                    MCPAutoCompileSystem.VerboseLogging = false;
                    return new SuccessResponse("Verbose logging disabled");

                default:
                    return new ErrorResponse($"Unknown action: {action}. Available: status, enable, disable, force_refresh, force_compile, restart, verbose_on, verbose_off");
            }
        }
    }
}
