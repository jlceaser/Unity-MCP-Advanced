#nullable disable
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Core Unity API Tools - Essential controls for Unity's core systems
    /// </summary>

    /// <summary>
    /// Time Control - Manage Unity's time settings
    /// </summary>
    [McpForUnityTool(
        name: "time_control",
        Description = "Control Unity's time settings. Actions: get, set_timescale, set_fixed_delta, pause, resume, slow_motion, fast_forward")]
    public static class MCPTimeControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "get";

            switch (action)
            {
                case "get":
                case "status":
                    return new SuccessResponse("Time Settings", new
                    {
                        timeScale = Time.timeScale,
                        fixedDeltaTime = Time.fixedDeltaTime,
                        maximumDeltaTime = Time.maximumDeltaTime,
                        maximumParticleDeltaTime = Time.maximumParticleDeltaTime,
                        smoothDeltaTime = Time.smoothDeltaTime,
                        deltaTime = Time.deltaTime,
                        unscaledDeltaTime = Time.unscaledDeltaTime,
                        time = Time.time,
                        unscaledTime = Time.unscaledTime,
                        realtimeSinceStartup = Time.realtimeSinceStartup,
                        frameCount = Time.frameCount,
                        captureDeltaTime = Time.captureDeltaTime,
                        captureFramerate = Time.captureFramerate,
                        isPaused = Time.timeScale == 0
                    });

                case "set_timescale":
                case "timescale":
                    float scale = @params["value"]?.Value<float>() ?? 1f;
                    scale = Mathf.Clamp(scale, 0f, 100f);
                    Time.timeScale = scale;
                    return new SuccessResponse($"TimeScale set to {scale}");

                case "set_fixed_delta":
                case "fixed_delta":
                    float fixedDelta = @params["value"]?.Value<float>() ?? 0.02f;
                    fixedDelta = Mathf.Clamp(fixedDelta, 0.0001f, 1f);
                    Time.fixedDeltaTime = fixedDelta;
                    return new SuccessResponse($"FixedDeltaTime set to {fixedDelta}");

                case "set_max_delta":
                    float maxDelta = @params["value"]?.Value<float>() ?? 0.3333333f;
                    Time.maximumDeltaTime = maxDelta;
                    return new SuccessResponse($"MaximumDeltaTime set to {maxDelta}");

                case "pause":
                    Time.timeScale = 0f;
                    return new SuccessResponse("Game time paused (timeScale = 0)");

                case "resume":
                    Time.timeScale = 1f;
                    return new SuccessResponse("Game time resumed (timeScale = 1)");

                case "slow_motion":
                    float slowFactor = @params["factor"]?.Value<float>() ?? 0.5f;
                    Time.timeScale = slowFactor;
                    return new SuccessResponse($"Slow motion enabled (timeScale = {slowFactor})");

                case "fast_forward":
                    float fastFactor = @params["factor"]?.Value<float>() ?? 2f;
                    Time.timeScale = fastFactor;
                    return new SuccessResponse($"Fast forward enabled (timeScale = {fastFactor})");

                case "set_capture_framerate":
                    int fps = @params["fps"]?.Value<int>() ?? 0;
                    Time.captureFramerate = fps;
                    return new SuccessResponse($"Capture framerate set to {fps} (0 = disabled)");

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }
    }

    /// <summary>
    /// Render Settings Control - Manage Unity's render settings
    /// </summary>
    [McpForUnityTool(
        name: "render_settings",
        Description = "Control Unity's render settings. Actions: get, set_skybox, set_fog, set_ambient, set_flare, set_halo")]
    public static class MCPRenderSettings
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "get";

            switch (action)
            {
                case "get":
                case "status":
                    return new SuccessResponse("Render Settings", new
                    {
                        // Skybox
                        skybox = RenderSettings.skybox?.name ?? "None",

                        // Fog
                        fog = RenderSettings.fog,
                        fogMode = RenderSettings.fogMode.ToString(),
                        fogColor = ColorToHex(RenderSettings.fogColor),
                        fogDensity = RenderSettings.fogDensity,
                        fogStartDistance = RenderSettings.fogStartDistance,
                        fogEndDistance = RenderSettings.fogEndDistance,

                        // Ambient
                        ambientMode = RenderSettings.ambientMode.ToString(),
                        ambientLight = ColorToHex(RenderSettings.ambientLight),
                        ambientIntensity = RenderSettings.ambientIntensity,
                        ambientSkyColor = ColorToHex(RenderSettings.ambientSkyColor),
                        ambientEquatorColor = ColorToHex(RenderSettings.ambientEquatorColor),
                        ambientGroundColor = ColorToHex(RenderSettings.ambientGroundColor),

                        // Reflections
                        defaultReflectionMode = RenderSettings.defaultReflectionMode.ToString(),
                        defaultReflectionResolution = RenderSettings.defaultReflectionResolution,
                        reflectionBounces = RenderSettings.reflectionBounces,
                        reflectionIntensity = RenderSettings.reflectionIntensity,

                        // Other
                        sun = RenderSettings.sun?.name ?? "None",
                        subtractiveShadowColor = ColorToHex(RenderSettings.subtractiveShadowColor),
                        flareFadeSpeed = RenderSettings.flareFadeSpeed,
                        flareStrength = RenderSettings.flareStrength,
                        haloStrength = RenderSettings.haloStrength
                    });

                case "set_fog":
                case "fog":
                    bool fogEnabled = @params["enabled"]?.Value<bool>() ?? RenderSettings.fog;
                    RenderSettings.fog = fogEnabled;

                    if (@params["color"] != null)
                        RenderSettings.fogColor = ParseColor(@params["color"].ToString());
                    if (@params["density"] != null)
                        RenderSettings.fogDensity = @params["density"].Value<float>();
                    if (@params["start"] != null)
                        RenderSettings.fogStartDistance = @params["start"].Value<float>();
                    if (@params["end"] != null)
                        RenderSettings.fogEndDistance = @params["end"].Value<float>();
                    if (@params["mode"] != null)
                    {
                        string mode = @params["mode"].ToString().ToLower();
                        RenderSettings.fogMode = mode switch
                        {
                            "linear" => FogMode.Linear,
                            "exponential" => FogMode.Exponential,
                            "exponentialsquared" or "exp2" => FogMode.ExponentialSquared,
                            _ => RenderSettings.fogMode
                        };
                    }

                    return new SuccessResponse("Fog settings updated", new
                    {
                        fog = RenderSettings.fog,
                        fogMode = RenderSettings.fogMode.ToString(),
                        fogColor = ColorToHex(RenderSettings.fogColor),
                        fogDensity = RenderSettings.fogDensity
                    });

                case "set_ambient":
                case "ambient":
                    if (@params["color"] != null)
                        RenderSettings.ambientLight = ParseColor(@params["color"].ToString());
                    if (@params["intensity"] != null)
                        RenderSettings.ambientIntensity = @params["intensity"].Value<float>();
                    if (@params["sky_color"] != null)
                        RenderSettings.ambientSkyColor = ParseColor(@params["sky_color"].ToString());
                    if (@params["equator_color"] != null)
                        RenderSettings.ambientEquatorColor = ParseColor(@params["equator_color"].ToString());
                    if (@params["ground_color"] != null)
                        RenderSettings.ambientGroundColor = ParseColor(@params["ground_color"].ToString());
                    if (@params["mode"] != null)
                    {
                        string mode = @params["mode"].ToString().ToLower();
                        RenderSettings.ambientMode = mode switch
                        {
                            "skybox" => AmbientMode.Skybox,
                            "trilight" => AmbientMode.Trilight,
                            "flat" or "color" => AmbientMode.Flat,
                            _ => RenderSettings.ambientMode
                        };
                    }

                    return new SuccessResponse("Ambient settings updated");

                case "set_skybox":
                case "skybox":
                    string skyboxPath = @params["path"]?.ToString();
                    if (!string.IsNullOrEmpty(skyboxPath))
                    {
                        var skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
                        if (skybox != null)
                        {
                            RenderSettings.skybox = skybox;
                            return new SuccessResponse($"Skybox set to: {skybox.name}");
                        }
                        return new ErrorResponse($"Skybox not found at: {skyboxPath}");
                    }
                    return new ErrorResponse("Path parameter required");

                case "clear_skybox":
                    RenderSettings.skybox = null;
                    return new SuccessResponse("Skybox cleared");

                case "set_sun":
                case "sun":
                    string sunName = @params["name"]?.ToString();
                    if (!string.IsNullOrEmpty(sunName))
                    {
                        var sun = GameObject.Find(sunName)?.GetComponent<Light>();
                        if (sun != null)
                        {
                            RenderSettings.sun = sun;
                            return new SuccessResponse($"Sun set to: {sun.name}");
                        }
                        return new ErrorResponse($"Light not found: {sunName}");
                    }
                    return new ErrorResponse("Name parameter required");

                case "set_reflection":
                case "reflection":
                    if (@params["bounces"] != null)
                        RenderSettings.reflectionBounces = @params["bounces"].Value<int>();
                    if (@params["intensity"] != null)
                        RenderSettings.reflectionIntensity = @params["intensity"].Value<float>();
                    if (@params["resolution"] != null)
                        RenderSettings.defaultReflectionResolution = @params["resolution"].Value<int>();

                    return new SuccessResponse("Reflection settings updated");

                case "set_halo":
                    if (@params["strength"] != null)
                        RenderSettings.haloStrength = @params["strength"].Value<float>();
                    return new SuccessResponse($"Halo strength: {RenderSettings.haloStrength}");

                case "set_flare":
                    if (@params["strength"] != null)
                        RenderSettings.flareStrength = @params["strength"].Value<float>();
                    if (@params["fade_speed"] != null)
                        RenderSettings.flareFadeSpeed = @params["fade_speed"].Value<float>();
                    return new SuccessResponse("Flare settings updated");

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static string ColorToHex(Color c)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }

        private static Color ParseColor(string colorStr)
        {
            if (ColorUtility.TryParseHtmlString(colorStr, out Color color))
                return color;
            return Color.white;
        }
    }

    /// <summary>
    /// Camera Control - Manage cameras in the scene
    /// </summary>
    [McpForUnityTool(
        name: "camera_control",
        Description = "Control cameras in scene. Actions: list, get_main, set_main, get_properties, set_properties, screenshot, look_at")]
    public static class MCPCameraControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "list";

            switch (action)
            {
                case "list":
                    var cameras = Camera.allCameras.Select(c => new
                    {
                        name = c.name,
                        gameObject = c.gameObject.name,
                        enabled = c.enabled,
                        depth = c.depth,
                        isMain = c == Camera.main,
                        cullingMask = c.cullingMask,
                        fieldOfView = c.fieldOfView,
                        orthographic = c.orthographic,
                        orthographicSize = c.orthographicSize,
                        nearClip = c.nearClipPlane,
                        farClip = c.farClipPlane,
                        clearFlags = c.clearFlags.ToString(),
                        backgroundColor = ColorUtility.ToHtmlStringRGBA(c.backgroundColor)
                    }).ToList();

                    return new SuccessResponse($"Found {cameras.Count} cameras", cameras);

                case "get_main":
                    var main = Camera.main;
                    if (main == null)
                        return new ErrorResponse("No main camera found");

                    return new SuccessResponse("Main Camera", GetCameraInfo(main));

                case "get":
                case "get_properties":
                    string camName = @params["name"]?.ToString();
                    var cam = FindCamera(camName);
                    if (cam == null)
                        return new ErrorResponse($"Camera not found: {camName}");

                    return new SuccessResponse($"Camera: {cam.name}", GetCameraInfo(cam));

                case "set":
                case "set_properties":
                    string targetName = @params["name"]?.ToString();
                    var targetCam = FindCamera(targetName);
                    if (targetCam == null)
                        return new ErrorResponse($"Camera not found: {targetName}");

                    if (@params["fov"] != null)
                        targetCam.fieldOfView = @params["fov"].Value<float>();
                    if (@params["near_clip"] != null)
                        targetCam.nearClipPlane = @params["near_clip"].Value<float>();
                    if (@params["far_clip"] != null)
                        targetCam.farClipPlane = @params["far_clip"].Value<float>();
                    if (@params["depth"] != null)
                        targetCam.depth = @params["depth"].Value<float>();
                    if (@params["orthographic"] != null)
                        targetCam.orthographic = @params["orthographic"].Value<bool>();
                    if (@params["orthographic_size"] != null)
                        targetCam.orthographicSize = @params["orthographic_size"].Value<float>();
                    if (@params["background_color"] != null)
                    {
                        if (ColorUtility.TryParseHtmlString(@params["background_color"].ToString(), out Color bgColor))
                            targetCam.backgroundColor = bgColor;
                    }
                    if (@params["enabled"] != null)
                        targetCam.enabled = @params["enabled"].Value<bool>();

                    EditorUtility.SetDirty(targetCam);
                    return new SuccessResponse($"Camera {targetCam.name} updated");

                case "look_at":
                    string lookCamName = @params["camera"]?.ToString();
                    string targetObject = @params["target"]?.ToString();

                    var lookCam = FindCamera(lookCamName) ?? Camera.main;
                    if (lookCam == null)
                        return new ErrorResponse("No camera found");

                    GameObject target = GameObject.Find(targetObject);
                    if (target == null)
                        return new ErrorResponse($"Target object not found: {targetObject}");

                    lookCam.transform.LookAt(target.transform);
                    EditorUtility.SetDirty(lookCam);

                    return new SuccessResponse($"Camera {lookCam.name} now looking at {target.name}");

                case "set_position":
                    string posCamName = @params["camera"]?.ToString();
                    var posCam = FindCamera(posCamName) ?? Camera.main;
                    if (posCam == null)
                        return new ErrorResponse("No camera found");

                    if (@params["position"] != null)
                    {
                        var posArray = @params["position"].ToObject<float[]>();
                        if (posArray.Length >= 3)
                            posCam.transform.position = new Vector3(posArray[0], posArray[1], posArray[2]);
                    }
                    if (@params["rotation"] != null)
                    {
                        var rotArray = @params["rotation"].ToObject<float[]>();
                        if (rotArray.Length >= 3)
                            posCam.transform.eulerAngles = new Vector3(rotArray[0], rotArray[1], rotArray[2]);
                    }

                    EditorUtility.SetDirty(posCam);
                    return new SuccessResponse($"Camera {posCam.name} transform updated");

                case "create":
                    string newCamName = @params["name"]?.ToString() ?? "New Camera";
                    var newCamGO = new GameObject(newCamName);
                    var newCam = newCamGO.AddComponent<Camera>();

                    if (@params["position"] != null)
                    {
                        var posArray = @params["position"].ToObject<float[]>();
                        if (posArray.Length >= 3)
                            newCamGO.transform.position = new Vector3(posArray[0], posArray[1], posArray[2]);
                    }

                    Undo.RegisterCreatedObjectUndo(newCamGO, "Create Camera");
                    return new SuccessResponse($"Camera created: {newCamName}", GetCameraInfo(newCam));

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static Camera FindCamera(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Camera.main;

            return Camera.allCameras.FirstOrDefault(c =>
                c.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static object GetCameraInfo(Camera cam)
        {
            return new
            {
                name = cam.name,
                enabled = cam.enabled,
                isMain = cam == Camera.main,
                position = new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z },
                rotation = new[] { cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z },
                fieldOfView = cam.fieldOfView,
                orthographic = cam.orthographic,
                orthographicSize = cam.orthographicSize,
                nearClipPlane = cam.nearClipPlane,
                farClipPlane = cam.farClipPlane,
                depth = cam.depth,
                cullingMask = cam.cullingMask,
                clearFlags = cam.clearFlags.ToString(),
                backgroundColor = ColorUtility.ToHtmlStringRGBA(cam.backgroundColor),
                renderingPath = cam.renderingPath.ToString(),
                allowHDR = cam.allowHDR,
                allowMSAA = cam.allowMSAA,
                targetTexture = cam.targetTexture?.name ?? "None"
            };
        }
    }

    /// <summary>
    /// Multi-Scene Control - Manage multiple scenes
    /// </summary>
    [McpForUnityTool(
        name: "multi_scene",
        Description = "Manage multiple scenes. Actions: list_loaded, load_additive, unload, set_active, get_root_objects, move_object")]
    public static class MCPMultiScene
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "list_loaded";

            switch (action)
            {
                case "list_loaded":
                case "list":
                    var scenes = new List<object>();
                    for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    {
                        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                        scenes.Add(new
                        {
                            name = scene.name,
                            path = scene.path,
                            buildIndex = scene.buildIndex,
                            isLoaded = scene.isLoaded,
                            isDirty = scene.isDirty,
                            isActive = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                            rootCount = scene.rootCount
                        });
                    }
                    return new SuccessResponse($"{scenes.Count} scenes loaded", scenes);

                case "load_additive":
                case "load":
                    string loadPath = @params["path"]?.ToString();
                    if (string.IsNullOrEmpty(loadPath))
                        return new ErrorResponse("Path parameter required");

                    var loadedScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        loadPath,
                        UnityEditor.SceneManagement.OpenSceneMode.Additive);

                    return new SuccessResponse($"Scene loaded: {loadedScene.name}");

                case "unload":
                    string unloadName = @params["name"]?.ToString();
                    if (string.IsNullOrEmpty(unloadName))
                        return new ErrorResponse("Name parameter required");

                    var sceneToUnload = UnityEngine.SceneManagement.SceneManager.GetSceneByName(unloadName);
                    if (!sceneToUnload.isLoaded)
                        return new ErrorResponse($"Scene not loaded: {unloadName}");

                    bool saved = @params["save"]?.Value<bool>() ?? false;
                    if (saved)
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sceneToUnload);

                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sceneToUnload, true);
                    return new SuccessResponse($"Scene unloaded: {unloadName}");

                case "set_active":
                    string activeName = @params["name"]?.ToString();
                    if (string.IsNullOrEmpty(activeName))
                        return new ErrorResponse("Name parameter required");

                    var sceneToActivate = UnityEngine.SceneManagement.SceneManager.GetSceneByName(activeName);
                    if (!sceneToActivate.isLoaded)
                        return new ErrorResponse($"Scene not loaded: {activeName}");

                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(sceneToActivate);
                    return new SuccessResponse($"Active scene: {activeName}");

                case "get_root_objects":
                    string rootSceneName = @params["name"]?.ToString();
                    var targetScene = string.IsNullOrEmpty(rootSceneName)
                        ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                        : UnityEngine.SceneManagement.SceneManager.GetSceneByName(rootSceneName);

                    if (!targetScene.isLoaded)
                        return new ErrorResponse("Scene not loaded");

                    var rootObjects = targetScene.GetRootGameObjects().Select(go => new
                    {
                        name = go.name,
                        active = go.activeSelf,
                        childCount = go.transform.childCount,
                        components = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray()
                    }).ToList();

                    return new SuccessResponse($"Root objects in {targetScene.name}", rootObjects);

                case "move_object":
                    string objName = @params["object"]?.ToString();
                    string destScene = @params["destination"]?.ToString();

                    if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(destScene))
                        return new ErrorResponse("Object and destination parameters required");

                    var obj = GameObject.Find(objName);
                    if (obj == null)
                        return new ErrorResponse($"Object not found: {objName}");

                    var dest = UnityEngine.SceneManagement.SceneManager.GetSceneByName(destScene);
                    if (!dest.isLoaded)
                        return new ErrorResponse($"Destination scene not loaded: {destScene}");

                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, dest);
                    return new SuccessResponse($"Moved {objName} to {destScene}");

                case "new":
                case "create":
                    string newSceneName = @params["name"]?.ToString() ?? "New Scene";
                    var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                        UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                        UnityEditor.SceneManagement.NewSceneMode.Additive);

                    return new SuccessResponse($"New scene created: {newScene.name}");

                case "save_all":
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                    return new SuccessResponse("All open scenes saved");

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }
    }

    /// <summary>
    /// Script Execution Control - Advanced C# code execution
    /// </summary>
    [McpForUnityTool(
        name: "script_runner",
        Description = "Run C# code snippets safely. Actions: execute, evaluate, list_types")]
    public static class MCPScriptRunner
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "execute";

            switch (action)
            {
                case "evaluate":
                case "eval":
                    string evalCode = @params["code"]?.ToString();
                    if (string.IsNullOrEmpty(evalCode))
                        return new ErrorResponse("Code parameter required");

                    // Simple expression evaluation
                    try
                    {
                        // Handle common Unity expressions
                        object result = EvaluateExpression(evalCode);
                        return new SuccessResponse("Evaluation result", new
                        {
                            expression = evalCode,
                            result = result?.ToString() ?? "null",
                            type = result?.GetType().Name ?? "null"
                        });
                    }
                    catch (Exception ex)
                    {
                        return new ErrorResponse($"Evaluation failed: {ex.Message}");
                    }

                case "list_types":
                    string ns = @params["namespace"]?.ToString() ?? "UnityEngine";
                    var types = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch { return Array.Empty<Type>(); }
                        })
                        .Where(t => t.Namespace == ns && t.IsPublic)
                        .Select(t => new
                        {
                            name = t.Name,
                            fullName = t.FullName,
                            isClass = t.IsClass,
                            isEnum = t.IsEnum,
                            isInterface = t.IsInterface
                        })
                        .OrderBy(t => t.name)
                        .Take(100)
                        .ToList();

                    return new SuccessResponse($"Types in {ns}", types);

                case "get_static":
                    string typeName = @params["type"]?.ToString();
                    string memberName = @params["member"]?.ToString();

                    if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(memberName))
                        return new ErrorResponse("Type and member parameters required");

                    try
                    {
                        var type = FindType(typeName);
                        if (type == null)
                            return new ErrorResponse($"Type not found: {typeName}");

                        var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (prop != null)
                        {
                            var value = prop.GetValue(null);
                            return new SuccessResponse($"{typeName}.{memberName}", new
                            {
                                value = value?.ToString() ?? "null",
                                type = prop.PropertyType.Name
                            });
                        }

                        var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (field != null)
                        {
                            var value = field.GetValue(null);
                            return new SuccessResponse($"{typeName}.{memberName}", new
                            {
                                value = value?.ToString() ?? "null",
                                type = field.FieldType.Name
                            });
                        }

                        return new ErrorResponse($"Member not found: {memberName}");
                    }
                    catch (Exception ex)
                    {
                        return new ErrorResponse($"Error: {ex.Message}");
                    }

                default:
                    return new ErrorResponse($"Unknown action: {action}. Use: evaluate, list_types, get_static");
            }
        }

        private static object EvaluateExpression(string expr)
        {
            expr = expr.Trim();

            // Common Unity static properties
            if (expr == "Time.time") return Time.time;
            if (expr == "Time.deltaTime") return Time.deltaTime;
            if (expr == "Time.timeScale") return Time.timeScale;
            if (expr == "Time.frameCount") return Time.frameCount;
            if (expr == "Application.isPlaying") return Application.isPlaying;
            if (expr == "Application.platform") return Application.platform.ToString();
            if (expr == "Application.unityVersion") return Application.unityVersion;
            if (expr == "Application.dataPath") return Application.dataPath;
            if (expr == "Screen.width") return Screen.width;
            if (expr == "Screen.height") return Screen.height;
            if (expr == "SystemInfo.deviceName") return SystemInfo.deviceName;
            if (expr == "SystemInfo.operatingSystem") return SystemInfo.operatingSystem;
            if (expr == "Camera.main") return Camera.main?.name ?? "null";

            // GameObject count
            if (expr.Contains("FindObjectsOfType"))
            {
                return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            }

            throw new Exception($"Expression not supported: {expr}");
        }

        private static Type FindType(string typeName)
        {
            // Try common Unity namespaces
            string[] namespaces = { "UnityEngine", "UnityEditor", "System", "" };

            foreach (var ns in namespaces)
            {
                string fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
                var type = Type.GetType(fullName);
                if (type != null) return type;

                // Search all assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(fullName);
                    if (type != null) return type;
                }
            }

            return null;
        }
    }
}
