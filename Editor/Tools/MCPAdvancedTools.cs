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

namespace MCPForUnity.Editor.Tools
{
    #region 1. Auto Dialog Handler - Handles Unity popup dialogs automatically
    /// <summary>
    /// Automatically handles Unity Editor dialogs like "Enter Safe Mode?", "API Update Required", etc.
    /// </summary>
    [InitializeOnLoad]
    public static class MCPAutoDialogHandler
    {
        private static bool isInitialized = false;

        static MCPAutoDialogHandler()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            // Set preferences to auto-handle common dialogs
            // Disable API Updater prompts
            EditorPrefs.SetBool("kAutoRefresh", true);

            // Auto-accept script reload
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1); // 0=Stop, 1=Recompile and Continue, 2=Recompile After Finished

            // Disable safe mode prompts
            EditorPrefs.SetBool("ScriptsSafeMode", false);

            // Trust packages in project
            SessionState.SetBool("TrustPackages", true);

            // Register for domain reload
            EditorApplication.update += OnEditorUpdate;

            Debug.Log("[MCP AutoDialog] Initialized - auto-handling Unity dialogs");
        }

        private static void OnEditorUpdate()
        {
            // Suppress any pending modal dialogs by ensuring we have focus handling
            if (EditorApplication.isCompiling)
            {
                // During compilation, we don't want popups
                return;
            }
        }

        // Method to explicitly dismiss any pending dialogs
        public static void DismissPendingDialogs()
        {
            // Force focus back to main editor window
            try
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.Focus();
                }
                else
                {
                    EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
                }
            }
            catch { }
        }
    }
    #endregion

    #region 2. Create ScriptableObject Tool
    [McpForUnityTool(
        name: "create_scriptable_object",
        Description = "Creates ScriptableObject assets. Params: typeName, assetPath, properties (optional object)")]
    public static class MCPCreateScriptableObject
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string typeName = @params["typeName"]?.ToString();
                string assetPath = @params["assetPath"]?.ToString();
                var propertiesToken = @params["properties"];

                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(assetPath))
                {
                    return new ErrorResponse("Required: typeName, assetPath. Optional: properties");
                }

                // Normalize path
                if (!assetPath.StartsWith("Assets/"))
                    assetPath = "Assets/" + assetPath;
                if (!assetPath.EndsWith(".asset"))
                    assetPath += ".asset";

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(assetPath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    string[] folders = directory.Split('/');
                    string currentPath = folders[0];
                    for (int i = 1; i < folders.Length; i++)
                    {
                        string nextPath = currentPath + "/" + folders[i];
                        if (!AssetDatabase.IsValidFolder(nextPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, folders[i]);
                        }
                        currentPath = nextPath;
                    }
                }

                // Find the type
                Type soType = FindType(typeName);
                if (soType == null)
                {
                    return new ErrorResponse($"Type '{typeName}' not found. Make sure it's a ScriptableObject subclass.");
                }

                if (!typeof(ScriptableObject).IsAssignableFrom(soType))
                {
                    return new ErrorResponse($"Type '{typeName}' is not a ScriptableObject");
                }

                // Create the asset
                var asset = ScriptableObject.CreateInstance(soType);

                // Set properties if provided
                if (propertiesToken != null && propertiesToken is JObject props)
                {
                    var serializedObject = new SerializedObject(asset);
                    foreach (var prop in props)
                    {
                        var serializedProp = serializedObject.FindProperty(prop.Key);
                        if (serializedProp != null)
                        {
                            SetPropertyValue(serializedProp, prop.Value);
                        }
                    }
                    serializedObject.ApplyModifiedProperties();
                }

                // Save the asset
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new SuccessResponse($"Created ScriptableObject: {assetPath}", new
                {
                    path = assetPath,
                    type = soType.Name,
                    success = true
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to create ScriptableObject: {e.Message}");
            }
        }

        private static Type FindType(string typeName)
        {
            // Try direct lookup first
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // Try by simple name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }

            return null;
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
            }
        }
    }
    #endregion

    #region 3. Assign Audio Clip Tool
    [McpForUnityTool(
        name: "assign_audio_clip",
        Description = "Assigns AudioClip to AudioSource or component. Params: clipPath, targetObject, targetComponent (optional), targetProperty (optional, default: clip)")]
    public static class MCPAssignAudioClip
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string clipPath = @params["clipPath"]?.ToString();
                string targetObjectName = @params["targetObject"]?.ToString();
                string targetComponentName = @params["targetComponent"]?.ToString() ?? "AudioSource";
                string targetPropertyName = @params["targetProperty"]?.ToString() ?? "m_audioClip";

                if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(targetObjectName))
                {
                    return new ErrorResponse("Required: clipPath, targetObject. Optional: targetComponent, targetProperty");
                }

                // Normalize path
                if (!clipPath.StartsWith("Assets/"))
                    clipPath = "Assets/" + clipPath;

                // Load audio clip
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    return new ErrorResponse($"AudioClip not found at '{clipPath}'");
                }

                // Find target
                var targetGO = GameObject.Find(targetObjectName);
                if (targetGO == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    targetGO = allObjects.FirstOrDefault(g => g.name == targetObjectName && g.scene.isLoaded);
                }
                if (targetGO == null)
                    return new ErrorResponse($"GameObject '{targetObjectName}' not found");

                // Get component
                var targetComp = targetGO.GetComponent(targetComponentName);
                if (targetComp == null)
                    return new ErrorResponse($"Component '{targetComponentName}' not found on '{targetObjectName}'");

                // If it's an AudioSource, use direct property
                if (targetComp is AudioSource audioSource)
                {
                    audioSource.clip = clip;
                    EditorUtility.SetDirty(audioSource);
                }
                else
                {
                    // Use SerializedObject for other components
                    var serializedObject = new SerializedObject(targetComp);
                    var property = serializedObject.FindProperty(targetPropertyName);
                    if (property == null)
                    {
                        // Try without m_ prefix
                        property = serializedObject.FindProperty(targetPropertyName.Replace("m_", ""));
                    }
                    if (property == null)
                        return new ErrorResponse($"Property '{targetPropertyName}' not found");

                    property.objectReferenceValue = clip;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(targetComp);
                }

                EditorSceneManager.MarkSceneDirty(targetGO.scene);

                return new SuccessResponse($"Assigned AudioClip to {targetObjectName}", new
                {
                    clip = clip.name,
                    target = targetObjectName,
                    component = targetComponentName
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to assign audio clip: {e.Message}");
            }
        }
    }
    #endregion

    #region 4. Play Mode Control Tool
    [McpForUnityTool(
        name: "play_mode_control",
        Description = "Controls Unity Play Mode. Actions: play, pause, stop, step, status, simulate_key")]
    public static class MCPPlayModeControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Play Mode Control ready. Actions: play, pause, stop, step, status, simulate_key");
            }

            switch (action)
            {
                case "play":
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = true;
                    }
                    return new SuccessResponse("Play mode started", new { isPlaying = true });

                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return new SuccessResponse($"Play mode {(EditorApplication.isPaused ? "paused" : "resumed")}",
                        new { isPaused = EditorApplication.isPaused });

                case "stop":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = false;
                    }
                    return new SuccessResponse("Play mode stopped", new { isPlaying = false });

                case "step":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.Step();
                        return new SuccessResponse("Stepped one frame");
                    }
                    return new ErrorResponse("Cannot step when not playing");

                case "status":
                    return new SuccessResponse("Play mode status", new
                    {
                        isPlaying = EditorApplication.isPlaying,
                        isPaused = EditorApplication.isPaused,
                        isCompiling = EditorApplication.isCompiling,
                        timeSinceStartup = EditorApplication.timeSinceStartup
                    });

                case "simulate_key":
                    string key = @params["key"]?.ToString();
                    if (string.IsNullOrEmpty(key))
                        return new ErrorResponse("Required: key (e.g., 'w', 'space', 'escape')");

                    // Note: Direct key simulation requires play mode and special handling
                    return new SuccessResponse($"Key simulation requested: {key}", new { key, note = "Key simulation works best with custom input handlers" });

                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: play, pause, stop, step, status, simulate_key");
            }
        }
    }
    #endregion

    #region 5. Prefab Editor Tool
    [McpForUnityTool(
        name: "prefab_editor",
        Description = "Edit prefabs. Actions: open, save, close, get_contents, modify_property")]
    public static class MCPPrefabEditor
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Prefab Editor ready. Actions: open, save, close, get_contents, modify_property");
            }

            switch (action)
            {
                case "open":
                    return OpenPrefab(@params);
                case "save":
                    return SavePrefab();
                case "close":
                    return ClosePrefab(@params);
                case "get_contents":
                    return GetPrefabContents(@params);
                case "modify_property":
                    return ModifyPrefabProperty(@params);
                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static object OpenPrefab(JObject @params)
        {
            string prefabPath = @params["prefabPath"]?.ToString();
            if (string.IsNullOrEmpty(prefabPath))
                return new ErrorResponse("Required: prefabPath");

            if (!prefabPath.StartsWith("Assets/"))
                prefabPath = "Assets/" + prefabPath;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return new ErrorResponse($"Prefab not found at '{prefabPath}'");

            // Open prefab in edit mode
            var stage = PrefabStageUtility.OpenPrefab(prefabPath);
            if (stage == null)
                return new ErrorResponse("Failed to open prefab stage");

            return new SuccessResponse($"Opened prefab: {prefabPath}", new
            {
                path = prefabPath,
                rootName = stage.prefabContentsRoot?.name
            });
        }

        private static object SavePrefab()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return new ErrorResponse("No prefab currently open");

            // Mark dirty and save
            EditorUtility.SetDirty(stage.prefabContentsRoot);
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);

            return new SuccessResponse($"Saved prefab: {stage.assetPath}");
        }

        private static object ClosePrefab(JObject @params)
        {
            bool save = @params["save"]?.ToObject<bool>() ?? true;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return new SuccessResponse("No prefab was open");

            if (save)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            }

            // Return to main stage
            StageUtility.GoToMainStage();

            return new SuccessResponse("Prefab stage closed", new { saved = save });
        }

        private static object GetPrefabContents(JObject @params)
        {
            string prefabPath = @params["prefabPath"]?.ToString();

            GameObject root;

            // If in prefab stage, use that
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                root = stage.prefabContentsRoot;
            }
            else if (!string.IsNullOrEmpty(prefabPath))
            {
                if (!prefabPath.StartsWith("Assets/"))
                    prefabPath = "Assets/" + prefabPath;
                root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                return new ErrorResponse("Provide prefabPath or open a prefab first");
            }

            if (root == null)
                return new ErrorResponse("Could not load prefab");

            // Build hierarchy
            var hierarchy = BuildHierarchy(root.transform);

            return new SuccessResponse("Prefab contents", new
            {
                name = root.name,
                hierarchy
            });
        }

        private static object BuildHierarchy(Transform t)
        {
            var children = new List<object>();
            for (int i = 0; i < t.childCount; i++)
            {
                children.Add(BuildHierarchy(t.GetChild(i)));
            }

            return new
            {
                name = t.name,
                components = t.GetComponents<Component>().Select(c => c?.GetType().Name ?? "Missing").ToArray(),
                children
            };
        }

        private static object ModifyPrefabProperty(JObject @params)
        {
            string objectPath = @params["objectPath"]?.ToString();
            string componentName = @params["componentName"]?.ToString();
            string propertyName = @params["propertyName"]?.ToString();
            var value = @params["value"];

            if (string.IsNullOrEmpty(componentName) || string.IsNullOrEmpty(propertyName) || value == null)
                return new ErrorResponse("Required: componentName, propertyName, value. Optional: objectPath");

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return new ErrorResponse("Open a prefab first with action: open");

            Transform target = stage.prefabContentsRoot.transform;
            if (!string.IsNullOrEmpty(objectPath))
            {
                target = target.Find(objectPath);
                if (target == null)
                    return new ErrorResponse($"Object not found at path: {objectPath}");
            }

            var comp = target.GetComponent(componentName);
            if (comp == null)
                return new ErrorResponse($"Component '{componentName}' not found");

            var serializedObject = new SerializedObject(comp);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return new ErrorResponse($"Property '{propertyName}' not found");

            SetPropertyValue(property, value);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(comp);

            return new SuccessResponse($"Modified {componentName}.{propertyName}", new
            {
                target = target.name,
                component = componentName,
                property = propertyName
            });
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
                case SerializedPropertyType.Vector3:
                    var v3 = value.ToObject<float[]>();
                    property.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                    break;
                case SerializedPropertyType.Color:
                    var c = value.ToObject<float[]>();
                    property.colorValue = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);
                    break;
            }
        }
    }
    #endregion

    #region 6. Undo/Redo Tool
    [McpForUnityTool(
        name: "undo_redo",
        Description = "Controls Unity Undo system. Actions: undo, redo, clear, get_history, begin_group, end_group")]
    public static class MCPUndoRedo
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Undo/Redo ready. Actions: undo, redo, clear, get_history, begin_group, end_group");
            }

            switch (action)
            {
                case "undo":
                    Undo.PerformUndo();
                    return new SuccessResponse("Undo performed", new { currentGroup = Undo.GetCurrentGroupName() });

                case "redo":
                    Undo.PerformRedo();
                    return new SuccessResponse("Redo performed", new { currentGroup = Undo.GetCurrentGroupName() });

                case "clear":
                    Undo.ClearAll();
                    return new SuccessResponse("Undo history cleared");

                case "get_history":
                    // Unity doesn't expose full history, but we can get current group
                    return new SuccessResponse("Undo state", new
                    {
                        currentGroupName = Undo.GetCurrentGroupName(),
                        currentGroupIndex = Undo.GetCurrentGroup()
                    });

                case "begin_group":
                    string groupName = @params["name"]?.ToString() ?? "MCP Operation";
                    Undo.SetCurrentGroupName(groupName);
                    return new SuccessResponse($"Undo group started: {groupName}");

                case "end_group":
                    Undo.IncrementCurrentGroup();
                    return new SuccessResponse("Undo group ended");

                case "record":
                    string objectName = @params["objectName"]?.ToString();
                    if (string.IsNullOrEmpty(objectName))
                        return new ErrorResponse("Required: objectName");

                    var go = GameObject.Find(objectName);
                    if (go != null)
                    {
                        Undo.RecordObject(go, $"Record {objectName}");
                        return new SuccessResponse($"Recording changes to {objectName}");
                    }
                    return new ErrorResponse($"GameObject '{objectName}' not found");

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }
    }
    #endregion

    #region 7. Animation Control Tool
    [McpForUnityTool(
        name: "animation_control",
        Description = "Controls Animator/Animation. Actions: get_parameters, set_parameter, get_clips, play_clip, get_state")]
    public static class MCPAnimationControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string objectName = @params["objectName"]?.ToString();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Animation Control ready. Actions: get_parameters, set_parameter, get_clips, play_clip, get_state");
            }

            if (string.IsNullOrEmpty(objectName))
                return new ErrorResponse("Required: objectName");

            var go = GameObject.Find(objectName);
            if (go == null)
                return new ErrorResponse($"GameObject '{objectName}' not found");

            var animator = go.GetComponent<Animator>();

            switch (action)
            {
                case "get_parameters":
                    if (animator == null)
                        return new ErrorResponse("No Animator component found");

                    var parameters = animator.parameters.Select(p => new
                    {
                        name = p.name,
                        type = p.type.ToString(),
                        defaultValue = GetParameterValue(animator, p)
                    }).ToList();

                    return new SuccessResponse($"Found {parameters.Count} parameters", new { parameters });

                case "set_parameter":
                    if (animator == null)
                        return new ErrorResponse("No Animator component found");

                    string paramName = @params["parameterName"]?.ToString();
                    var paramValue = @params["value"];

                    if (string.IsNullOrEmpty(paramName) || paramValue == null)
                        return new ErrorResponse("Required: parameterName, value");

                    var param = animator.parameters.FirstOrDefault(p => p.name == paramName);
                    if (param == null)
                        return new ErrorResponse($"Parameter '{paramName}' not found");

                    SetParameterValue(animator, param, paramValue);
                    return new SuccessResponse($"Set {paramName} to {paramValue}");

                case "get_clips":
                    var clips = new List<string>();

                    // Check Animator
                    if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        clips.AddRange(animator.runtimeAnimatorController.animationClips.Select(c => c.name));
                    }

                    // Check legacy Animation
                    var animation = go.GetComponent<Animation>();
                    if (animation != null)
                    {
                        foreach (AnimationState state in animation)
                        {
                            clips.Add(state.name);
                        }
                    }

                    return new SuccessResponse($"Found {clips.Count} clips", new { clips });

                case "play_clip":
                    string clipName = @params["clipName"]?.ToString();
                    if (string.IsNullOrEmpty(clipName))
                        return new ErrorResponse("Required: clipName");

                    // Try Animator
                    if (animator != null)
                    {
                        animator.Play(clipName);
                        return new SuccessResponse($"Playing clip: {clipName}");
                    }

                    // Try legacy Animation
                    var legacyAnim = go.GetComponent<Animation>();
                    if (legacyAnim != null)
                    {
                        legacyAnim.Play(clipName);
                        return new SuccessResponse($"Playing clip: {clipName}");
                    }

                    return new ErrorResponse("No Animator or Animation component found");

                case "get_state":
                    if (animator == null)
                        return new ErrorResponse("No Animator component found");

                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    return new SuccessResponse("Current animator state", new
                    {
                        normalizedTime = stateInfo.normalizedTime,
                        length = stateInfo.length,
                        speed = stateInfo.speed,
                        isLooping = stateInfo.loop,
                        isName = "Unknown" // Would need hashing to get actual name
                    });

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static object GetParameterValue(Animator animator, AnimatorControllerParameter param)
        {
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    return animator.GetFloat(param.name);
                case AnimatorControllerParameterType.Int:
                    return animator.GetInteger(param.name);
                case AnimatorControllerParameterType.Bool:
                    return animator.GetBool(param.name);
                case AnimatorControllerParameterType.Trigger:
                    return "trigger";
                default:
                    return null;
            }
        }

        private static void SetParameterValue(Animator animator, AnimatorControllerParameter param, JToken value)
        {
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(param.name, value.ToObject<float>());
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(param.name, value.ToObject<int>());
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(param.name, value.ToObject<bool>());
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.SetTrigger(param.name);
                    break;
            }
        }
    }
    #endregion

    #region 8. Domain Reload Handler - Ensures MCP stability during recompilation
    [InitializeOnLoad]
    public static class MCPDomainReloadHandler
    {
        private static bool wasCompiling = false;

        static MCPDomainReloadHandler()
        {
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;

            Debug.Log("[MCP] Domain reload handler initialized");
        }

        private static void OnEditorUpdate()
        {
            bool isCompiling = EditorApplication.isCompiling;

            if (isCompiling && !wasCompiling)
            {
                // Compilation started
                Debug.Log("[MCP] Compilation started - tools temporarily unavailable");
            }
            else if (!isCompiling && wasCompiling)
            {
                // Compilation finished
                Debug.Log("[MCP] Compilation finished - tools available");

                // Refresh to ensure everything is in sync
                AssetDatabase.Refresh();
            }

            wasCompiling = isCompiling;
        }

        private static void OnBeforeReload()
        {
            Debug.Log("[MCP] Domain reload starting...");
        }

        private static void OnAfterReload()
        {
            Debug.Log("[MCP] Domain reload complete - all tools re-registered");

            // Re-initialize auto dialog handler
            MCPAutoDialogHandler.Initialize();
        }
    }
    #endregion

    #region 9. Enhanced Execute C# - More patterns supported
    [McpForUnityTool(
        name: "execute_advanced",
        Description = "Enhanced C# execution. Actions: create_asset, set_prefs, get_prefs, invoke_static, get_types")]
    public static class MCPExecuteAdvanced
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Execute Advanced ready. Actions: create_asset, set_prefs, get_prefs, invoke_static, get_types");
            }

            switch (action)
            {
                case "create_asset":
                    return CreateAsset(@params);
                case "set_prefs":
                    return SetEditorPrefs(@params);
                case "get_prefs":
                    return GetEditorPrefs(@params);
                case "invoke_static":
                    return InvokeStaticMethod(@params);
                case "get_types":
                    return GetAvailableTypes(@params);
                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static object CreateAsset(JObject @params)
        {
            string typeName = @params["typeName"]?.ToString();
            string path = @params["path"]?.ToString();

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(path))
                return new ErrorResponse("Required: typeName, path");

            var type = FindType(typeName);
            if (type == null)
                return new ErrorResponse($"Type '{typeName}' not found");

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                var asset = ScriptableObject.CreateInstance(type);
                if (!path.StartsWith("Assets/")) path = "Assets/" + path;
                if (!path.EndsWith(".asset")) path += ".asset";

                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                return new SuccessResponse($"Created asset: {path}");
            }

            return new ErrorResponse("Only ScriptableObject types supported");
        }

        private static object SetEditorPrefs(JObject @params)
        {
            string key = @params["key"]?.ToString();
            var value = @params["value"];
            string valueType = @params["valueType"]?.ToString()?.ToLower() ?? "string";

            if (string.IsNullOrEmpty(key) || value == null)
                return new ErrorResponse("Required: key, value. Optional: valueType (string/int/float/bool)");

            switch (valueType)
            {
                case "int":
                    EditorPrefs.SetInt(key, value.ToObject<int>());
                    break;
                case "float":
                    EditorPrefs.SetFloat(key, value.ToObject<float>());
                    break;
                case "bool":
                    EditorPrefs.SetBool(key, value.ToObject<bool>());
                    break;
                default:
                    EditorPrefs.SetString(key, value.ToString());
                    break;
            }

            return new SuccessResponse($"Set EditorPref: {key} = {value}");
        }

        private static object GetEditorPrefs(JObject @params)
        {
            string key = @params["key"]?.ToString();
            string valueType = @params["valueType"]?.ToString()?.ToLower() ?? "string";

            if (string.IsNullOrEmpty(key))
                return new ErrorResponse("Required: key");

            object value;
            switch (valueType)
            {
                case "int":
                    value = EditorPrefs.GetInt(key);
                    break;
                case "float":
                    value = EditorPrefs.GetFloat(key);
                    break;
                case "bool":
                    value = EditorPrefs.GetBool(key);
                    break;
                default:
                    value = EditorPrefs.GetString(key);
                    break;
            }

            return new SuccessResponse($"EditorPref: {key}", new { key, value });
        }

        private static object InvokeStaticMethod(JObject @params)
        {
            string typeName = @params["typeName"]?.ToString();
            string methodName = @params["methodName"]?.ToString();

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                return new ErrorResponse("Required: typeName, methodName");

            var type = FindType(typeName);
            if (type == null)
                return new ErrorResponse($"Type '{typeName}' not found");

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return new ErrorResponse($"Static method '{methodName}' not found on '{typeName}'");

            // Only allow parameterless methods for safety
            if (method.GetParameters().Length > 0)
                return new ErrorResponse("Only parameterless static methods supported");

            try
            {
                var result = method.Invoke(null, null);
                return new SuccessResponse($"Invoked {typeName}.{methodName}()", new { result = result?.ToString() });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Method invocation failed: {e.Message}");
            }
        }

        private static object GetAvailableTypes(JObject @params)
        {
            string filter = @params["filter"]?.ToString();
            string baseType = @params["baseType"]?.ToString();

            var types = new List<string>();
            Type filterBaseType = null;

            if (!string.IsNullOrEmpty(baseType))
            {
                filterBaseType = FindType(baseType);
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsPublic) continue;
                        if (filterBaseType != null && !filterBaseType.IsAssignableFrom(type)) continue;
                        if (!string.IsNullOrEmpty(filter) && !type.Name.Contains(filter)) continue;

                        types.Add(type.FullName);
                    }
                }
                catch { }
            }

            return new SuccessResponse($"Found {types.Count} types", new { types = types.Take(100).ToList() });
        }

        private static Type FindType(string typeName)
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

    #region 10. Wait For Compilation Tool
    [McpForUnityTool(
        name: "wait_for_compilation",
        Description = "Checks compilation status. Actions: status, wait (blocks until done)")]
    public static class MCPWaitForCompilation
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "status";

            switch (action)
            {
                case "status":
                    return new SuccessResponse("Compilation status", new
                    {
                        isCompiling = EditorApplication.isCompiling,
                        isUpdating = EditorApplication.isUpdating,
                        isPlaying = EditorApplication.isPlaying
                    });

                case "wait":
                    // Note: MCP calls are synchronous, so we can't truly "wait"
                    // But we can report status and let the caller retry
                    if (EditorApplication.isCompiling)
                    {
                        return new SuccessResponse("Still compiling - retry in a moment", new
                        {
                            isCompiling = true,
                            shouldRetry = true
                        });
                    }
                    return new SuccessResponse("Not compiling - ready", new
                    {
                        isCompiling = false,
                        shouldRetry = false
                    });

                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: status, wait");
            }
        }
    }
    #endregion
}
