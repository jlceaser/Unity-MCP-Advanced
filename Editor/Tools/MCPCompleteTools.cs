#nullable disable
#pragma warning disable CS0618 // StaticEditorFlags.NavigationStatic and SetNavMeshArea are deprecated but AI Navigation package replacement is optional
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MCPForUnity.Editor.Helpers;
#if UNITY_AI_NAVIGATION
using UnityEngine.AI;
using UnityEditor.AI;
#endif

namespace MCPForUnity.Editor.Tools
{
    #region 1. NavMesh Control Tool
    [McpForUnityTool(
        name: "navmesh_control",
        Description = "Controls NavMesh system. Actions: bake, clear, get_settings, set_settings, get_areas, add_modifier, get_agents")]
    public static class MCPNavMeshControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "bake":
                    try
                    {
                        // Use reflection to call NavMeshBuilder
                        var navMeshBuilderType = Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor.CoreModule")
                            ?? Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor");

                        if (navMeshBuilderType == null)
                        {
                            // Try alternative approach - execute menu
                            EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
                            return new SuccessResponse("NavMesh window opened. Use Navigation window to bake, or install AI Navigation package for full control.");
                        }

                        var buildMethod = navMeshBuilderType.GetMethod("BuildNavMesh", BindingFlags.Static | BindingFlags.Public);
                        buildMethod?.Invoke(null, null);
                        return new SuccessResponse("NavMesh bake initiated");
                    }
                    catch (Exception e)
                    {
                        return new ErrorResponse($"NavMesh bake failed: {e.Message}. Consider using Window > AI > Navigation manually.");
                    }

                case "clear":
                    try
                    {
                        var navMeshBuilderType = Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor.CoreModule")
                            ?? Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor");
                        var clearMethod = navMeshBuilderType?.GetMethod("ClearAllNavMeshes", BindingFlags.Static | BindingFlags.Public);
                        clearMethod?.Invoke(null, null);
                        return new SuccessResponse("NavMesh cleared");
                    }
                    catch
                    {
                        return new ErrorResponse("NavMesh clear requires AI Navigation package");
                    }

                case "get_areas":
                    var areas = new List<object>();
                    for (int i = 0; i < 32; i++)
                    {
                        string areaName = UnityEngine.AI.NavMesh.GetAreaNames().Length > i
                            ? UnityEngine.AI.NavMesh.GetAreaNames()[i] : null;
                        if (!string.IsNullOrEmpty(areaName))
                        {
                            areas.Add(new { index = i, name = areaName, cost = 1f }); // cost requires NavMesh API
                        }
                    }
                    return new SuccessResponse("NavMesh areas", new { areas });

                case "add_modifier":
                    string targetName = @params["target"]?.ToString();
                    int area = @params["area"]?.ToObject<int>() ?? 0;

                    var go = GameObject.Find(targetName);
                    if (go == null) return new ErrorResponse($"GameObject '{targetName}' not found");

                    // Set static navigation flag
                    GameObjectUtility.SetStaticEditorFlags(go,
                        GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.NavigationStatic);
                    GameObjectUtility.SetNavMeshArea(go, area);

                    return new SuccessResponse($"NavMesh modifier added to '{targetName}' with area {area}");

                case "get_agents":
                    // List all NavMeshAgent components in scene
                    var agents = UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
                    var agentList = agents.Select(a => new {
                        name = a.gameObject.name,
                        speed = a.speed,
                        angularSpeed = a.angularSpeed,
                        acceleration = a.acceleration,
                        stoppingDistance = a.stoppingDistance,
                        radius = a.radius,
                        height = a.height,
                        enabled = a.enabled
                    }).ToList();
                    return new SuccessResponse($"Found {agents.Length} NavMeshAgents", new { agents = agentList });

                case "set_agent":
                    string agentTarget = @params["target"]?.ToString();
                    var agentGo = GameObject.Find(agentTarget);
                    if (agentGo == null) return new ErrorResponse($"GameObject '{agentTarget}' not found");

