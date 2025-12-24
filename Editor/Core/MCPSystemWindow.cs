#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.NativeServer.Core;
using Debug = UnityEngine.Debug;

namespace MCPForUnity.Editor.Core
{
    /// <summary>
    /// MCP System Control Panel - Unified management interface
    /// Features: Server control, dependency checking, configuration, troubleshooting
    /// </summary>
    public class MCPSystemWindow : EditorWindow
    {
        private const string VERSION = "1.0.0";
        private const int DEFAULT_PORT = 8080;
        private const float UPDATE_INTERVAL = 2f;

        #region UI State
        private Vector2 scrollPos;
        private float lastUpdateTime;
        private bool showServerSection = true;
        private bool showDependencySection = true;
        private bool showTroubleshootingSection = false;
        private bool showConfigSection = false;
        private bool showInfoSection = true;
        #endregion

        #region Status State
        private bool serverOnline = false;
        private Dictionary<string, MCPDependencyChecker.DependencyStatus> dependencyStatus;
        private List<string> troubleshootingTips;
        private string overallStatus = "CHECKING";
        private int customToolCount = 0;
        private bool showRestartNotification = false;
        private int serverRestartAttempts = 0;
        private float restartNotificationTime = 0f;
        #endregion

        #region Configuration
        private int configPort = DEFAULT_PORT;
        private string configUVPath = "";
        private int configLogLevel = 0;
        private bool configAutoStart = false;
        private bool configAutoRestart = true; // Auto-restart when server crashes
        private bool configShowTerminal = true;
        private bool useCustomServer = true; // Use Unity MCP Vibe server
        private bool useNativeServer = true; // Use Native C# server (recommended!)
        #endregion

        #region Colors
        private static readonly Color COLOR_ONLINE = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color COLOR_OFFLINE = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color COLOR_WARNING = new Color(0.9f, 0.7f, 0.2f);
        private static readonly Color COLOR_INFO = new Color(0.3f, 0.5f, 0.8f);
        private static readonly Color COLOR_DARK = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color COLOR_SECTION = new Color(0.2f, 0.22f, 0.25f);
        private static readonly Color COLOR_HEADER = new Color(0.12f, 0.12f, 0.15f);
        #endregion

        #region Menu Items

        [MenuItem("System/MCP Control Panel %#m", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPSystemWindow>("MCP System");
            window.minSize = new Vector2(450, 600);
            window.RefreshAll();
        }

        [MenuItem("System/Start Server", false, 20)]
        public static void MenuStartServer()
        {
            StartServerInternal();
        }

        [MenuItem("System/Stop Server", false, 21)]
        public static void MenuStopServer()
        {
            StopServerInternal();
        }

        [MenuItem("System/Restart Server", false, 22)]
        public static void MenuRestartServer()
        {
            // First stop the server
            StopServerInternal();
            
            // Force kill any lingering processes just in case
            if (MCPNativeServer.Instance != null)
            {
                MCPNativeServer.Instance.KillServerProcess();
            }
            
            // Wait a brief moment before starting to ensure sockets are freed
            EditorApplication.delayCall += () => 
            {
                 // Use a secondary delay to give the OS time to release the port
                 double startTime = EditorApplication.timeSinceStartup;
                 EditorApplication.CallbackFunction starter = null;
                 starter = () => 
                 {
                     if (EditorApplication.timeSinceStartup - startTime > 0.5f)
                     {
                         EditorApplication.update -= starter;
                         StartServerInternal();
                         Debug.Log("[MCP System] Server restart command issued.");
                     }
                 };
                 EditorApplication.update += starter;
            };
        }

        [MenuItem("System/Check Dependencies", false, 40)]
        public static void MenuCheckDependencies()
        {
            var status = MCPDependencyChecker.CheckAll();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== MCP Dependency Status ===\n");

            foreach (var kvp in status)
            {
                string icon = kvp.Value.IsInstalled ? "OK" : "X";
                sb.AppendLine($"[{icon}] {kvp.Key}: {(kvp.Value.IsInstalled ? kvp.Value.Version : kvp.Value.Error)}");
                if (kvp.Value.IsInstalled && !string.IsNullOrEmpty(kvp.Value.Path))
                    sb.AppendLine($"    Path: {kvp.Value.Path}");
            }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("MCP Dependencies", sb.ToString(), "OK");
        }

        [MenuItem("System/Refresh Unity", false, 60)]
        public static void MenuRefreshUnity()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[MCP System] Unity refreshed");
        }

