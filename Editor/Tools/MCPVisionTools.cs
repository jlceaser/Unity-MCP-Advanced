#nullable disable
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// "THE EYE" - Multimodal Vision Support for AI
    /// Enables AI to visually perceive the Unity scene through Base64-encoded screenshots.
    /// First MCP implementation with true visual feedback capability.
    /// </summary>
    [McpForUnityTool(
        name: "vision_capture",
        Description = "Multimodal vision: capture GameView/SceneView as Base64 image. Actions: screenshot, get_ui_bounds, analyze_layout")]
    public static class MCPVisionTools
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "screenshot";

            switch (action)
            {
                case "screenshot":
                case "capture":
                    return CaptureScreenshot(@params);

                case "get_ui_bounds":
                    return GetUIBounds(@params);

                case "analyze_layout":
                    return AnalyzeLayout();

                case "get_cameras":
                    return GetCameraList();

                default:
                    return new SuccessResponse("Vision Capture ready. Actions: screenshot, get_ui_bounds, analyze_layout, get_cameras");
            }
        }

        #region Screenshot Capture

        /// <summary>
        /// Captures a screenshot and returns it as Base64-encoded string.
        /// </summary>
        private static object CaptureScreenshot(JObject @params)
        {
            try
            {
                string viewType = @params["viewType"]?.ToString()?.ToLower() ?? "gameview";
                float scale = @params["scale"]?.ToObject<float>() ?? 1.0f;
                scale = Mathf.Clamp(scale, 0.1f, 2.0f);
                
                string format = @params["format"]?.ToString()?.ToLower() ?? "jpg";
                int quality = @params["quality"]?.ToObject<int>() ?? 75;
                quality = Mathf.Clamp(quality, 10, 100);

                Texture2D screenshot = null;
                int width = 0;
                int height = 0;

                if (viewType == "sceneview")
                {
                    screenshot = CaptureSceneView(scale, out width, out height);
                }
                else
                {
                    screenshot = CaptureGameView(scale, out width, out height);
                }

                if (screenshot == null)
                {
                    return new ErrorResponse("Failed to capture screenshot. Ensure a camera is available.");
                }

                // Encode to bytes
                byte[] imageBytes;
                string mimeType;

                if (format == "png")
                {
                    imageBytes = screenshot.EncodeToPNG();
                    mimeType = "image/png";
                }
                else
                {
                    imageBytes = screenshot.EncodeToJPG(quality);
                    mimeType = "image/jpeg";
                }

                // Cleanup
                UnityEngine.Object.DestroyImmediate(screenshot);

                // Convert to Base64
                string base64 = Convert.ToBase64String(imageBytes);

                return new SuccessResponse($"Screenshot captured: {width}x{height} ({format.ToUpper()})", new
                {
                    base64 = base64,
                    mimeType = mimeType,
                    width = width,
                    height = height,
                    format = format,
                    sizeBytes = imageBytes.Length,
                    viewType = viewType
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Screenshot failed: {ex.Message}");
            }
        }

        private static Texture2D CaptureGameView(float scale, out int width, out int height)
        {
            // Try to find main camera or any camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                cam = cameras.FirstOrDefault(c => c.enabled);
            }

            if (cam == null)
            {
                width = 0;
                height = 0;
                return null;
            }

            // Calculate resolution
            width = Mathf.RoundToInt(Screen.width * scale);
            height = Mathf.RoundToInt(Screen.height * scale);

            // Minimum size
            width = Mathf.Max(width, 320);
            height = Mathf.Max(height, 240);

            // Create render texture
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;

            // Render camera to texture
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            // Read pixels
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            // Cleanup
            UnityEngine.Object.DestroyImmediate(rt);

            return tex;
        }

        private static Texture2D CaptureSceneView(float scale, out int width, out int height)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null && SceneView.sceneViews.Count > 0)
            {
                sceneView = (SceneView)SceneView.sceneViews[0];
            }

            if (sceneView == null)
            {
                // Fallback to game view
                return CaptureGameView(scale, out width, out height);
            }

            Camera cam = sceneView.camera;
            if (cam == null)
            {
                width = 0;
                height = 0;
                return null;
            }

            // Use SceneView dimensions
            width = Mathf.RoundToInt(sceneView.position.width * scale);
            height = Mathf.RoundToInt(sceneView.position.height * scale);

            width = Mathf.Max(width, 320);
            height = Mathf.Max(height, 240);

            // Force repaint
            sceneView.Repaint();

            // Create render texture
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;

            // Render
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            // Read pixels
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            UnityEngine.Object.DestroyImmediate(rt);

            return tex;
        }

        #endregion

        #region UI Analysis

        /// <summary>
        /// Gets bounds of UI elements for layout verification.
        /// </summary>
        private static object GetUIBounds(JObject @params)
        {
            string targetName = @params["target"]?.ToString();

            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length == 0)
            {
                return new ErrorResponse("No Canvas found in scene");
            }

            var uiElements = new System.Collections.Generic.List<object>();

            foreach (var canvas in canvases)
            {
                var rectTransforms = canvas.GetComponentsInChildren<RectTransform>(true);
                foreach (var rt in rectTransforms)
                {
                    if (!string.IsNullOrEmpty(targetName) && !rt.name.Contains(targetName))
                        continue;

                    Vector3[] corners = new Vector3[4];
                    rt.GetWorldCorners(corners);

                    // Convert to screen coordinates
                    Camera cam = canvas.worldCamera ?? Camera.main;
                    if (cam != null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        Vector2 screenMin, screenMax;
                        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            screenMin = corners[0];
                            screenMax = corners[2];
                        }
                        else
                        {
                            screenMin = cam.WorldToScreenPoint(corners[0]);
                            screenMax = cam.WorldToScreenPoint(corners[2]);
                        }

                        uiElements.Add(new
                        {
                            name = rt.name,
                            path = GetPath(rt),
                            screenBounds = new
                            {
                                x = screenMin.x,
                                y = screenMin.y,
                                width = screenMax.x - screenMin.x,
                                height = screenMax.y - screenMin.y
                            },
                            anchoredPosition = new { x = rt.anchoredPosition.x, y = rt.anchoredPosition.y },
                            sizeDelta = new { x = rt.sizeDelta.x, y = rt.sizeDelta.y },
                            active = rt.gameObject.activeInHierarchy
                        });
                    }
                }
            }

            return new SuccessResponse($"Found {uiElements.Count} UI elements", new { elements = uiElements });
        }

        /// <summary>
        /// Analyzes the overall scene layout.
        /// </summary>
        private static object AnalyzeLayout()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);

            var cameraInfo = cameras.Select(c => new
            {
                name = c.name,
                position = new { x = c.transform.position.x, y = c.transform.position.y, z = c.transform.position.z },
                fieldOfView = c.fieldOfView,
                orthographic = c.orthographic,
                depth = c.depth,
                enabled = c.enabled
            }).ToList();

            var canvasInfo = canvases.Select(c => new
            {
                name = c.name,
                renderMode = c.renderMode.ToString(),
                sortingOrder = c.sortingOrder,
                childCount = c.transform.childCount
            }).ToList();

            var lightInfo = lights.Select(l => new
            {
                name = l.name,
                type = l.type.ToString(),
                intensity = l.intensity,
                color = $"#{ColorUtility.ToHtmlStringRGB(l.color)}",
                enabled = l.enabled
            }).ToList();

            return new SuccessResponse("Scene layout analyzed", new
            {
                cameras = cameraInfo,
                canvases = canvasInfo,
                lights = lightInfo,
                screenResolution = new { width = Screen.width, height = Screen.height },
                aspectRatio = (float)Screen.width / Screen.height
            });
        }

        private static object GetCameraList()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var list = cameras.Select(c => new
            {
                name = c.name,
                isMain = c == Camera.main,
                enabled = c.enabled,
                depth = c.depth,
                cullingMask = c.cullingMask
            }).ToList();

            return new SuccessResponse($"Found {cameras.Length} cameras", new { cameras = list });
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

        #endregion
    }
}
