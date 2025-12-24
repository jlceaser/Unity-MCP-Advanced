#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// SUPER TOOL - Execute any C# code directly in Unity Editor
    /// This single tool can replace almost all other tools by providing direct access to Unity API
    ///
    /// Examples:
    /// - execute: "GameObject.Find(\"Player\").transform.position = new Vector3(0, 5, 0);"
    /// - evaluate: "Camera.main.fieldOfView"
    /// - batch: ["Time.timeScale = 0.5f;", "RenderSettings.fog = true;"]
    /// - query: "FindObjectsOfType<Light>()"
    /// </summary>
    [McpForUnityTool(
        name: "execute_csharp",
        Description = "SUPER TOOL: Execute any C# code in Unity Editor. Actions: execute (run code), evaluate (get value), batch (multiple), query (find objects), help")]
    public static class MCPSuperTool
    {
        // Cache for commonly used types
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>
        {
            { "GameObject", typeof(GameObject) },
            { "Transform", typeof(Transform) },
            { "Camera", typeof(Camera) },
            { "Light", typeof(Light) },
            { "Rigidbody", typeof(Rigidbody) },
            { "Collider", typeof(Collider) },
            { "MeshRenderer", typeof(MeshRenderer) },
            { "AudioSource", typeof(AudioSource) },
            { "Animator", typeof(Animator) },
            { "Canvas", typeof(Canvas) },
            { "Image", typeof(UnityEngine.UI.Image) },
            { "Text", typeof(UnityEngine.UI.Text) },
            { "Button", typeof(UnityEngine.UI.Button) },
        };

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "execute";
            string code = @params["code"]?.ToString();

            switch (action)
            {
                case "execute":
                case "run":
                    return ExecuteCode(code, @params);

                case "evaluate":
                case "eval":
                case "get":
                    return EvaluateExpression(code, @params);

                case "batch":
                    var commands = @params["commands"]?.ToObject<string[]>() ??
                                   @params["code"]?.ToString()?.Split(new[] { ";\n", ";\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    return ExecuteBatch(commands);

                case "query":
                case "find":
                    return QueryObjects(code, @params);

                case "set_property":
                case "set":
                    return SetProperty(@params);

                case "call_method":
                case "call":
                    return CallMethod(@params);

                case "create":
                    return CreateObject(@params);

                case "destroy":
                    return DestroyObject(@params);

                case "help":
                    return GetHelp();

                default:
                    return new ErrorResponse($"Unknown action: {action}. Use: execute, evaluate, batch, query, set_property, call_method, create, destroy, help");
            }
        }

        #region Execute Code

        private static object ExecuteCode(string code, JObject @params)
        {
            if (string.IsNullOrEmpty(code))
                return new ErrorResponse("Code parameter required");

            try
            {
                // Parse and execute the code
                var result = ParseAndExecute(code);

                EditorUtility.SetDirty(Selection.activeGameObject);

                return new SuccessResponse("Code executed", new
                {
                    code = code,
                    result = FormatResult(result),
                    resultType = result?.GetType().Name ?? "void"
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Execution failed: {ex.Message}");
            }
        }

        private static object ParseAndExecute(string code)
        {
            code = code.Trim();
            if (code.EndsWith(";")) code = code.Substring(0, code.Length - 1);

            // Handle common patterns

            // 1. GameObject.Find("name").operation
            if (code.StartsWith("GameObject.Find"))
            {
                return ExecuteGameObjectFind(code);
            }

            // 2. FindObjectOfType<T>()
            if (code.Contains("FindObjectOfType") || code.Contains("FindObjectsOfType"))
            {
                return ExecuteFindObjectOfType(code);
            }

            // 3. Property assignment (e.g., Time.timeScale = 0.5f)
            if (code.Contains(" = ") && !code.Contains("=="))
            {
                return ExecuteAssignment(code);
            }

            // 4. Static property access (e.g., Time.time, Camera.main)
            if (code.Contains(".") && !code.Contains("("))
            {
                return ExecutePropertyAccess(code);
            }

            // 5. Static method call
            if (code.Contains("(") && code.Contains(")"))
            {
                return ExecuteMethodCall(code);
            }

            // 6. Simple evaluation
            return EvaluateSimple(code);
        }

        private static object ExecuteGameObjectFind(string code)
        {
            // Parse: GameObject.Find("name") or GameObject.Find("name").something
            var match = System.Text.RegularExpressions.Regex.Match(code, @"GameObject\.Find\([""'](.+?)[""']\)(.*)");
            if (!match.Success)
                throw new Exception("Invalid GameObject.Find syntax");

            string objName = match.Groups[1].Value;
            string remaining = match.Groups[2].Value.Trim();

            var go = GameObject.Find(objName);
            if (go == null)
                throw new Exception($"GameObject not found: {objName}");

            if (string.IsNullOrEmpty(remaining))
                return go;

            // Process chained operations
            return ProcessChainedOperations(go, remaining);
        }

        private static object ProcessChainedOperations(object target, string operations)
        {
            operations = operations.TrimStart('.');

            // Handle component access: GetComponent<T>()
            if (operations.StartsWith("GetComponent"))
            {
                var compMatch = System.Text.RegularExpressions.Regex.Match(operations, @"GetComponent<(.+?)>\(\)(.*)");
                if (compMatch.Success)
                {
                    string typeName = compMatch.Groups[1].Value;
                    string remaining = compMatch.Groups[2].Value.Trim();

                    var type = FindType(typeName);
                    if (type == null)
                        throw new Exception($"Type not found: {typeName}");

                    var component = (target as GameObject)?.GetComponent(type) ??
                                   (target as Component)?.GetComponent(type);

                    if (component == null)
                        throw new Exception($"Component not found: {typeName}");

                    if (string.IsNullOrEmpty(remaining))
                        return component;

                    return ProcessChainedOperations(component, remaining);
                }
            }

            // Handle property access or assignment
            if (operations.Contains(" = "))
            {
                // Assignment
                var parts = operations.Split(new[] { " = " }, 2, StringSplitOptions.None);
                string propPath = parts[0].TrimStart('.');
                string valueStr = parts[1].Trim();

                SetPropertyValue(target, propPath, valueStr);
                return $"Set {propPath} = {valueStr}";
            }
            else
            {
                // Property access
                return GetPropertyValue(target, operations);
            }
        }

        private static object ExecuteFindObjectOfType(string code)
        {
            bool findAll = code.Contains("FindObjectsOfType");
            var match = System.Text.RegularExpressions.Regex.Match(code, @"FindObjects?OfType<(.+?)>\(\)");

            if (!match.Success)
                throw new Exception("Invalid FindObjectOfType syntax");

            string typeName = match.Groups[1].Value;
            var type = FindType(typeName);
            if (type == null)
                throw new Exception($"Type not found: {typeName}");

            if (findAll)
            {
                var objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
                return objects.Select(o => new { name = o.name, type = o.GetType().Name }).ToList();
            }
            else
            {
                var obj = UnityEngine.Object.FindFirstObjectByType(type);
                return obj != null ? new { name = obj.name, type = obj.GetType().Name } : null;
            }
        }

        private static object ExecuteAssignment(string code)
        {
            var parts = code.Split(new[] { " = " }, 2, StringSplitOptions.None);
            string leftSide = parts[0].Trim();
            string rightSide = parts[1].Trim();

            // Parse the left side to find the target object and property
            var dotIndex = leftSide.LastIndexOf('.');
            if (dotIndex == -1)
                throw new Exception("Invalid assignment syntax");

            string targetPath = leftSide.Substring(0, dotIndex);
            string propertyName = leftSide.Substring(dotIndex + 1);

            object target = ResolveTarget(targetPath);
            SetPropertyValue(target, propertyName, rightSide);

            return $"Set {leftSide} = {rightSide}";
        }

        private static object ExecutePropertyAccess(string code)
        {
            return ResolveTarget(code);
        }

        private static object ExecuteMethodCall(string code)
        {
            // Parse: Type.Method(args) or instance.Method(args)
            var match = System.Text.RegularExpressions.Regex.Match(code, @"(.+?)\.(\w+)\((.*)\)");
            if (!match.Success)
                throw new Exception("Invalid method call syntax");

            string targetPath = match.Groups[1].Value;
            string methodName = match.Groups[2].Value;
            string argsStr = match.Groups[3].Value;

            object target = ResolveTarget(targetPath);
            var targetType = target is Type t ? t : target.GetType();
            var isStatic = target is Type;

            // Parse arguments
            object[] args = ParseArguments(argsStr);

            // Find and invoke method
            var method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            if (method == null)
                throw new Exception($"Method not found: {methodName}");

            return method.Invoke(isStatic ? null : target, args);
        }

        #endregion

        #region Evaluate Expression

        private static object EvaluateExpression(string code, JObject @params)
        {
            if (string.IsNullOrEmpty(code))
                return new ErrorResponse("Code parameter required");

            try
            {
                var result = EvaluateSimple(code);
                return new SuccessResponse("Evaluation result", new
                {
                    expression = code,
                    value = FormatResult(result),
                    type = result?.GetType().Name ?? "null"
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Evaluation failed: {ex.Message}");
            }
        }

        private static object EvaluateSimple(string code)
        {
            // Common Unity static properties
            var staticProps = new Dictionary<string, Func<object>>
            {
                { "Time.time", () => Time.time },
                { "Time.deltaTime", () => Time.deltaTime },
                { "Time.timeScale", () => Time.timeScale },
                { "Time.frameCount", () => Time.frameCount },
                { "Time.realtimeSinceStartup", () => Time.realtimeSinceStartup },
                { "Time.fixedDeltaTime", () => Time.fixedDeltaTime },
                { "Application.isPlaying", () => Application.isPlaying },
                { "Application.platform", () => Application.platform.ToString() },
                { "Application.unityVersion", () => Application.unityVersion },
                { "Application.dataPath", () => Application.dataPath },
                { "Application.persistentDataPath", () => Application.persistentDataPath },
                { "Screen.width", () => Screen.width },
                { "Screen.height", () => Screen.height },
                { "Screen.dpi", () => Screen.dpi },
                { "Camera.main", () => Camera.main?.name ?? "null" },
                { "Camera.allCamerasCount", () => Camera.allCamerasCount },
                { "SystemInfo.deviceName", () => SystemInfo.deviceName },
                { "SystemInfo.operatingSystem", () => SystemInfo.operatingSystem },
                { "SystemInfo.graphicsDeviceName", () => SystemInfo.graphicsDeviceName },
                { "SystemInfo.systemMemorySize", () => SystemInfo.systemMemorySize },
                { "QualitySettings.GetQualityLevel()", () => QualitySettings.GetQualityLevel() },
                { "QualitySettings.names", () => QualitySettings.names },
                { "RenderSettings.fog", () => RenderSettings.fog },
                { "RenderSettings.ambientIntensity", () => RenderSettings.ambientIntensity },
                { "EditorApplication.isPlaying", () => EditorApplication.isPlaying },
                { "EditorApplication.isCompiling", () => EditorApplication.isCompiling },
                { "Selection.activeGameObject", () => Selection.activeGameObject?.name ?? "null" },
                { "Selection.count", () => Selection.count },
            };

            if (staticProps.TryGetValue(code, out var func))
                return func();

            // Try to resolve as property path
            return ResolveTarget(code);
        }

        #endregion

        #region Batch Execution

        private static object ExecuteBatch(string[] commands)
        {
            if (commands == null || commands.Length == 0)
                return new ErrorResponse("Commands parameter required (array of code strings)");

            var results = new List<object>();
            int successCount = 0;
            int errorCount = 0;

            foreach (var cmd in commands)
            {
                var trimmed = cmd.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                try
                {
                    var result = ParseAndExecute(trimmed);
                    results.Add(new { command = trimmed, success = true, result = FormatResult(result) });
                    successCount++;
                }
                catch (Exception ex)
                {
                    results.Add(new { command = trimmed, success = false, error = ex.Message });
                    errorCount++;
                }
            }

            return new SuccessResponse($"Batch complete: {successCount} succeeded, {errorCount} failed", new
            {
                total = commands.Length,
                succeeded = successCount,
                failed = errorCount,
                results = results
            });
        }

        #endregion

        #region Query Objects

        private static object QueryObjects(string query, JObject @params)
        {
            if (string.IsNullOrEmpty(query))
                query = "GameObject";

            try
            {
                var type = FindType(query);
                if (type == null)
                {
                    // Try as component name
                    type = FindType(query) ?? typeof(GameObject);
                }

                var objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);

                int limit = @params["limit"]?.Value<int>() ?? 50;
                var results = objects.Take(limit).Select(o =>
                {
                    var go = o as GameObject ?? (o as Component)?.gameObject;
                    return new
                    {
                        name = o.name,
                        type = o.GetType().Name,
                        gameObject = go?.name,
                        active = go?.activeSelf ?? true,
                        path = go != null ? GetGameObjectPath(go) : null
                    };
                }).ToList();

                return new SuccessResponse($"Found {objects.Length} objects of type {type.Name}", new
                {
                    query = query,
                    type = type.Name,
                    count = objects.Length,
                    showing = results.Count,
                    objects = results
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Query failed: {ex.Message}");
            }
        }

        #endregion

        #region Set Property

        private static object SetProperty(JObject @params)
        {
            string target = @params["target"]?.ToString();
            string property = @params["property"]?.ToString();
            var value = @params["value"];

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(property))
                return new ErrorResponse("Target and property parameters required");

            try
            {
                object targetObj = ResolveTarget(target);
                SetPropertyValue(targetObj, property, value?.ToString());

                return new SuccessResponse($"Set {target}.{property}", new
                {
                    target = target,
                    property = property,
                    newValue = value?.ToString()
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Set property failed: {ex.Message}");
            }
        }

        #endregion

        #region Call Method

        private static object CallMethod(JObject @params)
        {
            string target = @params["target"]?.ToString();
            string method = @params["method"]?.ToString();
            var args = @params["args"]?.ToObject<object[]>() ?? Array.Empty<object>();

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(method))
                return new ErrorResponse("Target and method parameters required");

            try
            {
                object targetObj = ResolveTarget(target);
                var targetType = targetObj is Type t ? t : targetObj.GetType();
                var isStatic = targetObj is Type;

                var methodInfo = targetType.GetMethod(method,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                if (methodInfo == null)
                    return new ErrorResponse($"Method not found: {method}");

                var result = methodInfo.Invoke(isStatic ? null : targetObj, args);

                return new SuccessResponse($"Called {target}.{method}()", new
                {
                    target = target,
                    method = method,
                    result = FormatResult(result),
                    resultType = result?.GetType().Name ?? "void"
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Method call failed: {ex.Message}");
            }
        }

        #endregion

        #region Create/Destroy Objects

        private static object CreateObject(JObject @params)
        {
            string type = @params["type"]?.ToString() ?? "GameObject";
            string name = @params["name"]?.ToString() ?? "New Object";
            var position = @params["position"]?.ToObject<float[]>();
            string parent = @params["parent"]?.ToString();

            try
            {
                GameObject go;

                // Check for primitive types
                if (Enum.TryParse<PrimitiveType>(type, true, out var primitive))
                {
                    go = GameObject.CreatePrimitive(primitive);
                    go.name = name;
                }
                else if (type.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                {
                    go = new GameObject(name);
                }
                else
                {
                    // Create with component
                    go = new GameObject(name);
                    var compType = FindType(type);
                    if (compType != null && typeof(Component).IsAssignableFrom(compType))
                    {
                        go.AddComponent(compType);
                    }
                }

                if (position != null && position.Length >= 3)
                {
                    go.transform.position = new Vector3(position[0], position[1], position[2]);
                }

                if (!string.IsNullOrEmpty(parent))
                {
                    var parentObj = GameObject.Find(parent);
                    if (parentObj != null)
                    {
                        go.transform.SetParent(parentObj.transform);
                    }
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                return new SuccessResponse($"Created {name}", new
                {
                    name = go.name,
                    type = type,
                    position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                    path = GetGameObjectPath(go)
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Create failed: {ex.Message}");
            }
        }

        private static object DestroyObject(JObject @params)
        {
            string target = @params["target"]?.ToString();

            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("Target parameter required");

            try
            {
                var go = GameObject.Find(target);
                if (go == null)
                    return new ErrorResponse($"Object not found: {target}");

                string name = go.name;
                Undo.DestroyObjectImmediate(go);

                return new SuccessResponse($"Destroyed {name}");
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Destroy failed: {ex.Message}");
            }
        }

        #endregion

        #region Help

        private static object GetHelp()
        {
            return new SuccessResponse("ExecuteCSharp Super Tool - Help", new
            {
                description = "Execute any C# code directly in Unity Editor",
                actions = new
                {
                    execute = new
                    {
                        description = "Execute C# code",
                        example = "execute_csharp action=execute code=\"GameObject.Find(\\\"Player\\\").transform.position = new Vector3(0, 5, 0)\""
                    },
                    evaluate = new
                    {
                        description = "Evaluate expression and return value",
                        example = "execute_csharp action=evaluate code=\"Time.timeScale\""
                    },
                    batch = new
                    {
                        description = "Execute multiple commands",
                        example = "execute_csharp action=batch commands=[\"Time.timeScale = 0.5f\", \"RenderSettings.fog = true\"]"
                    },
                    query = new
                    {
                        description = "Find objects by type",
                        example = "execute_csharp action=query code=\"Light\" limit=10"
                    },
                    set_property = new
                    {
                        description = "Set object property",
                        example = "execute_csharp action=set_property target=\"Camera.main\" property=\"fieldOfView\" value=\"90\""
                    },
                    call_method = new
                    {
                        description = "Call method on object",
                        example = "execute_csharp action=call_method target=\"EditorApplication\" method=\"Beep\""
                    },
                    create = new
                    {
                        description = "Create new GameObject",
                        example = "execute_csharp action=create type=\"Cube\" name=\"MyCube\" position=[0,1,0]"
                    },
                    destroy = new
                    {
                        description = "Destroy GameObject",
                        example = "execute_csharp action=destroy target=\"MyCube\""
                    }
                },
                supported_expressions = new[]
                {
                    "Time.time, Time.timeScale, Time.deltaTime",
                    "Camera.main, Camera.allCamerasCount",
                    "Application.isPlaying, Application.platform",
                    "Screen.width, Screen.height",
                    "RenderSettings.fog, RenderSettings.ambientIntensity",
                    "QualitySettings.GetQualityLevel()",
                    "Selection.activeGameObject, Selection.count",
                    "GameObject.Find(\"name\")",
                    "FindObjectOfType<Type>()",
                    "FindObjectsOfType<Type>()"
                }
            });
        }

        #endregion

        #region Helper Methods

        private static object ResolveTarget(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new Exception("Empty path");

            // Check for static type access
            var knownTypes = new Dictionary<string, Type>
            {
                { "Time", typeof(Time) },
                { "Camera", typeof(Camera) },
                { "Application", typeof(Application) },
                { "Screen", typeof(Screen) },
                { "Input", typeof(Input) },
                { "Physics", typeof(Physics) },
                { "Physics2D", typeof(Physics2D) },
                { "RenderSettings", typeof(RenderSettings) },
                { "QualitySettings", typeof(QualitySettings) },
                { "PlayerPrefs", typeof(PlayerPrefs) },
                { "EditorApplication", typeof(EditorApplication) },
                { "Selection", typeof(Selection) },
                { "AssetDatabase", typeof(AssetDatabase) },
                { "Undo", typeof(Undo) },
                { "SceneManager", typeof(SceneManager) },
                { "GameObject", typeof(GameObject) },
                { "Debug", typeof(Debug) },
                { "Mathf", typeof(Mathf) },
                { "Vector3", typeof(Vector3) },
                { "Quaternion", typeof(Quaternion) },
                { "Color", typeof(Color) },
            };

            var parts = path.Split('.');
            string firstPart = parts[0];

            // Check if it's a known static type
            if (knownTypes.TryGetValue(firstPart, out var type))
            {
                if (parts.Length == 1)
                    return type;

                // Get static property/field
                object current = type;
                for (int i = 1; i < parts.Length; i++)
                {
                    current = GetPropertyValue(current, parts[i]);
                }
                return current;
            }

            // Try as GameObject.Find
            var go = GameObject.Find(firstPart);
            if (go != null)
            {
                if (parts.Length == 1)
                    return go;

                object current = go;
                for (int i = 1; i < parts.Length; i++)
                {
                    current = GetPropertyValue(current, parts[i]);
                }
                return current;
            }

            throw new Exception($"Cannot resolve: {path}");
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                throw new Exception("Null target");

            var type = target is Type t ? t : target.GetType();
            var isStatic = target is Type;
            var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            // Handle special cases
            if (propertyName == "main" && type == typeof(Camera))
                return Camera.main;

            // Try property
            var prop = type.GetProperty(propertyName, flags);
            if (prop != null)
                return prop.GetValue(isStatic ? null : target);

            // Try field
            var field = type.GetField(propertyName, flags);
            if (field != null)
                return field.GetValue(isStatic ? null : target);

            throw new Exception($"Property/field not found: {propertyName} on {type.Name}");
        }

        private static void SetPropertyValue(object target, string propertyName, string valueStr)
        {
            if (target == null)
                throw new Exception("Null target");

            var type = target is Type t ? t : target.GetType();
            var isStatic = target is Type;
            var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            // Try property
            var prop = type.GetProperty(propertyName, flags);
            if (prop != null && prop.CanWrite)
            {
                var value = ParseValue(valueStr, prop.PropertyType);
                prop.SetValue(isStatic ? null : target, value);
                return;
            }

            // Try field
            var field = type.GetField(propertyName, flags);
            if (field != null)
            {
                var value = ParseValue(valueStr, field.FieldType);
                field.SetValue(isStatic ? null : target, value);
                return;
            }

            throw new Exception($"Writable property/field not found: {propertyName} on {type.Name}");
        }

        private static object ParseValue(string valueStr, Type targetType)
        {
            if (string.IsNullOrEmpty(valueStr) || valueStr == "null")
                return null;

            // Remove quotes
            valueStr = valueStr.Trim().Trim('"', '\'');

            // Handle common types
            if (targetType == typeof(string))
                return valueStr;
            if (targetType == typeof(int))
                return int.Parse(valueStr.Replace("f", ""));
            if (targetType == typeof(float))
                return float.Parse(valueStr.Replace("f", ""));
            if (targetType == typeof(double))
                return double.Parse(valueStr.Replace("d", ""));
            if (targetType == typeof(bool))
                return bool.Parse(valueStr);
            if (targetType == typeof(Vector3))
            {
                var match = System.Text.RegularExpressions.Regex.Match(valueStr, @"Vector3\((.+),(.+),(.+)\)");
                if (match.Success)
                {
                    return new Vector3(
                        float.Parse(match.Groups[1].Value.Trim().Replace("f", "")),
                        float.Parse(match.Groups[2].Value.Trim().Replace("f", "")),
                        float.Parse(match.Groups[3].Value.Trim().Replace("f", ""))
                    );
                }
                // Try as new Vector3(x, y, z)
                match = System.Text.RegularExpressions.Regex.Match(valueStr, @"new Vector3\((.+),(.+),(.+)\)");
                if (match.Success)
                {
                    return new Vector3(
                        float.Parse(match.Groups[1].Value.Trim().Replace("f", "")),
                        float.Parse(match.Groups[2].Value.Trim().Replace("f", "")),
                        float.Parse(match.Groups[3].Value.Trim().Replace("f", ""))
                    );
                }
            }
            if (targetType == typeof(Color))
            {
                if (ColorUtility.TryParseHtmlString(valueStr, out var color))
                    return color;
            }
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, valueStr, true);
            }

            return Convert.ChangeType(valueStr, targetType);
        }

        private static object[] ParseArguments(string argsStr)
        {
            if (string.IsNullOrEmpty(argsStr))
                return Array.Empty<object>();

            // Simple parsing - split by comma
            var parts = argsStr.Split(',');
            return parts.Select(p => ParseArgumentValue(p.Trim())).ToArray();
        }

        private static object ParseArgumentValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "null")
                return null;

            // String
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
                return value.Substring(1, value.Length - 2);

            // Boolean
            if (value == "true") return true;
            if (value == "false") return false;

            // Number
            if (value.EndsWith("f") && float.TryParse(value.TrimEnd('f'), out var f))
                return f;
            if (int.TryParse(value, out var i))
                return i;
            if (float.TryParse(value, out var f2))
                return f2;

            return value;
        }

        private static Type FindType(string typeName)
        {
            // Check cache
            if (TypeCache.TryGetValue(typeName, out var cachedType))
                return cachedType;

            // Search in all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Try direct
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;

                    // Try with UnityEngine prefix
                    type = assembly.GetType($"UnityEngine.{typeName}");
                    if (type != null) return type;

                    // Try with UnityEditor prefix
                    type = assembly.GetType($"UnityEditor.{typeName}");
                    if (type != null) return type;
                }
                catch { }
            }

            return null;
        }

        private static string FormatResult(object result)
        {
            if (result == null)
                return "null";

            if (result is string s)
                return s;

            if (result is IEnumerable enumerable && !(result is string))
            {
                var items = enumerable.Cast<object>().Take(10).ToList();
                return $"[{string.Join(", ", items.Select(i => i?.ToString() ?? "null"))}]";
            }

            if (result is Vector3 v3)
                return $"({v3.x}, {v3.y}, {v3.z})";

            if (result is Quaternion q)
                return $"({q.x}, {q.y}, {q.z}, {q.w})";

            if (result is Color c)
                return $"#{ColorUtility.ToHtmlStringRGBA(c)}";

            return result.ToString();
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

        #endregion
    }
}
