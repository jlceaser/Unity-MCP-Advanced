#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    #region 1. Project Settings Tool
    [McpForUnityTool(
        name: "project_settings",
        Description = "Manages Unity Project Settings. Actions: get_physics, set_physics, get_quality, set_quality, get_time, set_time, get_tags_layers")]
    public static class MCPProjectSettings
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Project Settings ready. Actions: get_physics, set_physics, get_quality, set_quality, get_time, set_time, get_tags_layers, get_input");
            }

            switch (action)
            {
                case "get_physics":
                    return new SuccessResponse("Physics settings", new
                    {
                        gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                        defaultSolverIterations = Physics.defaultSolverIterations,
                        defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                        bounceThreshold = Physics.bounceThreshold,
                        sleepThreshold = Physics.sleepThreshold,
                        defaultContactOffset = Physics.defaultContactOffset,
                        autoSimulation = Physics.simulationMode.ToString()
                    });

                case "set_physics":
                    var gravityArr = @params["gravity"]?.ToObject<float[]>();
                    if (gravityArr != null && gravityArr.Length >= 3)
                        Physics.gravity = new Vector3(gravityArr[0], gravityArr[1], gravityArr[2]);

                    if (@params["solverIterations"] != null)
                        Physics.defaultSolverIterations = @params["solverIterations"].ToObject<int>();

                    if (@params["sleepThreshold"] != null)
                        Physics.sleepThreshold = @params["sleepThreshold"].ToObject<float>();

                    return new SuccessResponse("Physics settings updated");

                case "get_quality":
                    return new SuccessResponse("Quality settings", new
                    {
                        currentLevel = QualitySettings.GetQualityLevel(),
                        levelName = QualitySettings.names[QualitySettings.GetQualityLevel()],
                        allLevels = QualitySettings.names,
                        vSyncCount = QualitySettings.vSyncCount,
                        targetFrameRate = Application.targetFrameRate,
                        shadowDistance = QualitySettings.shadowDistance,
                        lodBias = QualitySettings.lodBias
                    });

                case "set_quality":
                    if (@params["level"] != null)
                    {
                        int level = @params["level"].ToObject<int>();
                        QualitySettings.SetQualityLevel(level, true);
                    }
                    if (@params["vSync"] != null)
                        QualitySettings.vSyncCount = @params["vSync"].ToObject<int>();
                    if (@params["targetFps"] != null)
                        Application.targetFrameRate = @params["targetFps"].ToObject<int>();

                    return new SuccessResponse("Quality settings updated");

                case "get_time":
                    return new SuccessResponse("Time settings", new
                    {
                        fixedDeltaTime = Time.fixedDeltaTime,
                        maximumDeltaTime = Time.maximumDeltaTime,
                        timeScale = Time.timeScale,
                        maximumParticleDeltaTime = Time.maximumParticleDeltaTime
                    });

                case "set_time":
                    if (@params["fixedDeltaTime"] != null)
                        Time.fixedDeltaTime = @params["fixedDeltaTime"].ToObject<float>();
                    if (@params["timeScale"] != null)
                        Time.timeScale = @params["timeScale"].ToObject<float>();
                    if (@params["maximumDeltaTime"] != null)
                        Time.maximumDeltaTime = @params["maximumDeltaTime"].ToObject<float>();

                    return new SuccessResponse("Time settings updated");

                case "get_tags_layers":
                    return new SuccessResponse("Tags and Layers", new
                    {
                        tags = UnityEditorInternal.InternalEditorUtility.tags,
                        layers = GetAllLayers(),
                        sortingLayers = SortingLayer.layers.Select(l => new { l.name, l.value, l.id }).ToArray()
                    });

                case "get_input":
                    // Get Input Manager axes (via SerializedObject)
                    var inputManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
                    if (inputManager.Length > 0)
                    {
                        var so = new SerializedObject(inputManager[0]);
                        var axes = so.FindProperty("m_Axes");
                        var axisNames = new List<string>();
                        for (int i = 0; i < axes.arraySize; i++)
                        {
                            var axis = axes.GetArrayElementAtIndex(i);
                            axisNames.Add(axis.FindPropertyRelative("m_Name").stringValue);
                        }
                        return new SuccessResponse("Input axes", new { axes = axisNames.Distinct().ToArray() });
                    }
                    return new ErrorResponse("Could not read Input Manager");

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static Dictionary<int, string> GetAllLayers()
        {
            var layers = new Dictionary<int, string>();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    layers[i] = name;
            }
            return layers;
        }
    }
    #endregion

    #region 2. Asset Importer Tool
    [McpForUnityTool(
        name: "asset_importer",
        Description = "Controls asset import settings. Actions: get_texture, set_texture, get_model, set_model, get_audio, set_audio, reimport")]
    public static class MCPAssetImporter
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string assetPath = @params["assetPath"]?.ToString();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Asset Importer ready. Actions: get_texture, set_texture, get_model, set_model, get_audio, set_audio, reimport");
            }

            if (string.IsNullOrEmpty(assetPath) && action != "reimport_all")
            {
                return new ErrorResponse("Required: assetPath");
            }

            if (!string.IsNullOrEmpty(assetPath) && !assetPath.StartsWith("Assets/"))
                assetPath = "Assets/" + assetPath;

            switch (action)
            {
                case "get_texture":
                    var texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (texImporter == null)
                        return new ErrorResponse("Not a texture or not found");

                    return new SuccessResponse("Texture import settings", new
                    {
                        textureType = texImporter.textureType.ToString(),
                        maxTextureSize = texImporter.maxTextureSize,
                        compression = texImporter.textureCompression.ToString(),
                        mipmapEnabled = texImporter.mipmapEnabled,
                        filterMode = texImporter.filterMode.ToString(),
                        wrapMode = texImporter.wrapMode.ToString(),
                        readable = texImporter.isReadable,
                        alphaSource = texImporter.alphaSource.ToString()
                    });

                case "set_texture":
                    var texImp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (texImp == null)
                        return new ErrorResponse("Not a texture or not found");

                    if (@params["maxSize"] != null)
                        texImp.maxTextureSize = @params["maxSize"].ToObject<int>();
                    if (@params["readable"] != null)
                        texImp.isReadable = @params["readable"].ToObject<bool>();
                    if (@params["mipmaps"] != null)
                        texImp.mipmapEnabled = @params["mipmaps"].ToObject<bool>();
                    if (@params["compression"] != null)
                    {
                        string comp = @params["compression"].ToString().ToLower();
                        texImp.textureCompression = comp switch
                        {
                            "none" => TextureImporterCompression.Uncompressed,
                            "low" => TextureImporterCompression.CompressedLQ,
                            "normal" => TextureImporterCompression.Compressed,
                            "high" => TextureImporterCompression.CompressedHQ,
                            _ => texImp.textureCompression
                        };
                    }

                    texImp.SaveAndReimport();
                    return new SuccessResponse($"Texture settings updated and reimported: {assetPath}");

                case "get_model":
                    var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (modelImporter == null)
                        return new ErrorResponse("Not a model or not found");

                    return new SuccessResponse("Model import settings", new
                    {
                        globalScale = modelImporter.globalScale,
                        meshCompression = modelImporter.meshCompression.ToString(),
                        isReadable = modelImporter.isReadable,
                        importNormals = modelImporter.importNormals.ToString(),
                        importAnimation = modelImporter.importAnimation,
                        animationType = modelImporter.animationType.ToString()
                    });

                case "set_model":
                    var modelImp = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (modelImp == null)
                        return new ErrorResponse("Not a model or not found");

                    if (@params["scale"] != null)
                        modelImp.globalScale = @params["scale"].ToObject<float>();
                    if (@params["readable"] != null)
                        modelImp.isReadable = @params["readable"].ToObject<bool>();
                    if (@params["importAnimation"] != null)
                        modelImp.importAnimation = @params["importAnimation"].ToObject<bool>();

                    modelImp.SaveAndReimport();
                    return new SuccessResponse($"Model settings updated: {assetPath}");

                case "get_audio":
                    var audioImporter = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                    if (audioImporter == null)
                        return new ErrorResponse("Not an audio file or not found");

                    var settings = audioImporter.defaultSampleSettings;
                    return new SuccessResponse("Audio import settings", new
                    {
                        loadType = settings.loadType.ToString(),
                        compressionFormat = settings.compressionFormat.ToString(),
                        quality = settings.quality,
                        sampleRateSetting = settings.sampleRateSetting.ToString(),
                        preloadAudioData = settings.preloadAudioData,
                        forceToMono = audioImporter.forceToMono
                    });

                case "set_audio":
                    var audioImp = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                    if (audioImp == null)
                        return new ErrorResponse("Not an audio file or not found");

                    var audioSettings = audioImp.defaultSampleSettings;

                    if (@params["loadType"] != null)
                    {
                        string lt = @params["loadType"].ToString().ToLower();
                        audioSettings.loadType = lt switch
                        {
                            "decompress" => AudioClipLoadType.DecompressOnLoad,
                            "compressed" => AudioClipLoadType.CompressedInMemory,
                            "streaming" => AudioClipLoadType.Streaming,
                            _ => audioSettings.loadType
                        };
                    }
                    if (@params["quality"] != null)
                        audioSettings.quality = @params["quality"].ToObject<float>();
                    if (@params["forceToMono"] != null)
                        audioImp.forceToMono = @params["forceToMono"].ToObject<bool>();

                    audioImp.defaultSampleSettings = audioSettings;
                    audioImp.SaveAndReimport();
                    return new SuccessResponse($"Audio settings updated: {assetPath}");

                case "reimport":
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    return new SuccessResponse($"Reimported: {assetPath}");

                case "reimport_all":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return new SuccessResponse("All assets reimported");

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }
    }
    #endregion

    #region 3. Build Pipeline Tool
    [McpForUnityTool(
        name: "build_pipeline",
        Description = "Controls build process. Actions: get_scenes, set_scenes, get_settings, build, get_defines, set_defines")]
    public static class MCPBuildPipeline
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Build Pipeline ready. Actions: get_scenes, set_scenes, get_settings, build, get_defines, set_defines");
            }

            switch (action)
            {
                case "get_scenes":
                    var scenes = EditorBuildSettings.scenes.Select(s => new
                    {
                        path = s.path,
                        enabled = s.enabled,
                        guid = s.guid.ToString()
                    }).ToArray();
                    return new SuccessResponse($"Build has {scenes.Length} scenes", new { scenes });

                case "set_scenes":
                    var scenePaths = @params["scenes"]?.ToObject<string[]>();
                    if (scenePaths == null)
                        return new ErrorResponse("Required: scenes (array of paths)");

                    var newScenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
                    EditorBuildSettings.scenes = newScenes;
                    return new SuccessResponse($"Set {newScenes.Length} scenes in build settings");

                case "get_settings":
                    return new SuccessResponse("Build settings", new
                    {
                        target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                        targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                        development = EditorUserBuildSettings.development,
                        scriptDebugging = EditorUserBuildSettings.allowDebugging,
                        productName = PlayerSettings.productName,
                        companyName = PlayerSettings.companyName,
                        version = PlayerSettings.bundleVersion
                    });

                case "get_defines":
                    string defines = PlayerSettings.GetScriptingDefineSymbols(
                        UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
                    return new SuccessResponse("Scripting define symbols", new
                    {
                        defines = defines.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                        raw = defines
                    });

                case "set_defines":
                    var newDefines = @params["defines"]?.ToObject<string[]>();
                    if (newDefines == null)
                        return new ErrorResponse("Required: defines (array of symbols)");

                    string defineStr = string.Join(";", newDefines);
                    PlayerSettings.SetScriptingDefineSymbols(
                        UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), defineStr);
                    return new SuccessResponse($"Set defines: {defineStr}");

                case "build":
                    string outputPath = @params["outputPath"]?.ToString();
                    if (string.IsNullOrEmpty(outputPath))
                        return new ErrorResponse("Required: outputPath");

                    bool development = @params["development"]?.ToObject<bool>() ?? false;

                    var buildScenes = EditorBuildSettings.scenes
                        .Where(s => s.enabled)
                        .Select(s => s.path)
                        .ToArray();

                    if (buildScenes.Length == 0)
                        return new ErrorResponse("No scenes in build settings");

                    var options = new BuildPlayerOptions
                    {
                        scenes = buildScenes,
                        locationPathName = outputPath,
                        target = EditorUserBuildSettings.activeBuildTarget,
                        options = development ? BuildOptions.Development : BuildOptions.None
                    };

                    var report = BuildPipeline.BuildPlayer(options);

                    return new SuccessResponse($"Build {report.summary.result}", new
                    {
                        result = report.summary.result.ToString(),
                        totalTime = report.summary.totalTime.TotalSeconds,
                        totalErrors = report.summary.totalErrors,
                        totalWarnings = report.summary.totalWarnings,
                        outputPath = report.summary.outputPath
                    });

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }
    }
    #endregion

    #region 4. Lighting Tool
    [McpForUnityTool(
        name: "lighting_control",
        Description = "Controls lighting and baking. Actions: get_settings, set_ambient, bake, clear_baked, get_probes")]
    public static class MCPLightingControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Lighting Control ready. Actions: get_settings, set_ambient, bake, clear_baked, get_probes");
            }

            switch (action)
            {
                case "get_settings":
                    return new SuccessResponse("Lighting settings", new
                    {
                        ambientMode = RenderSettings.ambientMode.ToString(),
                        ambientColor = ColorToArray(RenderSettings.ambientLight),
                        ambientIntensity = RenderSettings.ambientIntensity,
                        fog = RenderSettings.fog,
                        fogColor = ColorToArray(RenderSettings.fogColor),
                        fogDensity = RenderSettings.fogDensity,
                        skybox = RenderSettings.skybox?.name,
                        sun = RenderSettings.sun?.name,
                        realtimeGI = Lightmapping.realtimeGI,
                        bakedGI = Lightmapping.bakedGI,
                        isRunning = Lightmapping.isRunning
                    });

                case "set_ambient":
                    var colorArr = @params["color"]?.ToObject<float[]>();
                    if (colorArr != null && colorArr.Length >= 3)
                    {
                        RenderSettings.ambientLight = new Color(colorArr[0], colorArr[1], colorArr[2], colorArr.Length > 3 ? colorArr[3] : 1f);
                    }
                    if (@params["intensity"] != null)
                        RenderSettings.ambientIntensity = @params["intensity"].ToObject<float>();
                    if (@params["fog"] != null)
                        RenderSettings.fog = @params["fog"].ToObject<bool>();

                    return new SuccessResponse("Ambient settings updated");

                case "bake":
                    if (Lightmapping.isRunning)
                        return new ErrorResponse("Lightmapping is already running");

                    bool async = @params["async"]?.ToObject<bool>() ?? true;

                    if (async)
                    {
                        Lightmapping.BakeAsync();
                        return new SuccessResponse("Lightmap baking started (async)");
                    }
                    else
                    {
                        Lightmapping.Bake();
                        return new SuccessResponse("Lightmap baking completed");
                    }

                case "cancel_bake":
                    if (Lightmapping.isRunning)
                    {
                        Lightmapping.Cancel();
                        return new SuccessResponse("Lightmap baking cancelled");
                    }
                    return new SuccessResponse("No baking in progress");

                case "clear_baked":
                    Lightmapping.Clear();
                    Lightmapping.ClearDiskCache();
                    return new SuccessResponse("Baked lightmaps cleared");

                case "get_probes":
                    var probes = UnityEngine.Object.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None);
                    var reflectionProbes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);

                    return new SuccessResponse("Light probes info", new
                    {
                        lightProbeGroups = probes.Select(p => new { name = p.name, probeCount = p.probePositions.Length }).ToArray(),
                        reflectionProbes = reflectionProbes.Select(r => new { name = r.name, mode = r.mode.ToString(), size = r.size }).ToArray()
                    });

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static float[] ColorToArray(Color c) => new[] { c.r, c.g, c.b, c.a };
    }
    #endregion

    #region 5. Debug Visualizer Tool
    [McpForUnityTool(
        name: "debug_visualizer",
        Description = "Visual debugging without Unity screen. Actions: describe_scene, describe_object, get_bounds, get_spatial_map, compare_screenshots")]
    public static class MCPDebugVisualizer
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Debug Visualizer ready. Actions: describe_scene, describe_object, get_bounds, get_spatial_map, compare_screenshots");
            }

            switch (action)
            {
                case "describe_scene":
                    return DescribeScene();

                case "describe_object":
                    string objName = @params["objectName"]?.ToString();
                    if (string.IsNullOrEmpty(objName))
                        return new ErrorResponse("Required: objectName");
                    return DescribeObject(objName);

                case "get_bounds":
                    return GetSceneBounds();

                case "get_spatial_map":
                    float cellSize = @params["cellSize"]?.ToObject<float>() ?? 5f;
                    return GetSpatialMap(cellSize);

                case "compare_screenshots":
                    // Placeholder - would need image comparison library
                    return new ErrorResponse("Screenshot comparison requires external image processing");

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static object DescribeScene()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var description = new
            {
                sceneName = scene.name,
                rootObjectCount = rootObjects.Length,
                totalObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                cameras = DescribeCameras(),
                lights = DescribeLights(),
                players = DescribeTagged("Player"),
                importantObjects = rootObjects.Take(20).Select(o => new
                {
                    name = o.name,
                    position = VectorToString(o.transform.position),
                    children = o.transform.childCount,
                    components = o.GetComponents<Component>().Length
                }).ToArray()
            };

            return new SuccessResponse("Scene description", description);
        }

        private static object DescribeCameras()
        {
            return Camera.allCameras.Select(c => new
            {
                name = c.name,
                position = VectorToString(c.transform.position),
                rotation = VectorToString(c.transform.eulerAngles),
                fieldOfView = c.fieldOfView,
                isMain = c == Camera.main
            }).ToArray();
        }

        private static object DescribeLights()
        {
            return UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l => new
            {
                name = l.name,
                type = l.type.ToString(),
                position = VectorToString(l.transform.position),
                color = $"RGB({l.color.r:F2},{l.color.g:F2},{l.color.b:F2})",
                intensity = l.intensity
            }).ToArray();
        }

        private static object DescribeTagged(string tag)
        {
            try
            {
                return GameObject.FindGameObjectsWithTag(tag).Select(o => new
                {
                    name = o.name,
                    position = VectorToString(o.transform.position)
                }).ToArray();
            }
            catch { return new object[0]; }
        }

        private static object DescribeObject(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return new ErrorResponse($"Object '{name}' not found");

            var bounds = CalculateBounds(go);
            var components = go.GetComponents<Component>();

            return new SuccessResponse($"Object: {name}", new
            {
                name = go.name,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                position = VectorToString(go.transform.position),
                rotation = VectorToString(go.transform.eulerAngles),
                scale = VectorToString(go.transform.localScale),
                bounds = bounds != null ? new
                {
                    center = VectorToString(bounds.Value.center),
                    size = VectorToString(bounds.Value.size)
                } : null,
                components = components.Where(c => c != null).Select(c => c.GetType().Name).ToArray(),
                children = GetChildrenDescription(go.transform),
                parent = go.transform.parent?.name
            });
        }

        private static object GetSceneBounds()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            if (renderers.Length == 0)
                return new SuccessResponse("No renderers in scene", new { hasBounds = false });

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
            {
                bounds.Encapsulate(r.bounds);
            }

            return new SuccessResponse("Scene bounds", new
            {
                center = VectorToString(bounds.center),
                size = VectorToString(bounds.size),
                min = VectorToString(bounds.min),
                max = VectorToString(bounds.max)
            });
        }

        private static object GetSpatialMap(float cellSize)
        {
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var cells = new Dictionary<string, List<string>>();

            foreach (var go in allObjects)
            {
                if (!go.activeInHierarchy) continue;

                var pos = go.transform.position;
                int x = Mathf.FloorToInt(pos.x / cellSize);
                int y = Mathf.FloorToInt(pos.y / cellSize);
                int z = Mathf.FloorToInt(pos.z / cellSize);
                string key = $"{x},{y},{z}";

                if (!cells.ContainsKey(key))
                    cells[key] = new List<string>();

                if (cells[key].Count < 5) // Limit per cell
                    cells[key].Add(go.name);
            }

            return new SuccessResponse($"Spatial map ({cells.Count} cells)", new
            {
                cellSize,
                cells = cells.Select(kv => new { cell = kv.Key, objects = kv.Value }).ToArray()
            });
        }

        private static Bounds? CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static object[] GetChildrenDescription(Transform t)
        {
            var children = new List<object>();
            for (int i = 0; i < t.childCount && i < 10; i++)
            {
                var child = t.GetChild(i);
                children.Add(new
                {
                    name = child.name,
                    localPosition = VectorToString(child.localPosition),
                    hasChildren = child.childCount > 0
                });
            }
            return children.ToArray();
        }

        private static string VectorToString(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
    #endregion

    #region 6. Scene Comparison Tool
    [McpForUnityTool(
        name: "scene_diff",
        Description = "Compares scene states. Actions: snapshot, compare, list_snapshots, clear_snapshots")]
    public static class MCPSceneDiff
    {
        private static Dictionary<string, Dictionary<int, ObjectSnapshot>> snapshots = new Dictionary<string, Dictionary<int, ObjectSnapshot>>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Scene Diff ready. Actions: snapshot, compare, list_snapshots, clear_snapshots");
            }

            switch (action)
            {
                case "snapshot":
                    string snapshotName = @params["name"]?.ToString() ?? DateTime.Now.ToString("HHmmss");
                    return TakeSnapshot(snapshotName);

                case "compare":
                    string name1 = @params["snapshot1"]?.ToString();
                    string name2 = @params["snapshot2"]?.ToString();
                    if (string.IsNullOrEmpty(name1))
                        return new ErrorResponse("Required: snapshot1");
                    return CompareSnapshots(name1, name2);

                case "list_snapshots":
                    return new SuccessResponse($"{snapshots.Count} snapshots", new
                    {
                        snapshots = snapshots.Keys.ToArray()
                    });

                case "clear_snapshots":
                    snapshots.Clear();
                    return new SuccessResponse("Snapshots cleared");

                default:
                    return new ErrorResponse($"Unknown action '{action}'");
            }
        }

        private static object TakeSnapshot(string name)
        {
            var snapshot = new Dictionary<int, ObjectSnapshot>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                snapshot[go.GetInstanceID()] = new ObjectSnapshot
                {
                    name = go.name,
                    active = go.activeSelf,
                    position = go.transform.position,
                    rotation = go.transform.eulerAngles,
                    componentCount = go.GetComponents<Component>().Length
                };
            }

            snapshots[name] = snapshot;
            return new SuccessResponse($"Snapshot '{name}' taken with {snapshot.Count} objects");
        }

        private static object CompareSnapshots(string name1, string name2)
        {
            if (!snapshots.ContainsKey(name1))
                return new ErrorResponse($"Snapshot '{name1}' not found");

            Dictionary<int, ObjectSnapshot> snap1 = snapshots[name1];
            Dictionary<int, ObjectSnapshot> snap2;

            if (string.IsNullOrEmpty(name2))
            {
                // Compare with current state
                snap2 = new Dictionary<int, ObjectSnapshot>();
                foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    snap2[go.GetInstanceID()] = new ObjectSnapshot
                    {
                        name = go.name,
                        active = go.activeSelf,
                        position = go.transform.position,
                        rotation = go.transform.eulerAngles,
                        componentCount = go.GetComponents<Component>().Length
                    };
                }
            }
            else
            {
                if (!snapshots.ContainsKey(name2))
                    return new ErrorResponse($"Snapshot '{name2}' not found");
                snap2 = snapshots[name2];
            }

            var added = snap2.Keys.Except(snap1.Keys).Select(id => snap2[id].name).ToArray();
            var removed = snap1.Keys.Except(snap2.Keys).Select(id => snap1[id].name).ToArray();
            var modified = new List<object>();

            foreach (var id in snap1.Keys.Intersect(snap2.Keys))
            {
                var o1 = snap1[id];
                var o2 = snap2[id];

                var changes = new List<string>();
                if (o1.active != o2.active) changes.Add($"active: {o1.active} -> {o2.active}");
                if (Vector3.Distance(o1.position, o2.position) > 0.01f) changes.Add($"moved: {o1.position} -> {o2.position}");
                if (o1.componentCount != o2.componentCount) changes.Add($"components: {o1.componentCount} -> {o2.componentCount}");

                if (changes.Count > 0)
                {
                    modified.Add(new { name = o1.name, changes });
                }
            }

            return new SuccessResponse("Scene comparison", new
            {
                snapshot1 = name1,
                snapshot2 = name2 ?? "current",
                added = added.Length,
                removed = removed.Length,
                modified = modified.Count,
                addedObjects = added.Take(20),
                removedObjects = removed.Take(20),
                modifiedObjects = modified.Take(20)
            });
        }

        private class ObjectSnapshot
        {
            public string name;
            public bool active;
            public Vector3 position;
            public Vector3 rotation;
            public int componentCount;
        }
    }
    #endregion
}
