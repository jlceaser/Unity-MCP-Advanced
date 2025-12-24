#nullable disable
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Tools;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Systems
{
    /// <summary>
    /// MCPGenericValidator - Universal Project Validator
    /// Replaces game-specific testers with a generic, production-ready validation system.
    /// </summary>
    [McpForUnityTool(
        Name = "project_validator",
        Description = "Universal project validation. Actions: full_scan, scan_missing_scripts, scan_colliders, scan_duplicates, scan_references, check_layers, check_performance, validate_prefabs")]
    public static class MCPGenericValidator
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "full_scan";
            bool includeInactive = @params["include_inactive"]?.ToObject<bool>() ?? true;

            try
            {
                switch (action)
                {
                    case "full_scan":
                        return FullScan(includeInactive);
                    case "scan_missing_scripts":
                        return ScanMissingScripts(includeInactive);
                    case "scan_colliders":
                        return ScanColliderIssues(includeInactive);
                    case "scan_duplicates":
                        return ScanDuplicateNames(includeInactive);
                    case "scan_references":
                        return ScanMissingReferences(includeInactive);
                    case "check_layers":
                        return CheckLayersAndTags();
                    case "check_performance":
                        return CheckPerformanceIssues(includeInactive);
                    case "validate_prefabs":
                        return ValidatePrefabs();
                    default:
                        return new ErrorResponse($"Unknown action: {action}. Available: full_scan, scan_missing_scripts, scan_colliders, scan_duplicates, scan_references, check_layers, check_performance, validate_prefabs");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Validation failed: {ex.Message}");
            }
        }

        private static object FullScan(bool includeInactive)
        {
            var results = new Dictionary<string, object>();
            var issues = new List<Dictionary<string, object>>();
            var warnings = new List<Dictionary<string, object>>();
            var suggestions = new List<string>();

            var missingScripts = FindMissingScripts(includeInactive);
            if (missingScripts.Count > 0)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Missing Scripts",
                    ["severity"] = "ERROR",
                    ["count"] = missingScripts.Count,
                    ["objects"] = missingScripts.Take(20).ToList()
                });
            }

            var colliderIssues = FindColliderIssues(includeInactive);
            foreach (var issue in colliderIssues)
            {
                warnings.Add(issue);
            }

            var duplicates = FindDuplicateNames(includeInactive);
            if (duplicates.Count > 0)
            {
                warnings.Add(new Dictionary<string, object>
                {
                    ["type"] = "Duplicate Names",
                    ["severity"] = "WARNING",
                    ["count"] = duplicates.Count,
                    ["groups"] = duplicates.Take(10).ToList()
                });
            }

            var missingRefs = FindMissingReferences(includeInactive);
            if (missingRefs.Count > 0)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Missing References",
                    ["severity"] = "ERROR",
                    ["count"] = missingRefs.Count,
                    ["details"] = missingRefs.Take(20).ToList()
                });
            }

            var perfIssues = FindPerformanceIssues(includeInactive);
            foreach (var perf in perfIssues)
            {
                warnings.Add(perf);
            }

            string health = "HEALTHY";
            if (issues.Count > 0)
                health = "CRITICAL";
            else if (warnings.Count > 0)
                health = "WARNING";

            if (issues.Count > 0)
                suggestions.Add("Fix all CRITICAL issues before building.");
            if (missingScripts.Count > 0)
                suggestions.Add("Remove or replace missing script components.");
            if (duplicates.Count > 0)
                suggestions.Add("Consider renaming duplicate objects for clarity.");

            results["health"] = health;
            results["issues"] = issues;
            results["warnings"] = warnings;
            results["suggestions"] = suggestions;
            results["summary"] = new Dictionary<string, object>
            {
                ["total_issues"] = issues.Count,
                ["total_warnings"] = warnings.Count,
                ["objects_scanned"] = UnityEngine.Object.FindObjectsByType<GameObject>(
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length
            };

            return new SuccessResponse($"Full scan complete: {health}", results);
        }

        private static object ScanMissingScripts(bool includeInactive)
        {
            var missing = FindMissingScripts(includeInactive);
            return new SuccessResponse($"Found {missing.Count} objects with missing scripts", new Dictionary<string, object>
            {
                ["count"] = missing.Count,
                ["objects"] = missing
            });
        }

        private static List<string> FindMissingScripts(bool includeInactive)
        {
            var result = new List<string>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] == null)
                        {
                            result.Add(GetGameObjectPath(go));
                            break;
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private static object ScanColliderIssues(bool includeInactive)
        {
            var issues = FindColliderIssues(includeInactive);
            return new SuccessResponse($"Found {issues.Count} collider issues", new Dictionary<string, object>
            {
                ["count"] = issues.Count,
                ["issues"] = issues
            });
        }

        private static List<Dictionary<string, object>> FindColliderIssues(bool includeInactive)
        {
            var issues = new List<Dictionary<string, object>>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var triggersWithRB = new List<string>();
            var scaledColliders = new List<string>();

            foreach (var go in allObjects)
            {
                try
                {
                    var colliders = go.GetComponents<Collider>();
                    var rb = go.GetComponent<Rigidbody>();

                    foreach (var col in colliders)
                    {
                        if (col == null) continue;

                        if (col.isTrigger && rb != null && !rb.isKinematic)
                            triggersWithRB.Add(GetGameObjectPath(go));

                        Vector3 scale = go.transform.lossyScale;
                        if (Mathf.Abs(scale.x - scale.y) > 0.01f || Mathf.Abs(scale.y - scale.z) > 0.01f)
                        {
                            if (col is SphereCollider || col is CapsuleCollider)
                                scaledColliders.Add(GetGameObjectPath(go));
                        }
                    }
                }
                catch { }
            }

            if (triggersWithRB.Count > 0)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Trigger with non-kinematic Rigidbody",
                    ["severity"] = "WARNING",
                    ["count"] = triggersWithRB.Count,
                    ["objects"] = triggersWithRB.Take(10).ToList()
                });
            }

            if (scaledColliders.Count > 0)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Non-uniform scale on primitive colliders",
                    ["severity"] = "WARNING",
                    ["count"] = scaledColliders.Count,
                    ["objects"] = scaledColliders.Take(10).ToList()
                });
            }

            return issues;
        }

        private static object ScanDuplicateNames(bool includeInactive)
        {
            var duplicates = FindDuplicateNames(includeInactive);
            return new SuccessResponse($"Found {duplicates.Count} groups with duplicate names", new Dictionary<string, object>
            {
                ["count"] = duplicates.Count,
                ["groups"] = duplicates
            });
        }

        private static List<Dictionary<string, object>> FindDuplicateNames(bool includeInactive)
        {
            var result = new List<Dictionary<string, object>>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            // Use int (instanceID) as key since Transform can be null for root objects
            var siblingGroups = new Dictionary<int, Dictionary<string, List<GameObject>>>();
            var parentNames = new Dictionary<int, string>();
            const int ROOT_KEY = 0;

            foreach (var go in allObjects)
            {
                try
                {
                    Transform parent = go.transform.parent;
                    int parentKey = parent != null ? parent.GetInstanceID() : ROOT_KEY;
                    string parentName = parent != null ? parent.name : "(Root)";

                    if (!siblingGroups.ContainsKey(parentKey))
                    {
                        siblingGroups[parentKey] = new Dictionary<string, List<GameObject>>();
                        parentNames[parentKey] = parentName;
                    }

                    if (!siblingGroups[parentKey].ContainsKey(go.name))
                        siblingGroups[parentKey][go.name] = new List<GameObject>();

                    siblingGroups[parentKey][go.name].Add(go);
                }
                catch { }
            }

            foreach (var parentGroup in siblingGroups)
            {
                foreach (var nameGroup in parentGroup.Value)
                {
                    if (nameGroup.Value.Count > 1)
                    {
                        result.Add(new Dictionary<string, object>
                        {
                            ["name"] = nameGroup.Key,
                            ["parent"] = parentNames.ContainsKey(parentGroup.Key) ? parentNames[parentGroup.Key] : "(Unknown)",
                            ["count"] = nameGroup.Value.Count
                        });
                    }
                }
            }

            return result;
        }

        private static object ScanMissingReferences(bool includeInactive)
        {
            var missing = FindMissingReferences(includeInactive);
            return new SuccessResponse($"Found {missing.Count} missing references", new Dictionary<string, object>
            {
                ["count"] = missing.Count,
                ["references"] = missing
            });
        }

        private static List<Dictionary<string, object>> FindMissingReferences(bool includeInactive)
        {
            var result = new List<Dictionary<string, object>>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    foreach (var component in components)
                    {
                        if (component == null) continue;

                        var so = new SerializedObject(component);
                        var sp = so.GetIterator();

                        while (sp.NextVisible(true))
                        {
                            if (sp.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                                {
                                    result.Add(new Dictionary<string, object>
                                    {
                                        ["object"] = GetGameObjectPath(go),
                                        ["component"] = component.GetType().Name,
                                        ["property"] = sp.displayName
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private static object CheckLayersAndTags()
        {
            var result = new Dictionary<string, object>();
            result["tags"] = UnityEditorInternal.InternalEditorUtility.tags;

            var layers = new List<Dictionary<string, object>>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["name"] = layerName
                    });
                }
            }
            result["layers"] = layers;

            return new SuccessResponse("Layers and tags retrieved", result);
        }

        private static object CheckPerformanceIssues(bool includeInactive)
        {
            var issues = FindPerformanceIssues(includeInactive);
            return new SuccessResponse($"Found {issues.Count} performance concerns", new Dictionary<string, object>
            {
                ["count"] = issues.Count,
                ["issues"] = issues
            });
        }

        private static List<Dictionary<string, object>> FindPerformanceIssues(bool includeInactive)
        {
            var issues = new List<Dictionary<string, object>>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var heavyObjects = new List<string>();
            int totalComponents = 0;
            int meshRenderers = 0;
            int lights = 0;

            foreach (var go in allObjects)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    totalComponents += components.Length;

                    if (components.Length > 10)
                        heavyObjects.Add($"{GetGameObjectPath(go)} ({components.Length} components)");

                    if (go.GetComponent<MeshRenderer>() != null)
                        meshRenderers++;
                    if (go.GetComponent<Light>() != null)
                        lights++;
                }
                catch { }
            }

            if (heavyObjects.Count > 0)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Objects with many components (>10)",
                    ["severity"] = "INFO",
                    ["count"] = heavyObjects.Count,
                    ["objects"] = heavyObjects.Take(10).ToList()
                });
            }

            if (lights > 8)
            {
                issues.Add(new Dictionary<string, object>
                {
                    ["type"] = "Many realtime lights",
                    ["severity"] = "WARNING",
                    ["count"] = lights
                });
            }

            issues.Add(new Dictionary<string, object>
            {
                ["type"] = "Scene Statistics",
                ["severity"] = "INFO",
                ["data"] = new Dictionary<string, object>
                {
                    ["total_objects"] = allObjects.Length,
                    ["total_components"] = totalComponents,
                    ["mesh_renderers"] = meshRenderers,
                    ["lights"] = lights
                }
            });

            return issues;
        }

        private static object ValidatePrefabs()
        {
            var results = new Dictionary<string, object>();
            var issues = new List<Dictionary<string, object>>();

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int validCount = 0;
            int invalidCount = 0;

            foreach (string guid in prefabGuids.Take(100))
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab == null)
                    {
                        invalidCount++;
                        continue;
                    }

                    var components = prefab.GetComponentsInChildren<Component>(true);
                    bool hasMissing = components.Any(c => c == null);

                    if (hasMissing)
                    {
                        invalidCount++;
                        issues.Add(new Dictionary<string, object>
                        {
                            ["path"] = path,
                            ["issue"] = "Contains missing scripts"
                        });
                    }
                    else
                    {
                        validCount++;
                    }
                }
                catch { invalidCount++; }
            }

            results["total_checked"] = prefabGuids.Length > 100 ? 100 : prefabGuids.Length;
            results["total_prefabs"] = prefabGuids.Length;
            results["valid"] = validCount;
            results["invalid"] = invalidCount;
            results["issues"] = issues;

            return new SuccessResponse($"Validated {results["total_checked"]} prefabs", results);
        }

        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "(null)";

            var sb = new StringBuilder();
            Transform current = go.transform;

            while (current != null)
            {
                if (sb.Length > 0)
                    sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }

            return sb.ToString();
        }
    }
}
