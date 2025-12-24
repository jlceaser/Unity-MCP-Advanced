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
    #region 1. Select Objects Tool
    [McpForUnityTool(
        name: "select_objects",
        Description = "Selects objects in Unity Hierarchy. Params: names (array), searchMethod (by_name/by_tag/by_component), additive (bool)")]
    public static class MCPSelectObjects
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var namesToken = @params["names"];
                string searchMethod = @params["searchMethod"]?.ToString()?.ToLower() ?? "by_name";
                bool additive = @params["additive"]?.ToObject<bool>() ?? false;
                string singleName = @params["name"]?.ToString();

                List<GameObject> toSelect = new List<GameObject>();

                // Handle single name or array
                string[] names;
                if (namesToken != null)
                {
                    names = namesToken.ToObject<string[]>();
                }
                else if (!string.IsNullOrEmpty(singleName))
                {
                    names = new[] { singleName };
                }
                else
                {
                    return new ErrorResponse("Required: names (array) or name (string)");
                }

                foreach (var name in names)
                {
                    GameObject[] found = null;
                    switch (searchMethod)
                    {
                        case "by_tag":
                            try { found = GameObject.FindGameObjectsWithTag(name); } catch { }
                            break;
                        case "by_component":
                            var type = GetTypeByName(name);
                            if (type != null)
                            {
                                found = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None)
                                    .Select(c => (c as Component)?.gameObject)
                                    .Where(g => g != null).ToArray();
                            }
                            break;
                        case "by_name":
                        default:
                            var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                            found = allObjects.Where(g => g.name.Contains(name) && g.scene.isLoaded).ToArray();
                            break;
                    }

                    if (found != null)
                        toSelect.AddRange(found);
                }

                if (toSelect.Count == 0)
                    return new ErrorResponse($"No objects found matching criteria");

                if (additive)
                {
                    var current = Selection.gameObjects.ToList();
                    current.AddRange(toSelect);
                    Selection.objects = current.Distinct().ToArray();
                }
                else
                {
                    Selection.objects = toSelect.Distinct().ToArray();
                }

                // Ping the first one
                if (toSelect.Count > 0)
                    EditorGUIUtility.PingObject(toSelect[0]);

                return new SuccessResponse(
                    $"Selected {Selection.gameObjects.Length} objects",
                    new { count = Selection.gameObjects.Length, names = Selection.gameObjects.Select(g => g.name).ToArray() });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Selection failed: {e.Message}");
            }
        }

        private static Type GetTypeByName(string typeName)
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

    #region 2. Get All Properties Tool
    [McpForUnityTool(
        name: "get_all_properties",
        Description = "Gets ALL serialized property values from a component. Params: objectName, componentName")]
    public static class MCPGetAllProperties
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string objectName = @params["objectName"]?.ToString();
                string componentName = @params["componentName"]?.ToString();

                if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(componentName))
                    return new ErrorResponse("Required: objectName, componentName");

                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    go = allObjects.FirstOrDefault(g => g.name == objectName && g.scene.isLoaded);
                }
                if (go == null)
                    return new ErrorResponse($"GameObject '{objectName}' not found");

                var comp = go.GetComponent(componentName);
                if (comp == null)
                    return new ErrorResponse($"Component '{componentName}' not found");

                var serializedObject = new SerializedObject(comp);
                var properties = new Dictionary<string, object>();

                var iterator = serializedObject.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.name == "m_Script") continue;

                    properties[iterator.name] = GetPropertyValue(iterator);
                }

                return new SuccessResponse(
                    $"Retrieved {properties.Count} properties from {componentName}",
                    new { objectName, componentName, propertyCount = properties.Count, properties });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to get properties: {e.Message}");
            }
        }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum: return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Color: return new[] { prop.colorValue.r, prop.colorValue.g, prop.colorValue.b, prop.colorValue.a };
                case SerializedPropertyType.Vector2: return new[] { prop.vector2Value.x, prop.vector2Value.y };
                case SerializedPropertyType.Vector3: return new[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
                case SerializedPropertyType.Vector4: return new[] { prop.vector4Value.x, prop.vector4Value.y, prop.vector4Value.z, prop.vector4Value.w };
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue?.name ?? "null";
                case SerializedPropertyType.LayerMask: return prop.intValue;
                default: return $"<{prop.propertyType}>";
            }
        }
    }
    #endregion

    #region 3. Find Missing References Tool
    [McpForUnityTool(
        name: "find_missing",
        Description = "Finds all missing references and scripts in scene. Params: includeInactive (bool)")]
    public static class MCPFindMissing
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                bool includeInactive = @params["includeInactive"]?.ToObject<bool>() ?? true;

                var missingScripts = new List<object>();
                var missingReferences = new List<object>();

                var allObjects = includeInactive
                    ? UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.isLoaded)
                    : UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

                foreach (var go in allObjects)
                {
                    // Check for missing scripts
                    var components = go.GetComponents<Component>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] == null)
                        {
                            missingScripts.Add(new {
                                gameObject = go.name,
                                path = GetGameObjectPath(go),
                                componentIndex = i
                            });
                        }
                    }

                    // Check for missing references in valid components
                    foreach (var comp in components.Where(c => c != null))
                    {
                        var so = new SerializedObject(comp);
                        var prop = so.GetIterator();

                        while (prop.NextVisible(true))
                        {
                            if (prop.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                                {
                                    missingReferences.Add(new {
                                        gameObject = go.name,
                                        path = GetGameObjectPath(go),
                                        component = comp.GetType().Name,
                                        property = prop.name
                                    });
                                }
                            }
                        }
                    }
                }

                bool hasIssues = missingScripts.Count > 0 || missingReferences.Count > 0;

                return new SuccessResponse(
                    hasIssues
                        ? $"Found {missingScripts.Count} missing scripts, {missingReferences.Count} missing references"
                        : "No missing references or scripts found!",
                    new {
                        missingScriptCount = missingScripts.Count,
                        missingReferenceCount = missingReferences.Count,
                        missingScripts,
                        missingReferences,
                        healthy = !hasIssues
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Search failed: {e.Message}");
            }
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
    #endregion

    #region 4. Set Layer Recursive Tool
    [McpForUnityTool(
        name: "set_layer_recursive",
        Description = "Sets layer on object and ALL children. Params: objectName, layerName or layerIndex")]
    public static class MCPSetLayerRecursive
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string objectName = @params["objectName"]?.ToString();
                string layerName = @params["layerName"]?.ToString();
                int? layerIndex = @params["layerIndex"]?.ToObject<int>();

                if (string.IsNullOrEmpty(objectName))
                    return new ErrorResponse("Required: objectName");

                if (string.IsNullOrEmpty(layerName) && !layerIndex.HasValue)
                    return new ErrorResponse("Required: layerName or layerIndex");

                var go = GameObject.Find(objectName);
                if (go == null)
                    return new ErrorResponse($"GameObject '{objectName}' not found");

                int layer;
                if (!string.IsNullOrEmpty(layerName))
                {
                    layer = LayerMask.NameToLayer(layerName);
                    if (layer == -1)
                        return new ErrorResponse($"Layer '{layerName}' not found");
                }
                else
                {
                    layer = layerIndex.Value;
                }

                int count = SetLayerRecursively(go.transform, layer);

                EditorSceneManager.MarkSceneDirty(go.scene);

                return new SuccessResponse(
                    $"Set layer '{LayerMask.LayerToName(layer)}' on {count} objects",
                    new { rootObject = objectName, layer = LayerMask.LayerToName(layer), layerIndex = layer, affectedCount = count });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed: {e.Message}");
            }
        }

        private static int SetLayerRecursively(Transform transform, int layer)
        {
            int count = 1;
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
            {
                count += SetLayerRecursively(child, layer);
            }
            return count;
        }
    }
    #endregion

    #region 5. Instantiate Prefab Tool
    [McpForUnityTool(
        name: "instantiate_prefab",
        Description = "Instantiates a prefab in scene. Params: prefabPath, position (array), rotation (array), parent, name")]
    public static class MCPInstantiatePrefab
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string prefabPath = @params["prefabPath"]?.ToString();
                var posToken = @params["position"];
                var rotToken = @params["rotation"];
                string parentName = @params["parent"]?.ToString();
                string instanceName = @params["name"]?.ToString();

                if (string.IsNullOrEmpty(prefabPath))
                    return new ErrorResponse("Required: prefabPath");

                // Normalize path
                if (!prefabPath.StartsWith("Assets/"))
                    prefabPath = "Assets/" + prefabPath;
                if (!prefabPath.EndsWith(".prefab"))
                    prefabPath += ".prefab";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    return new ErrorResponse($"Prefab not found at '{prefabPath}'");

                // Parse position
                Vector3 position = Vector3.zero;
                if (posToken != null)
                {
                    var pos = posToken.ToObject<float[]>();
                    if (pos != null && pos.Length >= 3)
                        position = new Vector3(pos[0], pos[1], pos[2]);
                }

                // Parse rotation
                Quaternion rotation = Quaternion.identity;
                if (rotToken != null)
                {
                    var rot = rotToken.ToObject<float[]>();
                    if (rot != null && rot.Length >= 3)
                        rotation = Quaternion.Euler(rot[0], rot[1], rot[2]);
                }

                // Instantiate
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = position;
                instance.transform.rotation = rotation;

                // Set parent
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parent = GameObject.Find(parentName);
                    if (parent != null)
                        instance.transform.SetParent(parent.transform, true);
                }

                // Rename
                if (!string.IsNullOrEmpty(instanceName))
                    instance.name = instanceName;

                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");
                EditorSceneManager.MarkSceneDirty(instance.scene);

                return new SuccessResponse(
                    $"Instantiated '{instance.name}' from prefab",
                    new {
                        name = instance.name,
                        prefab = prefabPath,
                        position = new[] { position.x, position.y, position.z },
                        instanceID = instance.GetInstanceID()
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Instantiation failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 6. Raycast Scene Tool
    [McpForUnityTool(
        name: "raycast_scene",
        Description = "Performs raycast in scene. Params: origin (array), direction (array), maxDistance, layerMask")]
    public static class MCPRaycastScene
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var originToken = @params["origin"];
                var dirToken = @params["direction"];
                float maxDistance = @params["maxDistance"]?.ToObject<float>() ?? 1000f;
                int layerMask = @params["layerMask"]?.ToObject<int>() ?? ~0;

                // Also support named origin (from object)
                string fromObject = @params["fromObject"]?.ToString();

                Vector3 origin;
                Vector3 direction;

                if (!string.IsNullOrEmpty(fromObject))
                {
                    var go = GameObject.Find(fromObject);
                    if (go == null)
                        return new ErrorResponse($"Object '{fromObject}' not found");
                    origin = go.transform.position;
                    direction = go.transform.forward;
                }
                else
                {
                    if (originToken == null || dirToken == null)
                        return new ErrorResponse("Required: origin + direction arrays, or fromObject");

                    var orig = originToken.ToObject<float[]>();
                    var dir = dirToken.ToObject<float[]>();
                    origin = new Vector3(orig[0], orig[1], orig[2]);
                    direction = new Vector3(dir[0], dir[1], dir[2]).normalized;
                }

                RaycastHit hit;
                bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, layerMask);

                if (didHit)
                {
                    return new SuccessResponse(
                        $"Hit: {hit.collider.gameObject.name} at distance {hit.distance:F2}",
                        new {
                            hit = true,
                            objectName = hit.collider.gameObject.name,
                            objectPath = GetPath(hit.collider.transform),
                            point = new[] { hit.point.x, hit.point.y, hit.point.z },
                            normal = new[] { hit.normal.x, hit.normal.y, hit.normal.z },
                            distance = hit.distance,
                            layer = LayerMask.LayerToName(hit.collider.gameObject.layer)
                        });
                }
                else
                {
                    return new SuccessResponse("No hit", new { hit = false });
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Raycast failed: {e.Message}");
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

    #region 7. Measure Distance Tool
    [McpForUnityTool(
        name: "measure_distance",
        Description = "Measures distance between objects. Params: object1, object2 OR objects (array for chain)")]
    public static class MCPMeasureDistance
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string obj1 = @params["object1"]?.ToString();
                string obj2 = @params["object2"]?.ToString();
                var objectsToken = @params["objects"];

                List<string> objectNames = new List<string>();

                if (objectsToken != null)
                {
                    objectNames = objectsToken.ToObject<List<string>>();
                }
                else if (!string.IsNullOrEmpty(obj1) && !string.IsNullOrEmpty(obj2))
                {
                    objectNames.Add(obj1);
                    objectNames.Add(obj2);
                }
                else
                {
                    return new ErrorResponse("Required: object1 + object2, or objects array");
                }

                if (objectNames.Count < 2)
                    return new ErrorResponse("Need at least 2 objects");

                // Find all objects
                var gameObjects = new List<GameObject>();
                foreach (var name in objectNames)
                {
                    var go = GameObject.Find(name);
                    if (go == null)
                        return new ErrorResponse($"Object '{name}' not found");
                    gameObjects.Add(go);
                }

                // Calculate distances
                var distances = new List<object>();
                float totalDistance = 0f;

                for (int i = 0; i < gameObjects.Count - 1; i++)
                {
                    float dist = Vector3.Distance(
                        gameObjects[i].transform.position,
                        gameObjects[i + 1].transform.position);
                    totalDistance += dist;
                    distances.Add(new {
                        from = gameObjects[i].name,
                        to = gameObjects[i + 1].name,
                        distance = dist
                    });
                }

                return new SuccessResponse(
                    $"Total distance: {totalDistance:F2} units",
                    new { totalDistance, segments = distances });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Measurement failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 8. List Assets Folder Tool
    [McpForUnityTool(
        name: "list_assets_folder",
        Description = "Lists assets in a folder. Params: folderPath, filter (t:Material, t:Prefab, etc.), recursive")]
    public static class MCPListAssetsFolder
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string folderPath = @params["folderPath"]?.ToString() ?? "Assets";
                string filter = @params["filter"]?.ToString() ?? "";
                bool recursive = @params["recursive"]?.ToObject<bool>() ?? true;

                if (!folderPath.StartsWith("Assets"))
                    folderPath = "Assets/" + folderPath;

                string[] guids;
                if (recursive)
                {
                    guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
                }
                else
                {
                    // Non-recursive - filter by direct parent
                    guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
                    guids = guids.Where(g => {
                        var path = AssetDatabase.GUIDToAssetPath(g);
                        var dir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                        return dir == folderPath;
                    }).ToArray();
                }

                var assets = guids.Select(guid => {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    return new {
                        name = asset?.name ?? System.IO.Path.GetFileNameWithoutExtension(path),
                        path,
                        type = asset?.GetType().Name ?? "Unknown"
                    };
                }).ToList();

                return new SuccessResponse(
                    $"Found {assets.Count} assets in '{folderPath}'",
                    new { folder = folderPath, filter, count = assets.Count, assets });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"List failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 9. Batch Rename Tool
    [McpForUnityTool(
        name: "batch_rename",
        Description = "Renames multiple objects. Params: searchMethod, searchTerm, newName, pattern ({name}, {index}, {type})")]
    public static class MCPBatchRename
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string searchMethod = @params["searchMethod"]?.ToString()?.ToLower() ?? "by_name";
                string searchTerm = @params["searchTerm"]?.ToString();
                string newName = @params["newName"]?.ToString();
                string pattern = @params["pattern"]?.ToString();

                if (string.IsNullOrEmpty(searchTerm))
                    return new ErrorResponse("Required: searchTerm");

                if (string.IsNullOrEmpty(newName) && string.IsNullOrEmpty(pattern))
                    return new ErrorResponse("Required: newName or pattern");

                // Find objects
                GameObject[] targets = null;
                switch (searchMethod)
                {
                    case "by_tag":
                        targets = GameObject.FindGameObjectsWithTag(searchTerm);
                        break;
                    case "by_name":
                    default:
                        targets = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>()
                            .Where(g => g.name.Contains(searchTerm) && g.scene.isLoaded).ToArray();
                        break;
                }

                if (targets == null || targets.Length == 0)
                    return new ErrorResponse($"No objects found");

                var renamedList = new List<object>();
                int index = 0;

                foreach (var go in targets)
                {
                    string oldName = go.name;
                    string finalName;

                    if (!string.IsNullOrEmpty(pattern))
                    {
                        finalName = pattern
                            .Replace("{name}", oldName)
                            .Replace("{index}", index.ToString())
                            .Replace("{i}", index.ToString("D2"))
                            .Replace("{type}", go.GetComponents<Component>().LastOrDefault()?.GetType().Name ?? "GameObject");
                    }
                    else
                    {
                        finalName = newName;
                        if (targets.Length > 1)
                            finalName += $"_{index}";
                    }

                    Undo.RecordObject(go, "Batch Rename");
                    go.name = finalName;
                    renamedList.Add(new { oldName, newName = finalName });
                    index++;
                }

                EditorSceneManager.MarkAllScenesDirty();

                return new SuccessResponse(
                    $"Renamed {renamedList.Count} objects",
                    new { count = renamedList.Count, renamed = renamedList });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Rename failed: {e.Message}");
            }
        }
    }
    #endregion

    #region 10. Export Scene Data Tool
    [McpForUnityTool(
        name: "export_scene_data",
        Description = "Exports scene/object data to JSON. Params: objectName (optional, null=whole scene), includeComponents, includeChildren")]
    public static class MCPExportSceneData
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string objectName = @params["objectName"]?.ToString();
                bool includeComponents = @params["includeComponents"]?.ToObject<bool>() ?? true;
                bool includeChildren = @params["includeChildren"]?.ToObject<bool>() ?? true;

                List<object> exportData = new List<object>();

                if (string.IsNullOrEmpty(objectName))
                {
                    // Export root objects
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        exportData.Add(ExportGameObject(root, includeComponents, includeChildren));
                    }
                }
                else
                {
                    var go = GameObject.Find(objectName);
                    if (go == null)
                        return new ErrorResponse($"Object '{objectName}' not found");
                    exportData.Add(ExportGameObject(go, includeComponents, includeChildren));
                }

                return new SuccessResponse(
                    $"Exported {exportData.Count} objects",
                    new { count = exportData.Count, data = exportData });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Export failed: {e.Message}");
            }
        }

        private static object ExportGameObject(GameObject go, bool includeComponents, bool includeChildren)
        {
            var data = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["active"] = go.activeSelf,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["tag"] = go.tag,
                ["position"] = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                ["rotation"] = new[] { go.transform.eulerAngles.x, go.transform.eulerAngles.y, go.transform.eulerAngles.z },
                ["scale"] = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z }
            };

            if (includeComponents)
            {
                data["components"] = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToList();
            }

            if (includeChildren && go.transform.childCount > 0)
            {
                var children = new List<object>();
                foreach (Transform child in go.transform)
                {
                    children.Add(ExportGameObject(child.gameObject, includeComponents, includeChildren));
                }
                data["children"] = children;
            }

            return data;
        }
    }
    #endregion
}
