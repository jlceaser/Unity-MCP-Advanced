#nullable disable
#pragma warning disable CS0618 // FindObjectsOfType/FindObjectOfType is deprecated but extensively used in this file
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// ENDURANCE Project Bridge - Integrates VibeUnityBridge features into MCP
    /// Provides: screenshots, performance stats, physics state, audit trail, scene health
    /// </summary>
    [McpForUnityTool(
        name: "endurance_bridge",
        Description = "ENDURANCE project tools: screenshot, performance, physics_state, audit, scene_health, assign_reference")]
    public static class EnduranceBridge
    {
        private static readonly string BasePath = Path.Combine(Application.dataPath, "../.vibe-unity");
        private static List<AuditEntry> auditLog = new List<AuditEntry>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("ENDURANCE Bridge ready. Actions: screenshot, performance, physics_state, audit, scene_health, survival_stats, assign_reference");
            }

            switch (action)
            {
                case "screenshot":
                    return CaptureScreenshot(@params);

                case "performance":
                    return GetPerformanceStats();

                case "physics_state":
                    return GetPhysicsState();

                case "audit":
                    return GetAuditTrail(@params);

                case "scene_health":
                    return AnalyzeSceneHealth();

                case "survival_stats":
                    return GetSurvivalStats();

                case "assign_reference":
                    return AssignComponentReference(@params);

                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: screenshot, performance, physics_state, audit, scene_health, survival_stats, assign_reference");
            }
        }

        #region Screenshot
        private static object CaptureScreenshot(JObject @params)
        {
            try
            {
                string filename = @params["filename"]?.ToString() ?? $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string path = Path.Combine(BasePath, "state", filename);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // Capture game view
                var gameView = GetGameViewRenderTexture();
                if (gameView != null)
                {
                    var tex = new Texture2D(gameView.width, gameView.height, TextureFormat.RGB24, false);
                    RenderTexture.active = gameView;
                    tex.ReadPixels(new Rect(0, 0, gameView.width, gameView.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    byte[] bytes = tex.EncodeToPNG();
                    File.WriteAllBytes(path, bytes);
                    UnityEngine.Object.DestroyImmediate(tex);

                    LogAudit("screenshot", path, "Screenshot captured");
                    return new SuccessResponse($"Screenshot saved: {path}", new { path, size = bytes.Length });
                }

                return new ErrorResponse("Could not capture game view. Is a camera active?");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Screenshot failed: {e.Message}");
            }
        }

        private static RenderTexture GetGameViewRenderTexture()
        {
            var camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera == null) return null;

            var rt = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
            return rt;
        }
        #endregion

        #region Performance
        private static object GetPerformanceStats()
        {
            var stats = new
            {
                fps = 1f / Time.deltaTime,
                frameTime = Time.deltaTime * 1000f,
                fixedDeltaTime = Time.fixedDeltaTime,
                timeScale = Time.timeScale,
                frameCount = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup,

                // Memory
                totalAllocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024f / 1024f,
                totalReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024f / 1024f,
                totalUnusedReservedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1024f / 1024f,

                // Rendering
                drawCalls = UnityStats.drawCalls,
                batches = UnityStats.batches,
                triangles = UnityStats.triangles,
                vertices = UnityStats.vertices,

                // Scene
                gameObjectCount = UnityEngine.Object.FindObjectsOfType<GameObject>().Length,
                activeGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>().Count(g => g.activeInHierarchy)
            };

            return new SuccessResponse("Performance stats retrieved", stats);
        }
        #endregion

        #region Physics State
        private static object GetPhysicsState()
        {
            var rigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            var rbStates = rigidbodies.Select(rb => new
            {
                name = rb.gameObject.name,
                position = new { x = rb.position.x, y = rb.position.y, z = rb.position.z },
                velocity = new { x = rb.linearVelocity.x, y = rb.linearVelocity.y, z = rb.linearVelocity.z },
                angularVelocity = new { x = rb.angularVelocity.x, y = rb.angularVelocity.y, z = rb.angularVelocity.z },
                mass = rb.mass,
                drag = rb.linearDamping,
                isKinematic = rb.isKinematic,
                isSleeping = rb.IsSleeping()
            }).ToList();

            var state = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                rigidbodyCount = rbStates.Count,
                sleepingCount = rbStates.Count(r => (bool)r.isSleeping),
                rigidbodies = rbStates
            };

            return new SuccessResponse($"Physics state: {rbStates.Count} rigidbodies", state);
        }
        #endregion

        #region Audit Trail
        private static void LogAudit(string action, string target, string details)
        {
            auditLog.Add(new AuditEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                action = action,
                target = target,
                details = details
            });

            if (auditLog.Count > 500)
                auditLog.RemoveAt(0);
        }

        private static object GetAuditTrail(JObject @params)
        {
            int count = @params["count"]?.ToObject<int>() ?? 50;
            var entries = auditLog.TakeLast(count).ToList();

            return new SuccessResponse($"Audit trail: {entries.Count} entries", new { entries });
        }

        [System.Serializable]
        private class AuditEntry
        {
            public string timestamp;
            public string action;
            public string target;
            public string details;
        }
        #endregion

        #region Scene Health
        private static object AnalyzeSceneHealth()
        {
            var issues = new List<string>();
            var warnings = new List<string>();

            // Check cameras
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            if (cameras.Length == 0) issues.Add("No cameras in scene");
            if (cameras.Length > 3) warnings.Add($"Many cameras: {cameras.Length}");

            // Check lights
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (lights.Length == 0) warnings.Add("No lights in scene");

            // Check rigidbodies
            var rbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            if (rbs.Length > 200) warnings.Add($"Many rigidbodies: {rbs.Length}");

            // Check colliders without rigidbodies
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
            int orphanColliders = colliders.Count(c => c.GetComponent<Rigidbody>() == null && !c.isTrigger);

            // Check for missing scripts
            var allGOs = UnityEngine.Object.FindObjectsOfType<GameObject>();
            int missingScripts = 0;
            foreach (var go in allGOs)
            {
                var components = go.GetComponents<Component>();
                missingScripts += components.Count(c => c == null);
            }
            if (missingScripts > 0) issues.Add($"Missing scripts: {missingScripts}");

            var health = new
            {
                score = 100 - (issues.Count * 20) - (warnings.Count * 5),
                cameraCount = cameras.Length,
                lightCount = lights.Length,
                rigidbodyCount = rbs.Length,
                colliderCount = colliders.Length,
                gameObjectCount = allGOs.Length,
                missingScripts,
                issues,
                warnings
            };

            string status = issues.Count == 0 ? "Healthy" : $"{issues.Count} issues found";
            return new SuccessResponse($"Scene health: {status}", health);
        }
        #endregion

        #region Assign Component Reference
        /// <summary>
        /// Assigns a component reference from one GameObject to another's serialized field.
        /// Params: sourceObject, sourceComponent, targetObject, targetComponent, targetProperty
        /// </summary>
        private static object AssignComponentReference(JObject @params)
        {
            try
            {
                string sourceObjectName = @params["sourceObject"]?.ToString();
                string sourceComponentName = @params["sourceComponent"]?.ToString();
                string targetObjectName = @params["targetObject"]?.ToString();
                string targetComponentName = @params["targetComponent"]?.ToString();
                string targetPropertyName = @params["targetProperty"]?.ToString();

                if (string.IsNullOrEmpty(sourceObjectName) || string.IsNullOrEmpty(targetObjectName) ||
                    string.IsNullOrEmpty(targetComponentName) || string.IsNullOrEmpty(targetPropertyName))
                {
                    return new ErrorResponse("Required params: sourceObject, targetObject, targetComponent, targetProperty. Optional: sourceComponent");
                }

                // Find source GameObject
                var sourceGO = GameObject.Find(sourceObjectName);
                if (sourceGO == null)
                {
                    // Try finding in all objects including inactive
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

                // Get source component (or the GameObject itself if no component specified)
                UnityEngine.Object sourceRef;
                if (!string.IsNullOrEmpty(sourceComponentName))
                {
                    var sourceComp = sourceGO.GetComponent(sourceComponentName);
                    if (sourceComp == null)
                        return new ErrorResponse($"Source component '{sourceComponentName}' not found on '{sourceObjectName}'");
                    sourceRef = sourceComp;
                }
                else
                {
                    sourceRef = sourceGO;
                }

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
                    return new ErrorResponse($"Property '{targetPropertyName}' is not an object reference (type: {property.propertyType})");

                // Assign the reference
                property.objectReferenceValue = sourceRef;
                serializedObject.ApplyModifiedProperties();

                // Mark scene dirty
                EditorUtility.SetDirty(targetComp);
                EditorSceneManager.MarkSceneDirty(targetGO.scene);

                LogAudit("assign_reference", $"{targetObjectName}.{targetComponentName}.{targetPropertyName}",
                    $"Assigned {sourceObjectName}.{sourceComponentName ?? "GameObject"} reference");

                return new SuccessResponse($"Successfully assigned '{sourceObjectName}' to '{targetObjectName}.{targetComponentName}.{targetPropertyName}'",
                    new {
                        source = sourceObjectName,
                        sourceComponent = sourceComponentName ?? "GameObject",
                        target = targetObjectName,
                        targetComponent = targetComponentName,
                        property = targetPropertyName
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to assign reference: {e.Message}");
            }
        }
        #endregion

        #region Survival Stats
        private static object GetSurvivalStats()
        {
            // Find MetabolismController in scene
            var controllerType = System.Type.GetType("Endurance.Core.MetabolismController, Assembly-CSharp");
            if (controllerType == null)
            {
                return new ErrorResponse("MetabolismController type not found");
            }

            var controller = UnityEngine.Object.FindObjectOfType(controllerType);
            if (controller == null)
            {
                return new SuccessResponse("No active MetabolismController in scene", new { active = false });
            }

            // Use reflection to get stats
            try
            {
                var getStatsMethod = controllerType.GetMethod("GetStatsJSON");
                if (getStatsMethod != null)
                {
                    string json = (string)getStatsMethod.Invoke(controller, null);
                    return new SuccessResponse("Survival stats retrieved", new { active = true, stats = json });
                }
            }
            catch { }

            return new SuccessResponse("MetabolismController found but stats unavailable", new { active = true });
        }
        #endregion
    }
}
