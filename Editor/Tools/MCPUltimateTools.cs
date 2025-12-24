#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;
using Endurance.Runtime;

namespace MCPForUnity.Editor.Tools
{
    #region 1. Audio Mixer Control Tool
    [McpForUnityTool(
        name: "audio_mixer",
        Description = "Controls AudioMixer. Actions: list, get_groups, set_float, get_float, set_snapshot, get_snapshots, create")]
    public static class MCPAudioMixer
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "list":
                    string[] guids = AssetDatabase.FindAssets("t:AudioMixer");
                    var mixers = guids.Select(g => {
                        string path = AssetDatabase.GUIDToAssetPath(g);
                        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                        return new { path, name = mixer?.name };
                    }).ToList();
                    return new SuccessResponse($"Found {mixers.Count} AudioMixers", new { mixers });

                case "get_groups":
                    string mixerPath = @params["mixerPath"]?.ToString();
                    var targetMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
                    if (targetMixer == null) return new ErrorResponse($"AudioMixer not found at '{mixerPath}'");

                    // Get groups via reflection (AudioMixer doesn't expose them directly)
                    var groups = new List<object>();
                    var findMatchingGroups = targetMixer.FindMatchingGroups(string.Empty);
                    foreach (var group in findMatchingGroups)
                    {
                        groups.Add(new { name = group.name });
                    }
                    return new SuccessResponse($"Found {groups.Count} groups", new { groups });

                case "set_float":
                    string setMixerPath = @params["mixerPath"]?.ToString();
                    string paramName = @params["parameter"]?.ToString();
                    float paramValue = @params["value"]?.ToObject<float>() ?? 0f;

                    var setMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(setMixerPath);
                    if (setMixer == null) return new ErrorResponse($"AudioMixer not found at '{setMixerPath}'");

                    if (setMixer.SetFloat(paramName, paramValue))
                        return new SuccessResponse($"Set '{paramName}' to {paramValue}");
                    return new ErrorResponse($"Parameter '{paramName}' not found or not exposed");

                case "get_float":
                    string getMixerPath = @params["mixerPath"]?.ToString();
                    string getParamName = @params["parameter"]?.ToString();

                    var getMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(getMixerPath);
                    if (getMixer == null) return new ErrorResponse($"AudioMixer not found at '{getMixerPath}'");

                    if (getMixer.GetFloat(getParamName, out float value))
                        return new SuccessResponse($"'{getParamName}' = {value}", new { parameter = getParamName, value });
                    return new ErrorResponse($"Parameter '{getParamName}' not found or not exposed");

                case "get_snapshots":
                    string snapMixerPath = @params["mixerPath"]?.ToString();
                    var snapMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(snapMixerPath);
                    if (snapMixer == null) return new ErrorResponse($"AudioMixer not found at '{snapMixerPath}'");

                    // Get snapshots - they're sub-assets
                    var snapshots = AssetDatabase.LoadAllAssetsAtPath(snapMixerPath)
                        .Where(a => a is AudioMixerSnapshot)
                        .Select(a => a.name)
                        .ToList();

                    return new SuccessResponse($"Found {snapshots.Count} snapshots", new { snapshots });

                case "set_snapshot":
                    string transMixerPath = @params["mixerPath"]?.ToString();
                    string snapshotName = @params["snapshot"]?.ToString();
                    float transitionTime = @params["transitionTime"]?.ToObject<float>() ?? 0f;

                    var transMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(transMixerPath);
                    if (transMixer == null) return new ErrorResponse($"AudioMixer not found at '{transMixerPath}'");

                    var snapshot = AssetDatabase.LoadAllAssetsAtPath(transMixerPath)
                        .FirstOrDefault(a => a is AudioMixerSnapshot && a.name == snapshotName) as AudioMixerSnapshot;

                    if (snapshot == null) return new ErrorResponse($"Snapshot '{snapshotName}' not found");

                    snapshot.TransitionTo(transitionTime);
                    return new SuccessResponse($"Transitioning to snapshot '{snapshotName}' over {transitionTime}s");

                case "create":
                    string newPath = @params["path"]?.ToString() ?? "Assets/Audio/NewMixer.mixer";
                    string dirPath = System.IO.Path.GetDirectoryName(newPath);

