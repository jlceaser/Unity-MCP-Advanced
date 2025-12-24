#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// MCP Tool for emergency Unity recovery and reset operations.
    /// Provides comprehensive reset capabilities from soft to hard recovery.
    /// </summary>
    [McpForUnityTool(
        name: "unity_reset",
        Description = "Emergency Unity recovery and reset tool. Actions: soft_reset, hard_reset, force_recompile, clear_cache, reset_layout, reset_editor_prefs, recover_scene, reset_input, reset_physics, full_recovery, diagnose")]
    public static class MCPUnityReset
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "diagnose";

            try
            {
                switch (action)
                {
                    case "soft_reset":
                        return SoftReset(@params);
                    case "hard_reset":
                        return HardReset(@params);
                    case "force_recompile":
                        return ForceRecompile();
                    case "clear_cache":
                        return ClearCache(@params);
                    case "reset_layout":
                        return ResetLayout();
                    case "reset_editor_prefs":
                        return ResetEditorPrefs(@params);
                    case "recover_scene":
                        return RecoverScene(@params);
                    case "reset_input":
                        return ResetInputSystem();
                    case "reset_physics":
                        return ResetPhysics();
                    case "full_recovery":
                        return FullRecovery(@params);
                    case "diagnose":
                        return Diagnose();
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Available: soft_reset, hard_reset, force_recompile, clear_cache, reset_layout, reset_editor_prefs, recover_scene, reset_input, reset_physics, full_recovery, diagnose");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Reset operation failed: {e.Message}\n{e.StackTrace}");
            }
        }

        #region Soft Reset - Safe Recovery Operations

        private static object SoftReset(JObject @params)
        {
            var results = new System.Collections.Generic.List<string>();

            // 1. Stop play mode if running
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                results.Add("Stopped Play Mode");
            }

            // 2. Clear console
            ClearConsole();
            results.Add("Cleared Console");

            // 3. Refresh asset database
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            results.Add("Refreshed Asset Database");

            // 4. Save current scene
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
                results.Add("Saved Open Scenes");
            }

            // 5. Collect garbage
            GC.Collect();
            results.Add("Collected Garbage");

            // 6. Repaint all editor windows
            RepaintAllWindows();
            results.Add("Repainted All Windows");

            return new SuccessResponse("Soft reset completed", new
            {
                action = "soft_reset",
                operations = results,
                success = true
            });
        }

        #endregion

        #region Hard Reset - Force Recovery Operations

        private static object HardReset(JObject @params)
        {
            var results = new System.Collections.Generic.List<string>();

            // 1. Force stop play mode
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                EditorApplication.isPlaying = false;
                results.Add("Force stopped Play Mode");
            }

            // 2. Clear all caches
            Caching.ClearCache();
            results.Add("Cleared Unity Caching");

            // 3. Force reimport all assets
            bool forceReimport = @params["force_reimport"]?.ToObject<bool>() ?? false;
            if (forceReimport)
            {
                AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                results.Add("Force reimported all assets");
            }
            else
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                results.Add("Refreshed Asset Database with force update");
            }

            // 4. Clear GI cache
            try
            {
                Lightmapping.ClearLightingDataAsset();
                results.Add("Cleared Lighting Data Asset");
            }
            catch { }

            // 5. Reset scripting defines if requested
            bool resetDefines = @params["reset_defines"]?.ToObject<bool>() ?? false;
            if (resetDefines)
            {
                var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
                var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTarget);
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, "");
                results.Add("Cleared Scripting Define Symbols");
            }

            // 6. Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            results.Add("Force garbage collection (3-pass)");

            // 7. Clear console
            ClearConsole();
            results.Add("Cleared Console");

            return new SuccessResponse("Hard reset completed", new
            {
                action = "hard_reset",
                operations = results,
                warning = "Some changes may require Unity restart to take full effect",
                success = true
            });
        }

        #endregion

        #region Force Recompile

        private static object ForceRecompile()
        {
            // Touch a script to force recompile
            string dummyPath = "Assets/Scripts/Editor/_ForceRecompile.cs";
            string content = $"// Force recompile trigger - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n// This file can be safely deleted\nnamespace ForceRecompile {{ }}";

            File.WriteAllText(dummyPath, content);
            AssetDatabase.Refresh();

            // Request script compilation
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

            return new SuccessResponse("Force recompilation triggered", new
            {
                action = "force_recompile",
                triggerFile = dummyPath,
                note = "Scripts will recompile. Domain reload may occur.",
                success = true
            });
        }

        #endregion

        #region Clear Cache

        private static object ClearCache(JObject @params)
        {
            var results = new System.Collections.Generic.List<string>();

            string cacheType = @params["type"]?.ToString()?.ToLower() ?? "all";

            // Unity's general cache
            if (cacheType == "all" || cacheType == "unity")
            {
                Caching.ClearCache();
                results.Add("Cleared Unity Cache");
            }

            // GI Cache
            if (cacheType == "all" || cacheType == "gi")
            {
                string giCachePath = Path.Combine(Application.dataPath, "..", "Library", "GiCache");
                if (Directory.Exists(giCachePath))
                {
                    try
                    {
                        Directory.Delete(giCachePath, true);
                        results.Add("Cleared GI Cache");
                    }
                    catch (Exception e)
                    {
                        results.Add($"GI Cache clear failed: {e.Message}");
                    }
                }
            }

            // Shader Cache
            if (cacheType == "all" || cacheType == "shader")
            {
                string shaderCachePath = Path.Combine(Application.dataPath, "..", "Library", "ShaderCache");
                if (Directory.Exists(shaderCachePath))
                {
                    try
                    {
                        Directory.Delete(shaderCachePath, true);
                        results.Add("Cleared Shader Cache");
                    }
                    catch (Exception e)
                    {
                        results.Add($"Shader Cache clear failed: {e.Message}");
                    }
                }
            }

            // Script Assemblies (dangerous but sometimes necessary)
            if (cacheType == "assemblies")
            {
                string assemblyPath = Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies");
                if (Directory.Exists(assemblyPath))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(assemblyPath, "*.dll"))
                        {
                            File.Delete(file);
                        }
                        results.Add("Cleared Script Assemblies - RESTART REQUIRED");
                    }
                    catch (Exception e)
                    {
                        results.Add($"Assembly clear failed: {e.Message}");
                    }
                }
            }

            AssetDatabase.Refresh();

            return new SuccessResponse("Cache clearing completed", new
            {
                action = "clear_cache",
                type = cacheType,
                operations = results,
                success = true
            });
        }

        #endregion

        #region Reset Layout

        private static object ResetLayout()
        {
            try
            {
                // Reset to default layout
                EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
                return new SuccessResponse("Editor layout reset to Default", new
                {
                    action = "reset_layout",
                    layout = "Default",
                    success = true
                });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Layout reset failed: {e.Message}");
            }
        }

        #endregion

        #region Reset Editor Prefs

        private static object ResetEditorPrefs(JObject @params)
        {
            string scope = @params["scope"]?.ToString()?.ToLower() ?? "project";
            var cleared = new System.Collections.Generic.List<string>();

            if (scope == "project" || scope == "all")
            {
                // Clear project-specific prefs
                string projectKey = PlayerSettings.productName;
                EditorPrefs.DeleteKey($"{projectKey}_LastScene");
                EditorPrefs.DeleteKey($"{projectKey}_EditorState");
                cleared.Add("Project-specific preferences");
            }

            if (scope == "all")
            {
                // Warning: This is dangerous
                EditorPrefs.DeleteAll();
                cleared.Add("ALL Editor Preferences (DANGEROUS)");
            }

            return new SuccessResponse("Editor preferences reset", new
            {
                action = "reset_editor_prefs",
                scope = scope,
                cleared = cleared,
                warning = scope == "all" ? "All preferences cleared. Some settings may need reconfiguration." : null,
                success = true
            });
        }

        #endregion

        #region Recover Scene

        private static object RecoverScene(JObject @params)
        {
            string scenePath = @params["scene_path"]?.ToString();
            bool createBackup = @params["create_backup"]?.ToObject<bool>() ?? true;

            var results = new System.Collections.Generic.List<string>();

            // Check for auto-save backups
            string backupFolder = Path.Combine(Application.dataPath, "..", "Library", "Backup");
            string[] backupFiles = Directory.Exists(backupFolder) 
                ? Directory.GetFiles(backupFolder, "*.unity", SearchOption.AllDirectories) 
                : new string[0];

            // Check for _Recovery folder
            string recoveryFolder = Path.Combine(Application.dataPath, "_Recovery");
            string[] recoveryFiles = Directory.Exists(recoveryFolder)
                ? Directory.GetFiles(recoveryFolder, "*.unity")
                : new string[0];

            if (!string.IsNullOrEmpty(scenePath))
            {
                // Try to recover specific scene
                if (createBackup)
                {
                    string backupPath = scenePath.Replace(".unity", $"_backup_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
                    if (File.Exists(scenePath))
                    {
                        File.Copy(scenePath, backupPath);
                        results.Add($"Created backup: {backupPath}");
                    }
                }

                // Force reload scene
                if (File.Exists(scenePath))
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    results.Add($"Reloaded scene: {scenePath}");
                }
                else
                {
                    results.Add($"Scene not found: {scenePath}");
                }
            }
            else
            {
                // Create new empty scene as recovery
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                results.Add("Created new empty scene with default objects");

                // Save to recovery folder
                if (!Directory.Exists(recoveryFolder))
                {
                    Directory.CreateDirectory(recoveryFolder);
                    AssetDatabase.Refresh();
                }

                string recoveryPath = Path.Combine(recoveryFolder, $"Recovery_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
                EditorSceneManager.SaveScene(newScene, recoveryPath);
                results.Add($"Saved recovery scene: {recoveryPath}");
            }

            return new SuccessResponse("Scene recovery completed", new
            {
                action = "recover_scene",
                operations = results,
                availableBackups = backupFiles.Length,
                availableRecoveryFiles = recoveryFiles.Length,
                backupPaths = backupFiles.Take(5).ToArray(),
                recoveryPaths = recoveryFiles.Take(5).ToArray(),
                success = true
            });
        }

        #endregion

        #region Reset Input System

        private static object ResetInputSystem()
        {
            var results = new System.Collections.Generic.List<string>();

            // Reset Input Manager settings
            try
            {
                // Clear any stuck input states via reflection
                var inputSystemType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
                if (inputSystemType != null)
                {
                    var resetMethod = inputSystemType.GetMethod("ResetHaptics", BindingFlags.Public | BindingFlags.Static);
                    resetMethod?.Invoke(null, null);
                    results.Add("Reset InputSystem haptics");

                    // Try to disable/re-enable all devices
                    var devicesProperty = inputSystemType.GetProperty("devices", BindingFlags.Public | BindingFlags.Static);
                    if (devicesProperty != null)
                    {
                        results.Add("InputSystem devices accessible");
                    }
                }
            }
            catch (Exception e)
            {
                results.Add($"InputSystem reset partial: {e.Message}");
            }

            // Reset legacy Input
            try
            {
                Input.ResetInputAxes();
                results.Add("Reset legacy Input axes");
            }
            catch { }

            return new SuccessResponse("Input system reset completed", new
            {
                action = "reset_input",
                operations = results,
                success = true
            });
        }

        #endregion

        #region Reset Physics

        private static object ResetPhysics()
        {
            var results = new System.Collections.Generic.List<string>();

            // Reset physics simulation
            Physics.simulationMode = SimulationMode.FixedUpdate;
            results.Add("Reset Physics simulation mode to FixedUpdate");

            // Sync transforms manually (autoSyncTransforms is deprecated)
            Physics.SyncTransforms();
            results.Add("Synced Physics transforms");

            // Reset physics scene
            try
            {
                var physicsScene = PhysicsSceneExtensions.GetPhysicsScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                results.Add("Physics scene accessible");
            }
            catch { }

            // Clear all contacts (stop simulation briefly)
            Time.timeScale = 0f;
            Physics.SyncTransforms();
            Time.timeScale = 1f;
            results.Add("Synced transforms and reset timeScale");

            return new SuccessResponse("Physics reset completed", new
            {
                action = "reset_physics",
                operations = results,
                success = true
            });
        }

        #endregion

        #region Full Recovery - Nuclear Option

        private static object FullRecovery(JObject @params)
        {
            var results = new System.Collections.Generic.List<string>();
            bool confirmed = @params["confirm"]?.ToObject<bool>() ?? false;

            if (!confirmed)
            {
                return new ErrorResponse("Full recovery requires confirmation. Set 'confirm': true to proceed. This will: stop play mode, clear all caches, force recompile, reset physics/input, and reload scene.");
            }

            // 1. Stop everything
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                results.Add("Stopped Play Mode");
            }

            // 2. Clear console
            ClearConsole();
            results.Add("Cleared console");

            // 3. Save current scene path
            string currentScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            results.Add($"Saved scene reference: {currentScenePath}");

            // 4. Clear caches
            Caching.ClearCache();
            results.Add("Cleared Unity cache");

            // 5. Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            results.Add("Forced garbage collection");

            // 6. Refresh assets
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            results.Add("Refreshed asset database");

            // 7. Request recompile
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            results.Add("Requested script recompilation");

            // 8. Sync physics transforms
            Physics.SyncTransforms();
            results.Add("Synced physics transforms");

            // 9. Reset input
            try { Input.ResetInputAxes(); } catch { }
            results.Add("Reset input axes");

            // 10. Reload scene
            if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
                results.Add($"Reloaded scene: {currentScenePath}");
            }

            // 11. Repaint
            RepaintAllWindows();
            results.Add("Repainted all windows");

            return new SuccessResponse("FULL RECOVERY COMPLETED", new
            {
                action = "full_recovery",
                operations = results,
                operationCount = results.Count,
                warning = "Domain reload may occur. Some operations may require Unity restart.",
                success = true
            });
        }

        #endregion

        #region Diagnose - Check System Health

        private static object Diagnose()
        {
            var issues = new System.Collections.Generic.List<string>();
            var status = new System.Collections.Generic.Dictionary<string, object>();

            // Check play mode
            status["playMode"] = EditorApplication.isPlaying;
            status["isPaused"] = EditorApplication.isPaused;
            status["isCompiling"] = EditorApplication.isCompiling;

            // Check scene state
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            status["sceneName"] = activeScene.name;
            status["scenePath"] = activeScene.path;
            status["sceneIsDirty"] = activeScene.isDirty;
            status["sceneIsLoaded"] = activeScene.isLoaded;

            if (!activeScene.isLoaded)
                issues.Add("Active scene is not loaded");

            // Check memory
            long totalMemory = GC.GetTotalMemory(false);
            status["managedMemoryMB"] = totalMemory / (1024 * 1024);

            if (totalMemory > 500 * 1024 * 1024)
                issues.Add("High managed memory usage (>500MB)");

            // Check for errors in console
            // (We can't easily access console logs, but we can check compile errors)
            status["hasCompileErrors"] = EditorUtility.scriptCompilationFailed;

            if (EditorUtility.scriptCompilationFailed)
                issues.Add("Script compilation has errors");

            // Check project structure
            status["assetsPath"] = Application.dataPath;
            status["projectPath"] = Path.GetDirectoryName(Application.dataPath);

            // Check for common issues
            string libraryPath = Path.Combine(Application.dataPath, "..", "Library");
            status["libraryExists"] = Directory.Exists(libraryPath);

            if (!Directory.Exists(libraryPath))
                issues.Add("Library folder missing");

            // Check temp folder size
            string tempPath = Path.Combine(Application.dataPath, "..", "Temp");
            if (Directory.Exists(tempPath))
            {
                try
                {
                    long tempSize = GetDirectorySize(tempPath);
                    status["tempFolderMB"] = tempSize / (1024 * 1024);

                    if (tempSize > 1024 * 1024 * 1024) // 1GB
                        issues.Add("Temp folder is very large (>1GB)");
                }
                catch { }
            }

            // Overall health
            string health = issues.Count == 0 ? "HEALTHY" : 
                           issues.Count < 3 ? "WARNING" : "CRITICAL";

            return new SuccessResponse($"Diagnosis complete: {health}", new
            {
                action = "diagnose",
                health = health,
                issueCount = issues.Count,
                issues = issues,
                status = status,
                recommendations = GetRecommendations(issues),
                success = true
            });
        }

        private static string[] GetRecommendations(System.Collections.Generic.List<string> issues)
        {
            var recommendations = new System.Collections.Generic.List<string>();

            if (issues.Count == 0)
            {
                recommendations.Add("System is healthy. No action needed.");
                return recommendations.ToArray();
            }

            foreach (var issue in issues)
            {
                if (issue.Contains("memory"))
                    recommendations.Add("Run 'soft_reset' to collect garbage and free memory.");
                if (issue.Contains("compile"))
                    recommendations.Add("Fix script errors, then run 'force_recompile'.");
                if (issue.Contains("Temp"))
                    recommendations.Add("Consider running 'clear_cache' with type='all'.");
                if (issue.Contains("scene"))
                    recommendations.Add("Run 'recover_scene' to create a fresh scene.");
            }

            if (issues.Count >= 3)
                recommendations.Add("Consider running 'full_recovery' with confirm=true for complete reset.");

            return recommendations.ToArray();
        }

        #endregion

        #region Helper Methods

        private static void ClearConsole()
        {
            try
            {
                var assembly = Assembly.GetAssembly(typeof(SceneView));
                var type = assembly.GetType("UnityEditor.LogEntries");
                var method = type.GetMethod("Clear");
                method?.Invoke(null, null);
            }
            catch { }
        }

        private static void RepaintAllWindows()
        {
            foreach (var window in UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }
            SceneView.RepaintAll();
        }

        private static long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch { }
            return size;
        }

        #endregion
    }
}
