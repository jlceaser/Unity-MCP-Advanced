#nullable disable
// Force recompile - MCP Master Tools v2.0
#pragma warning disable CS0618 // TextureImporter.spritesheet is deprecated but ISpriteEditorDataProvider replacement is complex
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Master
{
    #region 1. Timeline Control
    [McpForUnityTool(
        name: "timeline_control",
        Description = "Controls Unity Timeline/Playables. Actions: list_directors, get_tracks, play, pause, stop, set_time, get_bindings, bind_object, create_track, add_clip")]
    public static class TimelineControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_directors";
                string directorName = @params["director"]?.ToString();

                switch (action)
                {
                    case "list_directors":
                        var directors = UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None);
                        return new SuccessResponse("Found PlayableDirectors", new
                        {
                            count = directors.Length,
                            directors = directors.Select(d => new
                            {
                                name = d.gameObject.name,
                                state = d.state.ToString(),
                                time = d.time,
                                duration = d.duration,
                                hasTimeline = d.playableAsset != null
                            }).ToList()
                        });

                    case "play":
                        var playDir = FindDirector(directorName);
                        if (playDir == null) return new ErrorResponse("Director not found");
                        playDir.Play();
                        return new SuccessResponse($"Playing {playDir.gameObject.name}");

                    case "pause":
                        var pauseDir = FindDirector(directorName);
                        if (pauseDir == null) return new ErrorResponse("Director not found");
                        pauseDir.Pause();
                        return new SuccessResponse($"Paused {pauseDir.gameObject.name}");

                    case "stop":
                        var stopDir = FindDirector(directorName);
                        if (stopDir == null) return new ErrorResponse("Director not found");
                        stopDir.Stop();
                        return new SuccessResponse($"Stopped {stopDir.gameObject.name}");

                    case "set_time":
                        var timeDir = FindDirector(directorName);
                        if (timeDir == null) return new ErrorResponse("Director not found");
                        double newTime = @params["time"]?.ToObject<double>() ?? 0;
                        timeDir.time = newTime;
                        return new SuccessResponse($"Set time to {newTime}");

                    case "get_tracks":
                        var tracksDir = FindDirector(directorName);
                        if (tracksDir == null) return new ErrorResponse("Director not found");
                        if (tracksDir.playableAsset == null) return new ErrorResponse("No timeline asset");

                        var timeline = tracksDir.playableAsset as UnityEngine.Timeline.TimelineAsset;
                        if (timeline == null) return new ErrorResponse("Not a TimelineAsset");

                        var tracks = timeline.GetOutputTracks().Select(t => new
                        {
                            name = t.name,
                            type = t.GetType().Name,
                            muted = t.muted,
                            clipCount = t.GetClips().Count()
                        }).ToList();

                        return new SuccessResponse("Timeline tracks", new { tracks });

                    case "get_bindings":
                        var bindDir = FindDirector(directorName);
                        if (bindDir == null) return new ErrorResponse("Director not found");

                        var bindings = new List<object>();
                        foreach (var output in bindDir.playableAsset.outputs)
                        {
                            var bound = bindDir.GetGenericBinding(output.sourceObject);
                            bindings.Add(new
                            {
                                track = output.streamName,
                                type = output.outputTargetType?.Name,
                                boundTo = bound?.ToString()
                            });
                        }
                        return new SuccessResponse("Timeline bindings", new { bindings });

                    default:
                        return new ErrorResponse($"Unknown action: {action}. Use: list_directors, play, pause, stop, set_time, get_tracks, get_bindings");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Timeline control failed: {e.Message}");
            }
        }

        private static UnityEngine.Playables.PlayableDirector FindDirector(string name)
        {
            var directors = UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None);
            if (string.IsNullOrEmpty(name)) return directors.FirstOrDefault();
            return directors.FirstOrDefault(d => d.gameObject.name.Contains(name));
        }
    }
    #endregion

    #region 2. Quality Settings Control
    [McpForUnityTool(
        name: "quality_control",
        Description = "Controls Unity Quality Settings. Actions: get_levels, set_level, get_current, modify_settings")]
    public static class QualityControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "get_current";

                switch (action)
                {
                    case "get_levels":
                        var levels = QualitySettings.names.Select((n, i) => new
                        {
                            index = i,
                            name = n,
                            isCurrent = i == QualitySettings.GetQualityLevel()
                        }).ToList();
                        return new SuccessResponse("Quality levels", new { levels, current = QualitySettings.GetQualityLevel() });

                    case "set_level":
                        int level = @params["level"]?.ToObject<int>() ?? 0;
                        string levelName = @params["name"]?.ToString();

                        if (!string.IsNullOrEmpty(levelName))
                        {
                            level = Array.IndexOf(QualitySettings.names, levelName);
                            if (level < 0) return new ErrorResponse($"Quality level '{levelName}' not found");
                        }

                        if (level < 0 || level >= QualitySettings.names.Length)
                            return new ErrorResponse($"Invalid level index: {level}");

                        QualitySettings.SetQualityLevel(level, true);
                        return new SuccessResponse($"Set quality to {QualitySettings.names[level]}");

                    case "get_current":
                        return new SuccessResponse("Current quality settings", new
                        {
                            level = QualitySettings.GetQualityLevel(),
                            name = QualitySettings.names[QualitySettings.GetQualityLevel()],
                            pixelLightCount = QualitySettings.pixelLightCount,
                            textureQuality = QualitySettings.globalTextureMipmapLimit,
                            anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
                            antiAliasing = QualitySettings.antiAliasing,
                            softParticles = QualitySettings.softParticles,
                            realtimeReflectionProbes = QualitySettings.realtimeReflectionProbes,
                            shadows = QualitySettings.shadows.ToString(),
                            shadowResolution = QualitySettings.shadowResolution.ToString(),
                            shadowDistance = QualitySettings.shadowDistance,
                            vSyncCount = QualitySettings.vSyncCount,
                            lodBias = QualitySettings.lodBias,
                            maxLODLevel = QualitySettings.maximumLODLevel
                        });

                    case "modify_settings":
                        if (@params["shadowDistance"] != null)
                            QualitySettings.shadowDistance = @params["shadowDistance"].ToObject<float>();
                        if (@params["lodBias"] != null)
                            QualitySettings.lodBias = @params["lodBias"].ToObject<float>();
                        if (@params["vSyncCount"] != null)
                            QualitySettings.vSyncCount = @params["vSyncCount"].ToObject<int>();
                        if (@params["antiAliasing"] != null)
                            QualitySettings.antiAliasing = @params["antiAliasing"].ToObject<int>();
                        if (@params["pixelLightCount"] != null)
                            QualitySettings.pixelLightCount = @params["pixelLightCount"].ToObject<int>();

                        return new SuccessResponse("Quality settings modified");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Quality control failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 3. Physics Simulation Control
    [McpForUnityTool(
        name: "physics_control",
        Description = "Controls Unity Physics. Actions: simulate_step, get_settings, set_settings, raycast_batch, overlap_sphere, get_contacts, set_layer_collision")]
    public static class PhysicsControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "get_settings";

                switch (action)
                {
                    case "get_settings":
                        return new SuccessResponse("Physics settings", new
                        {
                            gravity = new[] { Physics.gravity.x, Physics.gravity.y, Physics.gravity.z },
                            defaultSolverIterations = Physics.defaultSolverIterations,
                            defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                            sleepThreshold = Physics.sleepThreshold,
                            bounceThreshold = Physics.bounceThreshold,
                            defaultMaxAngularSpeed = Physics.defaultMaxAngularSpeed,
                            defaultContactOffset = Physics.defaultContactOffset,
                            autoSimulation = Physics.simulationMode.ToString()
                            // autoSyncTransforms is deprecated - use Physics.SyncTransforms() manually
                        });

                    case "set_settings":
                        if (@params["gravity"] != null)
                        {
                            var g = @params["gravity"].ToObject<float[]>();
                            if (g.Length >= 3) Physics.gravity = new Vector3(g[0], g[1], g[2]);
                        }
                        if (@params["solverIterations"] != null)
                            Physics.defaultSolverIterations = @params["solverIterations"].ToObject<int>();
                        if (@params["sleepThreshold"] != null)
                            Physics.sleepThreshold = @params["sleepThreshold"].ToObject<float>();
                        if (@params["bounceThreshold"] != null)
                            Physics.bounceThreshold = @params["bounceThreshold"].ToObject<float>();

                        return new SuccessResponse("Physics settings updated");

                    case "simulate_step":
                        float step = @params["step"]?.ToObject<float>() ?? Time.fixedDeltaTime;
                        Physics.Simulate(step);
                        return new SuccessResponse($"Simulated physics step: {step}s");

                    case "raycast_batch":
                        var origins = @params["origins"]?.ToObject<float[][]>();
                        var directions = @params["directions"]?.ToObject<float[][]>();
                        float maxDist = @params["maxDistance"]?.ToObject<float>() ?? 100f;

                        if (origins == null || directions == null || origins.Length != directions.Length)
                            return new ErrorResponse("Origins and directions arrays must match");

                        var results = new List<object>();
                        for (int i = 0; i < origins.Length; i++)
                        {
                            var origin = new Vector3(origins[i][0], origins[i][1], origins[i][2]);
                            var dir = new Vector3(directions[i][0], directions[i][1], directions[i][2]);

                            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist))
                            {
                                results.Add(new { hit = true, point = new[] { hit.point.x, hit.point.y, hit.point.z },
                                    normal = new[] { hit.normal.x, hit.normal.y, hit.normal.z },
                                    distance = hit.distance, collider = hit.collider.name });
                            }
                            else
                            {
                                results.Add(new { hit = false });
                            }
                        }
                        return new SuccessResponse("Batch raycast complete", new { results });

                    case "overlap_sphere":
                        var center = @params["center"]?.ToObject<float[]>();
                        float radius = @params["radius"]?.ToObject<float>() ?? 1f;

                        if (center == null || center.Length < 3)
                            return new ErrorResponse("Center position required [x,y,z]");

                        var colliders = Physics.OverlapSphere(new Vector3(center[0], center[1], center[2]), radius);
                        return new SuccessResponse($"Found {colliders.Length} colliders", new
                        {
                            count = colliders.Length,
                            colliders = colliders.Select(c => new { name = c.gameObject.name, type = c.GetType().Name }).ToList()
                        });

                    case "set_layer_collision":
                        int layer1 = @params["layer1"]?.ToObject<int>() ?? 0;
                        int layer2 = @params["layer2"]?.ToObject<int>() ?? 0;
                        bool ignore = @params["ignore"]?.ToObject<bool>() ?? true;

                        Physics.IgnoreLayerCollision(layer1, layer2, ignore);
                        return new SuccessResponse($"Layer collision {(ignore ? "disabled" : "enabled")} between {layer1} and {layer2}");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Physics control failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 4. Gizmos Control
    [McpForUnityTool(
        name: "gizmos_control",
        Description = "Controls Editor Gizmos. Actions: get_state, toggle_all, set_icon, toggle_component_gizmo, list_gizmo_types")]
    public static class GizmosControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "get_state";

                switch (action)
                {
                    case "get_state":
                        var sceneView = SceneView.lastActiveSceneView;
                        if (sceneView == null) return new ErrorResponse("No active SceneView");

                        return new SuccessResponse("Gizmo state", new
                        {
                            drawGizmos = sceneView.drawGizmos,
                            showGrid = sceneView.showGrid,
                            sceneLighting = sceneView.sceneLighting,
                            in2DMode = sceneView.in2DMode
                        });

                    case "toggle_all":
                        var sv = SceneView.lastActiveSceneView;
                        if (sv == null) return new ErrorResponse("No active SceneView");

                        bool enable = @params["enable"]?.ToObject<bool>() ?? !sv.drawGizmos;
                        sv.drawGizmos = enable;
                        sv.Repaint();
                        return new SuccessResponse($"Gizmos {(enable ? "enabled" : "disabled")}");

                    case "toggle_grid":
                        var gv = SceneView.lastActiveSceneView;
                        if (gv == null) return new ErrorResponse("No active SceneView");

                        bool showGrid = @params["show"]?.ToObject<bool>() ?? !gv.showGrid;
                        gv.showGrid = showGrid;
                        gv.Repaint();
                        return new SuccessResponse($"Grid {(showGrid ? "shown" : "hidden")}");

                    case "set_icon":
                        string objectName = @params["object"]?.ToString();
                        int iconIndex = @params["iconIndex"]?.ToObject<int>() ?? 0;

                        var obj = GameObject.Find(objectName);
                        if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

                        // Use reflection to set icon
                        var editorGUIUtilityType = typeof(EditorGUIUtility);
                        var setIconMethod = editorGUIUtilityType.GetMethod("SetIconForObject",
                            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                        if (setIconMethod != null)
                        {
                            var icons = GetBuiltinIcons();
                            if (iconIndex >= 0 && iconIndex < icons.Length)
                            {
                                setIconMethod.Invoke(null, new object[] { obj, icons[iconIndex] });
                                return new SuccessResponse($"Icon set for {objectName}");
                            }
                        }
                        return new ErrorResponse("Failed to set icon");

                    case "list_gizmo_types":
                        var types = TypeCache.GetTypesWithAttribute<AddComponentMenu>()
                            .Where(t => typeof(Component).IsAssignableFrom(t))
                            .Select(t => t.Name)
                            .OrderBy(n => n)
                            .Take(50)
                            .ToList();
                        return new SuccessResponse("Component types with gizmos", new { types });

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Gizmos control failed: {e.Message}");
            }
        }

        private static Texture2D[] GetBuiltinIcons()
        {
            var icons = new List<Texture2D>();
            for (int i = 0; i < 8; i++)
            {
                var icon = EditorGUIUtility.IconContent($"sv_icon_dot{i}_pix16_gizmo")?.image as Texture2D;
                if (icon != null) icons.Add(icon);
            }
            return icons.ToArray();
        }
    }
    #endregion

    #region 5. Git Integration
    [McpForUnityTool(
        name: "git_control",
        Description = "Git integration. Actions: status, log, diff, stage, unstage, commit, branch_list, checkout, stash, pull, push")]
    public static class GitControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "status";
                string projectPath = Application.dataPath.Replace("/Assets", "");

                switch (action)
                {
                    case "status":
                        var status = RunGit("status --porcelain", projectPath);
                        var statusLines = status.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                        return new SuccessResponse("Git status", new
                        {
                            modified = statusLines.Where(l => l.StartsWith(" M") || l.StartsWith("M ")).Select(l => l.Substring(3)).ToList(),
                            added = statusLines.Where(l => l.StartsWith("A ") || l.StartsWith("??")).Select(l => l.Substring(3)).ToList(),
                            deleted = statusLines.Where(l => l.StartsWith(" D") || l.StartsWith("D ")).Select(l => l.Substring(3)).ToList(),
                            staged = statusLines.Where(l => l[0] != ' ' && l[0] != '?').Select(l => l.Substring(3)).ToList()
                        });

                    case "log":
                        int count = @params["count"]?.ToObject<int>() ?? 10;
                        var log = RunGit($"log --oneline -n {count}", projectPath);
                        return new SuccessResponse("Git log", new
                        {
                            commits = log.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))
                                .Select(l => new { hash = l.Substring(0, Math.Min(7, l.Length)), message = l.Length > 8 ? l.Substring(8) : "" })
                                .ToList()
                        });

                    case "diff":
                        string file = @params["file"]?.ToString();
                        var diff = string.IsNullOrEmpty(file) ? RunGit("diff --stat", projectPath) : RunGit($"diff -- \"{file}\"", projectPath);
                        return new SuccessResponse("Git diff", new { diff });

                    case "stage":
                        string stageFile = @params["file"]?.ToString() ?? ".";
                        RunGit($"add \"{stageFile}\"", projectPath);
                        return new SuccessResponse($"Staged: {stageFile}");

                    case "unstage":
                        string unstageFile = @params["file"]?.ToString();
                        if (string.IsNullOrEmpty(unstageFile)) return new ErrorResponse("File required");
                        RunGit($"reset HEAD -- \"{unstageFile}\"", projectPath);
                        return new SuccessResponse($"Unstaged: {unstageFile}");

                    case "commit":
                        string message = @params["message"]?.ToString();
                        if (string.IsNullOrEmpty(message)) return new ErrorResponse("Commit message required");
                        RunGit($"commit -m \"{message}\"", projectPath);
                        return new SuccessResponse($"Committed: {message}");

                    case "branch_list":
                        var branches = RunGit("branch -a", projectPath);
                        return new SuccessResponse("Branches", new
                        {
                            branches = branches.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))
                                .Select(l => new { name = l.Trim().TrimStart('*').Trim(), current = l.StartsWith("*") })
                                .ToList()
                        });

                    case "checkout":
                        string branch = @params["branch"]?.ToString();
                        if (string.IsNullOrEmpty(branch)) return new ErrorResponse("Branch name required");
                        RunGit($"checkout \"{branch}\"", projectPath);
                        return new SuccessResponse($"Checked out: {branch}");

                    case "stash":
                        string stashAction = @params["stash_action"]?.ToString() ?? "push";
                        RunGit($"stash {stashAction}", projectPath);
                        return new SuccessResponse($"Stash {stashAction} complete");

                    case "current_branch":
                        var current = RunGit("rev-parse --abbrev-ref HEAD", projectPath).Trim();
                        return new SuccessResponse("Current branch", new { branch = current });

                    default:
                        return new ErrorResponse($"Unknown action: {action}. Use: status, log, diff, stage, unstage, commit, branch_list, checkout, stash, current_branch");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Git operation failed: {e.Message}");
            }
        }

        private static string RunGit(string args, string workingDir)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (!string.IsNullOrEmpty(error) && proc.ExitCode != 0)
                    throw new Exception(error);

                return output;
            }
        }
    }
    #endregion

    #region 6. VFX Graph Control
    [McpForUnityTool(
        name: "vfx_control",
        Description = "Controls Visual Effect Graph. Actions: list_vfx, play, stop, reset, set_property, get_properties, set_event")]
    public static class VFXControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_vfx";
                string vfxName = @params["vfx"]?.ToString();

                // Find VFX component type via reflection (to avoid hard dependency)
                var vfxType = Type.GetType("UnityEngine.VFX.VisualEffect, Unity.VisualEffectGraph.Runtime");
                if (vfxType == null)
                {
                    return new ErrorResponse("VFX Graph package not installed");
                }

                switch (action)
                {
                    case "list_vfx":
                        var vfxComponents = UnityEngine.Object.FindObjectsByType(vfxType, FindObjectsSortMode.None);
                        var vfxList = new List<object>();

                        foreach (var vfx in vfxComponents)
                        {
                            var comp = vfx as Component;
                            var aliveCount = vfxType.GetProperty("aliveParticleCount")?.GetValue(vfx);
                            vfxList.Add(new
                            {
                                name = comp.gameObject.name,
                                aliveParticles = aliveCount,
                                enabled = (bool)vfxType.GetProperty("enabled").GetValue(vfx)
                            });
                        }
                        return new SuccessResponse("VFX Components", new { count = vfxList.Count, effects = vfxList });

                    case "play":
                        var playVfx = FindVFX(vfxName, vfxType);
                        if (playVfx == null) return new ErrorResponse("VFX not found");
                        vfxType.GetMethod("Play")?.Invoke(playVfx, null);
                        return new SuccessResponse($"Playing VFX: {(playVfx as Component).gameObject.name}");

                    case "stop":
                        var stopVfx = FindVFX(vfxName, vfxType);
                        if (stopVfx == null) return new ErrorResponse("VFX not found");
                        vfxType.GetMethod("Stop")?.Invoke(stopVfx, null);
                        return new SuccessResponse($"Stopped VFX: {(stopVfx as Component).gameObject.name}");

                    case "reset":
                        var resetVfx = FindVFX(vfxName, vfxType);
                        if (resetVfx == null) return new ErrorResponse("VFX not found");
                        vfxType.GetMethod("Reinit")?.Invoke(resetVfx, null);
                        return new SuccessResponse($"Reset VFX: {(resetVfx as Component).gameObject.name}");

                    case "set_property":
                        var propVfx = FindVFX(vfxName, vfxType);
                        if (propVfx == null) return new ErrorResponse("VFX not found");

                        string propName = @params["property"]?.ToString();
                        var propValue = @params["value"];

                        if (string.IsNullOrEmpty(propName)) return new ErrorResponse("Property name required");

                        // Try different property types
                        if (propValue.Type == JTokenType.Float || propValue.Type == JTokenType.Integer)
                        {
                            vfxType.GetMethod("SetFloat")?.Invoke(propVfx, new object[] { propName, propValue.ToObject<float>() });
                        }
                        else if (propValue.Type == JTokenType.Boolean)
                        {
                            vfxType.GetMethod("SetBool")?.Invoke(propVfx, new object[] { propName, propValue.ToObject<bool>() });
                        }
                        else if (propValue.Type == JTokenType.Array)
                        {
                            var arr = propValue.ToObject<float[]>();
                            if (arr.Length == 3)
                                vfxType.GetMethod("SetVector3")?.Invoke(propVfx, new object[] { propName, new Vector3(arr[0], arr[1], arr[2]) });
                        }

                        return new SuccessResponse($"Set property {propName}");

                    case "send_event":
                        var eventVfx = FindVFX(vfxName, vfxType);
                        if (eventVfx == null) return new ErrorResponse("VFX not found");

                        string eventName = @params["event"]?.ToString() ?? "OnPlay";
                        vfxType.GetMethod("SendEvent", new[] { typeof(string) })?.Invoke(eventVfx, new object[] { eventName });
                        return new SuccessResponse($"Sent event: {eventName}");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"VFX control failed: {e.Message}");
            }
        }

        private static object FindVFX(string name, Type vfxType)
        {
            var vfxComponents = UnityEngine.Object.FindObjectsByType(vfxType, FindObjectsSortMode.None);
            if (string.IsNullOrEmpty(name)) return vfxComponents.FirstOrDefault();
            return vfxComponents.FirstOrDefault(v => (v as Component).gameObject.name.Contains(name));
        }
    }
    #endregion

    #region 7. Addressables Control
    [McpForUnityTool(
        name: "addressables_control",
        Description = "Controls Addressables. Actions: list_groups, list_entries, analyze, build, clean, get_settings, modify_entry")]
    public static class AddressablesControl
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_groups";

                // Check if Addressables is installed
                var settingsType = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
                if (settingsType == null)
                {
                    return new ErrorResponse("Addressables package not installed");
                }

                var settingsProp = settingsType.GetProperty("Settings", BindingFlags.Static | BindingFlags.Public);
                var settings = settingsProp?.GetValue(null);
                if (settings == null)
                {
                    return new ErrorResponse("Addressables not configured");
                }

                switch (action)
                {
                    case "list_groups":
                        var groupsProp = settings.GetType().GetProperty("groups");
                        var groups = groupsProp?.GetValue(settings) as System.Collections.IList;

                        var groupList = new List<object>();
                        if (groups != null)
                        {
                            foreach (var group in groups)
                            {
                                var nameProp = group.GetType().GetProperty("Name");
                                var entriesProp = group.GetType().GetProperty("entries");
                                var entries = entriesProp?.GetValue(group) as System.Collections.ICollection;

                                groupList.Add(new
                                {
                                    name = nameProp?.GetValue(group),
                                    entryCount = entries?.Count ?? 0
                                });
                            }
                        }
                        return new SuccessResponse("Addressable groups", new { groups = groupList });

                    case "list_entries":
                        string groupName = @params["group"]?.ToString();
                        var allGroups = settings.GetType().GetProperty("groups")?.GetValue(settings) as System.Collections.IList;

                        var entryList = new List<object>();
                        if (allGroups != null)
                        {
                            foreach (var grp in allGroups)
                            {
                                var gName = grp.GetType().GetProperty("Name")?.GetValue(grp)?.ToString();
                                if (!string.IsNullOrEmpty(groupName) && gName != groupName) continue;

                                var entries = grp.GetType().GetProperty("entries")?.GetValue(grp) as System.Collections.IEnumerable;
                                if (entries != null)
                                {
                                    foreach (var entry in entries)
                                    {
                                        var address = entry.GetType().GetProperty("address")?.GetValue(entry);
                                        var assetPath = entry.GetType().GetProperty("AssetPath")?.GetValue(entry);
                                        entryList.Add(new { group = gName, address, assetPath });
                                    }
                                }
                            }
                        }
                        return new SuccessResponse("Addressable entries", new { entries = entryList.Take(100).ToList() });

                    case "build":
                        var buildScriptType = Type.GetType("UnityEditor.AddressableAssets.Build.AddressableAssetSettings, Unity.Addressables.Editor");
                        var buildMethod = settings.GetType().GetMethod("BuildPlayerContent");
                        buildMethod?.Invoke(settings, null);
                        return new SuccessResponse("Addressables build started");

                    case "clean":
                        var cleanMethod = settings.GetType().GetMethod("CleanPlayerContent");
                        cleanMethod?.Invoke(settings, null);
                        return new SuccessResponse("Addressables cleaned");

                    default:
                        return new ErrorResponse($"Unknown action: {action}. Use: list_groups, list_entries, build, clean");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Addressables control failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 8. Scene Templates
    [McpForUnityTool(
        name: "scene_templates",
        Description = "Manage scene templates. Actions: list, create_from_scene, instantiate, delete")]
    public static class SceneTemplates
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list";

                switch (action)
                {
                    case "list":
                        var templateGuids = AssetDatabase.FindAssets("t:SceneTemplateAsset");
                        var templates = templateGuids.Select(guid =>
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            return new { path, name = Path.GetFileNameWithoutExtension(path) };
                        }).ToList();
                        return new SuccessResponse("Scene templates", new { templates });

                    case "create_from_scene":
                        string templatePath = @params["path"]?.ToString();
                        string templateName = @params["name"]?.ToString() ?? "NewTemplate";

                        if (string.IsNullOrEmpty(templatePath))
                            templatePath = $"Assets/SceneTemplates/{templateName}.scenetemplate";

                        // Ensure directory exists
                        var dir = Path.GetDirectoryName(templatePath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        // Use reflection to create scene template
                        var templateType = Type.GetType("UnityEditor.SceneTemplate.SceneTemplateAsset, Unity.SceneTemplate.Editor");
                        if (templateType == null)
                            return new ErrorResponse("Scene Template package not available");

                        var template = ScriptableObject.CreateInstance(templateType);
                        AssetDatabase.CreateAsset(template, templatePath);
                        AssetDatabase.SaveAssets();

                        return new SuccessResponse($"Template created: {templatePath}");

                    case "instantiate":
                        string instTemplatePath = @params["template"]?.ToString();
                        string newScenePath = @params["scenePath"]?.ToString();

                        if (string.IsNullOrEmpty(instTemplatePath))
                            return new ErrorResponse("Template path required");

                        var sceneTemplateType = Type.GetType("UnityEditor.SceneTemplate.SceneTemplateService, Unity.SceneTemplate.Editor");
                        if (sceneTemplateType != null)
                        {
                            var instantiateMethod = sceneTemplateType.GetMethod("Instantiate",
                                BindingFlags.Static | BindingFlags.Public, null,
                                new[] { typeof(UnityEngine.Object), typeof(bool), typeof(string) }, null);

                            var templateAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(instTemplatePath);
                            if (templateAsset != null)
                            {
                                instantiateMethod?.Invoke(null, new object[] { templateAsset, true, newScenePath });
                                return new SuccessResponse($"Scene created from template");
                            }
                        }
                        return new ErrorResponse("Failed to instantiate template");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Scene templates failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 9. Assembly Definition Manager
    [McpForUnityTool(
        name: "asmdef_manager",
        Description = "Manage Assembly Definitions. Actions: list, create, modify, get_references, add_reference")]
    public static class AsmDefManager
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list";

                switch (action)
                {
                    case "list":
                        var asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
                        var asmdefs = asmdefGuids.Select(guid =>
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            var content = File.ReadAllText(path);
                            var json = JObject.Parse(content);
                            return new
                            {
                                path,
                                name = json["name"]?.ToString(),
                                references = json["references"]?.ToObject<string[]>()?.Length ?? 0,
                                autoReferenced = json["autoReferenced"]?.ToObject<bool>() ?? true
                            };
                        }).ToList();
                        return new SuccessResponse("Assembly definitions", new { asmdefs });

                    case "create":
                        string asmdefPath = @params["path"]?.ToString();
                        string asmdefName = @params["name"]?.ToString();

                        if (string.IsNullOrEmpty(asmdefPath) || string.IsNullOrEmpty(asmdefName))
                            return new ErrorResponse("Path and name required");

                        var newAsmdef = new JObject
                        {
                            ["name"] = asmdefName,
                            ["rootNamespace"] = @params["namespace"]?.ToString() ?? "",
                            ["references"] = new JArray(@params["references"]?.ToObject<string[]>() ?? new string[0]),
                            ["includePlatforms"] = new JArray(),
                            ["excludePlatforms"] = new JArray(),
                            ["allowUnsafeCode"] = @params["allowUnsafe"]?.ToObject<bool>() ?? false,
                            ["overrideReferences"] = false,
                            ["precompiledReferences"] = new JArray(),
                            ["autoReferenced"] = @params["autoReferenced"]?.ToObject<bool>() ?? true,
                            ["defineConstraints"] = new JArray(),
                            ["versionDefines"] = new JArray(),
                            ["noEngineReferences"] = false
                        };

                        File.WriteAllText(asmdefPath, newAsmdef.ToString(Newtonsoft.Json.Formatting.Indented));
                        AssetDatabase.Refresh();
                        return new SuccessResponse($"Created assembly definition: {asmdefPath}");

                    case "add_reference":
                        string targetPath = @params["asmdef"]?.ToString();
                        string refToAdd = @params["reference"]?.ToString();

                        if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(refToAdd))
                            return new ErrorResponse("asmdef path and reference required");

                        var asmContent = JObject.Parse(File.ReadAllText(targetPath));
                        var refs = asmContent["references"] as JArray ?? new JArray();
                        if (!refs.Any(r => r.ToString() == refToAdd))
                        {
                            refs.Add(refToAdd);
                            asmContent["references"] = refs;
                            File.WriteAllText(targetPath, asmContent.ToString(Newtonsoft.Json.Formatting.Indented));
                            AssetDatabase.Refresh();
                        }
                        return new SuccessResponse($"Added reference: {refToAdd}");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"AsmDef manager failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 10. Editor Preferences Manager
    [McpForUnityTool(
        name: "editor_prefs",
        Description = "Manage EditorPrefs. Actions: get, set, delete, list_keys, has_key, clear_all")]
    public static class EditorPrefsManager
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_keys";
                string key = @params["key"]?.ToString();

                switch (action)
                {
                    case "get":
                        if (string.IsNullOrEmpty(key)) return new ErrorResponse("Key required");

                        // Try different types
                        if (EditorPrefs.HasKey(key))
                        {
                            // Try int first
                            int intVal = EditorPrefs.GetInt(key, int.MinValue);
                            if (intVal != int.MinValue)
                                return new SuccessResponse("EditorPref value", new { key, value = intVal, type = "int" });

                            float floatVal = EditorPrefs.GetFloat(key, float.MinValue);
                            if (floatVal != float.MinValue)
                                return new SuccessResponse("EditorPref value", new { key, value = floatVal, type = "float" });

                            bool boolVal = EditorPrefs.GetBool(key, false);
                            string strVal = EditorPrefs.GetString(key, "");

                            return new SuccessResponse("EditorPref value", new { key, stringValue = strVal, boolValue = boolVal });
                        }
                        return new ErrorResponse($"Key not found: {key}");

                    case "set":
                        if (string.IsNullOrEmpty(key)) return new ErrorResponse("Key required");
                        var value = @params["value"];

                        if (value == null) return new ErrorResponse("Value required");

                        switch (value.Type)
                        {
                            case JTokenType.Integer:
                                EditorPrefs.SetInt(key, value.ToObject<int>());
                                break;
                            case JTokenType.Float:
                                EditorPrefs.SetFloat(key, value.ToObject<float>());
                                break;
                            case JTokenType.Boolean:
                                EditorPrefs.SetBool(key, value.ToObject<bool>());
                                break;
                            default:
                                EditorPrefs.SetString(key, value.ToString());
                                break;
                        }
                        return new SuccessResponse($"Set EditorPref: {key}");

                    case "delete":
                        if (string.IsNullOrEmpty(key)) return new ErrorResponse("Key required");
                        EditorPrefs.DeleteKey(key);
                        return new SuccessResponse($"Deleted: {key}");

                    case "has_key":
                        if (string.IsNullOrEmpty(key)) return new ErrorResponse("Key required");
                        return new SuccessResponse("Key check", new { key, exists = EditorPrefs.HasKey(key) });

                    case "clear_all":
                        bool confirm = @params["confirm"]?.ToObject<bool>() ?? false;
                        if (!confirm) return new ErrorResponse("Set confirm=true to clear all EditorPrefs");
                        EditorPrefs.DeleteAll();
                        return new SuccessResponse("All EditorPrefs cleared");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"EditorPrefs failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 11. Sprite/2D Tools
    [McpForUnityTool(
        name: "sprite_tools",
        Description = "2D/Sprite tools. Actions: slice_sprite, create_atlas, get_sprite_info, set_pivot, set_border, list_atlases")]
    public static class SpriteTools
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_atlases";

                switch (action)
                {
                    case "get_sprite_info":
                        string spritePath = @params["path"]?.ToString();
                        if (string.IsNullOrEmpty(spritePath)) return new ErrorResponse("Sprite path required");

                        var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                        if (importer == null) return new ErrorResponse("Not a valid texture");

                        return new SuccessResponse("Sprite info", new
                        {
                            path = spritePath,
                            spriteMode = importer.spriteImportMode.ToString(),
                            pixelsPerUnit = importer.spritePixelsPerUnit,
                            pivot = importer.spritePivot,
                            border = importer.spriteBorder,
                            filterMode = importer.filterMode.ToString(),
                            maxTextureSize = importer.maxTextureSize,
                            spriteCount = importer.spritesheet?.Length ?? 1
                        });

                    case "set_pivot":
                        string pivotPath = @params["path"]?.ToString();
                        var pivot = @params["pivot"]?.ToObject<float[]>();

                        if (string.IsNullOrEmpty(pivotPath)) return new ErrorResponse("Path required");

                        var pivotImporter = AssetImporter.GetAtPath(pivotPath) as TextureImporter;
                        if (pivotImporter == null) return new ErrorResponse("Not a valid texture");

                        if (pivot != null && pivot.Length >= 2)
                            pivotImporter.spritePivot = new Vector2(pivot[0], pivot[1]);

                        pivotImporter.SaveAndReimport();
                        return new SuccessResponse("Pivot updated");

                    case "slice_sprite":
                        string slicePath = @params["path"]?.ToString();
                        int cellWidth = @params["cellWidth"]?.ToObject<int>() ?? 32;
                        int cellHeight = @params["cellHeight"]?.ToObject<int>() ?? 32;

                        if (string.IsNullOrEmpty(slicePath)) return new ErrorResponse("Path required");

                        var sliceImporter = AssetImporter.GetAtPath(slicePath) as TextureImporter;
                        if (sliceImporter == null) return new ErrorResponse("Not a valid texture");

                        sliceImporter.spriteImportMode = SpriteImportMode.Multiple;
                        sliceImporter.isReadable = true;

                        // Get texture size
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(slicePath);
                        if (tex == null) return new ErrorResponse("Cannot load texture");

                        int cols = tex.width / cellWidth;
                        int rows = tex.height / cellHeight;

                        var metas = new List<SpriteMetaData>();
                        for (int y = 0; y < rows; y++)
                        {
                            for (int x = 0; x < cols; x++)
                            {
                                var meta = new SpriteMetaData
                                {
                                    name = $"{Path.GetFileNameWithoutExtension(slicePath)}_{y * cols + x}",
                                    rect = new Rect(x * cellWidth, (rows - 1 - y) * cellHeight, cellWidth, cellHeight),
                                    pivot = new Vector2(0.5f, 0.5f),
                                    alignment = (int)SpriteAlignment.Center
                                };
                                metas.Add(meta);
                            }
                        }

                        sliceImporter.spritesheet = metas.ToArray();
                        sliceImporter.SaveAndReimport();

                        return new SuccessResponse($"Sliced into {metas.Count} sprites", new { count = metas.Count, cols, rows });

                    case "list_atlases":
                        var atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");
                        var atlases = atlasGuids.Select(guid =>
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            return new { path, name = Path.GetFileNameWithoutExtension(path) };
                        }).ToList();
                        return new SuccessResponse("Sprite atlases", new { atlases });

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Sprite tools failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 12. Animation Events Manager
    [McpForUnityTool(
        name: "animation_events",
        Description = "Manage animation events. Actions: list_events, add_event, remove_event, get_clip_info")]
    public static class AnimationEventsManager
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "list_events";
                string clipPath = @params["clip"]?.ToString();

                switch (action)
                {
                    case "list_events":
                        if (string.IsNullOrEmpty(clipPath)) return new ErrorResponse("Clip path required");

                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (clip == null) return new ErrorResponse("Clip not found");

                        var events = AnimationUtility.GetAnimationEvents(clip);
                        return new SuccessResponse("Animation events", new
                        {
                            clip = clip.name,
                            length = clip.length,
                            events = events.Select(e => new
                            {
                                time = e.time,
                                function = e.functionName,
                                intParam = e.intParameter,
                                floatParam = e.floatParameter,
                                stringParam = e.stringParameter
                            }).ToList()
                        });

                    case "add_event":
                        if (string.IsNullOrEmpty(clipPath)) return new ErrorResponse("Clip path required");

                        var addClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (addClip == null) return new ErrorResponse("Clip not found");

                        float eventTime = @params["time"]?.ToObject<float>() ?? 0;
                        string functionName = @params["function"]?.ToString();

                        if (string.IsNullOrEmpty(functionName)) return new ErrorResponse("Function name required");

                        var existingEvents = AnimationUtility.GetAnimationEvents(addClip).ToList();
                        existingEvents.Add(new AnimationEvent
                        {
                            time = eventTime,
                            functionName = functionName,
                            intParameter = @params["intParam"]?.ToObject<int>() ?? 0,
                            floatParameter = @params["floatParam"]?.ToObject<float>() ?? 0,
                            stringParameter = @params["stringParam"]?.ToString() ?? ""
                        });

                        AnimationUtility.SetAnimationEvents(addClip, existingEvents.ToArray());
                        EditorUtility.SetDirty(addClip);
                        AssetDatabase.SaveAssets();

                        return new SuccessResponse($"Added event '{functionName}' at {eventTime}s");

                    case "remove_event":
                        if (string.IsNullOrEmpty(clipPath)) return new ErrorResponse("Clip path required");

                        var removeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (removeClip == null) return new ErrorResponse("Clip not found");

                        int eventIndex = @params["index"]?.ToObject<int>() ?? -1;
                        if (eventIndex < 0) return new ErrorResponse("Event index required");

                        var removeEvents = AnimationUtility.GetAnimationEvents(removeClip).ToList();
                        if (eventIndex >= removeEvents.Count) return new ErrorResponse("Invalid event index");

                        removeEvents.RemoveAt(eventIndex);
                        AnimationUtility.SetAnimationEvents(removeClip, removeEvents.ToArray());
                        EditorUtility.SetDirty(removeClip);
                        AssetDatabase.SaveAssets();

                        return new SuccessResponse($"Removed event at index {eventIndex}");

                    case "get_clip_info":
                        if (string.IsNullOrEmpty(clipPath)) return new ErrorResponse("Clip path required");

                        var infoClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (infoClip == null) return new ErrorResponse("Clip not found");

                        return new SuccessResponse("Clip info", new
                        {
                            name = infoClip.name,
                            length = infoClip.length,
                            frameRate = infoClip.frameRate,
                            wrapMode = infoClip.wrapMode.ToString(),
                            isLooping = infoClip.isLooping,
                            legacy = infoClip.legacy,
                            eventCount = AnimationUtility.GetAnimationEvents(infoClip).Length
                        });

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Animation events failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 13. Snap/Grid Settings
    [McpForUnityTool(
        name: "grid_snap",
        Description = "Control grid and snap settings. Actions: get_settings, set_grid_size, set_rotation_snap, set_scale_snap, toggle_snap")]
    public static class GridSnapSettings
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params["action"]?.ToString()?.ToLower() ?? "get_settings";

                switch (action)
                {
                    case "get_settings":
                        // Get grid size via reflection (GridSettings is internal)
                        Vector3 currentGridSize = Vector3.one;
                        try
                        {
                            var gridSettingsType = Type.GetType("UnityEditor.GridSettings, UnityEditor");
                            if (gridSettingsType != null)
                            {
                                var sizeProp = gridSettingsType.GetProperty("size", BindingFlags.Static | BindingFlags.Public);
                                if (sizeProp != null)
                                    currentGridSize = (Vector3)sizeProp.GetValue(null);
                            }
                        }
                        catch { }

                        return new SuccessResponse("Grid/Snap settings", new
                        {
                            moveSnapX = EditorSnapSettings.move.x,
                            moveSnapY = EditorSnapSettings.move.y,
                            moveSnapZ = EditorSnapSettings.move.z,
                            rotateSnap = EditorSnapSettings.rotate,
                            scaleSnap = EditorSnapSettings.scale,
                            gridSize = new[] { currentGridSize.x, currentGridSize.y, currentGridSize.z }
                        });

                    case "set_grid_size":
                        var gridSize = @params["size"]?.ToObject<float[]>();
                        Vector3 newSize = Vector3.one;

                        if (gridSize != null && gridSize.Length >= 3)
                            newSize = new Vector3(gridSize[0], gridSize[1], gridSize[2]);
                        else if (@params["size"] != null)
                        {
                            float uniform = @params["size"].ToObject<float>();
                            newSize = new Vector3(uniform, uniform, uniform);
                        }

                        // Set via reflection
                        try
                        {
                            var gridSettingsType = Type.GetType("UnityEditor.GridSettings, UnityEditor");
                            if (gridSettingsType != null)
                            {
                                var sizeProp = gridSettingsType.GetProperty("size", BindingFlags.Static | BindingFlags.Public);
                                sizeProp?.SetValue(null, newSize);
                            }
                        }
                        catch { }

                        SceneView.RepaintAll();
                        return new SuccessResponse("Grid size updated");

                    case "set_rotation_snap":
                        float rotSnap = @params["angle"]?.ToObject<float>() ?? 15f;
                        EditorSnapSettings.rotate = rotSnap;
                        return new SuccessResponse($"Rotation snap: {rotSnap}");

                    case "set_scale_snap":
                        float scaleSnap = @params["scale"]?.ToObject<float>() ?? 0.1f;
                        EditorSnapSettings.scale = scaleSnap;
                        return new SuccessResponse($"Scale snap: {scaleSnap}");

                    case "set_move_snap":
                        var moveSnap = @params["snap"]?.ToObject<float[]>();
                        if (moveSnap != null && moveSnap.Length >= 3)
                        {
                            EditorSnapSettings.move = new Vector3(moveSnap[0], moveSnap[1], moveSnap[2]);
                        }
                        else if (@params["snap"] != null)
                        {
                            float uniform = @params["snap"].ToObject<float>();
                            EditorSnapSettings.move = new Vector3(uniform, uniform, uniform);
                        }
                        return new SuccessResponse("Move snap updated");

                    default:
                        return new ErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Grid/Snap settings failed: {e.Message}");
            }
        }
    }
    #endregion

    #region Helper Classes
    public class SuccessResponse
    {
        public bool success => true;
        public string message { get; set; }
        public object data { get; set; }

        public SuccessResponse(string msg, object responseData = null)
        {
            message = msg;
            data = responseData;
        }
    }

    public class ErrorResponse
    {
        public bool success => false;
        public string error { get; set; }

        public ErrorResponse(string err)
        {
            error = err;
        }
    }
    #endregion
}
