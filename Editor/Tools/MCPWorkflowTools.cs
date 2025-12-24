#nullable disable
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    #region 1. Transform Tools - Align, Distribute, Snap
    /// <summary>
    /// Advanced transform operations for multiple objects
    /// </summary>
    [McpForUnityTool(
        name: "transform_tools",
        Description = "Advanced transform operations. Actions: align, distribute, snap_to_grid, reset_transform, match_transform, randomize, center_pivot")]
    public static class MCPTransformTools
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "align":
                    return AlignObjects(@params);
                case "distribute":
                    return DistributeObjects(@params);
                case "snap_to_grid":
                    return SnapToGrid(@params);
                case "reset_transform":
                    return ResetTransform(@params);
                case "match_transform":
                    return MatchTransform(@params);
                case "randomize":
                    return RandomizeTransform(@params);
                case "center_pivot":
                    return CenterPivot(@params);
                default:
                    return new ErrorResponse($"Unknown action: {action}. Available: align, distribute, snap_to_grid, reset_transform, match_transform, randomize, center_pivot");
            }
        }

        private static object AlignObjects(JObject @params)
        {
            string axis = @params["axis"]?.ToString()?.ToLower() ?? "x";
            string alignTo = @params["align_to"]?.ToString()?.ToLower() ?? "first"; // first, last, center, min, max
            var names = @params["objects"]?.ToObject<string[]>();

            if (names == null || names.Length < 2)
                return new ErrorResponse("Need at least 2 object names in 'objects' array");

            var objects = names.Select(n => GameObject.Find(n)).Where(g => g != null).ToList();
            if (objects.Count < 2)
                return new ErrorResponse("Could not find enough objects");

            float targetValue = 0;
            switch (alignTo)
            {
                case "first": targetValue = GetAxisValue(objects[0].transform.position, axis); break;
                case "last": targetValue = GetAxisValue(objects[objects.Count - 1].transform.position, axis); break;
                case "center": targetValue = objects.Average(o => GetAxisValue(o.transform.position, axis)); break;
                case "min": targetValue = objects.Min(o => GetAxisValue(o.transform.position, axis)); break;
                case "max": targetValue = objects.Max(o => GetAxisValue(o.transform.position, axis)); break;
            }

            Undo.RecordObjects(objects.Select(o => o.transform).ToArray(), "Align Objects");

            foreach (var obj in objects)
            {
                Vector3 pos = obj.transform.position;
                pos = SetAxisValue(pos, axis, targetValue);
                obj.transform.position = pos;
            }

            return new SuccessResponse($"Aligned {objects.Count} objects on {axis.ToUpper()} axis to {alignTo}");
        }

        private static object DistributeObjects(JObject @params)
        {
            string axis = @params["axis"]?.ToString()?.ToLower() ?? "x";
            var names = @params["objects"]?.ToObject<string[]>();

            if (names == null || names.Length < 3)
                return new ErrorResponse("Need at least 3 object names to distribute");

            var objects = names.Select(n => GameObject.Find(n)).Where(g => g != null)
                .OrderBy(o => GetAxisValue(o.transform.position, axis)).ToList();

            if (objects.Count < 3)
                return new ErrorResponse("Could not find enough objects");

            float minVal = GetAxisValue(objects[0].transform.position, axis);
            float maxVal = GetAxisValue(objects[objects.Count - 1].transform.position, axis);
            float step = (maxVal - minVal) / (objects.Count - 1);

            Undo.RecordObjects(objects.Select(o => o.transform).ToArray(), "Distribute Objects");

            for (int i = 0; i < objects.Count; i++)
            {
                Vector3 pos = objects[i].transform.position;
                pos = SetAxisValue(pos, axis, minVal + step * i);
                objects[i].transform.position = pos;
            }

            return new SuccessResponse($"Distributed {objects.Count} objects evenly on {axis.ToUpper()} axis");
        }

        private static object SnapToGrid(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            float gridSize = @params["grid_size"]?.ToObject<float>() ?? 1f;

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            Undo.RecordObject(obj.transform, "Snap to Grid");

            Vector3 pos = obj.transform.position;
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            obj.transform.position = pos;

            return new SuccessResponse($"Snapped '{objectName}' to grid (size: {gridSize})", new { position = new[] { pos.x, pos.y, pos.z } });
        }

        private static object ResetTransform(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            bool resetPosition = @params["position"]?.ToObject<bool>() ?? true;
            bool resetRotation = @params["rotation"]?.ToObject<bool>() ?? true;
            bool resetScale = @params["scale"]?.ToObject<bool>() ?? true;

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            Undo.RecordObject(obj.transform, "Reset Transform");

            if (resetPosition) obj.transform.localPosition = Vector3.zero;
            if (resetRotation) obj.transform.localRotation = Quaternion.identity;
            if (resetScale) obj.transform.localScale = Vector3.one;

            return new SuccessResponse($"Reset transform of '{objectName}'");
        }

        private static object MatchTransform(JObject @params)
        {
            string sourceName = @params["source"]?.ToString();
            string targetName = @params["target"]?.ToString();
            bool matchPosition = @params["position"]?.ToObject<bool>() ?? true;
            bool matchRotation = @params["rotation"]?.ToObject<bool>() ?? true;
            bool matchScale = @params["scale"]?.ToObject<bool>() ?? false;

            var source = GameObject.Find(sourceName);
            var target = GameObject.Find(targetName);
            if (source == null) return new ErrorResponse($"Source '{sourceName}' not found");
            if (target == null) return new ErrorResponse($"Target '{targetName}' not found");

            Undo.RecordObject(target.transform, "Match Transform");

            if (matchPosition) target.transform.position = source.transform.position;
            if (matchRotation) target.transform.rotation = source.transform.rotation;
            if (matchScale) target.transform.localScale = source.transform.localScale;

            return new SuccessResponse($"Matched '{targetName}' transform to '{sourceName}'");
        }

        private static object RandomizeTransform(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            var posRange = @params["position_range"]?.ToObject<float[]>();
            var rotRange = @params["rotation_range"]?.ToObject<float[]>();
            var scaleRange = @params["scale_range"]?.ToObject<float[]>();

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            Undo.RecordObject(obj.transform, "Randomize Transform");

            if (posRange != null && posRange.Length >= 2)
            {
                obj.transform.position += new Vector3(
                    UnityEngine.Random.Range(-posRange[0], posRange[0]),
                    UnityEngine.Random.Range(-posRange[1], posRange[1]),
                    posRange.Length > 2 ? UnityEngine.Random.Range(-posRange[2], posRange[2]) : 0
                );
            }

            if (rotRange != null && rotRange.Length >= 1)
            {
                obj.transform.rotation *= Quaternion.Euler(
                    UnityEngine.Random.Range(-rotRange[0], rotRange[0]),
                    rotRange.Length > 1 ? UnityEngine.Random.Range(-rotRange[1], rotRange[1]) : 0,
                    rotRange.Length > 2 ? UnityEngine.Random.Range(-rotRange[2], rotRange[2]) : 0
                );
            }

            if (scaleRange != null && scaleRange.Length >= 2)
            {
                float scale = UnityEngine.Random.Range(scaleRange[0], scaleRange[1]);
                obj.transform.localScale = Vector3.one * scale;
            }

            return new SuccessResponse($"Randomized transform of '{objectName}'");
        }

        private static object CenterPivot(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new ErrorResponse("No renderers found to calculate center");

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            Vector3 center = bounds.center;
            Vector3 offset = obj.transform.position - center;

            Undo.RecordObject(obj.transform, "Center Pivot");
            foreach (Transform child in obj.transform)
            {
                Undo.RecordObject(child, "Center Pivot");
                child.position += offset;
            }
            obj.transform.position = center;

            return new SuccessResponse($"Centered pivot of '{objectName}'", new { center = new[] { center.x, center.y, center.z } });
        }

        private static float GetAxisValue(Vector3 v, string axis) => axis switch { "x" => v.x, "y" => v.y, "z" => v.z, _ => v.x };
        private static Vector3 SetAxisValue(Vector3 v, string axis, float val) { switch (axis) { case "x": v.x = val; break; case "y": v.y = val; break; case "z": v.z = val; break; } return v; }
    }
    #endregion

    #region 2. Hierarchy Organizer - Group, Sort, Cleanup
    [McpForUnityTool(
        name: "hierarchy_organizer",
        Description = "Organize hierarchy. Actions: group_by_type, group_by_name, sort_children, create_folder, move_to_folder, flatten, remove_empty")]
    public static class MCPHierarchyOrganizer
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "group_by_type":
                    return GroupByType(@params);
                case "group_by_name":
                    return GroupByName(@params);
                case "sort_children":
                    return SortChildren(@params);
                case "create_folder":
                    return CreateFolder(@params);
                case "move_to_folder":
                    return MoveToFolder(@params);
                case "flatten":
                    return FlattenHierarchy(@params);
                case "remove_empty":
                    return RemoveEmpty();
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object GroupByType(JObject @params)
        {
            var types = new Dictionary<string, List<GameObject>>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.transform.parent != null) continue; // Only root objects

                string typeName = "Other";
                if (go.GetComponent<Camera>()) typeName = "Cameras";
                else if (go.GetComponent<Light>()) typeName = "Lights";
                else if (go.GetComponent<Canvas>()) typeName = "UI";
                else if (go.GetComponent<ParticleSystem>()) typeName = "Particles";
                else if (go.GetComponent<AudioSource>()) typeName = "Audio";
                else if (go.isStatic) typeName = "Static";

                if (!types.ContainsKey(typeName)) types[typeName] = new List<GameObject>();
                types[typeName].Add(go);
            }

            int movedCount = 0;
            foreach (var kvp in types)
            {
                if (kvp.Value.Count < 2) continue;

                var folder = new GameObject($"--- {kvp.Key} ---");
                Undo.RegisterCreatedObjectUndo(folder, "Group by Type");

                foreach (var go in kvp.Value)
                {
                    Undo.SetTransformParent(go.transform, folder.transform, "Group by Type");
                    movedCount++;
                }
            }

            return new SuccessResponse($"Grouped {movedCount} objects into {types.Count} categories");
        }

        private static object GroupByName(JObject @params)
        {
            string prefix = @params["prefix"]?.ToString();
            string folderName = @params["folder"]?.ToString() ?? $"Group_{prefix}";

            if (string.IsNullOrEmpty(prefix))
                return new ErrorResponse("prefix parameter required");

            var matches = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && g.transform.parent == null)
                .ToList();

            if (matches.Count == 0)
                return new ErrorResponse($"No root objects found with prefix '{prefix}'");

            var folder = new GameObject(folderName);
            Undo.RegisterCreatedObjectUndo(folder, "Group by Name");

            foreach (var go in matches)
                Undo.SetTransformParent(go.transform, folder.transform, "Group by Name");

            return new SuccessResponse($"Grouped {matches.Count} objects with prefix '{prefix}' into '{folderName}'");
        }

        private static object SortChildren(JObject @params)
        {
            string parentName = @params["parent"]?.ToString();
            string sortBy = @params["sort_by"]?.ToString()?.ToLower() ?? "name"; // name, position_x, position_y, position_z

            var parent = string.IsNullOrEmpty(parentName) ? null : GameObject.Find(parentName)?.transform;
            var children = parent != null
                ? Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i).gameObject).ToList()
                : UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g.transform.parent == null).ToList();

            IOrderedEnumerable<GameObject> sorted = sortBy switch
            {
                "position_x" => children.OrderBy(g => g.transform.position.x),
                "position_y" => children.OrderBy(g => g.transform.position.y),
                "position_z" => children.OrderBy(g => g.transform.position.z),
                _ => children.OrderBy(g => g.name)
            };

            int index = 0;
            foreach (var go in sorted)
            {
                Undo.RecordObject(go.transform, "Sort Children");
                go.transform.SetSiblingIndex(index++);
            }

            return new SuccessResponse($"Sorted {children.Count} objects by {sortBy}");
        }

        private static object CreateFolder(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "New Folder";
            var folder = new GameObject($"--- {name} ---");
            Undo.RegisterCreatedObjectUndo(folder, "Create Folder");
            Selection.activeGameObject = folder;
            return new SuccessResponse($"Created folder '{name}'");
        }

        private static object MoveToFolder(JObject @params)
        {
            string folderName = @params["folder"]?.ToString();
            var objectNames = @params["objects"]?.ToObject<string[]>();

            if (string.IsNullOrEmpty(folderName) || objectNames == null)
                return new ErrorResponse("folder and objects parameters required");

            var folder = GameObject.Find(folderName);
            if (folder == null)
            {
                folder = new GameObject(folderName);
                Undo.RegisterCreatedObjectUndo(folder, "Create Folder");
            }

            int moved = 0;
            foreach (var name in objectNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.SetTransformParent(obj.transform, folder.transform, "Move to Folder");
                    moved++;
                }
            }

            return new SuccessResponse($"Moved {moved} objects to '{folderName}'");
        }

        private static object FlattenHierarchy(JObject @params)
        {
            string parentName = @params["parent"]?.ToString();
            var parent = GameObject.Find(parentName);
            if (parent == null) return new ErrorResponse($"Parent '{parentName}' not found");

            var allChildren = parent.GetComponentsInChildren<Transform>(true)
                .Where(t => t != parent.transform)
                .ToList();

            foreach (var child in allChildren)
            {
                Undo.SetTransformParent(child, parent.transform, "Flatten");
            }

            return new SuccessResponse($"Flattened {allChildren.Count} objects under '{parentName}'");
        }

        private static object RemoveEmpty()
        {
            var emptyObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.transform.childCount == 0 && g.GetComponents<Component>().Length == 1) // Only Transform
                .ToList();

            foreach (var go in emptyObjects)
                Undo.DestroyObjectImmediate(go);

            return new SuccessResponse($"Removed {emptyObjects.Count} empty GameObjects");
        }
    }
    #endregion

    #region 3. Tag & Layer Manager
    [McpForUnityTool(
        name: "tag_layer_manager",
        Description = "Manage tags and layers. Actions: list_tags, list_layers, add_tag, add_layer, set_tag, set_layer, find_by_tag, find_by_layer")]
    public static class MCPTagLayerManager
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "list_tags":
                    return new SuccessResponse("Tags", new { tags = UnityEditorInternal.InternalEditorUtility.tags });
                case "list_layers":
                    var layers = new List<object>();
                    for (int i = 0; i < 32; i++)
                    {
                        string name = LayerMask.LayerToName(i);
                        if (!string.IsNullOrEmpty(name))
                            layers.Add(new { index = i, name });
                    }
                    return new SuccessResponse("Layers", new { layers });
                case "add_tag":
                    return AddTag(@params["tag"]?.ToString());
                case "add_layer":
                    return AddLayer(@params["layer"]?.ToString());
                case "set_tag":
                    return SetTag(@params);
                case "set_layer":
                    return SetLayer(@params);
                case "find_by_tag":
                    return FindByTag(@params["tag"]?.ToString());
                case "find_by_layer":
                    return FindByLayer(@params);
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object AddTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new ErrorResponse("tag parameter required");

            var tags = UnityEditorInternal.InternalEditorUtility.tags.ToList();
            if (tags.Contains(tag))
                return new ErrorResponse($"Tag '{tag}' already exists");

            UnityEditorInternal.InternalEditorUtility.AddTag(tag);
            return new SuccessResponse($"Added tag '{tag}'");
        }

        private static object AddLayer(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return new ErrorResponse("layer parameter required");

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < 32; i++) // User layers start at 8
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layer;
                    tagManager.ApplyModifiedProperties();
                    return new SuccessResponse($"Added layer '{layer}' at index {i}");
                }
            }

            return new ErrorResponse("No empty layer slots available");
        }

        private static object SetTag(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            string tag = @params["tag"]?.ToString();

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            try
            {
                Undo.RecordObject(obj, "Set Tag");
                obj.tag = tag;
                return new SuccessResponse($"Set tag of '{objectName}' to '{tag}'");
            }
            catch
            {
                return new ErrorResponse($"Invalid tag '{tag}'");
            }
        }

        private static object SetLayer(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            var layerParam = @params["layer"];
            bool recursive = @params["recursive"]?.ToObject<bool>() ?? false;

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            int layer = layerParam.Type == JTokenType.Integer
                ? layerParam.ToObject<int>()
                : LayerMask.NameToLayer(layerParam.ToString());

            if (layer < 0) return new ErrorResponse($"Invalid layer");

            if (recursive)
            {
                foreach (var t in obj.GetComponentsInChildren<Transform>(true))
                {
                    Undo.RecordObject(t.gameObject, "Set Layer");
                    t.gameObject.layer = layer;
                }
            }
            else
            {
                Undo.RecordObject(obj, "Set Layer");
                obj.layer = layer;
            }

            return new SuccessResponse($"Set layer of '{objectName}' to {layer} ({LayerMask.LayerToName(layer)})");
        }

        private static object FindByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new ErrorResponse("tag parameter required");

            try
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                return new SuccessResponse($"Found {objects.Length} objects with tag '{tag}'", new
                {
                    count = objects.Length,
                    objects = objects.Select(o => o.name).ToArray()
                });
            }
            catch
            {
                return new ErrorResponse($"Invalid tag '{tag}'");
            }
        }

        private static object FindByLayer(JObject @params)
        {
            var layerParam = @params["layer"];
            int layer = layerParam.Type == JTokenType.Integer
                ? layerParam.ToObject<int>()
                : LayerMask.NameToLayer(layerParam.ToString());

            var objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.layer == layer)
                .ToArray();

            return new SuccessResponse($"Found {objects.Length} objects on layer {layer}", new
            {
                count = objects.Length,
                layer = LayerMask.LayerToName(layer),
                objects = objects.Select(o => o.name).ToArray()
            });
        }
    }
    #endregion

    #region 4. Component Search - Find all instances
    [McpForUnityTool(
        name: "component_search",
        Description = "Search for components. Actions: find_all, find_missing_scripts, count_by_type, get_component_types")]
    public static class MCPComponentSearch
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "find_all":
                    return FindAllOfType(@params);
                case "find_missing_scripts":
                    return FindMissingScripts();
                case "count_by_type":
                    return CountByType();
                case "get_component_types":
                    return GetComponentTypes(@params);
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object FindAllOfType(JObject @params)
        {
            string typeName = @params["type"]?.ToString();
            bool includeInactive = @params["include_inactive"]?.ToObject<bool>() ?? true;

            if (string.IsNullOrEmpty(typeName))
                return new ErrorResponse("type parameter required");

            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) && typeof(Component).IsAssignableFrom(t));

            if (type == null)
                return new ErrorResponse($"Component type '{typeName}' not found");

            var components = includeInactive
                ? UnityEngine.Resources.FindObjectsOfTypeAll(type).Cast<Component>().Where(c => c.gameObject.scene.isLoaded)
                : UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Cast<Component>();

            var results = components.Select(c => new
            {
                gameObject = c.gameObject.name,
                path = GetGameObjectPath(c.gameObject),
                enabled = (c is Behaviour b) ? b.enabled : true
            }).ToArray();

            return new SuccessResponse($"Found {results.Length} {typeName} components", new
            {
                type = typeName,
                count = results.Length,
                instances = results
            });
        }

        private static object FindMissingScripts()
        {
            var missing = new List<object>();

            foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.isLoaded) continue;

                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        missing.Add(new
                        {
                            gameObject = go.name,
                            path = GetGameObjectPath(go),
                            componentIndex = i
                        });
                    }
                }
            }

            return new SuccessResponse($"Found {missing.Count} missing scripts", new { missing });
        }

        private static object CountByType()
        {
            var counts = new Dictionary<string, int>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    counts[typeName] = counts.GetValueOrDefault(typeName, 0) + 1;
                }
            }

            var sorted = counts.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { type = kvp.Key, count = kvp.Value })
                .ToArray();

            return new SuccessResponse($"Component type counts", new
            {
                totalTypes = sorted.Length,
                types = sorted
            });
        }

        private static object GetComponentTypes(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            var types = obj.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => new
                {
                    type = c.GetType().Name,
                    fullType = c.GetType().FullName,
                    enabled = (c is Behaviour b) ? b.enabled : true
                })
                .ToArray();

            return new SuccessResponse($"Components on '{objectName}'", new { components = types });
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
    #endregion

    #region 5. PlayerPrefs Editor
    [McpForUnityTool(
        name: "playerprefs_editor",
        Description = "Edit PlayerPrefs for debugging. Actions: get, set, delete, delete_all, list_keys")]
    public static class MCPPlayerPrefsEditor
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get":
                    return GetPref(@params);
                case "set":
                    return SetPref(@params);
                case "delete":
                    string key = @params["key"]?.ToString();
                    if (string.IsNullOrEmpty(key)) return new ErrorResponse("key required");
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    return new SuccessResponse($"Deleted key '{key}'");
                case "delete_all":
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    return new SuccessResponse("Deleted all PlayerPrefs");
                case "list_keys":
                    return new ErrorResponse("PlayerPrefs doesn't support listing keys. Use EditorPrefs for editor-only prefs.");
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object GetPref(JObject @params)
        {
            string key = @params["key"]?.ToString();
            string type = @params["type"]?.ToString()?.ToLower() ?? "string";

            if (string.IsNullOrEmpty(key))
                return new ErrorResponse("key parameter required");

            if (!PlayerPrefs.HasKey(key))
                return new ErrorResponse($"Key '{key}' not found");

            object value = type switch
            {
                "int" => PlayerPrefs.GetInt(key),
                "float" => PlayerPrefs.GetFloat(key),
                _ => PlayerPrefs.GetString(key)
            };

            return new SuccessResponse($"PlayerPref '{key}'", new { key, value, type });
        }

        private static object SetPref(JObject @params)
        {
            string key = @params["key"]?.ToString();
            var value = @params["value"];
            string type = @params["type"]?.ToString()?.ToLower() ?? "string";

            if (string.IsNullOrEmpty(key) || value == null)
                return new ErrorResponse("key and value required");

            switch (type)
            {
                case "int":
                    PlayerPrefs.SetInt(key, value.ToObject<int>());
                    break;
                case "float":
                    PlayerPrefs.SetFloat(key, value.ToObject<float>());
                    break;
                default:
                    PlayerPrefs.SetString(key, value.ToString());
                    break;
            }

            PlayerPrefs.Save();
            return new SuccessResponse($"Set PlayerPref '{key}' = {value}");
        }
    }
    #endregion

    #region 6. Quick Notes - Scene Context for AI
    [McpForUnityTool(
        name: "quick_notes",
        Description = "Add notes/context to scene for AI understanding. Actions: add, get, list, clear, add_to_object")]
    public static class MCPQuickNotes
    {
        private static Dictionary<string, string> sceneNotes = new Dictionary<string, string>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "add":
                    string noteKey = @params["key"]?.ToString() ?? DateTime.Now.ToString("HHmmss");
                    string noteValue = @params["note"]?.ToString();
                    if (string.IsNullOrEmpty(noteValue)) return new ErrorResponse("note required");
                    sceneNotes[noteKey] = noteValue;
                    return new SuccessResponse($"Added note '{noteKey}'");

                case "get":
                    string getKey = @params["key"]?.ToString();
                    if (sceneNotes.TryGetValue(getKey, out string note))
                        return new SuccessResponse($"Note '{getKey}'", new { key = getKey, note });
                    return new ErrorResponse($"Note '{getKey}' not found");

                case "list":
                    return new SuccessResponse("Scene notes", new { notes = sceneNotes });

                case "clear":
                    sceneNotes.Clear();
                    return new SuccessResponse("Cleared all notes");

                case "add_to_object":
                    return AddNoteToObject(@params);

                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object AddNoteToObject(JObject @params)
        {
            string objectName = @params["object"]?.ToString();
            string note = @params["note"]?.ToString();

            var obj = GameObject.Find(objectName);
            if (obj == null) return new ErrorResponse($"Object '{objectName}' not found");

            // Store as a hidden comment component or in the object name
            sceneNotes[$"obj:{objectName}"] = note;
            return new SuccessResponse($"Added note to '{objectName}': {note}");
        }
    }
    #endregion

    #region 7. Duplicate Finder
    [McpForUnityTool(
        name: "duplicate_finder",
        Description = "Find duplicates in scene/project. Actions: find_duplicate_names, find_duplicate_materials, find_overlapping, find_identical_transforms")]
    public static class MCPDuplicateFinder
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "find_duplicate_names":
                    return FindDuplicateNames();
                case "find_duplicate_materials":
                    return FindDuplicateMaterials();
                case "find_overlapping":
                    return FindOverlapping(@params);
                case "find_identical_transforms":
                    return FindIdenticalTransforms();
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object FindDuplicateNames()
        {
            var nameCounts = new Dictionary<string, List<string>>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!nameCounts.ContainsKey(go.name))
                    nameCounts[go.name] = new List<string>();
                nameCounts[go.name].Add(GetPath(go));
            }

            var duplicates = nameCounts.Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new { name = kvp.Key, count = kvp.Value.Count, paths = kvp.Value })
                .OrderByDescending(d => d.count)
                .ToArray();

            return new SuccessResponse($"Found {duplicates.Length} duplicate names", new { duplicates });
        }

        private static object FindDuplicateMaterials()
        {
            var matCounts = new Dictionary<string, int>();
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string name = mat.name;
                    matCounts[name] = matCounts.GetValueOrDefault(name, 0) + 1;
                }
            }

            var sorted = matCounts.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { material = kvp.Key, usageCount = kvp.Value })
                .ToArray();

            return new SuccessResponse($"Material usage", new { materials = sorted });
        }

        private static object FindOverlapping(JObject @params)
        {
            float threshold = @params["threshold"]?.ToObject<float>() ?? 0.01f;
            var objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.GetComponent<Renderer>() != null)
                .ToList();

            var overlapping = new List<object>();

            for (int i = 0; i < objects.Count; i++)
            {
                for (int j = i + 1; j < objects.Count; j++)
                {
                    float dist = Vector3.Distance(objects[i].transform.position, objects[j].transform.position);
                    if (dist < threshold)
                    {
                        overlapping.Add(new
                        {
                            object1 = objects[i].name,
                            object2 = objects[j].name,
                            distance = dist
                        });
                    }
                }
            }

            return new SuccessResponse($"Found {overlapping.Count} overlapping object pairs", new { threshold, overlapping });
        }

        private static object FindIdenticalTransforms()
        {
            var transforms = new Dictionary<string, List<string>>();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                string key = $"{go.transform.position}|{go.transform.rotation.eulerAngles}|{go.transform.localScale}";
                if (!transforms.ContainsKey(key))
                    transforms[key] = new List<string>();
                transforms[key].Add(go.name);
            }

            var duplicates = transforms.Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new { objects = kvp.Value, count = kvp.Value.Count })
                .ToArray();

            return new SuccessResponse($"Found {duplicates.Length} groups with identical transforms", new { duplicates });
        }

        private static string GetPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }
    }
    #endregion

    #region 8. Batch Operations
    [McpForUnityTool(
        name: "batch_operations",
        Description = "Batch operations on multiple objects. Actions: enable_all, disable_all, delete_by_name, replace_component, set_property_all")]
    public static class MCPBatchOperations
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "enable_all":
                    return SetActiveByPattern(@params, true);
                case "disable_all":
                    return SetActiveByPattern(@params, false);
                case "delete_by_name":
                    return DeleteByName(@params);
                case "replace_component":
                    return ReplaceComponent(@params);
                case "set_property_all":
                    return SetPropertyAll(@params);
                default:
                    return new ErrorResponse($"Unknown action: {action}");
            }
        }

        private static object SetActiveByPattern(JObject @params, bool active)
        {
            string pattern = @params["pattern"]?.ToString();
            if (string.IsNullOrEmpty(pattern)) return new ErrorResponse("pattern required");

            var matches = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var go in matches)
            {
                Undo.RecordObject(go, active ? "Enable" : "Disable");
                go.SetActive(active);
            }

            return new SuccessResponse($"{(active ? "Enabled" : "Disabled")} {matches.Count} objects matching '{pattern}'");
        }

        private static object DeleteByName(JObject @params)
        {
            string pattern = @params["pattern"]?.ToString();
            bool confirm = @params["confirm"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(pattern)) return new ErrorResponse("pattern required");

            var matches = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(g => g.name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!confirm)
                return new SuccessResponse($"Would delete {matches.Count} objects. Set confirm=true to proceed.", new { count = matches.Count, objects = matches.Select(g => g.name).ToArray() });

            foreach (var go in matches)
                Undo.DestroyObjectImmediate(go);

            return new SuccessResponse($"Deleted {matches.Count} objects matching '{pattern}'");
        }

        private static object ReplaceComponent(JObject @params)
        {
            string oldType = @params["old_type"]?.ToString();
            string newType = @params["new_type"]?.ToString();

            if (string.IsNullOrEmpty(oldType) || string.IsNullOrEmpty(newType))
                return new ErrorResponse("old_type and new_type required");

            Type oldT = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == oldType && typeof(Component).IsAssignableFrom(t));

            Type newT = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == newType && typeof(Component).IsAssignableFrom(t));

            if (oldT == null || newT == null)
                return new ErrorResponse("Could not find component types");

            var components = UnityEngine.Object.FindObjectsByType(oldT, FindObjectsSortMode.None).Cast<Component>().ToList();
            int replaced = 0;

            foreach (var comp in components)
            {
                var go = comp.gameObject;
                Undo.DestroyObjectImmediate(comp);
                Undo.AddComponent(go, newT);
                replaced++;
            }

            return new SuccessResponse($"Replaced {replaced} {oldType} components with {newType}");
        }

        private static object SetPropertyAll(JObject @params)
        {
            string componentType = @params["component"]?.ToString();
            string propertyName = @params["property"]?.ToString();
            var value = @params["value"];

            if (string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(propertyName) || value == null)
                return new ErrorResponse("component, property, and value required");

            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == componentType && typeof(Component).IsAssignableFrom(t));

            if (type == null) return new ErrorResponse($"Component type '{componentType}' not found");

            var components = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None).Cast<Component>().ToList();
            int modified = 0;

            foreach (var comp in components)
            {
                var so = new SerializedObject(comp);
                var prop = so.FindProperty(propertyName);
                if (prop != null)
                {
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Float: prop.floatValue = value.ToObject<float>(); break;
                        case SerializedPropertyType.Integer: prop.intValue = value.ToObject<int>(); break;
                        case SerializedPropertyType.Boolean: prop.boolValue = value.ToObject<bool>(); break;
                        case SerializedPropertyType.String: prop.stringValue = value.ToString(); break;
                    }
                    so.ApplyModifiedProperties();
                    modified++;
                }
            }

            return new SuccessResponse($"Modified '{propertyName}' on {modified} {componentType} components");
        }
    }
    #endregion
}
