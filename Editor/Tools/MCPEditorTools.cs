#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

// UI and TMPro are optional
#if UNITY_UI_AVAILABLE
using UnityEngine.UI;
#endif
#if TMPRO_AVAILABLE
using TMPro;
#endif

namespace MCPForUnity.Editor.Tools
{
    #region 1. Editor Control Tool - Fixes auto-refresh issue
    /// <summary>
    /// MCP Tool for controlling Unity Editor settings and forcing refresh/recompile.
    /// Solves the issue where Unity doesn't detect file changes until focused.
    /// </summary>
    [McpForUnityTool(
        name: "editor_control",
        Description = "Control Unity Editor: force_refresh, force_recompile, get_settings, set_auto_refresh, focus_unity")]
    public static class MCPEditorControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Editor Control ready. Actions: force_refresh, force_recompile, get_settings, set_auto_refresh, focus_unity");
            }

            switch (action)
            {
                case "force_refresh":
                    return ForceRefresh(@params);

                case "force_recompile":
                    return ForceRecompile();

                case "get_settings":
                    return GetEditorSettings();

                case "set_auto_refresh":
                    return SetAutoRefresh(@params);

                case "focus_unity":
                    return FocusUnity();

                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: force_refresh, force_recompile, get_settings, set_auto_refresh, focus_unity");
            }
        }

        private static object ForceRefresh(JObject @params)
        {
            try
            {
                bool importAll = @params["import_all"]?.ToObject<bool>() ?? false;

                if (importAll)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                }
                else
                {
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                }

                return new SuccessResponse("Asset database refreshed successfully", new { importAll });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to refresh: {e.Message}");
            }
        }

        private static object ForceRecompile()
        {
            try
            {
                // Request script compilation
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

                return new SuccessResponse("Script recompilation requested");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to request recompile: {e.Message}");
            }
        }

        private static object GetEditorSettings()
        {
            try
            {
                bool autoRefresh = EditorPrefs.GetBool("kAutoRefresh", true);
                bool autoRefreshDisabledInBackground = EditorPrefs.GetBool("kAutoRefreshDisableInPlaymode", false);
                int scriptChangesWhilePlaying = EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0);

                return new SuccessResponse("Editor settings retrieved", new
                {
                    autoRefresh,
                    autoRefreshDisabledInBackground,
                    scriptChangesWhilePlaying,
                    isPlaying = EditorApplication.isPlaying,
                    isCompiling = EditorApplication.isCompiling,
                    isFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to get settings: {e.Message}");
            }
        }

        private static object SetAutoRefresh(JObject @params)
        {
            try
            {
                bool? enabled = @params["enabled"]?.ToObject<bool>();
                if (!enabled.HasValue)
                {
                    return new ErrorResponse("Required param: enabled (true/false)");
                }

                EditorPrefs.SetBool("kAutoRefresh", enabled.Value);

                // Also refresh now if enabling
                if (enabled.Value)
                {
                    AssetDatabase.Refresh();
                }

                return new SuccessResponse($"Auto-refresh set to: {enabled.Value}", new { autoRefresh = enabled.Value });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to set auto-refresh: {e.Message}");
            }
        }

        private static object FocusUnity()
        {
            try
            {
                // Try to focus Unity Editor window
                EditorApplication.ExecuteMenuItem("Window/Panels/1 Scene");

                // Also trigger a refresh
                AssetDatabase.Refresh();

                return new SuccessResponse("Unity focus requested and assets refreshed");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to focus: {e.Message}");
            }
        }
    }
    #endregion

    #region 2. Assign Asset Reference Tool
    /// <summary>
    /// MCP Tool for assigning asset references (Materials, Textures, Prefabs, etc.) to components.
    /// </summary>
    [McpForUnityTool(
        name: "assign_asset_reference",
        Description = "Assigns asset references to component properties. Params: assetPath, targetObject, targetComponent, targetProperty")]
    public static class MCPAssetReferenceAssigner
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string assetPath = @params["assetPath"]?.ToString();
                string targetObjectName = @params["targetObject"]?.ToString();
                string targetComponentName = @params["targetComponent"]?.ToString();
                string targetPropertyName = @params["targetProperty"]?.ToString();

                if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(targetObjectName) ||
                    string.IsNullOrEmpty(targetComponentName) || string.IsNullOrEmpty(targetPropertyName))
                {
                    return new ErrorResponse("Required: assetPath, targetObject, targetComponent, targetProperty");
                }

                // Normalize asset path
                if (!assetPath.StartsWith("Assets/"))
                {
                    assetPath = "Assets/" + assetPath;
                }

                // Load asset
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    return new ErrorResponse($"Asset not found at '{assetPath}'");
                }

                // Find target GameObject
                var targetGO = GameObject.Find(targetObjectName);
                if (targetGO == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    targetGO = allObjects.FirstOrDefault(g => g.name == targetObjectName && g.scene.isLoaded);
                }
                if (targetGO == null)
                    return new ErrorResponse($"Target GameObject '{targetObjectName}' not found");

                // Get target component
                var targetComp = targetGO.GetComponent(targetComponentName);
                if (targetComp == null)
                    return new ErrorResponse($"Target component '{targetComponentName}' not found on '{targetObjectName}'");

                // Use SerializedObject for proper Unity serialization
                var serializedObject = new SerializedObject(targetComp);
                var property = serializedObject.FindProperty(targetPropertyName);

                if (property == null)
                    return new ErrorResponse($"Property '{targetPropertyName}' not found on '{targetComponentName}'");

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    return new ErrorResponse($"Property '{targetPropertyName}' is not an object reference");

                // Assign the asset reference
                property.objectReferenceValue = asset;
                serializedObject.ApplyModifiedProperties();

                // Mark scene dirty
                EditorUtility.SetDirty(targetComp);
                EditorSceneManager.MarkSceneDirty(targetGO.scene);

                return new SuccessResponse(
                    $"Assigned asset '{assetPath}' to '{targetObjectName}.{targetComponentName}.{targetPropertyName}'",
                    new {
                        asset = assetPath,
                        assetType = asset.GetType().Name,
                        target = targetObjectName,
                        targetComponent = targetComponentName,
                        property = targetPropertyName
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to assign asset reference: {e.Message}");
            }
        }
    }
    #endregion

    #region 3. Bulk Set Property Tool
    /// <summary>
    /// MCP Tool for setting the same property on multiple GameObjects at once.
    /// </summary>
    [McpForUnityTool(
        name: "bulk_set_property",
        Description = "Sets property on multiple objects. Params: searchMethod (by_tag/by_name/by_component), searchTerm, componentName, propertyName, value")]
    public static class MCPBulkSetProperty
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string searchMethod = @params["searchMethod"]?.ToString()?.ToLower();
                string searchTerm = @params["searchTerm"]?.ToString();
                string componentName = @params["componentName"]?.ToString();
                string propertyName = @params["propertyName"]?.ToString();
                var valueToken = @params["value"];

                if (string.IsNullOrEmpty(searchMethod) || string.IsNullOrEmpty(searchTerm) ||
                    string.IsNullOrEmpty(componentName) || string.IsNullOrEmpty(propertyName) || valueToken == null)
                {
                    return new ErrorResponse("Required: searchMethod, searchTerm, componentName, propertyName, value");
                }

                // Find GameObjects based on search method
                GameObject[] targets = null;
                switch (searchMethod)
                {
                    case "by_tag":
                        targets = GameObject.FindGameObjectsWithTag(searchTerm);
                        break;
                    case "by_name":
                        targets = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>()
                            .Where(g => g.name.Contains(searchTerm) && g.scene.isLoaded).ToArray();
                        break;
                    case "by_component":
                        var type = GetTypeByName(searchTerm);
                        if (type != null)
                        {
                            targets = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None)
                                .Select(c => (c as Component)?.gameObject)
                                .Where(g => g != null).ToArray();
                        }
                        break;
                    default:
                        return new ErrorResponse("searchMethod must be: by_tag, by_name, or by_component");
                }

                if (targets == null || targets.Length == 0)
                {
                    return new ErrorResponse($"No GameObjects found with {searchMethod}: '{searchTerm}'");
                }

                int successCount = 0;
                int failCount = 0;
                var errors = new List<string>();

                foreach (var go in targets)
                {
                    try
                    {
                        var comp = go.GetComponent(componentName);
                        if (comp == null)
                        {
                            failCount++;
                            continue;
                        }

                        var serializedObject = new SerializedObject(comp);
                        var property = serializedObject.FindProperty(propertyName);

                        if (property == null)
                        {
                            failCount++;
                            continue;
                        }

                        SetPropertyValue(property, valueToken);
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(comp);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        errors.Add($"{go.name}: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    EditorSceneManager.MarkAllScenesDirty();
                }

                return new SuccessResponse(
                    $"Set '{propertyName}' on {successCount}/{targets.Length} objects",
                    new { successCount, failCount, totalFound = targets.Length, errors = errors.Take(5) });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Bulk set failed: {e.Message}");
            }
        }

        private static void SetPropertyValue(SerializedProperty property, JToken value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = value.ToObject<int>();
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value.ToObject<float>();
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value.ToObject<bool>();
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value.ToString();
                    break;
                case SerializedPropertyType.Vector2:
                    var v2 = value.ToObject<float[]>();
                    property.vector2Value = new Vector2(v2[0], v2[1]);
                    break;
                case SerializedPropertyType.Vector3:
                    var v3 = value.ToObject<float[]>();
                    property.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                    break;
                case SerializedPropertyType.Color:
                    var c = value.ToObject<float[]>();
                    property.colorValue = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);
                    break;
                default:
                    throw new Exception($"Unsupported property type: {property.propertyType}");
            }
        }

        private static Type GetTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;

                type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
    #endregion

    #region 4. Create UI Element Tool (Requires UI package)
