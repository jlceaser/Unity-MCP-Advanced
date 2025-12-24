#nullable disable
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro is available based on VibeUnityUI
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// MCPVibeTools
    /// Migrated functionality from legacy com.ricoder.vibe-unity package.
    /// Provides tools for:
    /// 1. Scene Management (Create, Load)
    /// 2. UI Generation (Canvas, Panels, Buttons, Text, ScrollViews)
    /// </summary>
    [McpForUnityTool(
        name: "vibe_tools",
        Description = "Legacy Vibe tools migrated to MCP. Actions: create_scene, add_canvas, add_panel, add_button, add_text, add_scrollview")]
    public static class MCPVibeTools
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("Vibe Tools ready. Actions: create_scene, add_canvas, add_panel, add_button, add_text, add_scrollview");
            }

            try
            {
                switch (action)
                {
                    case "create_scene":
                        return CreateScene(@params);
                    case "add_canvas":
                        return AddCanvas(@params);
                    case "add_panel":
                        return AddPanel(@params);
                    case "add_button":
                        return AddButton(@params);
                    case "add_text":
                        return AddText(@params);
                    case "add_scrollview":
                        return AddScrollView(@params);
                    default:
                        return new ErrorResponse($"Unknown action '{action}'");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Error executing {action}: {ex.Message}");
            }
        }

        #region Scene Management

        private static object CreateScene(JObject @params)
        {
            string name = @params["name"]?.ToString();
            string path = @params["path"]?.ToString() ?? "Assets/Scenes";
            string type = @params["type"]?.ToString() ?? "DefaultGameObjects";
            bool addToBuild = @params["addToBuild"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(name)) return new ErrorResponse("Required: name");

            // Ensure directory exists
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            string fullPath = $"{path}/{name}.unity";
            if (File.Exists(fullPath)) return new ErrorResponse($"Scene already exists: {fullPath}");

            NewSceneSetup setup = type.ToLower() switch
            {
                "empty" => NewSceneSetup.EmptyScene,
                _ => NewSceneSetup.DefaultGameObjects
            };

            var scene = EditorSceneManager.NewScene(setup, NewSceneMode.Single);
            bool saved = EditorSceneManager.SaveScene(scene, fullPath);

            if (saved)
            {
                if (addToBuild)
                {
                    var builds = EditorBuildSettings.scenes.ToList();
                    if (!builds.Any(s => s.path == fullPath))
                    {
                        builds.Add(new EditorBuildSettingsScene(fullPath, true));
                        EditorBuildSettings.scenes = builds.ToArray();
                    }
                }
                AssetDatabase.Refresh();
                return new SuccessResponse($"Created scene '{name}' at {fullPath}");
            }

            return new ErrorResponse("Failed to save scene");
        }

        #endregion

        #region UI Creation

        private static object AddCanvas(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "Canvas";
            string mode = @params["renderMode"]?.ToString() ?? "ScreenSpaceOverlay";
            int width = @params["width"]?.ToObject<int>() ?? 1920;
            int height = @params["height"]?.ToObject<int>() ?? 1080;

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            var scaler = go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            canvas.renderMode = mode.ToLower() switch
            {
                "screenspacecamera" => RenderMode.ScreenSpaceCamera,
                "worldspace" => RenderMode.WorldSpace,
                _ => RenderMode.ScreenSpaceOverlay
            };

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(width, height);

            EnsureEventSystem();
            
            return new SuccessResponse($"Created Canvas '{name}'");
        }

        private static object AddPanel(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "Panel";
            string parent = @params["parent"]?.ToString();
            
            var parentObj = FindParent(parent, true); // true = auto find canvas
            if (parentObj == null) return new ErrorResponse("No parent/Canvas found");

            var go = new GameObject(name);
            go.transform.SetParent(parentObj.transform, false);
            
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.39f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; // Full stretch

            return new SuccessResponse($"Created Panel '{name}'");
        }

        private static object AddButton(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "Button";
            string text = @params["text"]?.ToString() ?? "Button";
            string parent = @params["parent"]?.ToString();

            var parentObj = FindParent(parent, true);
            if (parentObj == null) return new ErrorResponse("No parent/Canvas found");

            var go = new GameObject(name);
            go.transform.SetParent(parentObj.transform, false);

            var img = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();

            var textGO = new GameObject("Text (TMP)");
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);

            return new SuccessResponse($"Created Button '{name}'");
        }

        private static object AddText(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "Text";
            string text = @params["text"]?.ToString() ?? "New Text";
            int size = @params["fontSize"]?.ToObject<int>() ?? 24;
            string parent = @params["parent"]?.ToString();

            var parentObj = FindParent(parent, true);
            if (parentObj == null) return new ErrorResponse("No parent/Canvas found");

            var go = new GameObject(name);
            go.transform.SetParent(parentObj.transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;

            return new SuccessResponse($"Created Text '{name}'");
        }

        private static object AddScrollView(JObject @params)
        {
            string name = @params["name"]?.ToString() ?? "ScrollView";
            string parent = @params["parent"]?.ToString();
            
            var parentObj = FindParent(parent, true);
            if (parentObj == null) return new ErrorResponse("No parent/Canvas found");

            // Minimal ScrollView creation
            var go = new GameObject(name);
            go.transform.SetParent(parentObj.transform, false);
            
            go.AddComponent<Image>().color = new Color(1, 1, 1, 0.39f);
            var scroll = go.AddComponent<ScrollRect>();
            
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            
            // Link refs
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            // Simple layout
            var vRect = viewport.GetComponent<RectTransform>();
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.sizeDelta = Vector2.zero;
            
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 200);

            return new SuccessResponse($"Created ScrollView '{name}'");
        }

        #endregion

        #region Helpers

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private static GameObject FindParent(string name, bool autoFindCanvas)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var go = GameObject.Find(name);
                if (go != null) return go;
            }

            if (autoFindCanvas)
            {
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas != null) return canvas.gameObject;
            }

            return null;
        }

        #endregion
    }
}