                    if (!AssetDatabase.IsValidFolder(dirPath))
                    {
                        string[] parts = dirPath.Replace("\\", "/").Split('/');
                        string current = parts[0];
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string next = current + "/" + parts[i];
                            if (!AssetDatabase.IsValidFolder(next))
                                AssetDatabase.CreateFolder(current, parts[i]);
                            current = next;
                        }
                    }

                    // AudioMixer must be created via menu - can't create programmatically
                    EditorApplication.ExecuteMenuItem("Assets/Create/Audio Mixer");
                    return new SuccessResponse($"Audio Mixer creation dialog opened. Save to '{newPath}'. Note: AudioMixer cannot be created programmatically.");

                default:
                    return new SuccessResponse("AudioMixer ready. Actions: list, get_groups, set_float, get_float, get_snapshots, set_snapshot, create");
            }
        }
    }
    #endregion

    #region 2. Cinemachine Control Tool
    [McpForUnityTool(
        name: "cinemachine_control",
        Description = "Controls Cinemachine cameras. Actions: list, create, set_follow, set_look_at, set_priority, get_settings, set_settings")]
    public static class MCPCinemachineControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            // Check if Cinemachine is installed
            var vcamType = Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine")
                ?? Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");

            if (vcamType == null && action != "check")
            {
                return new ErrorResponse("Cinemachine not found. Install via Package Manager: com.unity.cinemachine");
            }

            switch (action)
            {
                case "check":
                    return new SuccessResponse("Cinemachine status", new {
                        installed = vcamType != null,
                        version = vcamType != null ? "3.x or 2.x detected" : "Not installed"
                    });

                case "list":
                    var vcams = UnityEngine.Object.FindObjectsByType(vcamType, FindObjectsSortMode.None);
                    var camList = new List<object>();

                    foreach (var vcam in vcams)
                    {
                        var go = (vcam as Component)?.gameObject;
                        var priority = vcamType.GetProperty("Priority")?.GetValue(vcam);
                        var follow = vcamType.GetProperty("Follow")?.GetValue(vcam) as Transform;
                        var lookAt = vcamType.GetProperty("LookAt")?.GetValue(vcam) as Transform;

                        camList.Add(new {
                            name = go?.name,
                            priority,
                            follow = follow?.name,
                            lookAt = lookAt?.name,
                            enabled = (vcam as Behaviour)?.enabled
                        });
                    }

                    return new SuccessResponse($"Found {camList.Count} virtual cameras", new { cameras = camList });

                case "create":
                    string camName = @params["name"]?.ToString() ?? "VirtualCamera";
                    var posArr = @params["position"]?.ToObject<float[]>() ?? new float[] { 0, 5, -10 };

                    var camGo = new GameObject(camName);
                    camGo.transform.position = new Vector3(posArr[0], posArr[1], posArr[2]);

                    var vcamComponent = camGo.AddComponent(vcamType);

                    // Set default priority
                    vcamType.GetProperty("Priority")?.SetValue(vcamComponent, @params["priority"]?.ToObject<int>() ?? 10);

                    // Set follow target if specified
                    string followTarget = @params["follow"]?.ToString();
                    if (!string.IsNullOrEmpty(followTarget))
                    {
                        var followGo = GameObject.Find(followTarget);
                        if (followGo != null)
                            vcamType.GetProperty("Follow")?.SetValue(vcamComponent, followGo.transform);
                    }

                    // Set look at target if specified
                    string lookAtTarget = @params["lookAt"]?.ToString();
                    if (!string.IsNullOrEmpty(lookAtTarget))
                    {
                        var lookAtGo = GameObject.Find(lookAtTarget);
                        if (lookAtGo != null)
                            vcamType.GetProperty("LookAt")?.SetValue(vcamComponent, lookAtGo.transform);
                    }

                    Undo.RegisterCreatedObjectUndo(camGo, "Create Virtual Camera");
                    return new SuccessResponse($"Created Cinemachine camera '{camName}'");

                case "set_follow":
                    string setCamName = @params["camera"]?.ToString();
                    string setFollowName = @params["target"]?.ToString();

                    var setCamGo = GameObject.Find(setCamName);
                    if (setCamGo == null) return new ErrorResponse($"Camera '{setCamName}' not found");

                    var setVcam = setCamGo.GetComponent(vcamType);
                    if (setVcam == null) return new ErrorResponse($"No Cinemachine camera on '{setCamName}'");

                    var setFollowGo = GameObject.Find(setFollowName);
                    if (setFollowGo == null) return new ErrorResponse($"Target '{setFollowName}' not found");

                    vcamType.GetProperty("Follow")?.SetValue(setVcam, setFollowGo.transform);
                    EditorUtility.SetDirty(setVcam as UnityEngine.Object);

                    return new SuccessResponse($"Set '{setCamName}' to follow '{setFollowName}'");

                case "set_look_at":
                    string lookCamName = @params["camera"]?.ToString();
                    string lookTargetName = @params["target"]?.ToString();

                    var lookCamGo = GameObject.Find(lookCamName);
                    if (lookCamGo == null) return new ErrorResponse($"Camera '{lookCamName}' not found");

                    var lookVcam = lookCamGo.GetComponent(vcamType);
                    if (lookVcam == null) return new ErrorResponse($"No Cinemachine camera on '{lookCamName}'");

                    var lookTargetGo = GameObject.Find(lookTargetName);
                    if (lookTargetGo == null) return new ErrorResponse($"Target '{lookTargetName}' not found");

                    vcamType.GetProperty("LookAt")?.SetValue(lookVcam, lookTargetGo.transform);
                    EditorUtility.SetDirty(lookVcam as UnityEngine.Object);

                    return new SuccessResponse($"Set '{lookCamName}' to look at '{lookTargetName}'");

                case "set_priority":
                    string prioCamName = @params["camera"]?.ToString();
                    int newPriority = @params["priority"]?.ToObject<int>() ?? 10;

                    var prioCamGo = GameObject.Find(prioCamName);
                    if (prioCamGo == null) return new ErrorResponse($"Camera '{prioCamName}' not found");

                    var prioVcam = prioCamGo.GetComponent(vcamType);
                    if (prioVcam == null) return new ErrorResponse($"No Cinemachine camera on '{prioCamName}'");

                    vcamType.GetProperty("Priority")?.SetValue(prioVcam, newPriority);
                    EditorUtility.SetDirty(prioVcam as UnityEngine.Object);

                    return new SuccessResponse($"Set '{prioCamName}' priority to {newPriority}");

                default:
                    return new SuccessResponse("Cinemachine ready. Actions: check, list, create, set_follow, set_look_at, set_priority");
            }
        }
    }
    #endregion

    #region 3. Reflection Probe Control Tool
    [McpForUnityTool(
        name: "reflection_probe",
        Description = "Controls Reflection Probes. Actions: list, create, bake, bake_all, get_settings, set_settings")]
    public static class MCPReflectionProbe
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string targetName = @params["target"]?.ToString();

            switch (action)
            {
                case "list":
                    var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
                    var probeList = probes.Select(p => new {
                        name = p.gameObject.name,
                        position = $"({p.transform.position.x:F1},{p.transform.position.y:F1},{p.transform.position.z:F1})",
                        size = $"({p.size.x:F1},{p.size.y:F1},{p.size.z:F1})",
                        mode = p.mode.ToString(),
                        resolution = p.resolution,
                        importance = p.importance
                    }).ToList();
                    return new SuccessResponse($"Found {probeList.Count} reflection probes", new { probes = probeList });

                case "create":
                    string probeName = @params["name"]?.ToString() ?? "ReflectionProbe";
                    var posArr = @params["position"]?.ToObject<float[]>() ?? new float[] { 0, 1, 0 };
                    var sizeArr = @params["size"]?.ToObject<float[]>() ?? new float[] { 10, 10, 10 };

                    var probeGo = new GameObject(probeName);
                    probeGo.transform.position = new Vector3(posArr[0], posArr[1], posArr[2]);

                    var probe = probeGo.AddComponent<ReflectionProbe>();
                    probe.size = new Vector3(sizeArr[0], sizeArr[1], sizeArr[2]);
                    probe.mode = @params["realtime"]?.ToObject<bool>() == true
                        ? ReflectionProbeMode.Realtime
                        : ReflectionProbeMode.Baked;
                    probe.resolution = @params["resolution"]?.ToObject<int>() ?? 256;

                    Undo.RegisterCreatedObjectUndo(probeGo, "Create Reflection Probe");
                    return new SuccessResponse($"Created ReflectionProbe '{probeName}'");

                case "bake":
                    var bakeGo = GameObject.Find(targetName);
                    if (bakeGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var bakeProbe = bakeGo.GetComponent<ReflectionProbe>();
                    if (bakeProbe == null) return new ErrorResponse($"No ReflectionProbe on '{targetName}'");

                    Lightmapping.BakeReflectionProbe(bakeProbe, AssetDatabase.GetAssetPath(bakeProbe.bakedTexture) ?? $"Assets/{targetName}_reflection.exr");
                    return new SuccessResponse($"Baking reflection probe '{targetName}'");

                case "bake_all":
                    // Bake all probes by iterating
                    var allProbes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
                    foreach (var p in allProbes)
                    {
                        if (p.mode == ReflectionProbeMode.Baked)
                        {
                            string bakedPath = $"Assets/ReflectionProbes/{p.name}_reflection.exr";
                            Lightmapping.BakeReflectionProbe(p, bakedPath);
                        }
                    }
                    return new SuccessResponse($"Baking {allProbes.Length} reflection probes");

                case "get_settings":
                    var getGo = GameObject.Find(targetName);
                    if (getGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var getProbe = getGo.GetComponent<ReflectionProbe>();
                    if (getProbe == null) return new ErrorResponse($"No ReflectionProbe on '{targetName}'");

                    return new SuccessResponse("Reflection probe settings", new {
                        mode = getProbe.mode.ToString(),
                        refreshMode = getProbe.refreshMode.ToString(),
                        resolution = getProbe.resolution,
                        size = new { x = getProbe.size.x, y = getProbe.size.y, z = getProbe.size.z },
                        center = new { x = getProbe.center.x, y = getProbe.center.y, z = getProbe.center.z },
                        importance = getProbe.importance,
                        intensity = getProbe.intensity,
                        boxProjection = getProbe.boxProjection,
                        blendDistance = getProbe.blendDistance,
                        hdr = getProbe.hdr,
                        shadowDistance = getProbe.shadowDistance
                    });

                case "set_settings":
                    var setGo = GameObject.Find(targetName);
                    if (setGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var setProbe = setGo.GetComponent<ReflectionProbe>();
                    if (setProbe == null) return new ErrorResponse($"No ReflectionProbe on '{targetName}'");

                    if (@params["resolution"] != null) setProbe.resolution = @params["resolution"].ToObject<int>();
                    if (@params["importance"] != null) setProbe.importance = @params["importance"].ToObject<int>();
                    if (@params["intensity"] != null) setProbe.intensity = @params["intensity"].ToObject<float>();
                    if (@params["boxProjection"] != null) setProbe.boxProjection = @params["boxProjection"].ToObject<bool>();
                    if (@params["blendDistance"] != null) setProbe.blendDistance = @params["blendDistance"].ToObject<float>();

                    var newSize = @params["size"]?.ToObject<float[]>();
                    if (newSize != null && newSize.Length >= 3)
                        setProbe.size = new Vector3(newSize[0], newSize[1], newSize[2]);

                    EditorUtility.SetDirty(setProbe);
                    return new SuccessResponse($"Updated reflection probe '{targetName}'");

                default:
                    return new SuccessResponse("ReflectionProbe ready. Actions: list, create, bake, bake_all, get_settings, set_settings");
            }
        }
    }
    #endregion

    #region 4. Profiler & Memory Analyzer Tool
    [McpForUnityTool(
        name: "profiler_control",
        Description = "Controls Unity Profiler. Actions: get_memory, get_stats, get_object_count, find_heavy_objects, gc_collect")]
    public static class MCPProfilerControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_memory":
                    long totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    long reservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                    long unusedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();
                    long monoHeap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();
                    long monoUsed = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

                    return new SuccessResponse("Memory usage", new {
                        totalAllocatedMB = totalMemory / (1024f * 1024f),
                        totalReservedMB = reservedMemory / (1024f * 1024f),
                        unusedReservedMB = unusedMemory / (1024f * 1024f),
                        monoHeapMB = monoHeap / (1024f * 1024f),
                        monoUsedMB = monoUsed / (1024f * 1024f),
                        gcCollectionCount = GC.CollectionCount(0)
                    });

                case "get_stats":
                    return new SuccessResponse("Runtime stats", new {
                        fps = 1f / Time.unscaledDeltaTime,
                        deltaTime = Time.deltaTime,
                        fixedDeltaTime = Time.fixedDeltaTime,
                        timeScale = Time.timeScale,
                        frameCount = Time.frameCount,
                        realtimeSinceStartup = Time.realtimeSinceStartup,
                        isPlaying = Application.isPlaying
                    });

                case "get_object_count":
                    var objectCounts = new Dictionary<string, int>();

                    // Count by type
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
                    foreach (var obj in allObjects)
                    {
                        string typeName = obj.GetType().Name;
                        if (!objectCounts.ContainsKey(typeName))
                            objectCounts[typeName] = 0;
                        objectCounts[typeName]++;
                    }

                    var sortedCounts = objectCounts
                        .OrderByDescending(kv => kv.Value)
                        .Take(20)
                        .Select(kv => new { type = kv.Key, count = kv.Value })
                        .ToList();

                    return new SuccessResponse($"Object counts (top 20 of {objectCounts.Count} types)", new {
                        totalObjects = allObjects.Length,
                        byType = sortedCounts
                    });

                case "find_heavy_objects":
                    var heavyObjects = new List<object>();

                    // Find objects with most vertices
                    var meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
                    var meshStats = meshFilters
                        .Where(mf => mf.sharedMesh != null)
                        .Select(mf => new {
                            name = mf.gameObject.name,
                            vertices = mf.sharedMesh.vertexCount,
                            triangles = mf.sharedMesh.triangles.Length / 3,
                            meshName = mf.sharedMesh.name
                        })
                        .OrderByDescending(m => m.vertices)
                        .Take(10)
                        .ToList();

                    // Find objects with most components
                    var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                    var componentStats = new List<object>();
                    foreach (var root in rootObjects)
                    {
                        CountComponents(root.transform, componentStats);
                    }
                    var topComponents = componentStats
                        .OrderByDescending(c => ((dynamic)c).componentCount)
                        .Take(10)
                        .ToList();

                    return new SuccessResponse("Heavy objects analysis", new {
                        highPolyMeshes = meshStats,
                        manyComponents = topComponents
                    });

                case "gc_collect":
                    long beforeMem = GC.GetTotalMemory(false);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    long afterMem = GC.GetTotalMemory(true);

                    return new SuccessResponse("Garbage collection completed", new {
                        beforeMB = beforeMem / (1024f * 1024f),
                        afterMB = afterMem / (1024f * 1024f),
                        freedMB = (beforeMem - afterMem) / (1024f * 1024f)
                    });

                case "get_texture_memory":
                    var textures = UnityEngine.Resources.FindObjectsOfTypeAll<Texture2D>();
                    var textureStats = textures
                        .Where(t => t != null)
                        .Select(t => {
                            long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
                            return new {
                                name = t.name,
                                sizeMB = size / (1024f * 1024f),
                                dimensions = $"{t.width}x{t.height}",
                                format = t.format.ToString()
                            };
                        })
                        .OrderByDescending(t => t.sizeMB)
                        .Take(15)
                        .ToList();

                    float totalTextureMB = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)) / (1024f * 1024f);

                    return new SuccessResponse($"Texture memory: {totalTextureMB:F2} MB", new {
                        totalTextureMB,
                        textureCount = textures.Length,
                        largestTextures = textureStats
                    });

                default:
                    return new SuccessResponse("Profiler ready. Actions: get_memory, get_stats, get_object_count, find_heavy_objects, gc_collect, get_texture_memory");
            }
        }

        private static void CountComponents(Transform t, List<object> results)
        {
            var components = t.GetComponents<Component>();
            results.Add(new { name = t.name, componentCount = components.Length, path = GetPath(t) });

            foreach (Transform child in t)
            {
                CountComponents(child, results);
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
    #endregion

    #region 5. Asset Dependency Analyzer Tool
    [McpForUnityTool(
        name: "dependency_analyzer",
        Description = "Analyzes asset dependencies. Actions: get_dependencies, get_dependents, find_unused, find_duplicates")]
    public static class MCPDependencyAnalyzer
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_dependencies":
                    string assetPath = @params["assetPath"]?.ToString();
                    if (string.IsNullOrEmpty(assetPath)) return new ErrorResponse("assetPath required");

                    string[] deps = AssetDatabase.GetDependencies(assetPath, @params["recursive"]?.ToObject<bool>() ?? true);
                    var depList = deps.Where(d => d != assetPath).Select(d => new {
                        path = d,
                        type = AssetDatabase.GetMainAssetTypeAtPath(d)?.Name
                    }).ToList();

                    return new SuccessResponse($"'{assetPath}' has {depList.Count} dependencies", new { dependencies = depList });

                case "get_dependents":
                    string targetPath = @params["assetPath"]?.ToString();
                    if (string.IsNullOrEmpty(targetPath)) return new ErrorResponse("assetPath required");

                    // Find all assets that depend on this one
                    var dependents = new List<string>();
                    string[] allAssets = AssetDatabase.GetAllAssetPaths();

                    foreach (var asset in allAssets)
                    {
                        if (asset == targetPath) continue;
                        var assetDeps = AssetDatabase.GetDependencies(asset, false);
                        if (assetDeps.Contains(targetPath))
                        {
                            dependents.Add(asset);
                        }
                    }

                    return new SuccessResponse($"'{targetPath}' is used by {dependents.Count} assets", new { dependents });

                case "find_unused":
                    string searchPath = @params["path"]?.ToString() ?? "Assets";
                    string filterType = @params["filterType"]?.ToString(); // e.g., "Material", "Texture2D"

                    // Get all assets in path
                    string searchFilter = string.IsNullOrEmpty(filterType) ? "" : $"t:{filterType}";
                    string[] assetsInPath = AssetDatabase.FindAssets(searchFilter, new[] { searchPath });

                    // Get all scene assets and their dependencies
                    var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                    var usedAssets = new HashSet<string>();

                    foreach (var sceneGuid in sceneGuids)
                    {
                        string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                        var sceneDeps = AssetDatabase.GetDependencies(scenePath, true);
                        foreach (var dep in sceneDeps)
                        {
                            usedAssets.Add(dep);
                        }
                    }

                    // Also check prefabs
                    var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                    foreach (var prefabGuid in prefabGuids)
                    {
                        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                        usedAssets.Add(prefabPath);
                        var prefabDeps = AssetDatabase.GetDependencies(prefabPath, true);
                        foreach (var dep in prefabDeps)
                        {
                            usedAssets.Add(dep);
                        }
                    }

                    var unusedAssets = new List<object>();
                    foreach (var guid in assetsInPath)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!usedAssets.Contains(path) && !path.EndsWith(".cs") && !path.EndsWith(".unity"))
                        {
                            unusedAssets.Add(new {
                                path,
                                type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name
                            });
                        }
                    }

                    return new SuccessResponse($"Found {unusedAssets.Count} potentially unused assets", new {
                        unusedAssets = unusedAssets.Take(50).ToList(),
                        note = unusedAssets.Count > 50 ? $"Showing first 50 of {unusedAssets.Count}" : null
                    });

                case "find_duplicates":
                    string dupPath = @params["path"]?.ToString() ?? "Assets";
                    string dupType = @params["filterType"]?.ToString() ?? "Texture2D";

                    string[] dupAssets = AssetDatabase.FindAssets($"t:{dupType}", new[] { dupPath });

                    // Group by file size as a simple duplicate detection
                    var sizeGroups = new Dictionary<long, List<string>>();
                    foreach (var guid in dupAssets)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var fileInfo = new System.IO.FileInfo(path);
                        if (fileInfo.Exists)
                        {
                            if (!sizeGroups.ContainsKey(fileInfo.Length))
                                sizeGroups[fileInfo.Length] = new List<string>();
                            sizeGroups[fileInfo.Length].Add(path);
                        }
                    }

                    var potentialDuplicates = sizeGroups
                        .Where(kv => kv.Value.Count > 1)
                        .Select(kv => new {
                            sizeBytes = kv.Key,
                            files = kv.Value
                        })
                        .ToList();

                    return new SuccessResponse($"Found {potentialDuplicates.Count} groups of potential duplicates", new { potentialDuplicates });

                default:
                    return new SuccessResponse("Dependency Analyzer ready. Actions: get_dependencies, get_dependents, find_unused, find_duplicates");
            }
        }
    }
    #endregion

    #region 6. Code Generator Tool
    [McpForUnityTool(
        name: "code_generator",
        Description = "Generates boilerplate code. Actions: monobehaviour, scriptable_object, interface, singleton, state_machine, event_channel")]
    public static class MCPCodeGenerator
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string className = @params["className"]?.ToString();
            string namespaceName = @params["namespace"]?.ToString() ?? "Game";
            string path = @params["path"]?.ToString() ?? "Assets/Scripts";

            if (string.IsNullOrEmpty(className) && action != "list")
            {
                return new ErrorResponse("className required");
            }

            switch (action)
            {
                case "monobehaviour":
                    string mbCode = GenerateMonoBehaviour(className, namespaceName, @params);
                    return SaveScript(path, className, mbCode);

                case "scriptable_object":
                    string soCode = GenerateScriptableObject(className, namespaceName, @params);
                    return SaveScript(path, className, soCode);

                case "interface":
                    string ifCode = GenerateInterface(className, namespaceName, @params);
                    return SaveScript(path, className, ifCode);

                case "singleton":
                    string sgCode = GenerateSingleton(className, namespaceName, @params);
                    return SaveScript(path, className, sgCode);

                case "state_machine":
                    string smCode = GenerateStateMachine(className, namespaceName, @params);
                    return SaveScript(path, className, smCode);

                case "event_channel":
                    string ecCode = GenerateEventChannel(className, namespaceName, @params);
                    return SaveScript(path, className, ecCode);

                case "list":
                    return new SuccessResponse("Code Generator templates", new {
                        templates = new[] {
                            "monobehaviour - Basic MonoBehaviour with lifecycle methods",
                            "scriptable_object - ScriptableObject with CreateAssetMenu",
                            "interface - Interface with common methods",
                            "singleton - Thread-safe singleton MonoBehaviour",
                            "state_machine - State machine pattern implementation",
                            "event_channel - ScriptableObject-based event system"
                        }
                    });

                default:
                    return new SuccessResponse("Code Generator ready. Actions: monobehaviour, scriptable_object, interface, singleton, state_machine, event_channel, list");
            }
        }

        private static object SaveScript(string path, string className, string code)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Replace("\\", "/").Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            string fullPath = $"{path}/{className}.cs";
            System.IO.File.WriteAllText(fullPath, code);
            AssetDatabase.Refresh();

            return new SuccessResponse($"Generated '{className}.cs' at '{fullPath}'", new { path = fullPath });
        }

        private static string GenerateMonoBehaviour(string className, string ns, JObject @params)
        {
            bool includeUpdate = @params["includeUpdate"]?.ToObject<bool>() ?? true;
            bool includeFixedUpdate = @params["includeFixedUpdate"]?.ToObject<bool>() ?? false;

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : MonoBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine("        [Header(\"Settings\")]");
            sb.AppendLine("        [SerializeField] private float speed = 5f;");
            sb.AppendLine();
            sb.AppendLine("        private void Awake()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Initialize");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void Start()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Start logic");
            sb.AppendLine("        }");
            if (includeUpdate)
            {
                sb.AppendLine();
                sb.AppendLine("        private void Update()");
                sb.AppendLine("        {");
                sb.AppendLine("            // Frame update");
                sb.AppendLine("        }");
            }
            if (includeFixedUpdate)
            {
                sb.AppendLine();
                sb.AppendLine("        private void FixedUpdate()");
                sb.AppendLine("        {");
                sb.AppendLine("            // Physics update");
                sb.AppendLine("        }");
            }
            sb.AppendLine();
            sb.AppendLine("        private void OnEnable()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Subscribe to events");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void OnDisable()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Unsubscribe from events");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateScriptableObject(string className, string ns, JObject @params)
        {
            string menuName = @params["menuName"]?.ToString() ?? className;

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [CreateAssetMenu(fileName = \"{className}\", menuName = \"Game/{menuName}\")]");
            sb.AppendLine($"    public class {className} : ScriptableObject");
            sb.AppendLine("    {");
            sb.AppendLine("        [Header(\"Configuration\")]");
            sb.AppendLine("        [SerializeField] private string displayName;");
            sb.AppendLine("        [SerializeField] private float value = 1f;");
            sb.AppendLine("        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);");
            sb.AppendLine();
            sb.AppendLine("        public string DisplayName => displayName;");
            sb.AppendLine("        public float Value => value;");
            sb.AppendLine("        public AnimationCurve Curve => curve;");
            sb.AppendLine();
            sb.AppendLine("        public float Evaluate(float t)");
            sb.AppendLine("        {");
            sb.AppendLine("            return curve.Evaluate(t) * value;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateInterface(string className, string ns, JObject @params)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public interface {className}");
            sb.AppendLine("    {");
            sb.AppendLine("        void Initialize();");
            sb.AppendLine("        void Execute();");
            sb.AppendLine("        void Cleanup();");
            sb.AppendLine("        bool IsActive { get; }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateSingleton(string className, string ns, JObject @params)
        {
            bool persistent = @params["persistent"]?.ToObject<bool>() ?? true;

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : MonoBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine($"        private static {className} _instance;");
            sb.AppendLine("        private static readonly object _lock = new object();");
            sb.AppendLine("        private static bool _applicationIsQuitting = false;");
            sb.AppendLine();
            sb.AppendLine($"        public static {className} Instance");
            sb.AppendLine("        {");
            sb.AppendLine("            get");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_applicationIsQuitting)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    Debug.LogWarning(\"[Singleton] Instance of {className} already destroyed.\");");
            sb.AppendLine("                    return null;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                lock (_lock)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (_instance == null)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        _instance = FindFirstObjectByType<{className}>();");
            sb.AppendLine();
            sb.AppendLine("                        if (_instance == null)");
            sb.AppendLine("                        {");
            sb.AppendLine($"                            var singletonObject = new GameObject(\"{className}\");");
            sb.AppendLine($"                            _instance = singletonObject.AddComponent<{className}>();");
            if (persistent)
            {
                sb.AppendLine("                            DontDestroyOnLoad(singletonObject);");
            }
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                    return _instance;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void Awake()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_instance != null && _instance != this)");
            sb.AppendLine("            {");
            sb.AppendLine("                Destroy(gameObject);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            _instance = this;");
            if (persistent)
            {
                sb.AppendLine("            DontDestroyOnLoad(gameObject);");
            }
            sb.AppendLine("            Initialize();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void OnApplicationQuit()");
            sb.AppendLine("        {");
            sb.AppendLine("            _applicationIsQuitting = true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected virtual void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Override for initialization logic");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateStateMachine(string className, string ns, JObject @params)
        {
            var states = @params["states"]?.ToObject<string[]>() ?? new[] { "Idle", "Active", "Disabled" };

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public enum {className}State");
            sb.AppendLine("    {");
            foreach (var state in states)
            {
                sb.AppendLine($"        {state},");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public class {className} : MonoBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine($"        public event Action<{className}State> OnStateChanged;");
            sb.AppendLine();
            sb.AppendLine($"        [SerializeField] private {className}State currentState = {className}State.{states[0]};");
            sb.AppendLine();
            sb.AppendLine($"        public {className}State CurrentState => currentState;");
            sb.AppendLine();
            sb.AppendLine($"        public void SetState({className}State newState)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (currentState == newState) return;");
            sb.AppendLine();
            sb.AppendLine("            ExitState(currentState);");
            sb.AppendLine("            currentState = newState;");
            sb.AppendLine("            EnterState(currentState);");
            sb.AppendLine();
            sb.AppendLine("            OnStateChanged?.Invoke(currentState);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private void EnterState({className}State state)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (state)");
            sb.AppendLine("            {");
            foreach (var state in states)
            {
                sb.AppendLine($"                case {className}State.{state}:");
                sb.AppendLine($"                    Enter{state}();");
                sb.AppendLine("                    break;");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private void ExitState({className}State state)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (state)");
            sb.AppendLine("            {");
            foreach (var state in states)
            {
                sb.AppendLine($"                case {className}State.{state}:");
                sb.AppendLine($"                    Exit{state}();");
                sb.AppendLine("                    break;");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            foreach (var state in states)
            {
                sb.AppendLine($"        protected virtual void Enter{state}() {{ }}");
                sb.AppendLine($"        protected virtual void Exit{state}() {{ }}");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateEventChannel(string className, string ns, JObject @params)
        {
            string eventType = @params["eventType"]?.ToString() ?? "void";
            bool hasPayload = eventType != "void";

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [CreateAssetMenu(fileName = \"{className}\", menuName = \"Events/{className}\")]");
            sb.AppendLine($"    public class {className} : ScriptableObject");
            sb.AppendLine("    {");
            if (hasPayload)
            {
                sb.AppendLine($"        public event Action<{eventType}> OnEventRaised;");
                sb.AppendLine();
                sb.AppendLine($"        public void RaiseEvent({eventType} payload)");
                sb.AppendLine("        {");
                sb.AppendLine("            OnEventRaised?.Invoke(payload);");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine("        public event Action OnEventRaised;");
                sb.AppendLine();
                sb.AppendLine("        public void RaiseEvent()");
                sb.AppendLine("        {");
                sb.AppendLine("            OnEventRaised?.Invoke();");
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
    #endregion

    #region 7. Scene Optimizer Tool
    [McpForUnityTool(
        name: "scene_optimizer",
        Description = "Optimizes scene. Actions: analyze, set_static, combine_meshes, optimize_lights, remove_empty, fix_scale")]
    public static class MCPSceneOptimizer
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "analyze":
                    var analysis = new {
                        totalGameObjects = CountAllGameObjects(),
                        activeMeshRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length,
                        activeSkinnedMeshes = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Length,
                        activeLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length,
                        realtimeLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.lightmapBakeType == LightmapBakeType.Realtime),
                        activeColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length,
                        activeRigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length,
                        particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length,
                        audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,
                        canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,
                        staticObjects = CountStaticObjects(),
                        nonStaticObjects = CountAllGameObjects() - CountStaticObjects(),
                        emptyGameObjects = CountEmptyGameObjects(),
                        negativeScaleObjects = CountNegativeScaleObjects()
                    };

                    var recommendations = new List<string>();
                    if (analysis.realtimeLights > 4)
                        recommendations.Add($"Consider baking {analysis.realtimeLights - 4} realtime lights");
                    if (analysis.nonStaticObjects > analysis.staticObjects && analysis.totalGameObjects > 100)
                        recommendations.Add("Many non-static objects - consider marking static for batching");
                    if (analysis.emptyGameObjects > 10)
                        recommendations.Add($"Found {analysis.emptyGameObjects} empty GameObjects - consider removing");
                    if (analysis.negativeScaleObjects > 0)
                        recommendations.Add($"Found {analysis.negativeScaleObjects} objects with negative scale - may cause issues");

                    return new SuccessResponse("Scene analysis", new { analysis, recommendations });

                case "set_static":
                    string targetName = @params["target"]?.ToString();
                    bool includeChildren = @params["includeChildren"]?.ToObject<bool>() ?? true;
                    int? flagsInt = @params["flags"]?.ToObject<int>();
                    StaticEditorFlags flags = flagsInt.HasValue
                        ? (StaticEditorFlags)flagsInt.Value
                        : (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

                    if (string.IsNullOrEmpty(targetName))
                    {
                        // Set all non-moving objects to static
                        int count = 0;
                        var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                        foreach (var renderer in allRenderers)
                        {
                            if (renderer.GetComponent<Rigidbody>() == null &&
                                renderer.GetComponent<Animator>() == null)
                            {
                                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                                count++;
                            }
                        }
                        return new SuccessResponse($"Set {count} objects to static");
                    }
                    else
                    {
                        var targetGo = GameObject.Find(targetName);
                        if (targetGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                        int count = SetStaticRecursive(targetGo.transform, flags, includeChildren);
                        return new SuccessResponse($"Set {count} objects to static");
                    }

                case "optimize_lights":
                    var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                    int optimized = 0;

                    foreach (var light in lights)
                    {
                        // Set realtime lights to mixed or baked if they don't move
                        if (light.lightmapBakeType == LightmapBakeType.Realtime)
                        {
                            if (light.GetComponent<Animator>() == null)
                            {
                                light.lightmapBakeType = LightmapBakeType.Mixed;
                                EditorUtility.SetDirty(light);
                                optimized++;
                            }
                        }

                        // Reduce shadow resolution on distant/small lights
                        if (light.type == LightType.Point && light.range < 5f)
                        {
                            light.shadows = LightShadows.None;
                            EditorUtility.SetDirty(light);
                        }
                    }

                    return new SuccessResponse($"Optimized {optimized} lights (set to Mixed bake mode)");

                case "remove_empty":
                    int removed = 0;
                    var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                    var toRemove = new List<GameObject>();

                    foreach (var root in rootObjects)
                    {
                        FindEmptyGameObjects(root.transform, toRemove);
                    }

                    foreach (var go in toRemove)
                    {
                        if (go != null && go.transform.childCount == 0)
                        {
                            Undo.DestroyObjectImmediate(go);
                            removed++;
                        }
                    }

                    return new SuccessResponse($"Removed {removed} empty GameObjects");

                case "fix_scale":
                    int fixed_count = 0;
                    var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

                    foreach (var t in allTransforms)
                    {
                        Vector3 scale = t.localScale;
                        bool needsFix = scale.x < 0 || scale.y < 0 || scale.z < 0;

                        if (needsFix)
                        {
                            Undo.RecordObject(t, "Fix Scale");
                            t.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                            fixed_count++;
                        }
                    }

                    return new SuccessResponse($"Fixed {fixed_count} objects with negative scale");

                default:
                    return new SuccessResponse("Scene Optimizer ready. Actions: analyze, set_static, optimize_lights, remove_empty, fix_scale");
            }
        }

        private static int CountAllGameObjects()
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        }

        private static int CountStaticObjects()
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(t => t.gameObject.isStatic);
        }

        private static int CountEmptyGameObjects()
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(t => t.GetComponents<Component>().Length == 1 && t.childCount == 0);
        }

        private static int CountNegativeScaleObjects()
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(t => t.localScale.x < 0 || t.localScale.y < 0 || t.localScale.z < 0);
        }

        private static int SetStaticRecursive(Transform t, StaticEditorFlags flags, bool includeChildren)
        {
            int count = 0;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            count++;

            if (includeChildren)
            {
                foreach (Transform child in t)
                {
                    count += SetStaticRecursive(child, flags, true);
                }
            }

            return count;
        }

        private static void FindEmptyGameObjects(Transform t, List<GameObject> list)
        {
            if (t.GetComponents<Component>().Length == 1 && t.childCount == 0)
            {
                list.Add(t.gameObject);
            }

            foreach (Transform child in t)
            {
                FindEmptyGameObjects(child, list);
            }
        }
    }
    #endregion

    #region 8. Enhanced Input Simulation Tool (Updated)
    [McpForUnityTool(
        name: "input_simulation_enhanced",
        Description = "Enhanced input simulation with InputSystem support. Actions: setup_receiver, send_key, send_axis, send_mouse, get_status")]
    public static class MCPInputSimulationEnhanced
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "setup_receiver":
                    // Check if InputSimulationReceiver exists in scene
                    var receiver = UnityEngine.Object.FindFirstObjectByType<InputSimulationReceiver>();

                    if (receiver == null)
                    {
                        // Create it
                        var receiverGo = new GameObject("InputSimulationReceiver");
                        receiver = receiverGo.AddComponent<InputSimulationReceiver>();
                        Undo.RegisterCreatedObjectUndo(receiverGo, "Create InputSimulationReceiver");

                        return new SuccessResponse("InputSimulationReceiver created in scene. It will persist across scenes and receive simulated inputs.");
                    }

                    return new SuccessResponse("InputSimulationReceiver already exists in scene", new {
                        gameObject = receiver.gameObject.name
                    });

                case "send_key":
                    if (!EditorApplication.isPlaying)
                        return new ErrorResponse("Play mode required for input simulation");

                    string keyName = @params["key"]?.ToString();
                    string keyAction = @params["keyAction"]?.ToString()?.ToLower() ?? "press";

                    if (string.IsNullOrEmpty(keyName))
                        return new ErrorResponse("key parameter required");

                    if (!Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                        return new ErrorResponse($"Invalid key: {keyName}");

                    var keyReceiver = UnityEngine.Object.FindFirstObjectByType<InputSimulationReceiver>();
                    if (keyReceiver == null)
                        return new ErrorResponse("InputSimulationReceiver not found. Use setup_receiver first.");

                    switch (keyAction)
                    {
                        case "down":
                            keyReceiver.OnSimulatedKeyDown(keyCode);
                            break;
                        case "up":
                            keyReceiver.OnSimulatedKeyUp(keyCode);
                            break;
                        case "press":
                        default:
                            keyReceiver.OnSimulatedKeyDown(keyCode);
                            // Schedule key up
                            EditorApplication.delayCall += () => {
                                if (keyReceiver != null)
                                    keyReceiver.OnSimulatedKeyUp(keyCode);
                            };
                            break;
                    }

                    return new SuccessResponse($"Key {keyAction}: {keyName}");

                case "send_axis":
                    if (!EditorApplication.isPlaying)
                        return new ErrorResponse("Play mode required for input simulation");

                    string axisName = @params["axis"]?.ToString();
                    float axisValue = @params["value"]?.ToObject<float>() ?? 0f;

                    if (string.IsNullOrEmpty(axisName))
                        return new ErrorResponse("axis parameter required");

                    var axisReceiver = UnityEngine.Object.FindFirstObjectByType<InputSimulationReceiver>();
                    if (axisReceiver == null)
                        return new ErrorResponse("InputSimulationReceiver not found. Use setup_receiver first.");

                    axisReceiver.OnSimulatedAxis(new object[] { axisName, axisValue });
                    return new SuccessResponse($"Axis {axisName} set to {axisValue}");

                case "send_mouse":
                    if (!EditorApplication.isPlaying)
                        return new ErrorResponse("Play mode required for input simulation");

                    int mouseButton = @params["button"]?.ToObject<int>() ?? 0;
                    var mousePos = @params["position"]?.ToObject<float[]>();

                    var mouseReceiver = UnityEngine.Object.FindFirstObjectByType<InputSimulationReceiver>();
                    if (mouseReceiver == null)
                        return new ErrorResponse("InputSimulationReceiver not found. Use setup_receiver first.");

                    if (mousePos != null && mousePos.Length >= 2)
                    {
                        mouseReceiver.OnSimulatedMouseMove(new Vector2(mousePos[0], mousePos[1]));
                    }

                    mouseReceiver.OnSimulatedMouseClick(mouseButton);
                    return new SuccessResponse($"Mouse click button {mouseButton}");

                case "get_status":
                    var statusReceiver = UnityEngine.Object.FindFirstObjectByType<InputSimulationReceiver>();

                    return new SuccessResponse("Input simulation status", new {
                        receiverExists = statusReceiver != null,
                        isPlayMode = EditorApplication.isPlaying,
                        ready = statusReceiver != null && EditorApplication.isPlaying
                    });

                default:
                    return new SuccessResponse("Enhanced Input Simulation ready. Actions: setup_receiver, send_key, send_axis, send_mouse, get_status");
            }
        }
    }
    #endregion

    #region 9. LOD Group Control Tool
    [McpForUnityTool(
        name: "lod_control",
        Description = "Controls LOD Groups. Actions: list, create, get_settings, set_settings, calculate_bounds")]
    public static class MCPLODControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string targetName = @params["target"]?.ToString();

            switch (action)
            {
                case "list":
                    var lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
                    var lodList = lodGroups.Select(lg => new {
                        name = lg.gameObject.name,
                        lodCount = lg.lodCount,
                        size = lg.size,
                        fadeMode = lg.fadeMode.ToString()
                    }).ToList();
                    return new SuccessResponse($"Found {lodList.Count} LOD Groups", new { lodGroups = lodList });

                case "create":
                    var targetGo = GameObject.Find(targetName);
                    if (targetGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var existingLOD = targetGo.GetComponent<LODGroup>();
                    if (existingLOD != null) return new ErrorResponse($"LODGroup already exists on '{targetName}'");

                    var lodGroup = targetGo.AddComponent<LODGroup>();

                    // Get all mesh renderers in children
                    var renderers = targetGo.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length == 0) return new ErrorResponse("No MeshRenderers found in children");

                    // Create default LOD setup
                    var lods = new LOD[] {
                        new LOD(0.5f, renderers),
                        new LOD(0.2f, new Renderer[0]),
                        new LOD(0.01f, new Renderer[0])
                    };
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();

                    EditorUtility.SetDirty(lodGroup);
                    return new SuccessResponse($"Created LODGroup on '{targetName}' with {renderers.Length} renderers");

                case "get_settings":
                    var getGo = GameObject.Find(targetName);
                    if (getGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var getLOD = getGo.GetComponent<LODGroup>();
                    if (getLOD == null) return new ErrorResponse($"No LODGroup on '{targetName}'");

                    var lodsInfo = new List<object>();
                    var currentLODs = getLOD.GetLODs();
                    for (int i = 0; i < currentLODs.Length; i++)
                    {
                        lodsInfo.Add(new {
                            index = i,
                            screenRelativeTransitionHeight = currentLODs[i].screenRelativeTransitionHeight,
                            rendererCount = currentLODs[i].renderers?.Length ?? 0
                        });
                    }

                    return new SuccessResponse("LODGroup settings", new {
                        lodCount = getLOD.lodCount,
                        size = getLOD.size,
                        fadeMode = getLOD.fadeMode.ToString(),
                        animateCrossFading = getLOD.animateCrossFading,
                        lods = lodsInfo
                    });

                case "set_settings":
                    var setGo = GameObject.Find(targetName);
                    if (setGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var setLOD = setGo.GetComponent<LODGroup>();
                    if (setLOD == null) return new ErrorResponse($"No LODGroup on '{targetName}'");

                    if (@params["fadeMode"] != null)
                    {
                        if (Enum.TryParse<LODFadeMode>(@params["fadeMode"].ToString(), true, out var mode))
                            setLOD.fadeMode = mode;
                    }

                    if (@params["animateCrossFading"] != null)
                        setLOD.animateCrossFading = @params["animateCrossFading"].ToObject<bool>();

                    // Update LOD thresholds
                    var thresholds = @params["thresholds"]?.ToObject<float[]>();
                    if (thresholds != null)
                    {
                        var existingLODs = setLOD.GetLODs();
                        for (int i = 0; i < Mathf.Min(thresholds.Length, existingLODs.Length); i++)
                        {
                            existingLODs[i].screenRelativeTransitionHeight = thresholds[i];
                        }
                        setLOD.SetLODs(existingLODs);
                    }

                    EditorUtility.SetDirty(setLOD);
                    return new SuccessResponse($"Updated LODGroup on '{targetName}'");

                case "calculate_bounds":
                    var boundsGo = GameObject.Find(targetName);
                    if (boundsGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    var boundsLOD = boundsGo.GetComponent<LODGroup>();
                    if (boundsLOD == null) return new ErrorResponse($"No LODGroup on '{targetName}'");

                    boundsLOD.RecalculateBounds();
                    EditorUtility.SetDirty(boundsLOD);
                    return new SuccessResponse($"Recalculated bounds for '{targetName}'");

                default:
                    return new SuccessResponse("LOD Control ready. Actions: list, create, get_settings, set_settings, calculate_bounds");
            }
        }
    }
    #endregion

    #region 10. Occlusion Culling Tool
    [McpForUnityTool(
        name: "occlusion_culling",
        Description = "Controls Occlusion Culling. Actions: get_settings, set_settings, bake, clear, visualize")]
    public static class MCPOcclusionCulling
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_settings":
                    return new SuccessResponse("Occlusion Culling settings", new {
                        smallestOccluder = StaticOcclusionCulling.smallestOccluder,
                        smallestHole = StaticOcclusionCulling.smallestHole,
                        backfaceThreshold = StaticOcclusionCulling.backfaceThreshold,
                        isRunning = StaticOcclusionCulling.isRunning
                    });

                case "set_settings":
                    if (@params["smallestOccluder"] != null)
                        StaticOcclusionCulling.smallestOccluder = @params["smallestOccluder"].ToObject<float>();
                    if (@params["smallestHole"] != null)
                        StaticOcclusionCulling.smallestHole = @params["smallestHole"].ToObject<float>();
                    if (@params["backfaceThreshold"] != null)
                        StaticOcclusionCulling.backfaceThreshold = @params["backfaceThreshold"].ToObject<float>();

                    return new SuccessResponse("Occlusion settings updated");

                case "bake":
                    // Set objects as Occluder/Occludee static first
                    var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                    int prepared = 0;
                    foreach (var renderer in renderers)
                    {
                        var currentFlags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                        if ((currentFlags & StaticEditorFlags.OccluderStatic) == 0 ||
                            (currentFlags & StaticEditorFlags.OccludeeStatic) == 0)
                        {
                            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                                currentFlags | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                            prepared++;
                        }
                    }

                    StaticOcclusionCulling.Compute();
                    return new SuccessResponse($"Occlusion culling bake started. Prepared {prepared} objects.");

                case "clear":
                    StaticOcclusionCulling.Clear();
                    return new SuccessResponse("Occlusion culling data cleared");

                case "visualize":
                    bool enable = @params["enable"]?.ToObject<bool>() ?? true;
                    StaticOcclusionCullingVisualization.showOcclusionCulling = enable;
                    return new SuccessResponse($"Occlusion visualization {(enable ? "enabled" : "disabled")}");

                default:
                    return new SuccessResponse("Occlusion Culling ready. Actions: get_settings, set_settings, bake, clear, visualize");
            }
        }
    }
    #endregion
}