#if UNITY_UI_AVAILABLE
    /// <summary>
    /// MCP Tool for creating UI elements with proper setup in one call.
    /// </summary>
    [McpForUnityTool(
        name: "create_ui_element",
        Description = "Creates UI elements with full setup. Params: type (image/text/button/panel/slider), name, parent, anchor, color, size, text")]
    public static class MCPCreateUIElement
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string type = @params["type"]?.ToString()?.ToLower();
                string name = @params["name"]?.ToString() ?? "NewUIElement";
                string parentName = @params["parent"]?.ToString();
                string anchor = @params["anchor"]?.ToString()?.ToLower() ?? "center";
                var colorToken = @params["color"];
                var sizeToken = @params["size"];
                string text = @params["text"]?.ToString();

                if (string.IsNullOrEmpty(type))
                {
                    return new ErrorResponse("Required: type (image, text, button, panel, slider, toggle, inputfield)");
                }

                // Find or create Canvas
                Canvas canvas = null;
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parentGO = GameObject.Find(parentName);
                    if (parentGO != null)
                    {
                        canvas = parentGO.GetComponentInParent<Canvas>();
                    }
                }
                if (canvas == null)
                {
                    canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                }
                if (canvas == null)
                {
                    return new ErrorResponse("No Canvas found in scene. Create a Canvas first.");
                }

                // Determine parent transform
                Transform parent = canvas.transform;
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parentGO = GameObject.Find(parentName);
                    if (parentGO != null)
                    {
                        parent = parentGO.transform;
                    }
                }

                // Create the UI element
                GameObject uiElement = CreateUIElement(type, name, parent, text);
                if (uiElement == null)
                {
                    return new ErrorResponse($"Unknown UI type: '{type}'. Valid: image, text, button, panel, slider, toggle, inputfield");
                }

                // Configure RectTransform
                RectTransform rect = uiElement.GetComponent<RectTransform>();
                SetAnchor(rect, anchor);

                // Set size if provided
                if (sizeToken != null)
                {
                    var size = sizeToken.ToObject<float[]>();
                    if (size != null && size.Length >= 2)
                    {
                        rect.sizeDelta = new Vector2(size[0], size[1]);
                    }
                }

                // Set color if provided
                if (colorToken != null)
                {
                    var colorArr = colorToken.ToObject<float[]>();
                    if (colorArr != null && colorArr.Length >= 3)
                    {
                        Color color = new Color(colorArr[0], colorArr[1], colorArr[2], colorArr.Length > 3 ? colorArr[3] : 1f);
                        var image = uiElement.GetComponent<Image>();
                        if (image != null) image.color = color;
                    }
                }

                // Register undo and mark dirty
                Undo.RegisterCreatedObjectUndo(uiElement, $"Create UI {type}");
                EditorSceneManager.MarkSceneDirty(uiElement.scene);

                return new SuccessResponse(
                    $"Created UI {type}: '{name}'",
                    new {
                        name = uiElement.name,
                        type,
                        parent = parent.name,
                        anchor,
                        instanceID = uiElement.GetInstanceID()
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to create UI element: {e.Message}");
            }
        }

        private static GameObject CreateUIElement(string type, string name, Transform parent, string text)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            switch (type)
            {
                case "image":
                case "panel":
                    var img = go.AddComponent<Image>();
                    img.color = type == "panel" ? new Color(0, 0, 0, 0.5f) : Color.white;
                    break;

                case "text":
#if TMPRO_AVAILABLE
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.text = text ?? "Text";
                    tmp.fontSize = 24;
                    tmp.alignment = TextAlignmentOptions.Center;
#else
                    // Fallback to legacy Text
                    var legacyText = go.AddComponent<UnityEngine.UI.Text>();
                    legacyText.text = text ?? "Text";
                    legacyText.fontSize = 24;
                    legacyText.alignment = TextAnchor.MiddleCenter;
#endif
                    break;

                case "button":
                    var btnImg = go.AddComponent<Image>();
                    var btn = go.AddComponent<Button>();
                    // Create text child
                    var btnTextGO = new GameObject("Text");
                    btnTextGO.transform.SetParent(go.transform, false);
                    var btnRect = btnTextGO.AddComponent<RectTransform>();
                    btnRect.anchorMin = Vector2.zero;
                    btnRect.anchorMax = Vector2.one;
                    btnRect.offsetMin = Vector2.zero;
                    btnRect.offsetMax = Vector2.zero;
#if TMPRO_AVAILABLE
                    var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
                    btnText.text = text ?? "Button";
                    btnText.fontSize = 20;
                    btnText.alignment = TextAlignmentOptions.Center;
                    btnText.color = Color.black;
#else
                    var btnLegacyText = btnTextGO.AddComponent<UnityEngine.UI.Text>();
                    btnLegacyText.text = text ?? "Button";
                    btnLegacyText.fontSize = 20;
                    btnLegacyText.alignment = TextAnchor.MiddleCenter;
                    btnLegacyText.color = Color.black;
#endif
                    break;

                case "slider":
                    // Simplified slider - just the main component
                    var sliderImg = go.AddComponent<Image>();
                    sliderImg.color = new Color(0.3f, 0.3f, 0.3f);
                    var slider = go.AddComponent<Slider>();
                    break;

                case "toggle":
                    var toggleImg = go.AddComponent<Image>();
                    var toggle = go.AddComponent<Toggle>();
                    break;

                case "inputfield":
                    var inputImg = go.AddComponent<Image>();
#if TMPRO_AVAILABLE
                    var input = go.AddComponent<TMP_InputField>();
                    // Create text area
                    var textAreaGO = new GameObject("Text Area");
                    textAreaGO.transform.SetParent(go.transform, false);
                    var textAreaRect = textAreaGO.AddComponent<RectTransform>();
                    textAreaRect.anchorMin = Vector2.zero;
                    textAreaRect.anchorMax = Vector2.one;
                    textAreaRect.offsetMin = new Vector2(10, 5);
                    textAreaRect.offsetMax = new Vector2(-10, -5);
                    var inputText = textAreaGO.AddComponent<TextMeshProUGUI>();
                    inputText.fontSize = 18;
                    input.textComponent = inputText;
#else
                    var legacyInput = go.AddComponent<InputField>();
                    var inputTextGO = new GameObject("Text");
                    inputTextGO.transform.SetParent(go.transform, false);
                    var inputTextRect = inputTextGO.AddComponent<RectTransform>();
                    inputTextRect.anchorMin = Vector2.zero;
                    inputTextRect.anchorMax = Vector2.one;
                    var inputLegacyText = inputTextGO.AddComponent<UnityEngine.UI.Text>();
                    inputLegacyText.fontSize = 18;
                    legacyInput.textComponent = inputLegacyText;
#endif
                    break;

                default:
                    UnityEngine.Object.DestroyImmediate(go);
                    return null;
            }

            return go;
        }

        private static void SetAnchor(RectTransform rect, string anchor)
        {
            switch (anchor)
            {
                case "stretch":
                case "full":
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    break;
                case "top":
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    break;
                case "bottom":
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    break;
                case "left":
                    rect.anchorMin = new Vector2(0f, 0.5f);
                    rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    break;
                case "right":
                    rect.anchorMin = new Vector2(1f, 0.5f);
                    rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    break;
                case "topleft":
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case "topright":
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    break;
                case "bottomleft":
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.zero;
                    rect.pivot = Vector2.zero;
                    break;
                case "bottomright":
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    break;
                case "center":
                default:
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }
    }
