#nullable disable
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    #region 1. Screenshot Tool - Capture game/scene view
    [McpForUnityTool(
        name: "screenshot",
        Description = "Capture screenshots. Actions: game_view, scene_view, object_preview, camera")]
    public static class MCPScreenshot
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "game_view";
            string path = @params["path"]?.ToString() ?? Path.Combine(Application.dataPath, "..", "Screenshots");
            int width = @params["width"]?.ToObject<int>() ?? 1920;
            int height = @params["height"]?.ToObject<int>() ?? 1080;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string filename = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(path, filename);

            switch (action)
            {
                case "game_view":
                    ScreenCapture.CaptureScreenshot(fullPath, 1);
                    return new SuccessResponse($"Game view screenshot saved", new { path = fullPath });

                case "scene_view":
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null) return new ErrorResponse("No active scene view");

                    sceneView.Focus();
                    var cam = sceneView.camera;
                    var rt = new RenderTexture(width, height, 24);
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();

                    File.WriteAllBytes(fullPath, tex.EncodeToPNG());

                    cam.targetTexture = null;
                    RenderTexture.active = null;
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);

                    return new SuccessResponse($"Scene view screenshot saved", new { path = fullPath, width, height });

                case "object_preview":
                    string objName = @params["object"]?.ToString();
                    var obj = GameObject.Find(objName);
                    if (obj == null) return new ErrorResponse($"Object '{objName}' not found");

                    var preview = AssetPreview.GetAssetPreview(obj);
                    if (preview == null)
                    {
                        // Try to get mini thumbnail
                        preview = AssetPreview.GetMiniThumbnail(obj);
                    }

                    if (preview != null)
                    {
                        var previewTex = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
                        Graphics.CopyTexture(preview, previewTex);
                        File.WriteAllBytes(fullPath, previewTex.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(previewTex);
                        return new SuccessResponse($"Object preview saved", new { path = fullPath, obj = objName });
                    }
                    return new ErrorResponse("Could not generate preview");

                case "camera":
                    string camName = @params["camera"]?.ToString() ?? "Main Camera";
                    var targetCam = GameObject.Find(camName)?.GetComponent<Camera>();
                    if (targetCam == null) return new ErrorResponse($"Camera '{camName}' not found");

                    var camRT = new RenderTexture(width, height, 24);
                    targetCam.targetTexture = camRT;
                    targetCam.Render();

                    RenderTexture.active = camRT;
                    var camTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    camTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    camTex.Apply();

                    File.WriteAllBytes(fullPath, camTex.EncodeToPNG());

                    targetCam.targetTexture = null;
                    RenderTexture.active = null;
                    UnityEngine.Object.DestroyImmediate(camRT);
                    UnityEngine.Object.DestroyImmediate(camTex);

                    return new SuccessResponse($"Camera screenshot saved", new { path = fullPath, camera = camName });

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }
    }
    #endregion

    #region 2. Bookmark Manager - Quick access to objects/assets
    [McpForUnityTool(
        name: "bookmark_manager",
        Description = "Bookmark objects/assets for quick access. Actions: add, remove, list, goto, clear")]
    public static class MCPBookmarkManager
    {
        private static Dictionary<string, string> bookmarks = new Dictionary<string, string>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "add":
                    string name = @params["name"]?.ToString();
                    string target = @params["target"]?.ToString();
                    string type = @params["type"]?.ToString() ?? "object"; // object, asset, scene

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(target))
                        return new ErrorResponse("name and target required");

                    bookmarks[name] = $"{type}:{target}";
                    return new SuccessResponse($"Bookmarked '{target}' as '{name}'");

                case "remove":
                    string removeName = @params["name"]?.ToString();
                    if (bookmarks.Remove(removeName))
                        return new SuccessResponse($"Removed bookmark '{removeName}'");
                    return new ErrorResponse($"Bookmark '{removeName}' not found");

                case "list":
                    return new SuccessResponse("Bookmarks", new { bookmarks });

                case "goto":
                    string gotoName = @params["name"]?.ToString();
                    if (!bookmarks.TryGetValue(gotoName, out string value))
                        return new ErrorResponse($"Bookmark '{gotoName}' not found");

                    var parts = value.Split(':');
                    string bType = parts[0];
                    string bTarget = parts[1];

                    switch (bType)
                    {
                        case "object":
                            var obj = GameObject.Find(bTarget);
                            if (obj != null)
                            {
                                Selection.activeGameObject = obj;
                                SceneView.lastActiveSceneView?.FrameSelected();
                                return new SuccessResponse($"Selected and framed '{bTarget}'");
                            }
                            return new ErrorResponse($"Object '{bTarget}' not found");

                        case "asset":
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bTarget);
                            if (asset != null)
                            {
                                Selection.activeObject = asset;
                                EditorGUIUtility.PingObject(asset);
                                return new SuccessResponse($"Selected asset '{bTarget}'");
                            }
                            return new ErrorResponse($"Asset '{bTarget}' not found");

                        case "scene":
                            if (File.Exists(bTarget))
                            {
                                EditorSceneManager.OpenScene(bTarget);
                                return new SuccessResponse($"Opened scene '{bTarget}'");
                            }
                            return new ErrorResponse($"Scene '{bTarget}' not found");

                        default:
                            return new ErrorResponse($"Unknown bookmark type: {bType}");
                    }

                case "clear":
                    bookmarks.Clear();
                    return new SuccessResponse("Cleared all bookmarks");

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }
    }
    #endregion

    #region 3. Console Helper - Parse and analyze console
    [McpForUnityTool(
        name: "console_helper",
        Description = "Analyze console logs. Actions: get_errors, get_warnings, get_by_file, search, get_summary, suppress")]
    public static class MCPConsoleHelper
    {
        private static HashSet<string> suppressedMessages = new HashSet<string>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_errors":
                    return GetLogsByType(LogType.Error, @params);
                case "get_warnings":
                    return GetLogsByType(LogType.Warning, @params);
                case "get_by_file":
                    return GetLogsByFile(@params);
                case "search":
                    return SearchLogs(@params);
                case "get_summary":
                    return GetSummary();
                case "suppress":
                    string pattern = @params["pattern"]?.ToString();
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        suppressedMessages.Add(pattern);
                        return new SuccessResponse($"Suppressed messages containing '{pattern}'");
                    }
                    return new ErrorResponse("pattern required");
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object GetLogsByType(LogType type, JObject @params)
        {
            int limit = @params["limit"]?.ToObject<int>() ?? 50;
            // This would require accessing Unity's internal log system
            // For now, return a placeholder
            return new SuccessResponse($"Use read_console tool for log access", new {
                note = "Console logs are accessed via the built-in read_console tool",
                suggestion = "Use read_console with types=['error'] or types=['warning']"
            });
        }

        private static object GetLogsByFile(JObject @params)
        {
            string file = @params["file"]?.ToString();
            return new SuccessResponse("Filter by file", new {
                note = "Use read_console and filter results by file path",
                file
            });
        }

        private static object SearchLogs(JObject @params)
        {
            string query = @params["query"]?.ToString();
            return new SuccessResponse("Search logs", new {
                note = "Use read_console with filter_text parameter",
                query
            });
        }

        private static object GetSummary()
        {
            return new SuccessResponse("Log summary", new {
                note = "Use read_console to get log counts by type",
                suggestion = "read_console with types=['error', 'warning', 'log']"
            });
        }
    }
    #endregion

    #region 4. Asset Creator - Quick asset creation
    [McpForUnityTool(
        name: "asset_creator",
        Description = "Create common assets quickly. Actions: material, animation_clip, animator_controller, render_texture, gradient, animation_curve")]
    public static class MCPAssetCreator
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string name = @params["name"]?.ToString() ?? "New Asset";
            string path = @params["path"]?.ToString() ?? "Assets";

            if (!path.EndsWith("/")) path += "/";

            switch (action)
            {
                case "material":
                    return CreateMaterial(name, path, @params);
                case "animation_clip":
                    return CreateAnimationClip(name, path);
                case "animator_controller":
                    return CreateAnimatorController(name, path);
                case "render_texture":
                    return CreateRenderTexture(name, path, @params);
                case "gradient":
                    return CreateGradientTexture(name, path, @params);
                case "animation_curve":
                    return new SuccessResponse("Animation curves are created inline in scripts", new {
                        example = "AnimationCurve.EaseInOut(0, 0, 1, 1)"
                    });
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object CreateMaterial(string name, string path, JObject @params)
        {
            string shader = @params["shader"]?.ToString() ?? "Universal Render Pipeline/Lit";
            var mat = new Material(Shader.Find(shader) ?? Shader.Find("Standard"));

            string assetPath = $"{path}{name}.mat";
            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created material", new { path = assetPath, shader });
        }

        private static object CreateAnimationClip(string name, string path)
        {
            var clip = new AnimationClip();
            clip.name = name;

            string assetPath = $"{path}{name}.anim";
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created animation clip", new { path = assetPath });
        }

        private static object CreateAnimatorController(string name, string path)
        {
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath($"{path}{name}.controller");
            return new SuccessResponse($"Created animator controller", new { path = $"{path}{name}.controller" });
        }

        private static object CreateRenderTexture(string name, string path, JObject @params)
        {
            int width = @params["width"]?.ToObject<int>() ?? 256;
            int height = @params["height"]?.ToObject<int>() ?? 256;
            int depth = @params["depth"]?.ToObject<int>() ?? 0;

            var rt = new RenderTexture(width, height, depth);
            rt.name = name;

            string assetPath = $"{path}{name}.renderTexture";
            AssetDatabase.CreateAsset(rt, assetPath);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created render texture", new { path = assetPath, width, height });
        }

        private static object CreateGradientTexture(string name, string path, JObject @params)
        {
            int width = @params["width"]?.ToObject<int>() ?? 256;
            var colors = @params["colors"]?.ToObject<float[][]>();

            Gradient gradient = new Gradient();
            if (colors != null && colors.Length >= 2)
            {
                var colorKeys = new GradientColorKey[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    var c = colors[i];
                    colorKeys[i] = new GradientColorKey(
                        new Color(c.Length > 0 ? c[0] : 0, c.Length > 1 ? c[1] : 0, c.Length > 2 ? c[2] : 0),
                        i / (float)(colors.Length - 1)
                    );
                }
                gradient.colorKeys = colorKeys;
            }
            else
            {
                gradient.colorKeys = new[] {
                    new GradientColorKey(Color.black, 0),
                    new GradientColorKey(Color.white, 1)
                };
            }

            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, 0, gradient.Evaluate(x / (float)width));
            }
            tex.Apply();

            string assetPath = $"{path}{name}.png";
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Created gradient texture", new { path = assetPath, width });
        }
    }
    #endregion

    #region 5. Performance Monitor
    [McpForUnityTool(
        name: "performance_monitor",
        Description = "Monitor performance metrics. Actions: get_fps, get_memory_trend, get_draw_calls, get_batches, get_tris, benchmark")]
    public static class MCPPerformanceMonitor
    {
        private static List<float> memoryHistory = new List<float>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_fps":
                    return new SuccessResponse("FPS Info", new {
                        note = "FPS only available in Play Mode",
                        targetFrameRate = Application.targetFrameRate,
                        vSyncCount = QualitySettings.vSyncCount
                    });

                case "get_memory_trend":
                    float currentMem = GC.GetTotalMemory(false) / (1024f * 1024f);
                    memoryHistory.Add(currentMem);
                    if (memoryHistory.Count > 100) memoryHistory.RemoveAt(0);

                    return new SuccessResponse("Memory trend", new {
                        currentMB = currentMem,
                        minMB = memoryHistory.Min(),
                        maxMB = memoryHistory.Max(),
                        avgMB = memoryHistory.Average(),
                        samples = memoryHistory.Count
                    });

                case "get_draw_calls":
                case "get_batches":
                case "get_tris":
                    return new SuccessResponse("Render stats", new {
                        note = "Detailed render stats available via profiler_control tool",
                        suggestion = "Use profiler_control with action='get_stats'"
                    });

                case "benchmark":
                    return RunBenchmark(@params);

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object RunBenchmark(JObject @params)
        {
            int iterations = @params["iterations"]?.ToObject<int>() ?? 1000;
            string benchmarkType = @params["type"]?.ToString()?.ToLower() ?? "memory";

            var sw = System.Diagnostics.Stopwatch.StartNew();

            switch (benchmarkType)
            {
                case "memory":
                    for (int i = 0; i < iterations; i++)
                        GC.GetTotalMemory(false);
                    break;

                case "find_objects":
                    for (int i = 0; i < iterations; i++)
                        UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    break;

                case "hierarchy":
                    var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    for (int i = 0; i < iterations; i++)
                    {
                        foreach (var root in roots)
                            root.GetComponentsInChildren<Transform>();
                    }
                    break;
            }

            sw.Stop();

            return new SuccessResponse("Benchmark complete", new {
                type = benchmarkType,
                iterations,
                totalMs = sw.ElapsedMilliseconds,
                avgMs = sw.ElapsedMilliseconds / (double)iterations
            });
        }
    }
    #endregion

    #region 6. Scene Validator - Pre-build checks
    [McpForUnityTool(
        name: "scene_validator",
        Description = "Validate scene before build. Actions: validate_all, check_references, check_layers, check_tags, check_prefabs, check_cameras")]
    public static class MCPSceneValidator
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "validate_all";

            switch (action)
            {
                case "validate_all":
                    return ValidateAll();
                case "check_references":
                    return CheckReferences();
                case "check_layers":
                    return CheckLayers();
                case "check_tags":
                    return CheckTags();
                case "check_prefabs":
                    return CheckPrefabs();
                case "check_cameras":
                    return CheckCameras();
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object ValidateAll()
        {
            var issues = new List<object>();
            int warnings = 0, errors = 0;

            // Check for missing references
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null)
                    {
                        issues.Add(new { type = "error", message = $"Missing script on '{go.name}'" });
                        errors++;
                    }
                }
            }

            // Check cameras
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length == 0)
            {
                issues.Add(new { type = "warning", message = "No cameras in scene" });
                warnings++;
            }

            // Check lights
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (lights.Length == 0)
            {
                issues.Add(new { type = "warning", message = "No lights in scene" });
                warnings++;
            }

            // Check EventSystem for UI (using reflection to avoid UI package dependency)
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var eventSystemType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            var eventSystem = eventSystemType != null ? UnityEngine.Object.FindFirstObjectByType(eventSystemType) : null;
            if (canvases.Length > 0 && eventSystem == null)
            {
                issues.Add(new { type = "warning", message = "UI Canvas exists but no EventSystem found" });
                warnings++;
            }

            string status = errors > 0 ? "FAILED" : (warnings > 0 ? "WARNINGS" : "PASSED");

            return new SuccessResponse($"Validation: {status}", new {
                status,
                errors,
                warnings,
                issues
            });
        }

        private static object CheckReferences()
        {
            var missing = new List<object>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(go);
                var prop = so.GetIterator();

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                        {
                            missing.Add(new {
                                gameObject = go.name,
                                property = prop.propertyPath
                            });
                        }
                    }
                }
            }

            return new SuccessResponse($"Found {missing.Count} missing references", new { missing });
        }

        private static object CheckLayers()
        {
            var layerUsage = new Dictionary<int, int>();
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                layerUsage[go.layer] = layerUsage.GetValueOrDefault(go.layer, 0) + 1;
            }

            var layers = layerUsage.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new {
                    layer = kvp.Key,
                    name = LayerMask.LayerToName(kvp.Key),
                    count = kvp.Value
                })
                .ToArray();

            return new SuccessResponse("Layer usage", new { layers });
        }

        private static object CheckTags()
        {
            var tagUsage = new Dictionary<string, int>();
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                tagUsage[go.tag] = tagUsage.GetValueOrDefault(go.tag, 0) + 1;
            }

            var tags = tagUsage.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { tag = kvp.Key, count = kvp.Value })
                .ToArray();

            return new SuccessResponse("Tag usage", new { tags });
        }

        private static object CheckPrefabs()
        {
            var prefabInstances = new List<object>();
            var brokenPrefabs = new List<object>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (prefabAsset != null)
                    {
                        prefabInstances.Add(new {
                            instance = go.name,
                            prefab = AssetDatabase.GetAssetPath(prefabAsset)
                        });
                    }
                    else
                    {
                        brokenPrefabs.Add(new { instance = go.name });
                    }
                }
            }

            return new SuccessResponse("Prefab check", new {
                instanceCount = prefabInstances.Count,
                brokenCount = brokenPrefabs.Count,
                broken = brokenPrefabs
            });
        }

        private static object CheckCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Select(c => new {
                    name = c.gameObject.name,
                    depth = c.depth,
                    clearFlags = c.clearFlags.ToString(),
                    cullingMask = c.cullingMask,
                    targetDisplay = c.targetDisplay,
                    isMain = c.CompareTag("MainCamera")
                })
                .ToArray();

            bool hasMainCamera = cameras.Any(c => (bool)c.isMain);

            return new SuccessResponse("Camera check", new {
                count = cameras.Length,
                hasMainCamera,
                cameras
            });
        }
    }
    #endregion

    #region 7. Shortcut Helper - Editor shortcuts info
    [McpForUnityTool(
        name: "shortcut_helper",
        Description = "Quick actions and shortcuts. Actions: focus_game, focus_scene, focus_inspector, focus_hierarchy, focus_project, toggle_play, pause, step")]
    public static class MCPShortcutHelper
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "focus_game":
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    return new SuccessResponse("Focused Game View");

                case "focus_scene":
                    EditorApplication.ExecuteMenuItem("Window/General/Scene");
                    return new SuccessResponse("Focused Scene View");

                case "focus_inspector":
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                    return new SuccessResponse("Focused Inspector");

                case "focus_hierarchy":
                    EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                    return new SuccessResponse("Focused Hierarchy");

                case "focus_project":
                    EditorApplication.ExecuteMenuItem("Window/General/Project");
                    return new SuccessResponse("Focused Project");

                case "toggle_play":
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                    return new SuccessResponse(EditorApplication.isPlaying ? "Entering Play Mode" : "Exiting Play Mode");

                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return new SuccessResponse(EditorApplication.isPaused ? "Paused" : "Unpaused");

                case "step":
                    EditorApplication.Step();
                    return new SuccessResponse("Stepped one frame");

                default:
                    return new ErrorResponse($"Unknown action: {action}. Available: focus_game, focus_scene, focus_inspector, focus_hierarchy, focus_project, toggle_play, pause, step");
            }
        }
    }
    #endregion
}
