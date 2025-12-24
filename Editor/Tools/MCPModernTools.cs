#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// "FUTURE PROOF" - Modern Unity Standards
    /// Supports UI Toolkit, New Input System, and Package Manager.
    /// Positions the package as next-gen ready.
    /// </summary>
    [McpForUnityTool(
        name: "modern_unity",
        Description = "Modern Unity support: UI Toolkit (UXML/USS), Input System, Package Manager. Actions: generate_uxml, generate_uss, get_input_actions, create_input_action")]
    public static class MCPModernTools
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "help";

            switch (action)
            {
                // UI Toolkit
                case "generate_uxml":
                    return GenerateUXML(@params);
                case "generate_uss":
                    return GenerateUSS(@params);
                case "list_visual_elements":
                    return ListVisualElementTypes();

                // Input System
                case "get_input_actions":
                    return GetInputActions();
                case "create_input_action":
                    return CreateInputAction(@params);

                // Package Manager
                case "list_packages":
                    return ListPackages();
                case "add_package":
                    return AddPackage(@params);
                case "remove_package":
                    return RemovePackage(@params);
                case "search_packages":
                    return SearchPackages(@params);

                default:
                    return new SuccessResponse("Modern Unity Tools. Actions: generate_uxml, generate_uss, get_input_actions, list_packages, add_package, remove_package");
            }
        }

        #region UI Toolkit - UXML Generation

        /// <summary>
        /// Generates a UXML file from a layout description.
        /// </summary>
        private static object GenerateUXML(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string layout = @params["layout"]?.ToString();
            var elements = @params["elements"]?.ToObject<List<UXMLElement>>();

            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/UI/GeneratedUI.uxml";
            }

            if (!path.EndsWith(".uxml"))
            {
                path += ".uxml";
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\" editor-extension-mode=\"False\">");
                sb.AppendLine("    <Style src=\"project://database/Assets/UI/GeneratedUI.uss?fileID=7433441132597879392&amp;guid=auto\" />");
                sb.AppendLine();

                // Parse layout string or use elements array
                if (!string.IsNullOrEmpty(layout))
                {
                    GenerateFromLayout(sb, layout, 1);
                }
                else if (elements != null && elements.Count > 0)
                {
                    foreach (var element in elements)
                    {
                        GenerateElement(sb, element, 1);
                    }
                }
                else
                {
                    // Default template
                    sb.AppendLine("    <ui:VisualElement name=\"root\" class=\"container\">");
                    sb.AppendLine("        <ui:Label text=\"Hello UI Toolkit!\" class=\"header\" />");
                    sb.AppendLine("        <ui:Button name=\"btn-action\" text=\"Click Me\" class=\"button-primary\" />");
                    sb.AppendLine("    </ui:VisualElement>");
                }

                sb.AppendLine("</ui:UXML>");

                // Ensure directory exists
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, sb.ToString());
                AssetDatabase.ImportAsset(path);

                return new SuccessResponse($"UXML created: {path}", new
                {
                    path = path,
                    content = sb.ToString()
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"UXML generation failed: {ex.Message}");
            }
        }

        private static void GenerateFromLayout(StringBuilder sb, string layout, int indent)
        {
            // Parse layout string like: "Header, Button, ScrollView(Label, Label, Button)"
            var parts = ParseLayoutParts(layout);
            string prefix = new string(' ', indent * 4);

            foreach (var part in parts)
            {
                string elementName = part.name.ToLower();
                string uxmlTag = GetUXMLTag(elementName);

                if (part.children != null && part.children.Length > 0)
                {
                    sb.AppendLine($"{prefix}<ui:{uxmlTag} name=\"{ToKebabCase(part.name)}\">");
                    foreach (var child in part.children)
                    {
                        string childTag = GetUXMLTag(child.ToLower());
                        sb.AppendLine($"{prefix}    <ui:{childTag} name=\"{ToKebabCase(child)}\" />");
                    }
                    sb.AppendLine($"{prefix}</ui:{uxmlTag}>");
                }
                else
                {
                    sb.AppendLine($"{prefix}<ui:{uxmlTag} name=\"{ToKebabCase(part.name)}\" />");
                }
            }
        }

        private static void GenerateElement(StringBuilder sb, UXMLElement element, int indent)
        {
            string prefix = new string(' ', indent * 4);
            string tag = GetUXMLTag(element.type);

            var attrs = new List<string>();
            if (!string.IsNullOrEmpty(element.name)) attrs.Add($"name=\"{element.name}\"");
            if (!string.IsNullOrEmpty(element.text)) attrs.Add($"text=\"{element.text}\"");
            if (!string.IsNullOrEmpty(element.className)) attrs.Add($"class=\"{element.className}\"");

            string attrStr = attrs.Count > 0 ? " " + string.Join(" ", attrs) : "";

            if (element.children != null && element.children.Count > 0)
            {
                sb.AppendLine($"{prefix}<ui:{tag}{attrStr}>");
                foreach (var child in element.children)
                {
                    GenerateElement(sb, child, indent + 1);
                }
                sb.AppendLine($"{prefix}</ui:{tag}>");
            }
            else
            {
                sb.AppendLine($"{prefix}<ui:{tag}{attrStr} />");
            }
        }

        private static string GetUXMLTag(string name)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "header", "Label" },
                { "title", "Label" },
                { "label", "Label" },
                { "text", "Label" },
                { "button", "Button" },
                { "btn", "Button" },
                { "input", "TextField" },
                { "textfield", "TextField" },
                { "toggle", "Toggle" },
                { "checkbox", "Toggle" },
                { "slider", "Slider" },
                { "dropdown", "DropdownField" },
                { "select", "DropdownField" },
                { "container", "VisualElement" },
                { "div", "VisualElement" },
                { "panel", "VisualElement" },
                { "box", "Box" },
                { "scrollview", "ScrollView" },
                { "scroll", "ScrollView" },
                { "foldout", "Foldout" },
                { "image", "Image" },
                { "listview", "ListView" },
                { "progressbar", "ProgressBar" },
            };

            return mapping.TryGetValue(name, out string tag) ? tag : "VisualElement";
        }

        #endregion

        #region UI Toolkit - USS Generation

        /// <summary>
        /// Generates a USS stylesheet.
        /// </summary>
        private static object GenerateUSS(JObject @params)
        {
            string path = @params["path"]?.ToString() ?? "Assets/UI/GeneratedUI.uss";
            var styles = @params["styles"]?.ToObject<Dictionary<string, Dictionary<string, string>>>();

            if (!path.EndsWith(".uss"))
            {
                path += ".uss";
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("/* Generated by MCP Modern Tools */");
                sb.AppendLine();

                if (styles != null)
                {
                    foreach (var selector in styles)
                    {
                        sb.AppendLine($"{selector.Key} {{");
                        foreach (var prop in selector.Value)
                        {
                            sb.AppendLine($"    {prop.Key}: {prop.Value};");
                        }
                        sb.AppendLine("}");
                        sb.AppendLine();
                    }
                }
                else
                {
                    // Default styles
                    sb.AppendLine(".container {");
                    sb.AppendLine("    flex-grow: 1;");
                    sb.AppendLine("    padding: 16px;");
                    sb.AppendLine("    background-color: #2d2d2d;");
                    sb.AppendLine("}");
                    sb.AppendLine();
                    sb.AppendLine(".header {");
                    sb.AppendLine("    font-size: 24px;");
                    sb.AppendLine("    -unity-font-style: bold;");
                    sb.AppendLine("    color: #ffffff;");
                    sb.AppendLine("    margin-bottom: 16px;");
                    sb.AppendLine("}");
                    sb.AppendLine();
                    sb.AppendLine(".button-primary {");
                    sb.AppendLine("    background-color: #3498db;");
                    sb.AppendLine("    color: #ffffff;");
                    sb.AppendLine("    border-radius: 4px;");
                    sb.AppendLine("    padding: 8px 16px;");
                    sb.AppendLine("}");
                    sb.AppendLine();
                    sb.AppendLine(".button-primary:hover {");
                    sb.AppendLine("    background-color: #2980b9;");
                    sb.AppendLine("}");
                }

                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, sb.ToString());
                AssetDatabase.ImportAsset(path);

                return new SuccessResponse($"USS created: {path}", new
                {
                    path = path,
                    selectors = styles?.Keys.ToArray() ?? new[] { ".container", ".header", ".button-primary" }
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"USS generation failed: {ex.Message}");
            }
        }

        private static object ListVisualElementTypes()
        {
            var elements = new[]
            {
                new { type = "VisualElement", description = "Base container element" },
                new { type = "Label", description = "Text display" },
                new { type = "Button", description = "Clickable button" },
                new { type = "TextField", description = "Text input field" },
                new { type = "Toggle", description = "Checkbox/switch" },
                new { type = "Slider", description = "Value slider" },
                new { type = "SliderInt", description = "Integer slider" },
                new { type = "MinMaxSlider", description = "Range slider" },
                new { type = "DropdownField", description = "Dropdown selection" },
                new { type = "ScrollView", description = "Scrollable container" },
                new { type = "ListView", description = "Virtual list" },
                new { type = "Foldout", description = "Collapsible section" },
                new { type = "Box", description = "Styled container" },
                new { type = "Image", description = "Image display" },
                new { type = "ProgressBar", description = "Progress indicator" },
                new { type = "RadioButton", description = "Radio selection" },
                new { type = "RadioButtonGroup", description = "Radio group" },
            };

            return new SuccessResponse($"{elements.Length} UI Toolkit element types available", new { elements });
        }

        #endregion

        #region Input System

        /// <summary>
        /// Gets all Input Actions from the project.
        /// </summary>
        private static object GetInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            var inputAssets = AssetDatabase.FindAssets("t:InputActionAsset");
            var actions = new List<object>();

            foreach (var guid in inputAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset == null) continue;

                foreach (var map in asset.actionMaps)
                {
                    var mapActions = map.actions.Select(a => new
                    {
                        name = a.name,
                        type = a.type.ToString(),
                        bindings = a.bindings.Select(b => new
                        {
                            path = b.path,
                            interactions = b.interactions,
                            isComposite = b.isComposite
                        }).ToList()
                    }).ToList();

                    actions.Add(new
                    {
                        assetPath = path,
                        assetName = asset.name,
                        actionMap = map.name,
                        actions = mapActions
                    });
                }
            }

            return new SuccessResponse($"Found {actions.Count} action maps", new { actionMaps = actions });
