#nullable disable
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using MCPForUnity.Editor.Tools;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Core
{
    /// <summary>
    /// MCPVibeSystem - The Innovation
    /// Allows AI to instantly "Vibe" with the project context.
    /// No more asking "Are you using URP?" - the AI knows immediately.
    /// </summary>
    [McpForUnityTool(
        Name = "get_project_vibe",
        Description = "Get instant project context. Returns: Unity version, render pipeline (URP/HDRP/Built-in), input system, paths, active scene, object count. Use this FIRST to understand the project.")]
    public static class MCPVibeSystem
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var vibe = new Dictionary<string, object>();

                // Unity Environment
                vibe["unity_version"] = Application.unityVersion;
                vibe["platform"] = Application.platform.ToString();
                vibe["build_target"] = EditorUserBuildSettings.activeBuildTarget.ToString();
                vibe["scripting_backend"] = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString();
                vibe["api_compatibility"] = PlayerSettings.GetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString();

                // Render Pipeline Detection
                vibe["render_pipeline"] = DetectRenderPipeline();
                vibe["color_space"] = PlayerSettings.colorSpace.ToString();

                // Input System Detection
                vibe["input_system"] = DetectInputSystem();

                // Project Paths
                vibe["project_path"] = Application.dataPath.Replace("/Assets", "");
                vibe["assets_path"] = Application.dataPath;
                vibe["persistent_data_path"] = Application.persistentDataPath;
                vibe["streaming_assets_path"] = Application.streamingAssetsPath;

                // Active Scene Info
                var scene = SceneManager.GetActiveScene();
                vibe["active_scene"] = new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["is_dirty"] = scene.isDirty,
                    ["root_count"] = scene.rootCount,
                    ["total_objects"] = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length
                };

                // Project Stats
                vibe["project_stats"] = GetProjectStats();

                // Editor State
                vibe["editor_state"] = new Dictionary<string, object>
                {
                    ["is_playing"] = EditorApplication.isPlaying,
                    ["is_paused"] = EditorApplication.isPaused,
                    ["is_compiling"] = EditorApplication.isCompiling,
                    ["time_since_startup"] = EditorApplication.timeSinceStartup
                };

                // Installed Packages (key ones)
                vibe["key_packages"] = DetectKeyPackages();

                // Player Settings Summary
                vibe["player_settings"] = new Dictionary<string, object>
                {
                    ["product_name"] = PlayerSettings.productName,
                    ["company_name"] = PlayerSettings.companyName,
                    ["version"] = PlayerSettings.bundleVersion,
                    ["default_resolution"] = $"{PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}"
                };

                // Physics Settings
                vibe["physics"] = new Dictionary<string, object>
                {
                    ["gravity"] = Physics.gravity.ToString(),
                    ["default_solver_iterations"] = Physics.defaultSolverIterations,
                    ["auto_sync_transforms"] = true // Physics.SyncTransforms() is now called manually when needed
                };

                // Tags and Layers
                vibe["tags"] = UnityEditorInternal.InternalEditorUtility.tags;
                vibe["layers"] = GetDefinedLayers();

                // Quality Settings
                vibe["quality"] = new Dictionary<string, object>
                {
                    ["current_level"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
                    ["all_levels"] = QualitySettings.names,
                    ["vsync"] = QualitySettings.vSyncCount,
                    ["shadow_resolution"] = QualitySettings.shadowResolution.ToString()
                };

                return new SuccessResponse("Project vibe captured successfully", vibe);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to get project vibe: {ex.Message}");
            }
        }

        private static string DetectRenderPipeline()
        {
            try
            {
                var currentRP = GraphicsSettings.currentRenderPipeline;
                if (currentRP == null)
                    return "Built-in";

                string rpName = currentRP.GetType().Name;

                if (rpName.Contains("Universal") || rpName.Contains("URP"))
                    return "URP (Universal Render Pipeline)";
                if (rpName.Contains("HD") || rpName.Contains("HDRP"))
                    return "HDRP (High Definition Render Pipeline)";

                return $"Custom ({rpName})";
            }
            catch
            {
                return "Built-in (detection failed)";
            }
        }

        private static Dictionary<string, object> DetectInputSystem()
        {
            var result = new Dictionary<string, object>();

            try
            {
                bool hasNewInputSystem = false;
                bool hasLegacyInput = true;

                #if ENABLE_INPUT_SYSTEM
                hasNewInputSystem = true;
                #endif

                #if ENABLE_LEGACY_INPUT_MANAGER
                hasLegacyInput = true;
                #else
                hasLegacyInput = false;
                #endif

                if (hasNewInputSystem && hasLegacyInput)
                    result["mode"] = "Both (New + Legacy)";
                else if (hasNewInputSystem)
                    result["mode"] = "New Input System";
                else
                    result["mode"] = "Legacy Input Manager";

                result["new_input_system"] = hasNewInputSystem;
                result["legacy_input"] = hasLegacyInput;
            }
            catch
            {
                result["mode"] = "Legacy Input Manager";
                result["new_input_system"] = false;
                result["legacy_input"] = true;
            }

            return result;
        }

        private static Dictionary<string, object> GetProjectStats()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                stats["total_scenes"] = AssetDatabase.FindAssets("t:Scene").Length;
                stats["total_prefabs"] = AssetDatabase.FindAssets("t:Prefab").Length;
                stats["total_materials"] = AssetDatabase.FindAssets("t:Material").Length;
                stats["total_textures"] = AssetDatabase.FindAssets("t:Texture").Length;
                stats["total_scripts"] = AssetDatabase.FindAssets("t:Script").Length;
                stats["total_audio_clips"] = AssetDatabase.FindAssets("t:AudioClip").Length;
                stats["total_animations"] = AssetDatabase.FindAssets("t:AnimationClip").Length;
                stats["total_scriptable_objects"] = AssetDatabase.FindAssets("t:ScriptableObject").Length;
            }
            catch
            {
                stats["error"] = "Failed to gather project stats";
            }

            return stats;
        }

        private static List<string> DetectKeyPackages()
        {
            var packages = new List<string>();

            try
            {
                var checks = new Dictionary<string, string>
                {
                    ["TextMeshPro"] = "TMPro.TextMeshProUGUI, Unity.TextMeshPro",
                    ["Cinemachine"] = "Cinemachine.CinemachineVirtualCamera, Cinemachine",
                    ["ProBuilder"] = "UnityEngine.ProBuilder.ProBuilderMesh, Unity.ProBuilder",
                    ["Addressables"] = "UnityEngine.AddressableAssets.Addressables, Unity.Addressables",
                    ["DOTween"] = "DG.Tweening.DOTween, DOTween",
                    ["UniTask"] = "Cysharp.Threading.Tasks.UniTask, UniTask",
                    ["Newtonsoft.Json"] = "Newtonsoft.Json.JsonConvert, Newtonsoft.Json"
                };

                foreach (var kvp in checks)
                {
                    try
                    {
                        if (Type.GetType(kvp.Value) != null)
                            packages.Add(kvp.Key);
                    }
                    catch { }
                }

                if (Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime") != null)
                    packages.Add("URP");
                if (Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime") != null)
                    packages.Add("HDRP");
            }
            catch { }

            return packages;
        }

        private static List<string> GetDefinedLayers()
        {
            var layers = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                    layers.Add($"{i}: {layerName}");
            }
            return layers;
        }
    }

    /// <summary>
    /// Quick vibe check - lightweight version for frequent calls
    /// </summary>
    [McpForUnityTool(
        Name = "quick_vibe",
        Description = "Quick lightweight vibe check. Returns: scene name, object count, play state, compile state. Use for fast status updates.")]
    public static class MCPQuickVibe
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var scene = SceneManager.GetActiveScene();

                return new SuccessResponse("Quick vibe", new Dictionary<string, object>
                {
                    ["scene"] = scene.name,
                    ["objects"] = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                    ["playing"] = EditorApplication.isPlaying,
                    ["paused"] = EditorApplication.isPaused,
                    ["compiling"] = EditorApplication.isCompiling,
                    ["dirty"] = scene.isDirty,
                    ["memory_mb"] = (int)(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576),
                    ["fps"] = (int)(1f / Time.unscaledDeltaTime)
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Quick vibe failed: {ex.Message}");
            }
        }
    }
}