        // Scene State submenu
        [MenuItem("System/Scene State/Export Current Scene %#e", false, 80)]
        public static void MenuExportScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string path = EditorUtility.SaveFilePanel("Export Scene State", Application.dataPath, sceneName + "_state", "json");
            if (!string.IsNullOrEmpty(path))
            {
                // Will be implemented with Vibe migration
                Debug.Log($"[MCP System] Scene export to: {path}");
                EditorUtility.DisplayDialog("Scene Export", "Scene state export will be available after Vibe migration.", "OK");
            }
        }

        [MenuItem("System/Scene State/Import from State...", false, 81)]
        public static void MenuImportScene()
        {
            string path = EditorUtility.OpenFilePanel("Import Scene State", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                // Will be implemented with Vibe migration
                Debug.Log($"[MCP System] Scene import from: {path}");
                EditorUtility.DisplayDialog("Scene Import", "Scene state import will be available after Vibe migration.", "OK");
            }
        }

        // Development submenu
        [MenuItem("System/Development/Force Recompile %#u", false, 100)]
        public static void MenuForceRecompile()
        {
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[MCP System] Script recompilation requested");
        }

        [MenuItem("System/Development/Force Asset Refresh", false, 101)]
        public static void MenuForceAssetRefresh()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("[MCP System] Asset database force refreshed");
        }

        [MenuItem("System/Development/Clear Console", false, 102)]
        public static void MenuClearConsole()
        {
            var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntries != null)
            {
                var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clearMethod?.Invoke(null, null);
            }
            Debug.Log("[MCP System] Console cleared");
        }

        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            LoadConfig();
            RefreshAll();
            EditorApplication.update += OnEditorUpdate;

            // Subscribe to server events for immediate updates
            if (MCPNativeServer.Instance != null)
            {
                MCPNativeServer.Instance.OnServerStarted += OnServerStateChanged;
                MCPNativeServer.Instance.OnServerStopped += OnServerStateChanged;
                MCPNativeServer.Instance.OnServerRestarted += OnServerRestarted;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            // Unsubscribe from server events
            if (MCPNativeServer.Instance != null)
            {
                MCPNativeServer.Instance.OnServerStarted -= OnServerStateChanged;
                MCPNativeServer.Instance.OnServerStopped -= OnServerStateChanged;
                MCPNativeServer.Instance.OnServerRestarted -= OnServerRestarted;
            }

            SaveConfig();
        }

        private void OnServerStateChanged()
        {
            RefreshServerStatus();
            Repaint();
        }

        private void OnServerRestarted(int attemptCount)
        {
            serverRestartAttempts = attemptCount;
            showRestartNotification = true;
            restartNotificationTime = Time.realtimeSinceStartup;
            RefreshServerStatus();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (Time.realtimeSinceStartup - lastUpdateTime > UPDATE_INTERVAL)
            {
                lastUpdateTime = Time.realtimeSinceStartup;
                RefreshServerStatus();
                Repaint();
            }

            // Auto-hide restart notification after 10 seconds
            if (showRestartNotification && Time.realtimeSinceStartup - restartNotificationTime > 10f)
            {
                showRestartNotification = false;
                Repaint();
            }
        }

        #endregion

        #region Main GUI

        private void OnGUI()
        {
            DrawHeader();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawStatusBar();
            EditorGUILayout.Space(5);

            DrawServerSection();
            DrawDependencySection();
            DrawTroubleshootingSection();
            DrawConfigSection();
            DrawInfoSection();

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        #endregion

        #region Header & Footer

        private void DrawHeader()
        {
            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(50));
            EditorGUI.DrawRect(headerRect, COLOR_HEADER);

            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            var titleRect = new Rect(headerRect.x + 15, headerRect.y, headerRect.width - 130, headerRect.height);
            EditorGUI.LabelField(titleRect, "MCP SYSTEM CONTROL", titleStyle);

            // Version
            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            var versionRect = new Rect(headerRect.x + 200, headerRect.y + 18, 50, 20);
            EditorGUI.LabelField(versionRect, $"v{VERSION}", versionStyle);

            // Status Badge
            var badgeRect = new Rect(headerRect.xMax - 110, headerRect.y + 12, 100, 26);
            DrawStatusBadge(badgeRect);
        }

        private void DrawStatusBadge(Rect rect)
        {
            Color badgeColor = serverOnline ? COLOR_ONLINE : COLOR_OFFLINE;
            EditorGUI.DrawRect(rect, badgeColor);

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            string statusText = serverOnline ? "ONLINE" : "OFFLINE";
            EditorGUI.LabelField(rect, statusText, labelStyle);
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Mode
            GUI.contentColor = EditorApplication.isPlaying ? Color.green : Color.gray;
            GUILayout.Label(EditorApplication.isPlaying ? "PLAYING" : "EDITOR", EditorStyles.boldLabel, GUILayout.Width(60));
            GUI.contentColor = Color.white;

            GUILayout.Label("|", GUILayout.Width(10));

            // Compiling
            GUI.contentColor = EditorApplication.isCompiling ? COLOR_WARNING : Color.gray;
            GUILayout.Label(EditorApplication.isCompiling ? "COMPILING" : "READY", GUILayout.Width(70));
            GUI.contentColor = Color.white;

            GUILayout.Label("|", GUILayout.Width(10));

            // Tools
            GUILayout.Label($"Tools: {customToolCount}", GUILayout.Width(70));

            GUILayout.FlexibleSpace();

            // Refresh button
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };
            GUILayout.Label("MCP System Control | Unity-MCP Integration", footerStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Server Section

        private void DrawServerSection()
        {
            showServerSection = DrawSectionHeader("SERVER CONTROL", COLOR_INFO, showServerSection);
            if (!showServerSection) return;

            EditorGUILayout.BeginVertical(CreateBoxStyle(new Color(0.18f, 0.2f, 0.25f)));

            // Status line
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Status:", EditorStyles.boldLabel, GUILayout.Width(60));

            GUI.contentColor = serverOnline ? COLOR_ONLINE : COLOR_OFFLINE;
            GUILayout.Label(serverOnline ? "ONLINE" : "OFFLINE", EditorStyles.boldLabel, GUILayout.Width(70));
            GUI.contentColor = Color.white;

            GUILayout.Label($"Port: {configPort}", GUILayout.Width(80));
            GUILayout.Label($"URL: http://localhost:{configPort}", GUILayout.Width(180));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Show restart notification if server auto-restarted
            if (showRestartNotification && serverRestartAttempts > 0)
            {
                EditorGUILayout.HelpBox($"Server auto-restarted (attempt {serverRestartAttempts})", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Control buttons
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = COLOR_ONLINE;
            GUI.enabled = !serverOnline;
            if (GUILayout.Button("START", GUILayout.Height(35)))
            {
                StartServerInternal();
                EditorApplication.delayCall += RefreshServerStatus;
            }

            GUI.backgroundColor = COLOR_OFFLINE;
            GUI.enabled = serverOnline;
            if (GUILayout.Button("STOP", GUILayout.Height(35)))
            {
                StopServerInternal();
                EditorApplication.delayCall += RefreshServerStatus;
            }

            GUI.backgroundColor = COLOR_WARNING;
            GUI.enabled = true;
            if (GUILayout.Button("RESTART", GUILayout.Height(35)))
            {
                StopServerInternal();
                EditorApplication.delayCall += () =>
                {
                    StartServerInternal();
                    RefreshServerStatus();
                };
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Options Row 1
            EditorGUILayout.BeginHorizontal();
            configAutoStart = EditorGUILayout.ToggleLeft("Auto-start on Unity launch", configAutoStart, GUILayout.Width(200));
            configShowTerminal = EditorGUILayout.ToggleLeft("Show terminal window", configShowTerminal, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // Options Row 2 - Auto-Restart
            EditorGUILayout.BeginHorizontal();
            bool oldAutoRestart = configAutoRestart;
            configAutoRestart = EditorGUILayout.ToggleLeft("Auto-restart on crash", configAutoRestart, GUILayout.Width(200));
            if (oldAutoRestart != configAutoRestart)
            {
                MCPNativeServer.AutoRestart = configAutoRestart;
                Debug.Log($"[MCP System] Auto-restart {(configAutoRestart ? "enabled" : "disabled")}");
            }

            // Show restart count if native server is running
            if (MCPNativeServer.Instance.IsRunning)
            {
                GUILayout.Label($"(Restart attempts: {MCPNativeServer.Instance.RestartCount})", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        #endregion

        #region Dependencies Section

        private void DrawDependencySection()
        {
            showDependencySection = DrawSectionHeader("DEPENDENCIES", COLOR_WARNING, showDependencySection);
            if (!showDependencySection) return;

            EditorGUILayout.BeginVertical(CreateBoxStyle(new Color(0.2f, 0.2f, 0.18f)));

            if (dependencyStatus == null)
            {
                EditorGUILayout.HelpBox("Click 'Check All' to scan dependencies.", MessageType.Info);
            }
            else
            {
                foreach (var kvp in dependencyStatus)
                {
                    DrawDependencyRow(kvp.Key, kvp.Value);
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Check All", GUILayout.Height(28)))
            {
                RefreshDependencies();
            }

            GUI.enabled = dependencyStatus != null &&
                          dependencyStatus.TryGetValue("UV", out var uv) && !uv.IsInstalled;
            if (GUILayout.Button("Install UV", GUILayout.Height(28)))
            {
                Application.OpenURL("https://docs.astral.sh/uv/getting-started/installation/");
            }
            GUI.enabled = true;

            if (GUILayout.Button("Help", GUILayout.Height(28)))
            {
                Application.OpenURL("https://github.com/anthropics/unity-mcp");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private void DrawDependencyRow(string name, MCPDependencyChecker.DependencyStatus status)
        {
            EditorGUILayout.BeginHorizontal();

            // Icon
            GUI.contentColor = status.IsInstalled ? COLOR_ONLINE : COLOR_OFFLINE;
            GUILayout.Label(status.IsInstalled ? "[OK]" : "[X]", EditorStyles.boldLabel, GUILayout.Width(35));
            GUI.contentColor = Color.white;

            // Name
            GUILayout.Label(name, EditorStyles.boldLabel, GUILayout.Width(80));

            // Status/Version
            if (status.IsInstalled)
            {
                GUILayout.Label(status.Version ?? "Installed", GUILayout.Width(100));
            }
            else
            {
                GUI.contentColor = COLOR_OFFLINE;
                GUILayout.Label(status.Error ?? "Not found", GUILayout.Width(100));
                GUI.contentColor = Color.white;
            }

            GUILayout.FlexibleSpace();

            // Path (truncated)
            if (!string.IsNullOrEmpty(status.Path))
            {
                string shortPath = status.Path.Length > 40
                    ? "..." + status.Path.Substring(status.Path.Length - 37)
                    : status.Path;
                GUILayout.Label(shortPath, EditorStyles.miniLabel, GUILayout.Width(200));
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Troubleshooting Section

        private void DrawTroubleshootingSection()
        {
            showTroubleshootingSection = DrawSectionHeader("TROUBLESHOOTING", COLOR_WARNING, showTroubleshootingSection);
            if (!showTroubleshootingSection) return;

            EditorGUILayout.BeginVertical(CreateBoxStyle(new Color(0.22f, 0.18f, 0.15f)));

            if (troubleshootingTips == null || troubleshootingTips.Count == 0)
            {
                EditorGUILayout.HelpBox("Run 'Check All' in Dependencies to get troubleshooting tips.", MessageType.Info);
            }
            else
            {
                foreach (var tip in troubleshootingTips)
                {
                    if (tip == "---")
                    {
                        EditorGUILayout.Space(5);
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("->", GUILayout.Width(20));
                    EditorGUILayout.SelectableLabel(tip, EditorStyles.wordWrappedLabel, GUILayout.Height(20));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Run Diagnostics", GUILayout.Height(28)))
            {
                RunDiagnostics();
            }

            if (GUILayout.Button("View Console", GUILayout.Height(28)))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Console");
            }

            if (GUILayout.Button("Restart Unity", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Restart Unity",
                    "This will restart Unity. Save your work first!\n\nContinue?", "Restart", "Cancel"))
                {
                    EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        #endregion

        #region Config Section

        private void DrawConfigSection()
        {
            showConfigSection = DrawSectionHeader("CONFIGURATION", COLOR_INFO, showConfigSection);
            if (!showConfigSection) return;

            EditorGUILayout.BeginVertical(CreateBoxStyle(new Color(0.18f, 0.18f, 0.22f)));

            // Native Server Toggle (RECOMMENDED!)
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = useNativeServer ? new Color(0.2f, 0.8f, 0.4f) : Color.gray;
            useNativeServer = EditorGUILayout.ToggleLeft("NATIVE C# SERVER (Recommended!)", useNativeServer, EditorStyles.boldLabel);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (useNativeServer)
            {
                EditorGUILayout.HelpBox("Native C# Server: Zero Python, Maximum Performance, Direct Unity API!", MessageType.Info);
            }
            else
            {
                // Python Server Mode Toggle
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = useCustomServer ? new Color(0.6f, 0.2f, 0.8f) : Color.gray;
                useCustomServer = EditorGUILayout.ToggleLeft("Use Unity MCP Vibe (Python Server)", useCustomServer, EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (useCustomServer)
                {
                    EditorGUILayout.HelpBox("Python Server: Custom branded server with FastMCP", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(5);

            // Port
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("HTTP Port:", GUILayout.Width(100));
            configPort = EditorGUILayout.IntField(configPort, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // UV Path (Auto-detected or Override)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("UV Path:", GUILayout.Width(100));

            // Show auto-detected path if no override
            string displayPath = string.IsNullOrEmpty(configUVPath) ? GetUVPath() ?? "(not found)" : configUVPath;
            GUI.enabled = false;
            EditorGUILayout.TextField(displayPath);
            GUI.enabled = true;

            if (GUILayout.Button("Auto", GUILayout.Width(40)))
            {
                configUVPath = ""; // Clear override to use auto-detection
                string detected = GetUVPath();
                if (detected != null)
                    Debug.Log($"[MCP System] UV auto-detected: {detected}");
                else
                    Debug.LogWarning("[MCP System] UV not found. Please install UV.");
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select UV executable", "", "exe");
                if (!string.IsNullOrEmpty(path)) configUVPath = path;
            }
            EditorGUILayout.EndHorizontal();

            // Show status
            if (string.IsNullOrEmpty(configUVPath))
            {
                string autoPath = GetUVPath();
                if (autoPath != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(100);
                    GUI.contentColor = COLOR_ONLINE;
                    GUILayout.Label($"Auto-detected", EditorStyles.miniLabel);
                    GUI.contentColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
            }

            // Log Level
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Log Level:", GUILayout.Width(100));
            configLogLevel = EditorGUILayout.Popup(configLogLevel, new[] { "Error", "Warning", "Info", "Debug" }, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Config", GUILayout.Height(28)))
            {
                SaveConfig();
                Debug.Log("[MCP System] Configuration saved");
            }

            if (GUILayout.Button("Reset Defaults", GUILayout.Height(28)))
            {
                ResetConfigToDefaults();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        #endregion

        #region Info Section

        private void DrawInfoSection()
        {
            showInfoSection = DrawSectionHeader("SYSTEM INFO", new Color(0.3f, 0.3f, 0.35f), showInfoSection);
            if (!showInfoSection) return;

            EditorGUILayout.BeginVertical(CreateBoxStyle(new Color(0.16f, 0.16f, 0.18f)));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Unity: {Application.unityVersion}", GUILayout.Width(150));
            GUILayout.Label($"Platform: {Application.platform}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Custom Tools: {customToolCount}", GUILayout.Width(150));
            GUILayout.Label($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            int objectCount = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            GUILayout.Label($"Objects: {objectCount}", GUILayout.Width(150));
            GUILayout.Label($"Compiling: {(EditorApplication.isCompiling ? "Yes" : "No")}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        #endregion

        #region Helper Methods

        private bool DrawSectionHeader(string title, Color color, bool isExpanded)
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(26));
            EditorGUI.DrawRect(rect, color);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            var arrowRect = new Rect(rect.x + 5, rect.y, 20, rect.height);
            var titleRect = new Rect(rect.x + 25, rect.y, rect.width - 30, rect.height);

            EditorGUI.LabelField(arrowRect, isExpanded ? "v" : ">", style);
            EditorGUI.LabelField(titleRect, title, style);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                isExpanded = !isExpanded;
                Event.current.Use();
                Repaint();
            }

            return isExpanded;
        }

        private GUIStyle CreateBoxStyle(Color bgColor)
        {
            var style = new GUIStyle(EditorStyles.helpBox);
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, bgColor);
            tex.Apply();
            style.normal.background = tex;
            style.padding = new RectOffset(10, 10, 8, 8);
            return style;
        }

        #endregion

        #region Data Methods

        private void RefreshAll()
        {
            RefreshServerStatus();
            RefreshDependencies();
            RefreshToolCount();
        }

        private void RefreshServerStatus()
        {
            // Check Native Server first
            if (MCPNativeServer.Instance.IsRunning)
            {
                serverOnline = true;
                return;
            }

            // Fall back to TCP check
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("localhost", configPort, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    serverOnline = success && client.Connected;
                    if (success) client.EndConnect(result);
                }
            }
            catch
            {
                serverOnline = false;
            }
        }

        private void RefreshDependencies()
        {
            dependencyStatus = MCPDependencyChecker.CheckAll();
            troubleshootingTips = MCPDependencyChecker.GetTroubleshootingTips(dependencyStatus);
            overallStatus = MCPDependencyChecker.GetOverallStatus(dependencyStatus);
        }

        private void RefreshToolCount()
        {
            // Get actual count from Native Server
            if (MCPNativeServer.Instance.IsRunning)
            {
                customToolCount = MCPNativeServer.Instance.ToolCount;
            }
            else
            {
                customToolCount = 84; // Fallback known count
            }
        }

        private static void StartServerInternal()
        {
            try
            {
                bool useNative = EditorPrefs.GetBool("MCP_UseNativeServer", true);
                bool useCustom = EditorPrefs.GetBool("MCP_UseCustomServer", true);
                int port = EditorPrefs.GetInt("MCP_Port", DEFAULT_PORT);

                if (useNative)
                {
                    // Native C# Server - BEST OPTION!
                    StartNativeServer(port);
                }
                else if (useCustom)
                {
                    // Python-based custom server
                    StartCustomServer(port);
                }
                else
                {
                    // Original FastMCP server
                    var serverService = new ServerManagementService();
                    serverService.StartLocalHttpServer();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP System] Failed to start server: {ex.Message}");
            }
        }

        private static void StartNativeServer(int port)
        {
            try
            {
                var server = MCPNativeServer.Instance;
                server.Start(port);
                Debug.Log($"[MCP Native] Server started on port {port} with {server.ToolCount} tools!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Native] Failed to start: {ex.Message}");
                Debug.LogWarning("[MCP Native] Falling back to Python server...");
                StartCustomServer(port);
            }
        }

        private static void StartCustomServer(int port)
        {
            // Find the custom server path
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string serverPath = System.IO.Path.Combine(projectPath, "Server");

            if (!System.IO.Directory.Exists(serverPath))
            {
                Debug.LogError($"[MCP System] Custom server not found at: {serverPath}");
                Debug.LogError("[MCP System] Falling back to default server...");
                var serverService = new ServerManagementService();
                serverService.StartLocalHttpServer();
                return;
            }

            // Build the command
            string uvPath = GetUVPath();
            if (string.IsNullOrEmpty(uvPath))
            {
                Debug.LogError("[MCP System] UV not found. Please install UV first.");
                return;
            }

            string logLevel = GetLogLevelString();
            string args = $"run unity-mcp-vibe --port {port} --log-level {logLevel}";

            Debug.Log($"[Unity MCP Vibe] Starting custom server on port {port}...");
            Debug.Log($"[Unity MCP Vibe] Command: {uvPath} {args}");
            Debug.Log($"[Unity MCP Vibe] Working Directory: {serverPath}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = uvPath,
                    Arguments = args,
                    WorkingDirectory = serverPath,
                    UseShellExecute = true,
                    CreateNoWindow = !EditorPrefs.GetBool("MCP_ShowTerminal", true)
                };

                Process.Start(startInfo);
                Debug.Log("[Unity MCP Vibe] Server started successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unity MCP Vibe] Failed to start: {ex.Message}");
            }
        }

        private static string GetUVPath()
        {
            // Check EditorPrefs first
            string customPath = EditorPrefs.GetString("MCP_UVPath", "");
            if (!string.IsNullOrEmpty(customPath) && System.IO.File.Exists(customPath))
                return customPath;

            // Check common paths
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] possiblePaths = new[]
            {
                System.IO.Path.Combine(userProfile, ".local", "bin", "uv.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "uv", "uv.exe"),
                System.IO.Path.Combine(userProfile, ".cargo", "bin", "uv.exe"),
                "uv" // Try PATH
            };

            foreach (var path in possiblePaths)
            {
                if (path == "uv" || System.IO.File.Exists(path))
                    return path;
            }

            return null;
        }

        private static string GetLogLevelString()
        {
            int level = EditorPrefs.GetInt("MCP_LogLevel", 2);
            return level switch
            {
                0 => "ERROR",
                1 => "WARNING",
                2 => "INFO",
                3 => "DEBUG",
                _ => "WARNING"
            };
        }

        private static void StopServerInternal()
        {
            try
            {
                // Try to stop Native server first
                if (MCPNativeServer.Instance.IsRunning)
                {
                    MCPNativeServer.Instance.Stop();
                    Debug.Log("[MCP Native] Server stopped");
                    return;
                }

                // Fall back to stopping Python server
                var serverService = new ServerManagementService();
                serverService.StopLocalHttpServer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP System] Failed to stop server: {ex.Message}");
            }
        }

        private void RunDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== MCP System Diagnostics ===\n");

            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Platform: {Application.platform}");
            sb.AppendLine($"Server Status: {(serverOnline ? "Online" : "Offline")}");
            sb.AppendLine($"Port: {configPort}");
            sb.AppendLine($"Compiling: {EditorApplication.isCompiling}");
            sb.AppendLine($"Playing: {EditorApplication.isPlaying}");
            sb.AppendLine("");

            sb.AppendLine("Dependencies:");
            if (dependencyStatus != null)
            {
                foreach (var kvp in dependencyStatus)
                {
                    sb.AppendLine($"  {kvp.Key}: {(kvp.Value.IsInstalled ? "OK - " + kvp.Value.Version : "MISSING")}");
                }
            }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("MCP Diagnostics", sb.ToString(), "OK");
        }

        #endregion

        #region Config Persistence

        private void LoadConfig()
        {
            configPort = EditorPrefs.GetInt("MCP_Port", DEFAULT_PORT);
            configUVPath = EditorPrefs.GetString("MCP_UVPath", "");
            configLogLevel = EditorPrefs.GetInt("MCP_LogLevel", 2);
            configAutoStart = EditorPrefs.GetBool("MCP_AutoStart", false);
            configAutoRestart = EditorPrefs.GetBool("MCP_Native_AutoRestart", true);
            configShowTerminal = EditorPrefs.GetBool("MCP_ShowTerminal", true);
            useCustomServer = EditorPrefs.GetBool("MCP_UseCustomServer", true);
            useNativeServer = EditorPrefs.GetBool("MCP_UseNativeServer", true);

            // Sync with Native Server
            MCPNativeServer.AutoRestart = configAutoRestart;
        }

        private void SaveConfig()
        {
            EditorPrefs.SetInt("MCP_Port", configPort);
            EditorPrefs.SetString("MCP_UVPath", configUVPath);
            EditorPrefs.SetInt("MCP_LogLevel", configLogLevel);
            EditorPrefs.SetBool("MCP_AutoStart", configAutoStart);
            EditorPrefs.SetBool("MCP_Native_AutoRestart", configAutoRestart);
            EditorPrefs.SetBool("MCP_ShowTerminal", configShowTerminal);
            EditorPrefs.SetBool("MCP_UseCustomServer", useCustomServer);
            EditorPrefs.SetBool("MCP_UseNativeServer", useNativeServer);

            // Sync with Native Server
            MCPNativeServer.AutoRestart = configAutoRestart;
        }

        private void ResetConfigToDefaults()
        {
            configPort = DEFAULT_PORT;
            configUVPath = "";
            configLogLevel = 2;
            configAutoStart = false;
            configAutoRestart = true; // Default to enabled
            configShowTerminal = true;
            useCustomServer = true;
            useNativeServer = true; // Default to Native!
            SaveConfig();
            Debug.Log("[MCP System] Configuration reset to defaults");
        }

        #endregion
    }
}