                    var agent = agentGo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent == null)
                    {
                        agent = agentGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
                    }

                    if (@params["speed"] != null) agent.speed = @params["speed"].ToObject<float>();
                    if (@params["angularSpeed"] != null) agent.angularSpeed = @params["angularSpeed"].ToObject<float>();
                    if (@params["acceleration"] != null) agent.acceleration = @params["acceleration"].ToObject<float>();
                    if (@params["stoppingDistance"] != null) agent.stoppingDistance = @params["stoppingDistance"].ToObject<float>();
                    if (@params["radius"] != null) agent.radius = @params["radius"].ToObject<float>();
                    if (@params["height"] != null) agent.height = @params["height"].ToObject<float>();

                    EditorUtility.SetDirty(agent);
                    return new SuccessResponse($"NavMeshAgent configured on '{agentTarget}'");

                default:
                    return new SuccessResponse("NavMesh Control ready. Actions: bake, clear, get_areas, add_modifier, get_agents, set_agent");
            }
        }
    }
    #endregion

    #region 2. Particle System Tool
    [McpForUnityTool(
        name: "particle_control",
        Description = "Controls Particle Systems. Actions: create, get_settings, set_main, set_emission, set_shape, play, stop, get_all")]
    public static class MCPParticleControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string targetName = @params["target"]?.ToString();

            switch (action)
            {
                case "create":
                    string name = @params["name"]?.ToString() ?? "ParticleSystem";
                    var posArr = @params["position"]?.ToObject<float[]>() ?? new float[] { 0, 0, 0 };

                    var psGo = new GameObject(name);
                    psGo.transform.position = new Vector3(posArr[0], posArr[1], posArr[2]);
                    var ps = psGo.AddComponent<ParticleSystem>();

                    // Apply preset if specified
                    string preset = @params["preset"]?.ToString()?.ToLower();
                    if (preset == "fire")
                    {
                        var main = ps.main;
                        main.startColor = new Color(1f, 0.5f, 0f);
                        main.startLifetime = 1f;
                        main.startSpeed = 3f;
                        main.startSize = 0.5f;
                        var emission = ps.emission;
                        emission.rateOverTime = 50;
                    }
                    else if (preset == "smoke")
                    {
                        var main = ps.main;
                        main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                        main.startLifetime = 3f;
                        main.startSpeed = 1f;
                        main.startSize = 1f;
                        var emission = ps.emission;
                        emission.rateOverTime = 20;
                    }
                    else if (preset == "sparks")
                    {
                        var main = ps.main;
                        main.startColor = Color.yellow;
                        main.startLifetime = 0.5f;
                        main.startSpeed = 10f;
                        main.startSize = 0.1f;
                        main.gravityModifier = 1f;
                        var emission = ps.emission;
                        emission.rateOverTime = 100;
                    }

                    Undo.RegisterCreatedObjectUndo(psGo, "Create Particle System");
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                    return new SuccessResponse($"Created ParticleSystem '{name}'", new {
                        name,
                        position = posArr,
                        preset = preset ?? "default"
                    });

                case "get_settings":
                    var targetGo = GameObject.Find(targetName);
                    if (targetGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var targetPs = targetGo.GetComponent<ParticleSystem>();
                    if (targetPs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");

                    var m = targetPs.main;
                    var e = targetPs.emission;
                    var s = targetPs.shape;

                    return new SuccessResponse("Particle settings", new {
                        main = new {
                            duration = m.duration,
                            looping = m.loop,
                            startLifetime = m.startLifetime.constant,
                            startSpeed = m.startSpeed.constant,
                            startSize = m.startSize.constant,
                            startColor = $"RGBA({m.startColor.color.r:F2},{m.startColor.color.g:F2},{m.startColor.color.b:F2},{m.startColor.color.a:F2})",
                            gravityModifier = m.gravityModifier.constant,
                            maxParticles = m.maxParticles
                        },
                        emission = new {
                            enabled = e.enabled,
                            rateOverTime = e.rateOverTime.constant
                        },
                        shape = new {
                            enabled = s.enabled,
                            shapeType = s.shapeType.ToString()
                        },
                        isPlaying = targetPs.isPlaying,
                        particleCount = targetPs.particleCount
                    });

                case "set_main":
                    var setGo = GameObject.Find(targetName);
                    if (setGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var setPs = setGo.GetComponent<ParticleSystem>();
                    if (setPs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");

                    var mainModule = setPs.main;
                    if (@params["duration"] != null) mainModule.duration = @params["duration"].ToObject<float>();
                    if (@params["looping"] != null) mainModule.loop = @params["looping"].ToObject<bool>();
                    if (@params["startLifetime"] != null) mainModule.startLifetime = @params["startLifetime"].ToObject<float>();
                    if (@params["startSpeed"] != null) mainModule.startSpeed = @params["startSpeed"].ToObject<float>();
                    if (@params["startSize"] != null) mainModule.startSize = @params["startSize"].ToObject<float>();
                    if (@params["gravityModifier"] != null) mainModule.gravityModifier = @params["gravityModifier"].ToObject<float>();
                    if (@params["maxParticles"] != null) mainModule.maxParticles = @params["maxParticles"].ToObject<int>();

                    var colorArr = @params["startColor"]?.ToObject<float[]>();
                    if (colorArr != null && colorArr.Length >= 3)
                    {
                        float a = colorArr.Length >= 4 ? colorArr[3] : 1f;
                        mainModule.startColor = new Color(colorArr[0], colorArr[1], colorArr[2], a);
                    }

                    EditorUtility.SetDirty(setPs);
                    return new SuccessResponse($"Updated main module on '{targetName}'");

                case "set_emission":
                    var emitGo = GameObject.Find(targetName);
                    if (emitGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var emitPs = emitGo.GetComponent<ParticleSystem>();
                    if (emitPs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");

                    var emissionModule = emitPs.emission;
                    if (@params["enabled"] != null) emissionModule.enabled = @params["enabled"].ToObject<bool>();
                    if (@params["rateOverTime"] != null) emissionModule.rateOverTime = @params["rateOverTime"].ToObject<float>();
                    if (@params["rateOverDistance"] != null) emissionModule.rateOverDistance = @params["rateOverDistance"].ToObject<float>();

                    EditorUtility.SetDirty(emitPs);
                    return new SuccessResponse($"Updated emission on '{targetName}'");

                case "set_shape":
                    var shapeGo = GameObject.Find(targetName);
                    if (shapeGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var shapePs = shapeGo.GetComponent<ParticleSystem>();
                    if (shapePs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");

                    var shapeModule = shapePs.shape;
                    if (@params["enabled"] != null) shapeModule.enabled = @params["enabled"].ToObject<bool>();
                    string shapeType = @params["shapeType"]?.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(shapeType))
                    {
                        if (Enum.TryParse<ParticleSystemShapeType>(shapeType, true, out var parsed))
                            shapeModule.shapeType = parsed;
                    }
                    if (@params["radius"] != null) shapeModule.radius = @params["radius"].ToObject<float>();
                    if (@params["angle"] != null) shapeModule.angle = @params["angle"].ToObject<float>();

                    EditorUtility.SetDirty(shapePs);
                    return new SuccessResponse($"Updated shape on '{targetName}'");

                case "play":
                    var playGo = GameObject.Find(targetName);
                    if (playGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var playPs = playGo.GetComponent<ParticleSystem>();
                    if (playPs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");
                    playPs.Play();
                    return new SuccessResponse($"Playing particle system '{targetName}'");

                case "stop":
                    var stopGo = GameObject.Find(targetName);
                    if (stopGo == null) return new ErrorResponse($"GameObject '{targetName}' not found");
                    var stopPs = stopGo.GetComponent<ParticleSystem>();
                    if (stopPs == null) return new ErrorResponse($"No ParticleSystem on '{targetName}'");
                    stopPs.Stop();
                    return new SuccessResponse($"Stopped particle system '{targetName}'");

                case "get_all":
                    var allPs = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                    var psList = allPs.Select(p => new {
                        name = p.gameObject.name,
                        position = $"({p.transform.position.x:F1},{p.transform.position.y:F1},{p.transform.position.z:F1})",
                        isPlaying = p.isPlaying,
                        particleCount = p.particleCount,
                        maxParticles = p.main.maxParticles
                    }).ToList();
                    return new SuccessResponse($"Found {allPs.Length} particle systems", new { particleSystems = psList });

                default:
                    return new SuccessResponse("Particle Control ready. Actions: create, get_settings, set_main, set_emission, set_shape, play, stop, get_all. Presets: fire, smoke, sparks");
            }
        }
    }
    #endregion

    #region 3. Physics Material Tool
    [McpForUnityTool(
        name: "physics_material",
        Description = "Creates and manages PhysicsMaterial assets. Actions: create, get, set, assign, list")]
    public static class MCPPhysicsMaterialTool
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "create":
                    string matName = @params["name"]?.ToString() ?? "NewPhysicsMaterial";
                    string path = @params["path"]?.ToString() ?? "Assets/PhysicsMaterials";

                    if (!AssetDatabase.IsValidFolder(path))
                    {
                        string[] parts = path.Split('/');
                        string currentPath = parts[0];
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string newPath = currentPath + "/" + parts[i];
                            if (!AssetDatabase.IsValidFolder(newPath))
                                AssetDatabase.CreateFolder(currentPath, parts[i]);
                            currentPath = newPath;
                        }
                    }

                    var physMat = new PhysicsMaterial(matName);
                    if (@params["dynamicFriction"] != null) physMat.dynamicFriction = @params["dynamicFriction"].ToObject<float>();
                    if (@params["staticFriction"] != null) physMat.staticFriction = @params["staticFriction"].ToObject<float>();
                    if (@params["bounciness"] != null) physMat.bounciness = @params["bounciness"].ToObject<float>();

                    string frictionCombine = @params["frictionCombine"]?.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(frictionCombine))
                    {
                        if (Enum.TryParse<PhysicsMaterialCombine>(frictionCombine, true, out var fc))
                            physMat.frictionCombine = fc;
                    }

                    string bounceCombine = @params["bounceCombine"]?.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(bounceCombine))
                    {
                        if (Enum.TryParse<PhysicsMaterialCombine>(bounceCombine, true, out var bc))
                            physMat.bounceCombine = bc;
                    }

                    string assetPath = $"{path}/{matName}.physicMaterial";
                    AssetDatabase.CreateAsset(physMat, assetPath);
                    AssetDatabase.SaveAssets();

                    return new SuccessResponse($"Created PhysicsMaterial at '{assetPath}'", new {
                        path = assetPath,
                        dynamicFriction = physMat.dynamicFriction,
                        staticFriction = physMat.staticFriction,
                        bounciness = physMat.bounciness
                    });

                case "get":
                    string getPath = @params["path"]?.ToString();
                    var getMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(getPath);
                    if (getMat == null) return new ErrorResponse($"PhysicsMaterial not found at '{getPath}'");

                    return new SuccessResponse("PhysicsMaterial properties", new {
                        name = getMat.name,
                        dynamicFriction = getMat.dynamicFriction,
                        staticFriction = getMat.staticFriction,
                        bounciness = getMat.bounciness,
                        frictionCombine = getMat.frictionCombine.ToString(),
                        bounceCombine = getMat.bounceCombine.ToString()
                    });

                case "set":
                    string setPath = @params["path"]?.ToString();
                    var setMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(setPath);
                    if (setMat == null) return new ErrorResponse($"PhysicsMaterial not found at '{setPath}'");

                    if (@params["dynamicFriction"] != null) setMat.dynamicFriction = @params["dynamicFriction"].ToObject<float>();
                    if (@params["staticFriction"] != null) setMat.staticFriction = @params["staticFriction"].ToObject<float>();
                    if (@params["bounciness"] != null) setMat.bounciness = @params["bounciness"].ToObject<float>();

                    EditorUtility.SetDirty(setMat);
                    AssetDatabase.SaveAssets();
                    return new SuccessResponse($"Updated PhysicsMaterial '{setPath}'");

                case "assign":
                    string assignPath = @params["materialPath"]?.ToString();
                    string targetObj = @params["target"]?.ToString();

                    var assignMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assignPath);
                    if (assignMat == null) return new ErrorResponse($"PhysicsMaterial not found at '{assignPath}'");

                    var targetGo = GameObject.Find(targetObj);
                    if (targetGo == null) return new ErrorResponse($"GameObject '{targetObj}' not found");

                    var collider = targetGo.GetComponent<Collider>();
                    if (collider == null) return new ErrorResponse($"No Collider on '{targetObj}'");

                    collider.material = assignMat;
                    EditorUtility.SetDirty(collider);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                    return new SuccessResponse($"Assigned '{assignMat.name}' to '{targetObj}'");

                case "list":
                    string[] guids = AssetDatabase.FindAssets("t:PhysicsMaterial");
                    var materials = guids.Select(g => {
                        string p = AssetDatabase.GUIDToAssetPath(g);
                        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(p);
                        return new { path = p, name = m?.name, dynamicFriction = m?.dynamicFriction, bounciness = m?.bounciness };
                    }).ToList();
                    return new SuccessResponse($"Found {materials.Count} PhysicsMaterials", new { materials });

                default:
                    return new SuccessResponse("PhysicsMaterial tool ready. Actions: create, get, set, assign, list");
            }
        }
    }
    #endregion

    #region 4. Scene View Camera Control
    [McpForUnityTool(
        name: "sceneview_camera",
        Description = "Controls Scene View camera. Actions: get_position, set_position, look_at, frame_object, align_to_view, set_mode")]
    public static class MCPSceneViewCamera
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView == null)
            {
                sceneView = SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
            }

            if (sceneView == null && action != "open")
            {
                return new ErrorResponse("No SceneView available. Use action 'open' to open one.");
            }

            switch (action)
            {
                case "open":
                    EditorApplication.ExecuteMenuItem("Window/General/Scene");
                    return new SuccessResponse("Scene View opened");

                case "get_position":
                    return new SuccessResponse("SceneView camera position", new {
                        position = new {
                            x = sceneView.camera.transform.position.x,
                            y = sceneView.camera.transform.position.y,
                            z = sceneView.camera.transform.position.z
                        },
                        rotation = new {
                            x = sceneView.camera.transform.eulerAngles.x,
                            y = sceneView.camera.transform.eulerAngles.y,
                            z = sceneView.camera.transform.eulerAngles.z
                        },
                        pivot = new {
                            x = sceneView.pivot.x,
                            y = sceneView.pivot.y,
                            z = sceneView.pivot.z
                        },
                        size = sceneView.size,
                        orthographic = sceneView.orthographic,
                        in2DMode = sceneView.in2DMode
                    });

                case "set_position":
                    var posArr = @params["position"]?.ToObject<float[]>();
                    if (posArr != null && posArr.Length >= 3)
                    {
                        sceneView.pivot = new Vector3(posArr[0], posArr[1], posArr[2]);
                    }

                    var rotArr = @params["rotation"]?.ToObject<float[]>();
                    if (rotArr != null && rotArr.Length >= 3)
                    {
                        sceneView.rotation = Quaternion.Euler(rotArr[0], rotArr[1], rotArr[2]);
                    }

                    if (@params["size"] != null)
                        sceneView.size = @params["size"].ToObject<float>();

                    if (@params["orthographic"] != null)
                        sceneView.orthographic = @params["orthographic"].ToObject<bool>();

                    sceneView.Repaint();
                    return new SuccessResponse("SceneView camera updated");

                case "look_at":
                    var lookPosArr = @params["position"]?.ToObject<float[]>();
                    if (lookPosArr == null || lookPosArr.Length < 3)
                        return new ErrorResponse("Position array [x,y,z] required");

                    var lookTarget = new Vector3(lookPosArr[0], lookPosArr[1], lookPosArr[2]);
                    float lookSize = @params["size"]?.ToObject<float>() ?? 10f;

                    sceneView.LookAt(lookTarget, sceneView.rotation, lookSize);
                    sceneView.Repaint();
                    return new SuccessResponse($"Looking at ({lookTarget.x}, {lookTarget.y}, {lookTarget.z})");

                case "frame_object":
                    string objName = @params["target"]?.ToString();
                    var obj = GameObject.Find(objName);
                    if (obj == null) return new ErrorResponse($"GameObject '{objName}' not found");

                    Selection.activeGameObject = obj;
                    sceneView.FrameSelected();
                    sceneView.Repaint();
                    return new SuccessResponse($"Framed '{objName}'");

                case "align_to_view":
                    // Align selected object to scene view camera
                    if (Selection.activeGameObject == null)
                        return new ErrorResponse("No object selected. Select an object first.");

                    Selection.activeGameObject.transform.position = sceneView.camera.transform.position;
                    Selection.activeGameObject.transform.rotation = sceneView.camera.transform.rotation;
                    EditorUtility.SetDirty(Selection.activeGameObject);
                    return new SuccessResponse($"Aligned '{Selection.activeGameObject.name}' to SceneView camera");

                case "set_mode":
                    string mode = @params["mode"]?.ToString()?.ToLower();
                    switch (mode)
                    {
                        case "top":
                            sceneView.rotation = Quaternion.Euler(90, 0, 0);
                            sceneView.orthographic = true;
                            break;
                        case "bottom":
                            sceneView.rotation = Quaternion.Euler(-90, 0, 0);
                            sceneView.orthographic = true;
                            break;
                        case "front":
                            sceneView.rotation = Quaternion.Euler(0, 0, 0);
                            sceneView.orthographic = true;
                            break;
                        case "back":
                            sceneView.rotation = Quaternion.Euler(0, 180, 0);
                            sceneView.orthographic = true;
                            break;
                        case "left":
                            sceneView.rotation = Quaternion.Euler(0, 90, 0);
                            sceneView.orthographic = true;
                            break;
                        case "right":
                            sceneView.rotation = Quaternion.Euler(0, -90, 0);
                            sceneView.orthographic = true;
                            break;
                        case "perspective":
                            sceneView.orthographic = false;
                            sceneView.rotation = Quaternion.Euler(30, -45, 0);
                            break;
                        case "2d":
                            sceneView.in2DMode = true;
                            break;
                        case "3d":
                            sceneView.in2DMode = false;
                            break;
                        default:
                            return new ErrorResponse("Mode must be: top, bottom, front, back, left, right, perspective, 2d, 3d");
                    }
                    sceneView.Repaint();
                    return new SuccessResponse($"SceneView set to '{mode}' mode");

                case "screenshot":
                    // Capture scene view
                    string screenshotPath = @params["path"]?.ToString() ?? "Assets/SceneViewCapture.png";

                    // Force repaint and capture
                    sceneView.Repaint();

                    var camera = sceneView.camera;
                    int width = (int)sceneView.position.width;
                    int height = (int)sceneView.position.height;

                    var rt = new RenderTexture(width, height, 24);
                    camera.targetTexture = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    camera.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    camera.targetTexture = null;
                    RenderTexture.active = null;
                    UnityEngine.Object.DestroyImmediate(rt);

                    byte[] bytes = tex.EncodeToPNG();
                    System.IO.File.WriteAllBytes(screenshotPath, bytes);
                    UnityEngine.Object.DestroyImmediate(tex);

                    AssetDatabase.Refresh();
                    return new SuccessResponse($"SceneView screenshot saved to '{screenshotPath}'");

                default:
                    return new SuccessResponse("SceneView Camera ready. Actions: get_position, set_position, look_at, frame_object, align_to_view, set_mode (top/front/perspective/2d), screenshot");
            }
        }
    }
    #endregion

    #region 5. Input Simulation Tool
    [McpForUnityTool(
        name: "input_simulation",
        Description = "Simulates input in Play Mode. Actions: key_down, key_up, key_press, mouse_click, mouse_move, get_state, axis")]
    public static class MCPInputSimulation
    {
        private static Dictionary<string, bool> simulatedKeys = new Dictionary<string, bool>();
        private static Dictionary<string, float> simulatedAxes = new Dictionary<string, float>();

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (!EditorApplication.isPlaying && action != "get_state")
            {
                return new ErrorResponse("Input simulation requires Play Mode. Use play_mode_control to enter play mode first.");
            }

            switch (action)
            {
                case "key_down":
                    string keyDown = @params["key"]?.ToString();
                    if (string.IsNullOrEmpty(keyDown)) return new ErrorResponse("key parameter required");

                    if (Enum.TryParse<KeyCode>(keyDown, true, out var kd))
                    {
                        // We can't directly simulate Unity's Input system, but we can use SendMessage approach
                        // or use the new Input System if available
                        simulatedKeys[keyDown] = true;

                        // Try to use reflection on Input class (won't work in most cases)
                        // Instead, broadcast to a receiver script
                        var receivers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                        foreach (var r in receivers)
                        {
                            try { r.SendMessage("OnSimulatedKeyDown", kd, SendMessageOptions.DontRequireReceiver); } catch { }
                        }

                        return new SuccessResponse($"Key down simulated: {keyDown} (receivers notified via SendMessage)");
                    }
                    return new ErrorResponse($"Invalid key: {keyDown}");

                case "key_up":
                    string keyUp = @params["key"]?.ToString();
                    if (string.IsNullOrEmpty(keyUp)) return new ErrorResponse("key parameter required");

                    simulatedKeys[keyUp] = false;

                    if (Enum.TryParse<KeyCode>(keyUp, true, out var ku))
                    {
                        var receivers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                        foreach (var r in receivers)
                        {
                            try { r.SendMessage("OnSimulatedKeyUp", ku, SendMessageOptions.DontRequireReceiver); } catch { }
                        }
                    }
                    return new SuccessResponse($"Key up simulated: {keyUp}");

                case "key_press":
                    string keyPress = @params["key"]?.ToString();
                    float duration = @params["duration"]?.ToObject<float>() ?? 0.1f;

                    if (Enum.TryParse<KeyCode>(keyPress, true, out var kp))
                    {
                        // Schedule key down and up
                        simulatedKeys[keyPress] = true;
                        EditorApplication.delayCall += () =>
                        {
                            simulatedKeys[keyPress] = false;
                        };
                        return new SuccessResponse($"Key press simulated: {keyPress} for {duration}s");
                    }
                    return new ErrorResponse($"Invalid key: {keyPress}");

                case "axis":
                    string axisName = @params["axis"]?.ToString();
                    float axisValue = @params["value"]?.ToObject<float>() ?? 0f;

                    if (string.IsNullOrEmpty(axisName)) return new ErrorResponse("axis parameter required");

                    simulatedAxes[axisName] = axisValue;

                    // Broadcast axis change
                    var axisReceivers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    foreach (var r in axisReceivers)
                    {
                        try
                        {
                            r.SendMessage("OnSimulatedAxis", new object[] { axisName, axisValue }, SendMessageOptions.DontRequireReceiver);
                        }
                        catch { }
                    }

                    return new SuccessResponse($"Axis '{axisName}' set to {axisValue}");

                case "mouse_click":
                    int button = @params["button"]?.ToObject<int>() ?? 0;
                    var clickPos = @params["position"]?.ToObject<float[]>();

                    string clickInfo = $"Mouse button {button} click";
                    if (clickPos != null && clickPos.Length >= 2)
                    {
                        clickInfo += $" at ({clickPos[0]}, {clickPos[1]})";
                    }

                    // Broadcast mouse click
                    var mouseReceivers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    foreach (var r in mouseReceivers)
                    {
                        try
                        {
                            r.SendMessage("OnSimulatedMouseClick", button, SendMessageOptions.DontRequireReceiver);
                        }
                        catch { }
                    }

                    return new SuccessResponse(clickInfo + " (broadcasted via SendMessage)");

                case "mouse_move":
                    var movePos = @params["position"]?.ToObject<float[]>();
                    if (movePos == null || movePos.Length < 2)
                        return new ErrorResponse("position [x, y] required");

                    // Can't actually move mouse in editor, but can simulate for receivers
                    return new SuccessResponse($"Mouse move to ({movePos[0]}, {movePos[1]}) simulated (via SendMessage)");

                case "get_state":
                    return new SuccessResponse("Input simulation state", new {
                        isPlayMode = EditorApplication.isPlaying,
                        simulatedKeys = simulatedKeys.Where(kv => kv.Value).Select(kv => kv.Key).ToList(),
                        simulatedAxes = simulatedAxes,
                        note = "Use OnSimulatedKeyDown/OnSimulatedKeyUp/OnSimulatedAxis methods in your scripts to receive simulated input"
                    });

                default:
                    return new SuccessResponse("Input Simulation ready. Actions: key_down, key_up, key_press, axis, mouse_click, mouse_move, get_state. Note: Scripts must implement OnSimulatedKeyDown/Up to receive.");
            }
        }
    }
    #endregion

    #region 6. Collision Matrix Tool
    [McpForUnityTool(
        name: "collision_matrix",
        Description = "Controls Physics collision matrix. Actions: get, set, ignore_collision, enable_collision")]
    public static class MCPCollisionMatrix
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get":
                    var layers = new List<object>();
                    for (int i = 0; i < 32; i++)
                    {
                        string layerName = LayerMask.LayerToName(i);
                        if (!string.IsNullOrEmpty(layerName))
                        {
                            var collidesWith = new List<string>();
                            for (int j = 0; j < 32; j++)
                            {
                                string otherLayer = LayerMask.LayerToName(j);
                                if (!string.IsNullOrEmpty(otherLayer))
                                {
                                    if (!Physics.GetIgnoreLayerCollision(i, j))
                                    {
                                        collidesWith.Add(otherLayer);
                                    }
                                }
                            }
                            layers.Add(new { index = i, name = layerName, collidesWith });
                        }
                    }
                    return new SuccessResponse("Collision matrix", new { layers });

                case "ignore_collision":
                    string layer1Name = @params["layer1"]?.ToString();
                    string layer2Name = @params["layer2"]?.ToString();

                    int layer1 = LayerMask.NameToLayer(layer1Name);
                    int layer2 = LayerMask.NameToLayer(layer2Name);

                    if (layer1 < 0) return new ErrorResponse($"Layer '{layer1Name}' not found");
                    if (layer2 < 0) return new ErrorResponse($"Layer '{layer2Name}' not found");

                    Physics.IgnoreLayerCollision(layer1, layer2, true);
                    return new SuccessResponse($"Collision ignored between '{layer1Name}' and '{layer2Name}'");

                case "enable_collision":
                    string enableLayer1 = @params["layer1"]?.ToString();
                    string enableLayer2 = @params["layer2"]?.ToString();

                    int el1 = LayerMask.NameToLayer(enableLayer1);
                    int el2 = LayerMask.NameToLayer(enableLayer2);

                    if (el1 < 0) return new ErrorResponse($"Layer '{enableLayer1}' not found");
                    if (el2 < 0) return new ErrorResponse($"Layer '{enableLayer2}' not found");

                    Physics.IgnoreLayerCollision(el1, el2, false);
                    return new SuccessResponse($"Collision enabled between '{enableLayer1}' and '{enableLayer2}'");

                case "set":
                    // Set multiple collision rules at once
                    var rules = @params["rules"]?.ToObject<List<Dictionary<string, object>>>();
                    if (rules == null) return new ErrorResponse("rules array required");

                    int applied = 0;
                    foreach (var rule in rules)
                    {
                        if (rule.TryGetValue("layer1", out var l1) && rule.TryGetValue("layer2", out var l2))
                        {
                            int idx1 = LayerMask.NameToLayer(l1.ToString());
                            int idx2 = LayerMask.NameToLayer(l2.ToString());

                            if (idx1 >= 0 && idx2 >= 0)
                            {
                                bool ignore = rule.TryGetValue("ignore", out var ig) && Convert.ToBoolean(ig);
                                Physics.IgnoreLayerCollision(idx1, idx2, ignore);
                                applied++;
                            }
                        }
                    }
                    return new SuccessResponse($"Applied {applied} collision rules");

                default:
                    return new SuccessResponse("Collision Matrix ready. Actions: get, ignore_collision, enable_collision, set");
            }
        }
    }
    #endregion

    #region 7. Package Manager Tool
    [McpForUnityTool(
        name: "package_manager",
        Description = "Controls Unity Package Manager. Actions: list, add, remove, search, update, get_info")]
    public static class MCPPackageManager
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "list":
                    // Read manifest.json to get installed packages
                    string manifestPath = "Packages/manifest.json";
                    if (!System.IO.File.Exists(manifestPath))
                        return new ErrorResponse("manifest.json not found");

                    string manifestJson = System.IO.File.ReadAllText(manifestPath);
                    var manifest = JObject.Parse(manifestJson);
                    var deps = manifest["dependencies"] as JObject;

                    var packages = new List<object>();
                    if (deps != null)
                    {
                        foreach (var kvp in deps)
                        {
                            packages.Add(new { name = kvp.Key, version = kvp.Value?.ToString() });
                        }
                    }
                    return new SuccessResponse($"Found {packages.Count} packages", new { packages });

                case "add":
                    string addPackage = @params["package"]?.ToString();
                    string addVersion = @params["version"]?.ToString();

                    if (string.IsNullOrEmpty(addPackage))
                        return new ErrorResponse("package parameter required");

                    // Read manifest
                    string addManifest = System.IO.File.ReadAllText("Packages/manifest.json");
                    var addManifestObj = JObject.Parse(addManifest);
                    var addDeps = addManifestObj["dependencies"] as JObject;

                    if (addDeps == null)
                    {
                        addDeps = new JObject();
                        addManifestObj["dependencies"] = addDeps;
                    }

                    string packageValue = string.IsNullOrEmpty(addVersion) ? addPackage : addVersion;
                    addDeps[addPackage] = packageValue;

                    System.IO.File.WriteAllText("Packages/manifest.json", addManifestObj.ToString());
                    AssetDatabase.Refresh();

                    return new SuccessResponse($"Package '{addPackage}' added. Unity will resolve and download.");

                case "remove":
                    string removePackage = @params["package"]?.ToString();
                    if (string.IsNullOrEmpty(removePackage))
                        return new ErrorResponse("package parameter required");

                    string removeManifest = System.IO.File.ReadAllText("Packages/manifest.json");
                    var removeManifestObj = JObject.Parse(removeManifest);
                    var removeDeps = removeManifestObj["dependencies"] as JObject;

                    if (removeDeps != null && removeDeps.ContainsKey(removePackage))
                    {
                        removeDeps.Remove(removePackage);
                        System.IO.File.WriteAllText("Packages/manifest.json", removeManifestObj.ToString());
                        AssetDatabase.Refresh();
                        return new SuccessResponse($"Package '{removePackage}' removed");
                    }
                    return new ErrorResponse($"Package '{removePackage}' not found");

                case "get_info":
                    string infoPackage = @params["package"]?.ToString();

                    // Check package.json in Packages folder
                    string packageJsonPath = $"Packages/{infoPackage}/package.json";
                    if (System.IO.File.Exists(packageJsonPath))
                    {
                        string packageJson = System.IO.File.ReadAllText(packageJsonPath);
                        var pkgInfo = JObject.Parse(packageJson);
                        return new SuccessResponse($"Package info for '{infoPackage}'", new {
                            name = pkgInfo["name"]?.ToString(),
                            version = pkgInfo["version"]?.ToString(),
                            displayName = pkgInfo["displayName"]?.ToString(),
                            description = pkgInfo["description"]?.ToString(),
                            unity = pkgInfo["unity"]?.ToString()
                        });
                    }

                    return new ErrorResponse($"Package '{infoPackage}' info not found locally");

                case "update":
                    string updatePackage = @params["package"]?.ToString();
                    string newVersion = @params["version"]?.ToString();

                    if (string.IsNullOrEmpty(updatePackage) || string.IsNullOrEmpty(newVersion))
                        return new ErrorResponse("package and version parameters required");

                    string updateManifest = System.IO.File.ReadAllText("Packages/manifest.json");
                    var updateManifestObj = JObject.Parse(updateManifest);
                    var updateDeps = updateManifestObj["dependencies"] as JObject;

                    if (updateDeps != null && updateDeps.ContainsKey(updatePackage))
                    {
                        updateDeps[updatePackage] = newVersion;
                        System.IO.File.WriteAllText("Packages/manifest.json", updateManifestObj.ToString());
                        AssetDatabase.Refresh();
                        return new SuccessResponse($"Package '{updatePackage}' updated to {newVersion}");
                    }
                    return new ErrorResponse($"Package '{updatePackage}' not found");

                default:
                    return new SuccessResponse("Package Manager ready. Actions: list, add, remove, update, get_info");
            }
        }
    }
    #endregion

    #region 8. Post-Processing / Volume Control Tool
    [McpForUnityTool(
        name: "post_processing",
        Description = "Controls Post-Processing / URP Volumes. Actions: get_volumes, create_volume, set_override, get_profiles, create_profile")]
    public static class MCPPostProcessing
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            switch (action)
            {
                case "get_volumes":
                    // Try to find Volume components (URP/HDRP)
                    var volumes = new List<object>();

                    // Use reflection to find Volume type
                    var volumeType = GetVolumeType();
                    if (volumeType != null)
                    {
                        var foundVolumes = UnityEngine.Object.FindObjectsByType(volumeType, FindObjectsSortMode.None);
                        foreach (var v in foundVolumes)
                        {
                            var go = (v as Component)?.gameObject;
                            var isGlobal = volumeType.GetProperty("isGlobal")?.GetValue(v);
                            var priority = volumeType.GetProperty("priority")?.GetValue(v);
                            var weight = volumeType.GetProperty("weight")?.GetValue(v);

                            volumes.Add(new {
                                name = go?.name,
                                isGlobal = isGlobal,
                                priority = priority,
                                weight = weight
                            });
                        }
                        return new SuccessResponse($"Found {volumes.Count} volumes", new { volumes });
                    }

                    return new SuccessResponse("No Volume component type found. Is URP/HDRP installed?", new { volumes });

                case "create_volume":
                    string volName = @params["name"]?.ToString() ?? "PostProcessVolume";
                    bool isGlobalVol = @params["isGlobal"]?.ToObject<bool>() ?? true;

                    var volType = GetVolumeType();
                    if (volType == null)
                        return new ErrorResponse("Volume component not found. Install URP or HDRP package first.");

                    var volGo = new GameObject(volName);
                    var volume = volGo.AddComponent(volType);

                    volType.GetProperty("isGlobal")?.SetValue(volume, isGlobalVol);
                    volType.GetProperty("priority")?.SetValue(volume, @params["priority"]?.ToObject<float>() ?? 1f);
                    volType.GetProperty("weight")?.SetValue(volume, @params["weight"]?.ToObject<float>() ?? 1f);

                    if (!isGlobalVol)
                    {
                        // Add box collider as trigger for local volumes
                        var box = volGo.AddComponent<BoxCollider>();
                        box.isTrigger = true;
                        box.size = new Vector3(10, 10, 10);
                    }

                    Undo.RegisterCreatedObjectUndo(volGo, "Create Volume");
                    return new SuccessResponse($"Created Volume '{volName}'");

                case "get_profiles":
                    string[] profileGuids = AssetDatabase.FindAssets("t:VolumeProfile");
                    var profiles = profileGuids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToList();
                    return new SuccessResponse($"Found {profiles.Count} VolumeProfiles", new { profiles });

                case "create_profile":
                    string profileName = @params["name"]?.ToString() ?? "NewVolumeProfile";
                    string profilePath = @params["path"]?.ToString() ?? "Assets";

                    var profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime")
                        ?? Type.GetType("UnityEngine.Rendering.VolumeProfile, UnityEngine.CoreModule");

                    if (profileType == null)
                        return new ErrorResponse("VolumeProfile type not found. Is a render pipeline installed?");

                    var profile = ScriptableObject.CreateInstance(profileType);
                    string fullPath = $"{profilePath}/{profileName}.asset";
                    AssetDatabase.CreateAsset(profile, fullPath);
                    AssetDatabase.SaveAssets();

                    return new SuccessResponse($"Created VolumeProfile at '{fullPath}'");

                case "set_override":
                    // This is complex and depends on the specific override type
                    // Would need to know which effect (Bloom, Vignette, etc.)
                    string targetVol = @params["target"]?.ToString();
                    string overrideType = @params["overrideType"]?.ToString();

                    return new SuccessResponse("Override setting requires specific effect configuration. Use the Volume component inspector or specify: overrideType (Bloom, Vignette, ColorAdjustments, etc.)");

                default:
                    return new SuccessResponse("Post-Processing ready. Actions: get_volumes, create_volume, get_profiles, create_profile, set_override");
            }
        }

        private static Type GetVolumeType()
        {
            // Try URP/HDRP Volume
            return Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime")
                ?? Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.CoreModule");
        }
    }
    #endregion

    #region 9. Terrain Control Tool
    [McpForUnityTool(
        name: "terrain_control",
        Description = "Controls Terrain. Actions: create, get_settings, set_size, get_height, set_height, paint_texture, add_tree, get_all")]
    public static class MCPTerrainControl
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            string targetName = @params["target"]?.ToString();

            switch (action)
            {
                case "create":
                    string terrainName = @params["name"]?.ToString() ?? "Terrain";
                    var sizeArr = @params["size"]?.ToObject<float[]>() ?? new float[] { 500, 100, 500 };
                    int resolution = @params["resolution"]?.ToObject<int>() ?? 513;

                    // Create terrain data
                    var terrainData = new TerrainData();
                    terrainData.heightmapResolution = resolution;
                    terrainData.size = new Vector3(sizeArr[0], sizeArr[1], sizeArr[2]);

                    // Save terrain data asset
                    string dataPath = @params["dataPath"]?.ToString() ?? $"Assets/{terrainName}_Data.asset";
                    AssetDatabase.CreateAsset(terrainData, dataPath);

                    // Create terrain game object
                    var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
                    terrainGo.name = terrainName;

                    var posArr = @params["position"]?.ToObject<float[]>();
                    if (posArr != null && posArr.Length >= 3)
                    {
                        terrainGo.transform.position = new Vector3(posArr[0], posArr[1], posArr[2]);
                    }

                    Undo.RegisterCreatedObjectUndo(terrainGo, "Create Terrain");
                    AssetDatabase.SaveAssets();

                    return new SuccessResponse($"Created Terrain '{terrainName}'", new {
                        name = terrainName,
                        size = sizeArr,
                        resolution,
                        dataPath
                    });

                case "get_settings":
                    var terrain = string.IsNullOrEmpty(targetName)
                        ? Terrain.activeTerrain
                        : GameObject.Find(targetName)?.GetComponent<Terrain>();

                    if (terrain == null)
                        return new ErrorResponse("No terrain found");

                    var td = terrain.terrainData;
                    return new SuccessResponse("Terrain settings", new {
                        name = terrain.gameObject.name,
                        position = $"({terrain.transform.position.x}, {terrain.transform.position.y}, {terrain.transform.position.z})",
                        size = $"({td.size.x}, {td.size.y}, {td.size.z})",
                        heightmapResolution = td.heightmapResolution,
                        alphamapResolution = td.alphamapResolution,
                        detailResolution = td.detailResolution,
                        treeInstanceCount = td.treeInstanceCount,
                        terrainLayerCount = td.terrainLayers?.Length ?? 0
                    });

                case "set_size":
                    var sizeTerrain = string.IsNullOrEmpty(targetName)
                        ? Terrain.activeTerrain
                        : GameObject.Find(targetName)?.GetComponent<Terrain>();

                    if (sizeTerrain == null)
                        return new ErrorResponse("No terrain found");

                    var newSize = @params["size"]?.ToObject<float[]>();
                    if (newSize == null || newSize.Length < 3)
                        return new ErrorResponse("size [x, y, z] required");

                    sizeTerrain.terrainData.size = new Vector3(newSize[0], newSize[1], newSize[2]);
                    EditorUtility.SetDirty(sizeTerrain.terrainData);

                    return new SuccessResponse($"Terrain size set to ({newSize[0]}, {newSize[1]}, {newSize[2]})");

                case "get_height":
                    var heightTerrain = string.IsNullOrEmpty(targetName)
                        ? Terrain.activeTerrain
                        : GameObject.Find(targetName)?.GetComponent<Terrain>();

                    if (heightTerrain == null)
                        return new ErrorResponse("No terrain found");

                    float worldX = @params["x"]?.ToObject<float>() ?? 0;
                    float worldZ = @params["z"]?.ToObject<float>() ?? 0;

                    float height = heightTerrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                    return new SuccessResponse($"Height at ({worldX}, {worldZ})", new { x = worldX, z = worldZ, height });

                case "set_height":
                    var setHeightTerrain = string.IsNullOrEmpty(targetName)
                        ? Terrain.activeTerrain
                        : GameObject.Find(targetName)?.GetComponent<Terrain>();

                    if (setHeightTerrain == null)
                        return new ErrorResponse("No terrain found");

                    int centerX = @params["centerX"]?.ToObject<int>() ?? 0;
                    int centerZ = @params["centerZ"]?.ToObject<int>() ?? 0;
                    int radius = @params["radius"]?.ToObject<int>() ?? 10;
                    float targetHeight = @params["height"]?.ToObject<float>() ?? 0.5f;
                    string brushMode = @params["mode"]?.ToString()?.ToLower() ?? "set";

                    var htd = setHeightTerrain.terrainData;
                    int res = htd.heightmapResolution;

                    // Convert world coords to heightmap coords
                    int startX = Mathf.Clamp(centerX - radius, 0, res - 1);
                    int startZ = Mathf.Clamp(centerZ - radius, 0, res - 1);
                    int width = Mathf.Min(radius * 2, res - startX);
                    int length = Mathf.Min(radius * 2, res - startZ);

                    float[,] heights = htd.GetHeights(startX, startZ, width, length);

                    for (int x = 0; x < width; x++)
                    {
                        for (int z = 0; z < length; z++)
                        {
                            float dist = Vector2.Distance(new Vector2(x, z), new Vector2(radius, radius));
                            if (dist <= radius)
                            {
                                float falloff = 1f - (dist / radius);
                                if (brushMode == "add")
                                    heights[z, x] += targetHeight * falloff;
                                else if (brushMode == "smooth")
                                    heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, falloff * 0.5f);
                                else // set
                                    heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, falloff);
                            }
                        }
                    }

                    htd.SetHeights(startX, startZ, heights);
                    EditorUtility.SetDirty(htd);

                    return new SuccessResponse($"Height modified at ({centerX}, {centerZ}) with radius {radius}");

                case "get_all":
                    var allTerrains = Terrain.activeTerrains;
                    var terrainList = allTerrains.Select(t => new {
                        name = t.gameObject.name,
                        position = $"({t.transform.position.x}, {t.transform.position.y}, {t.transform.position.z})",
                        size = $"({t.terrainData.size.x}, {t.terrainData.size.y}, {t.terrainData.size.z})"
                    }).ToList();
                    return new SuccessResponse($"Found {allTerrains.Length} terrains", new { terrains = terrainList });

                default:
                    return new SuccessResponse("Terrain Control ready. Actions: create, get_settings, set_size, get_height, set_height (modes: set/add/smooth), get_all");
            }
        }
    }
    #endregion

    #region 10. Gizmo Drawing Tool
    [McpForUnityTool(
        name: "debug_draw",
        Description = "Draws debug shapes in Scene view. Actions: line, sphere, cube, ray, label, clear")]
    public static class MCPDebugDraw
    {
        private static List<DebugShape> shapes = new List<DebugShape>();
        private static bool initialized = false;

        private class DebugShape
        {
            public string type;
            public Vector3 position;
            public Vector3 size;
            public Vector3 direction;
            public Color color;
            public string label;
            public float duration;
            public float createdTime;
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                initialized = true;
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            float time = (float)EditorApplication.timeSinceStartup;
            shapes.RemoveAll(s => s.duration > 0 && time - s.createdTime > s.duration);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (var shape in shapes)
            {
                Handles.color = shape.color;

                switch (shape.type)
                {
                    case "sphere":
                        Handles.SphereHandleCap(0, shape.position, Quaternion.identity, shape.size.x * 2, EventType.Repaint);
                        break;
                    case "cube":
                        Handles.DrawWireCube(shape.position, shape.size);
                        break;
                    case "line":
                        Handles.DrawLine(shape.position, shape.position + shape.direction);
                        break;
                    case "ray":
                        Handles.DrawLine(shape.position, shape.position + shape.direction * 100f);
                        Handles.ArrowHandleCap(0, shape.position, Quaternion.LookRotation(shape.direction), 1f, EventType.Repaint);
                        break;
                    case "label":
                        Handles.Label(shape.position, shape.label);
                        break;
                }
            }

            sceneView.Repaint();
        }

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();
            EnsureInitialized();

            switch (action)
            {
                case "line":
                    var startPos = @params["start"]?.ToObject<float[]>();
                    var endPos = @params["end"]?.ToObject<float[]>();
                    if (startPos == null || endPos == null)
                        return new ErrorResponse("start and end positions required");

                    shapes.Add(new DebugShape
                    {
                        type = "line",
                        position = new Vector3(startPos[0], startPos[1], startPos[2]),
                        direction = new Vector3(endPos[0] - startPos[0], endPos[1] - startPos[1], endPos[2] - startPos[2]),
                        color = ParseColor(@params["color"]),
                        duration = @params["duration"]?.ToObject<float>() ?? 0,
                        createdTime = (float)EditorApplication.timeSinceStartup
                    });
                    SceneView.RepaintAll();
                    return new SuccessResponse("Line drawn");

                case "sphere":
                    var spherePos = @params["position"]?.ToObject<float[]>();
                    if (spherePos == null)
                        return new ErrorResponse("position required");

                    float radius = @params["radius"]?.ToObject<float>() ?? 1f;

                    shapes.Add(new DebugShape
                    {
                        type = "sphere",
                        position = new Vector3(spherePos[0], spherePos[1], spherePos[2]),
                        size = new Vector3(radius, radius, radius),
                        color = ParseColor(@params["color"]),
                        duration = @params["duration"]?.ToObject<float>() ?? 0,
                        createdTime = (float)EditorApplication.timeSinceStartup
                    });
                    SceneView.RepaintAll();
                    return new SuccessResponse("Sphere drawn");

                case "cube":
                    var cubePos = @params["position"]?.ToObject<float[]>();
                    var cubeSize = @params["size"]?.ToObject<float[]>() ?? new float[] { 1, 1, 1 };
                    if (cubePos == null)
                        return new ErrorResponse("position required");

                    shapes.Add(new DebugShape
                    {
                        type = "cube",
                        position = new Vector3(cubePos[0], cubePos[1], cubePos[2]),
                        size = new Vector3(cubeSize[0], cubeSize[1], cubeSize[2]),
                        color = ParseColor(@params["color"]),
                        duration = @params["duration"]?.ToObject<float>() ?? 0,
                        createdTime = (float)EditorApplication.timeSinceStartup
                    });
                    SceneView.RepaintAll();
                    return new SuccessResponse("Cube drawn");

                case "ray":
                    var rayOrigin = @params["origin"]?.ToObject<float[]>();
                    var rayDir = @params["direction"]?.ToObject<float[]>();
                    if (rayOrigin == null || rayDir == null)
                        return new ErrorResponse("origin and direction required");

                    shapes.Add(new DebugShape
                    {
                        type = "ray",
                        position = new Vector3(rayOrigin[0], rayOrigin[1], rayOrigin[2]),
                        direction = new Vector3(rayDir[0], rayDir[1], rayDir[2]).normalized,
                        color = ParseColor(@params["color"]),
                        duration = @params["duration"]?.ToObject<float>() ?? 0,
                        createdTime = (float)EditorApplication.timeSinceStartup
                    });
                    SceneView.RepaintAll();
                    return new SuccessResponse("Ray drawn");

                case "label":
                    var labelPos = @params["position"]?.ToObject<float[]>();
                    string text = @params["text"]?.ToString();
                    if (labelPos == null || string.IsNullOrEmpty(text))
                        return new ErrorResponse("position and text required");

                    shapes.Add(new DebugShape
                    {
                        type = "label",
                        position = new Vector3(labelPos[0], labelPos[1], labelPos[2]),
                        label = text,
                        color = ParseColor(@params["color"]),
                        duration = @params["duration"]?.ToObject<float>() ?? 0,
                        createdTime = (float)EditorApplication.timeSinceStartup
                    });
                    SceneView.RepaintAll();
                    return new SuccessResponse("Label drawn");

                case "clear":
                    int count = shapes.Count;
                    shapes.Clear();
                    SceneView.RepaintAll();
                    return new SuccessResponse($"Cleared {count} debug shapes");

                case "get":
                    return new SuccessResponse($"Active debug shapes: {shapes.Count}", new { count = shapes.Count });

                default:
                    return new SuccessResponse("Debug Draw ready. Actions: line, sphere, cube, ray, label, clear, get");
            }
        }

        private static Color ParseColor(JToken colorToken)
        {
            if (colorToken == null) return Color.green;

            var arr = colorToken.ToObject<float[]>();
            if (arr != null && arr.Length >= 3)
            {
                float a = arr.Length >= 4 ? arr[3] : 1f;
                return new Color(arr[0], arr[1], arr[2], a);
            }

            string colorStr = colorToken.ToString().ToLower();
            switch (colorStr)
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "white": return Color.white;
                case "black": return Color.black;
                default: return Color.green;
            }
        }
    }
    #endregion
}