#else
            return new ErrorResponse("Input System package not installed. Add 'com.unity.inputsystem' via Package Manager.");
#endif
        }

        /// <summary>
        /// Creates a new Input Action asset.
        /// </summary>
        private static object CreateInputAction(JObject @params)
        {
#if ENABLE_INPUT_SYSTEM
            string path = @params["path"]?.ToString() ?? "Assets/Input/NewInputActions.inputactions";
            string actionName = @params["name"]?.ToString() ?? "NewAction";
            string mapName = @params["map"]?.ToString() ?? "Player";

            try
            {
                if (!path.EndsWith(".inputactions"))
                    path += ".inputactions";

                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Create or load asset
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                bool isNew = asset == null;

                if (isNew)
                {
                    asset = ScriptableObject.CreateInstance<InputActionAsset>();
                }

                // Get or create action map
                var map = asset.FindActionMap(mapName);
                if (map == null)
                {
                    map = asset.AddActionMap(mapName);
                }

                // Add action
                if (map.FindAction(actionName) == null)
                {
                    var action = map.AddAction(actionName, InputActionType.Button);
                    action.AddBinding("<Keyboard>/space");
                }

                if (isNew)
                {
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    EditorUtility.SetDirty(asset);
                }

                AssetDatabase.SaveAssets();

                return new SuccessResponse($"Input action created: {actionName} in {mapName}", new
                {
                    path = path,
                    actionMap = mapName,
                    action = actionName
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Failed to create input action: {ex.Message}");
            }
#else
            return new ErrorResponse("Input System package not installed.");
#endif
        }

        #endregion

        #region Package Manager

        private static ListRequest _listRequest;
        private static SearchRequest _searchRequest;
        private static AddRequest _addRequest;
        private static RemoveRequest _removeRequest;

        /// <summary>
        /// Lists installed packages.
        /// </summary>
        private static object ListPackages()
        {
            _listRequest = Client.List(true);
            
            // Wait for completion (with timeout)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!_listRequest.IsCompleted && sw.ElapsedMilliseconds < 10000)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (_listRequest.Status == StatusCode.Success)
            {
                var packages = _listRequest.Result.Select(p => new
                {
                    name = p.name,
                    displayName = p.displayName,
                    version = p.version,
                    source = p.source.ToString(),
                    description = p.description?.Substring(0, Math.Min(100, p.description?.Length ?? 0))
                }).ToList();

                return new SuccessResponse($"Found {packages.Count} packages", new { packages });
            }

            return new ErrorResponse($"Failed to list packages: {_listRequest.Error?.message}");
        }

        /// <summary>
        /// Adds a package by ID or Git URL.
        /// </summary>
        private static object AddPackage(JObject @params)
        {
            string packageId = @params["package"]?.ToString() ?? @params["id"]?.ToString();
            if (string.IsNullOrEmpty(packageId))
            {
                return new ErrorResponse("Package ID or URL required");
            }

            // Security check
            var denied = MCPSecurity.CheckApprovalOrError(
                "add_package",
                $"Install package: {packageId}",
                DangerousOperationType.ProjectSettings
            );
            if (denied != null) return denied;

            _addRequest = Client.Add(packageId);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!_addRequest.IsCompleted && sw.ElapsedMilliseconds < 60000)
            {
                System.Threading.Thread.Sleep(500);
            }

            if (_addRequest.Status == StatusCode.Success)
            {
                return new SuccessResponse($"Package installed: {_addRequest.Result.displayName} v{_addRequest.Result.version}", new
                {
                    name = _addRequest.Result.name,
                    version = _addRequest.Result.version,
                    displayName = _addRequest.Result.displayName
                });
            }

            return new ErrorResponse($"Failed to install package: {_addRequest.Error?.message}");
        }

        /// <summary>
        /// Removes a package.
        /// </summary>
        private static object RemovePackage(JObject @params)
        {
            string packageName = @params["package"]?.ToString() ?? @params["name"]?.ToString();
            if (string.IsNullOrEmpty(packageName))
            {
                return new ErrorResponse("Package name required");
            }

            // Security check
            var denied = MCPSecurity.CheckApprovalOrError(
                "remove_package",
                $"Uninstall package: {packageName}",
                DangerousOperationType.Destructive
            );
            if (denied != null) return denied;

            _removeRequest = Client.Remove(packageName);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!_removeRequest.IsCompleted && sw.ElapsedMilliseconds < 30000)
            {
                System.Threading.Thread.Sleep(500);
            }

            if (_removeRequest.Status == StatusCode.Success)
            {
                return new SuccessResponse($"Package removed: {packageName}");
            }

            return new ErrorResponse($"Failed to remove package: {_removeRequest.Error?.message}");
        }

        /// <summary>
        /// Searches for packages in the Unity registry.
        /// </summary>
        private static object SearchPackages(JObject @params)
        {
            string query = @params["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
            {
                return new ErrorResponse("Search query required");
            }

            _searchRequest = Client.SearchAll();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!_searchRequest.IsCompleted && sw.ElapsedMilliseconds < 15000)
            {
                System.Threading.Thread.Sleep(200);
            }

            if (_searchRequest.Status == StatusCode.Success)
            {
                var results = _searchRequest.Result
                    .Where(p => p.name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               (p.displayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (p.description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Take(20)
                    .Select(p => new
                    {
                        name = p.name,
                        displayName = p.displayName,
                        version = p.versions?.latest ?? "unknown",
                        description = p.description?.Substring(0, Math.Min(100, p.description?.Length ?? 0))
                    }).ToList();

                return new SuccessResponse($"Found {results.Count} packages matching '{query}'", new { results });
            }

            return new ErrorResponse($"Search failed: {_searchRequest.Error?.message}");
        }

        #endregion

        #region Helpers

        private class UXMLElement
        {
            public string type { get; set; }
            public string name { get; set; }
            public string text { get; set; }
            public string className { get; set; }
            public List<UXMLElement> children { get; set; }
        }

        private class LayoutPart
        {
            public string name;
            public string[] children;
        }

        private static List<LayoutPart> ParseLayoutParts(string layout)
        {
            var parts = new List<LayoutPart>();
            var regex = new System.Text.RegularExpressions.Regex(@"(\w+)(?:\(([^)]+)\))?");
            var matches = regex.Matches(layout);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var part = new LayoutPart
                {
                    name = match.Groups[1].Value
                };

                if (match.Groups[2].Success)
                {
                    part.children = match.Groups[2].Value.Split(',').Select(s => s.Trim()).ToArray();
                }

                parts.Add(part);
            }

            return parts;
        }

        private static string ToKebabCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append('-');
                }
                sb.Append(char.ToLower(c));
            }
            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Unified Package Manager tool (replaces old package_manager).
    /// </summary>
    [McpForUnityTool(
        name: "upm_control",
        Description = "Unity Package Manager: list, add, remove, search packages. Modern async API.")]
    public static class MCPUPMControl
    {
        public static object HandleCommand(JObject @params)
        {
            // Delegate to MCPModernTools for unified implementation
            string action = @params["action"]?.ToString()?.ToLower() ?? "list_packages";
            
            // Map old actions to new
            switch (action)
            {
                case "list":
                    @params["action"] = "list_packages";
                    break;
                case "add":
                case "install":
                    @params["action"] = "add_package";
                    break;
                case "remove":
                case "uninstall":
                    @params["action"] = "remove_package";
                    break;
                case "search":
                    @params["action"] = "search_packages";
                    break;
            }

            return MCPModernTools.HandleCommand(@params);
        }
    }
}