#endif
    #endregion

    #region 5. Execute C# Tool
    /// <summary>
    /// MCP Tool for executing arbitrary C# code snippets in the Unity Editor.
    /// Use with caution - powerful but can cause issues if misused.
    /// </summary>
    [McpForUnityTool(
        name: "execute_csharp",
        Description = "Executes C# code in Unity Editor. Params: code (C# expression or statement), returnResult (bool)")]
    public static class MCPExecuteCSharp
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string code = @params["code"]?.ToString();
                bool returnResult = @params["returnResult"]?.ToObject<bool>() ?? false;

                if (string.IsNullOrEmpty(code))
                {
                    return new ErrorResponse("Required: code (C# expression or statement)");
                }

                // For safety, we'll use a whitelist of allowed operations
                // and execute common patterns directly

                // Pattern: Find and get component
                if (code.StartsWith("GameObject.Find"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"GameObject\.Find\([""'](.+?)[""']\)");
                    if (match.Success)
                    {
                        var go = GameObject.Find(match.Groups[1].Value);
                        return new SuccessResponse($"Found: {go?.name ?? "null"}", new { found = go != null, name = go?.name });
                    }
                }

                // Pattern: FindObjectOfType
                if (code.Contains("FindObjectOfType") || code.Contains("FindAnyObjectByType"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"FindObjectOfType<(.+?)>\(\)|FindAnyObjectByType<(.+?)>\(\)");
                    if (match.Success)
                    {
                        string typeName = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(typeName)) typeName = match.Groups[2].Value;
                        var type = GetTypeByName(typeName);
                        if (type != null)
                        {
                            var obj = UnityEngine.Object.FindAnyObjectByType(type);
                            return new SuccessResponse($"Found: {obj?.name ?? "null"}", new { found = obj != null, name = obj?.name, type = type.Name });
                        }
                    }
                }

                // Pattern: Selection operations
                if (code.Contains("Selection."))
                {
                    if (code.Contains("Selection.activeGameObject"))
                    {
                        var selected = Selection.activeGameObject;
                        return new SuccessResponse($"Selected: {selected?.name ?? "null"}", new { name = selected?.name });
                    }
                    if (code.Contains("Selection.objects"))
                    {
                        var selected = Selection.objects;
                        return new SuccessResponse($"Selected {selected.Length} objects",
                            new { count = selected.Length, names = selected.Select(o => o.name).ToArray() });
                    }
                }

                // Pattern: EditorApplication states
                if (code.Contains("EditorApplication."))
                {
                    if (code.Contains("isPlaying"))
                        return new SuccessResponse($"isPlaying: {EditorApplication.isPlaying}");
                    if (code.Contains("isCompiling"))
                        return new SuccessResponse($"isCompiling: {EditorApplication.isCompiling}");
                    if (code.Contains("isPaused"))
                        return new SuccessResponse($"isPaused: {EditorApplication.isPaused}");
                }

                // Pattern: Debug.Log
                if (code.StartsWith("Debug.Log"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"Debug\.Log\([""'](.+?)[""']\)");
                    if (match.Success)
                    {
                        Debug.Log($"[MCP] {match.Groups[1].Value}");
                        return new SuccessResponse("Logged message to console");
                    }
                }

                // Pattern: Time values
                if (code.Contains("Time."))
                {
                    return new SuccessResponse("Time values", new {
                        time = Time.time,
                        deltaTime = Time.deltaTime,
                        fixedDeltaTime = Time.fixedDeltaTime,
                        timeScale = Time.timeScale,
                        frameCount = Time.frameCount
                    });
                }

                // Pattern: Scene info
                if (code.Contains("SceneManager") || code.Contains("GetActiveScene"))
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    return new SuccessResponse("Active scene info", new {
                        name = scene.name,
                        path = scene.path,
                        rootCount = scene.rootCount,
                        isDirty = scene.isDirty
                    });
                }

                // If no pattern matched, explain what's supported
                return new ErrorResponse("Code pattern not recognized. Supported patterns: GameObject.Find, FindObjectOfType, Selection.*, EditorApplication.*, Debug.Log, Time.*, SceneManager");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Execution failed: {e.Message}");
            }
        }

        private static Type GetTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
                type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
    #endregion

    #region 6. Copy Component Tool
    /// <summary>
    /// MCP Tool for copying component values from one GameObject to another.
    /// </summary>
    [McpForUnityTool(
        name: "copy_component",
        Description = "Copies component values between GameObjects. Params: sourceObject, targetObject, componentName, properties (optional array)")]
    public static class MCPCopyComponent
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string sourceObjectName = @params["sourceObject"]?.ToString();
                string targetObjectName = @params["targetObject"]?.ToString();
                string componentName = @params["componentName"]?.ToString();
                var propertiesToken = @params["properties"];

                if (string.IsNullOrEmpty(sourceObjectName) || string.IsNullOrEmpty(targetObjectName) ||
                    string.IsNullOrEmpty(componentName))
                {
                    return new ErrorResponse("Required: sourceObject, targetObject, componentName. Optional: properties (array of property names)");
                }

                // Find source GameObject
                var sourceGO = GameObject.Find(sourceObjectName);
                if (sourceGO == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    sourceGO = allObjects.FirstOrDefault(g => g.name == sourceObjectName && g.scene.isLoaded);
                }
                if (sourceGO == null)
                    return new ErrorResponse($"Source GameObject '{sourceObjectName}' not found");

                // Find target GameObject
                var targetGO = GameObject.Find(targetObjectName);
                if (targetGO == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    targetGO = allObjects.FirstOrDefault(g => g.name == targetObjectName && g.scene.isLoaded);
                }
                if (targetGO == null)
                    return new ErrorResponse($"Target GameObject '{targetObjectName}' not found");

                // Get components
                var sourceComp = sourceGO.GetComponent(componentName);
                if (sourceComp == null)
                    return new ErrorResponse($"Source component '{componentName}' not found on '{sourceObjectName}'");

                var targetComp = targetGO.GetComponent(componentName);
                if (targetComp == null)
                {
                    // Add component if it doesn't exist
                    targetComp = targetGO.AddComponent(sourceComp.GetType());
                }

                // Get specific properties to copy, or all if not specified
                string[] propertiesToCopy = null;
                if (propertiesToken != null)
                {
                    propertiesToCopy = propertiesToken.ToObject<string[]>();
                }

                // Use SerializedObject to copy properties
                var sourceSerial = new SerializedObject(sourceComp);
                var targetSerial = new SerializedObject(targetComp);

                int copiedCount = 0;
                var copiedProperties = new List<string>();

                var iterator = sourceSerial.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    // Skip script reference
                    if (iterator.name == "m_Script") continue;

                    // If specific properties requested, check if this is one
                    if (propertiesToCopy != null && !propertiesToCopy.Contains(iterator.name))
                        continue;

                    var targetProp = targetSerial.FindProperty(iterator.propertyPath);
                    if (targetProp != null && targetProp.propertyType == iterator.propertyType)
                    {
                        try
                        {
                            CopyPropertyValue(iterator, targetProp);
                            copiedProperties.Add(iterator.name);
                            copiedCount++;
                        }
                        catch { }
                    }
                }

                targetSerial.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetComp);
                EditorSceneManager.MarkSceneDirty(targetGO.scene);

                return new SuccessResponse(
                    $"Copied {copiedCount} properties from '{sourceObjectName}' to '{targetObjectName}'",
                    new {
                        source = sourceObjectName,
                        target = targetObjectName,
                        component = componentName,
                        copiedCount,
                        properties = copiedProperties
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to copy component: {e.Message}");
            }
        }

        private static void CopyPropertyValue(SerializedProperty source, SerializedProperty target)
        {
            switch (source.propertyType)
            {
                case SerializedPropertyType.Integer:
                    target.intValue = source.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    target.boolValue = source.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    target.floatValue = source.floatValue;
                    break;
                case SerializedPropertyType.String:
                    target.stringValue = source.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    target.colorValue = source.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    target.objectReferenceValue = source.objectReferenceValue;
                    break;
                case SerializedPropertyType.Vector2:
                    target.vector2Value = source.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    target.vector3Value = source.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    target.vector4Value = source.vector4Value;
                    break;
                case SerializedPropertyType.Quaternion:
                    target.quaternionValue = source.quaternionValue;
                    break;
                case SerializedPropertyType.Rect:
                    target.rectValue = source.rectValue;
                    break;
                case SerializedPropertyType.Bounds:
                    target.boundsValue = source.boundsValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    target.animationCurveValue = source.animationCurveValue;
                    break;
                case SerializedPropertyType.Enum:
                    target.enumValueIndex = source.enumValueIndex;
                    break;
            }
        }
    }
    #endregion
}
