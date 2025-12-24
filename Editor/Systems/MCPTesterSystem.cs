#nullable disable
#pragma warning disable CS0618 // FindObjectsOfType/FindObjectOfType deprecated but extensively used in this test file
using System;
using System.Collections.Generic;
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
    /// Comprehensive TESTER System for ENDURANCE Project
    /// Actions: full_scan, test_component, test_references, test_systems,
    ///          stress_test, validate_scene, find_bugs, performance_profile
    /// </summary>
    [McpForUnityTool(
        name: "tester",
        Description = "TESTER System: full_scan, test_component, test_references, test_systems, stress_test, validate_scene, find_bugs, performance_profile")]
    public static class MCPTesterSystem
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return new SuccessResponse("TESTER System ready. Actions: full_scan, test_component, test_references, test_systems, stress_test, validate_scene, find_bugs, performance_profile");
            }

            switch (action)
            {
                case "full_scan":
                    return FullScan();

                case "test_component":
                    return TestComponent(@params);

                case "test_references":
                    return TestReferences(@params);

                case "test_systems":
                    return TestSystems();

                case "stress_test":
                    return StressTest(@params);

                case "validate_scene":
                    return ValidateScene();

                case "find_bugs":
                    return FindBugs();

                case "performance_profile":
                    return PerformanceProfile();

                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: full_scan, test_component, test_references, test_systems, stress_test, validate_scene, find_bugs, performance_profile");
            }
        }

        #region Full Scan
        private static object FullScan()
        {
            var results = new Dictionary<string, object>();
            var bugs = new List<BugReport>();
            var warnings = new List<string>();
            var passed = new List<string>();

            // 1. Scene Validation
            var sceneResult = ValidateSceneInternal();
            results["scene"] = sceneResult;
            bugs.AddRange(sceneResult.bugs);
            warnings.AddRange(sceneResult.warnings);
            passed.AddRange(sceneResult.passed);

            // 2. Reference Validation
            var refResult = TestAllReferences();
            results["references"] = refResult;
            bugs.AddRange(refResult.bugs);
            warnings.AddRange(refResult.warnings);

            // 3. System Tests
            var sysResult = TestAllSystems();
            results["systems"] = sysResult;
            bugs.AddRange(sysResult.bugs);
            warnings.AddRange(sysResult.warnings);
            passed.AddRange(sysResult.passed);

            // 4. Performance Analysis
            var perfResult = AnalyzePerformance();
            results["performance"] = perfResult;
            warnings.AddRange(perfResult.warnings);

            // Summary
            var summary = new
            {
                totalBugs = bugs.Count,
                totalWarnings = warnings.Count,
                totalPassed = passed.Count,
                score = CalculateHealthScore(bugs.Count, warnings.Count, passed.Count),
                bugs = bugs.Select(b => new { b.severity, b.category, b.message, b.location }).ToList(),
                warnings,
                passed
            };

            string status = bugs.Count == 0 ? "✓ All tests passed" : $"✗ {bugs.Count} bugs found";
            return new SuccessResponse($"FULL SCAN: {status}", summary);
        }
        #endregion

        #region Test Component
        private static object TestComponent(JObject @params)
        {
            string objectName = @params["objectName"]?.ToString();
            string componentName = @params["componentName"]?.ToString();

            if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(componentName))
                return new ErrorResponse("Required: objectName, componentName");

            var go = GameObject.Find(objectName);
            if (go == null)
                return new ErrorResponse($"GameObject '{objectName}' not found");

            var component = go.GetComponent(componentName);
            if (component == null)
                return new ErrorResponse($"Component '{componentName}' not found on '{objectName}'");

            var bugs = new List<BugReport>();
            var warnings = new List<string>();
            var info = new List<string>();

            // Analyze component
            var serializedObject = new SerializedObject(component);
            var prop = serializedObject.GetIterator();

            int totalFields = 0;
            int nullReferences = 0;
            int assignedReferences = 0;
            var fieldDetails = new List<object>();

            while (prop.NextVisible(true))
            {
                totalFields++;

                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    bool isNull = prop.objectReferenceValue == null;

                    // Check if it's a required field (no default value, serialized)
                    bool isRequired = !prop.name.Contains("optional") &&
                                     !prop.name.Contains("Optional") &&
                                     prop.depth == 0;

                    if (isNull)
                    {
                        nullReferences++;
                        if (isRequired)
                        {
                            bugs.Add(new BugReport
                            {
                                severity = "high",
                                category = "missing_reference",
                                message = $"Null reference: {prop.name}",
                                location = $"{objectName}.{componentName}.{prop.name}"
                            });
                        }
                        else
                        {
                            warnings.Add($"Null field: {prop.name}");
                        }
                    }
                    else
                    {
                        assignedReferences++;
                        info.Add($"✓ {prop.name} = {prop.objectReferenceValue.name}");
                    }

                    fieldDetails.Add(new
                    {
                        name = prop.name,
                        type = "ObjectReference",
                        isNull,
                        value = isNull ? null : prop.objectReferenceValue?.name
                    });
                }
                else if (prop.propertyType == SerializedPropertyType.Float ||
                         prop.propertyType == SerializedPropertyType.Integer)
                {
                    // Check for suspicious values
                    if (prop.propertyType == SerializedPropertyType.Float && float.IsNaN(prop.floatValue))
                    {
                        bugs.Add(new BugReport
                        {
                            severity = "critical",
                            category = "invalid_value",
                            message = $"NaN value in {prop.name}",
                            location = $"{objectName}.{componentName}.{prop.name}"
                        });
                    }
                    else if (prop.propertyType == SerializedPropertyType.Float && float.IsInfinity(prop.floatValue))
                    {
                        bugs.Add(new BugReport
                        {
                            severity = "critical",
                            category = "invalid_value",
                            message = $"Infinity value in {prop.name}",
                            location = $"{objectName}.{componentName}.{prop.name}"
                        });
                    }
                }
            }

            var result = new
            {
                component = componentName,
                gameObject = objectName,
                totalFields,
                objectReferences = new { total = nullReferences + assignedReferences, assigned = assignedReferences, missing = nullReferences },
                bugCount = bugs.Count,
                warningCount = warnings.Count,
                bugs = bugs.Select(b => new { b.severity, b.message }).ToList(),
                warnings,
                assignedFields = info,
                details = fieldDetails
            };

            return new SuccessResponse($"Component test: {componentName} - {bugs.Count} bugs, {warnings.Count} warnings", result);
        }
        #endregion

        #region Test References
        private static object TestReferences(JObject @params)
        {
            string objectName = @params["objectName"]?.ToString();
            bool deepScan = @params["deepScan"]?.ToObject<bool>() ?? true;

            var bugs = new List<BugReport>();
            var results = new List<object>();

            IEnumerable<GameObject> objectsToTest;

            if (!string.IsNullOrEmpty(objectName))
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                    return new ErrorResponse($"GameObject '{objectName}' not found");
                objectsToTest = deepScan ? go.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject) : new[] { go };
            }
            else
            {
                objectsToTest = UnityEngine.Object.FindObjectsOfType<GameObject>();
            }

            foreach (var go in objectsToTest)
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        bugs.Add(new BugReport
                        {
                            severity = "critical",
                            category = "missing_script",
                            message = "Missing script component",
                            location = go.name
                        });
                        continue;
                    }

                    var so = new SerializedObject(comp);
                    var prop = so.GetIterator();

                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                            prop.objectReferenceValue == null &&
                            prop.depth == 0 &&
                            !prop.name.StartsWith("m_"))
                        {
                            // Check if required by attribute
                            var fieldInfo = comp.GetType().GetField(prop.name,
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            bool hasSerializeField = fieldInfo?.GetCustomAttribute<SerializeField>() != null;
                            bool isRequired = hasSerializeField && !prop.name.ToLower().Contains("optional");

                            if (isRequired)
                            {
                                bugs.Add(new BugReport
                                {
                                    severity = "high",
                                    category = "null_reference",
                                    message = $"Required reference is null: {prop.name}",
                                    location = $"{go.name}.{comp.GetType().Name}.{prop.name}"
                                });
                            }

                            results.Add(new
                            {
                                gameObject = go.name,
                                component = comp.GetType().Name,
                                field = prop.name,
                                isNull = true,
                                isRequired
                            });
                        }
                    }
                }
            }

            return new SuccessResponse($"Reference test: {bugs.Count} missing required references", new
            {
                testedObjects = objectsToTest.Count(),
                bugCount = bugs.Count,
                bugs = bugs.Select(b => new { b.severity, b.message, b.location }).ToList(),
                nullReferences = results
            });
        }

        private static TestResult TestAllReferences()
        {
            var result = new TestResult();
            var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        result.bugs.Add(new BugReport
                        {
                            severity = "critical",
                            category = "missing_script",
                            message = "Missing script component",
                            location = go.name
                        });
                        continue;
                    }

                    try
                    {
                        var so = new SerializedObject(comp);
                        var prop = so.GetIterator();

                        while (prop.NextVisible(true))
                        {
                            if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                                prop.objectReferenceValue == null &&
                                prop.depth == 0 &&
                                !prop.name.StartsWith("m_") &&
                                !prop.name.ToLower().Contains("optional"))
                            {
                                var fieldInfo = comp.GetType().GetField(prop.name,
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                                if (fieldInfo?.GetCustomAttribute<SerializeField>() != null)
                                {
                                    result.bugs.Add(new BugReport
                                    {
                                        severity = "medium",
                                        category = "null_reference",
                                        message = $"Serialized field is null: {prop.name}",
                                        location = $"{go.name}.{comp.GetType().Name}"
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            return result;
        }
        #endregion

        #region Test Systems
        private static object TestSystems()
        {
            var result = TestAllSystems();

            return new SuccessResponse($"Systems test: {result.passed.Count} passed, {result.bugs.Count} bugs", new
            {
                passed = result.passed,
                bugs = result.bugs.Select(b => new { b.severity, b.category, b.message, b.location }).ToList(),
                warnings = result.warnings
            });
        }

        private static TestResult TestAllSystems()
        {
            var result = new TestResult();

            // Test DamageFeedbackController
            TestDamageFeedbackController(result);

            // Test PhysicalMovement
            TestPhysicalMovement(result);

            // Test MetabolismController
            TestMetabolismController(result);

            // Test UIManager
            TestUIManager(result);

            // Test Camera Systems
            TestCameraSystems(result);

            return result;
        }

        private static void TestDamageFeedbackController(TestResult result)
        {
            var type = Type.GetType("Endurance.FX.DamageFeedbackController, Assembly-CSharp");
            if (type == null)
            {
                result.warnings.Add("DamageFeedbackController type not found");
                return;
            }

            var instances = UnityEngine.Object.FindObjectsOfType(type);
            if (instances.Length == 0)
            {
                result.warnings.Add("No DamageFeedbackController in scene");
                return;
            }

            foreach (var instance in instances)
            {
                var comp = instance as Component;
                var so = new SerializedObject(comp);

                // Check postProcessVolume
                var volumeProp = so.FindProperty("postProcessVolume");
                if (volumeProp != null && volumeProp.objectReferenceValue == null)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "high",
                        category = "missing_reference",
                        message = "postProcessVolume is not assigned - effects won't work",
                        location = $"{comp.gameObject.name}.DamageFeedbackController"
                    });
                }
                else
                {
                    result.passed.Add("DamageFeedbackController.postProcessVolume assigned");
                }

                // Check healthProvider
                var healthProp = so.FindProperty("healthProvider");
                if (healthProp != null && healthProp.objectReferenceValue == null)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "high",
                        category = "missing_reference",
                        message = "healthProvider is not assigned - damage feedback won't respond to damage",
                        location = $"{comp.gameObject.name}.DamageFeedbackController"
                    });
                }
                else
                {
                    result.passed.Add("DamageFeedbackController.healthProvider assigned");
                }

                // Check blackoutUI for microsleep
                var blackoutProp = so.FindProperty("blackoutUI");
                if (blackoutProp != null && blackoutProp.objectReferenceValue == null)
                {
                    result.warnings.Add("DamageFeedbackController.blackoutUI not assigned - microsleep effects disabled");
                }
                else if (blackoutProp != null)
                {
                    result.passed.Add("DamageFeedbackController.blackoutUI assigned");
                }

                // Check threshold values
                var microsleepThreshold = so.FindProperty("microsleepThreshold");
                if (microsleepThreshold != null)
                {
                    float value = microsleepThreshold.floatValue;
                    if (value <= 0 || value > 1)
                    {
                        result.bugs.Add(new BugReport
                        {
                            severity = "medium",
                            category = "invalid_value",
                            message = $"microsleepThreshold ({value}) should be between 0 and 1",
                            location = $"{comp.gameObject.name}.DamageFeedbackController"
                        });
                    }
                }
            }
        }

        private static void TestPhysicalMovement(TestResult result)
        {
            var type = Type.GetType("Endurance.Player.PhysicalMovement, Assembly-CSharp");
            if (type == null)
            {
                result.warnings.Add("PhysicalMovement type not found");
                return;
            }

            var instances = UnityEngine.Object.FindObjectsOfType(type);
            if (instances.Length == 0)
            {
                result.warnings.Add("No PhysicalMovement in scene");
                return;
            }

            foreach (var instance in instances)
            {
                var comp = instance as Component;
                var go = comp.gameObject;

                // Check required components
                if (go.GetComponent<Rigidbody>() == null)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "critical",
                        category = "missing_component",
                        message = "Rigidbody required by PhysicalMovement is missing",
                        location = go.name
                    });
                }
                else
                {
                    result.passed.Add($"{go.name}.Rigidbody present for PhysicalMovement");
                }

                if (go.GetComponent<CapsuleCollider>() == null)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "critical",
                        category = "missing_component",
                        message = "CapsuleCollider required by PhysicalMovement is missing",
                        location = go.name
                    });
                }
                else
                {
                    result.passed.Add($"{go.name}.CapsuleCollider present for PhysicalMovement");
                }

                // Check parameter values
                var so = new SerializedObject(comp);

                var maxSpeed = so.FindProperty("maxSpeed");
                if (maxSpeed != null && maxSpeed.floatValue <= 0)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "high",
                        category = "invalid_value",
                        message = $"maxSpeed ({maxSpeed.floatValue}) must be positive",
                        location = $"{go.name}.PhysicalMovement"
                    });
                }

                var jumpHeight = so.FindProperty("jumpHeight");
                if (jumpHeight != null && jumpHeight.floatValue < 0)
                {
                    result.bugs.Add(new BugReport
                    {
                        severity = "medium",
                        category = "invalid_value",
                        message = $"jumpHeight ({jumpHeight.floatValue}) is negative",
                        location = $"{go.name}.PhysicalMovement"
                    });
                }

                var mass = so.FindProperty("mass");
                if (mass != null && (mass.floatValue < 50 || mass.floatValue > 120))
                {
                    result.warnings.Add($"{go.name}.PhysicalMovement.mass ({mass.floatValue}) is outside realistic range (50-120kg)");
                }
            }
        }

        private static void TestMetabolismController(TestResult result)
        {
            var type = Type.GetType("Endurance.Core.MetabolismController, Assembly-CSharp");
            if (type == null)
            {
                result.warnings.Add("MetabolismController type not found");
                return;
            }

            var instances = UnityEngine.Object.FindObjectsOfType(type);
            if (instances.Length == 0)
            {
                result.warnings.Add("No MetabolismController in scene - survival mechanics disabled");
                return;
            }

            result.passed.Add($"MetabolismController found ({instances.Length} instances)");
        }

        private static void TestUIManager(TestResult result)
        {
            // Check for Canvas
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            if (canvases.Length == 0)
            {
                result.warnings.Add("No Canvas in scene - UI won't render");
                return;
            }

            // Check for EventSystem
            var eventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                result.bugs.Add(new BugReport
                {
                    severity = "high",
                    category = "missing_component",
                    message = "No EventSystem in scene - UI interactions won't work",
                    location = "Scene"
                });
            }
            else
            {
                result.passed.Add("EventSystem present");
            }

            // Check PlayerUI specifically
            var playerUI = GameObject.Find("PlayerUI");
            if (playerUI != null)
            {
                result.passed.Add("PlayerUI Canvas found");

                // Check for BlackoutOverlay
                var blackoutOverlay = playerUI.transform.Find("BlackoutOverlay");
                if (blackoutOverlay == null)
                {
                    result.warnings.Add("BlackoutOverlay not found under PlayerUI - microsleep effects may not work");
                }
                else
                {
                    var canvasGroup = blackoutOverlay.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        result.bugs.Add(new BugReport
                        {
                            severity = "medium",
                            category = "missing_component",
                            message = "BlackoutOverlay missing CanvasGroup - fade effects won't work",
                            location = "PlayerUI/BlackoutOverlay"
                        });
                    }
                    else
                    {
                        result.passed.Add("BlackoutOverlay with CanvasGroup configured");
                    }
                }
            }
        }

        private static void TestCameraSystems(TestResult result)
        {
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            if (cameras.Length == 0)
            {
                result.bugs.Add(new BugReport
                {
                    severity = "critical",
                    category = "missing_component",
                    message = "No cameras in scene - nothing will render",
                    location = "Scene"
                });
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                result.warnings.Add("No MainCamera tag set - some scripts may not find camera");
            }
            else
            {
                result.passed.Add("MainCamera found");

                // Check for post-processing
                var volume = mainCamera.GetComponent("Volume");
                if (volume == null)
                {
                    // Try parent/children
                    volume = mainCamera.GetComponentInParent(typeof(Component));
                }

                // Check for common camera components
                var audioListener = mainCamera.GetComponent<AudioListener>();
                if (audioListener == null)
                {
                    result.warnings.Add("MainCamera missing AudioListener - no audio will be heard");
                }
                else
                {
                    result.passed.Add("MainCamera has AudioListener");
                }
            }
        }
        #endregion

        #region Stress Test
        private static object StressTest(JObject @params)
        {
            string target = @params["target"]?.ToString() ?? "scene";
            int iterations = @params["iterations"]?.ToObject<int>() ?? 100;

            var results = new List<object>();
            var stopwatch = new System.Diagnostics.Stopwatch();

            switch (target.ToLower())
            {
                case "physics":
                    // Test physics queries
                    stopwatch.Start();
                    for (int i = 0; i < iterations; i++)
                    {
                        Physics.RaycastAll(Vector3.zero, Vector3.down, 1000f);
                    }
                    stopwatch.Stop();
                    results.Add(new { test = "Raycast", iterations, totalMs = stopwatch.ElapsedMilliseconds, avgMs = stopwatch.ElapsedMilliseconds / (float)iterations });

                    stopwatch.Restart();
                    for (int i = 0; i < iterations; i++)
                    {
                        Physics.OverlapSphere(Vector3.zero, 50f);
                    }
                    stopwatch.Stop();
                    results.Add(new { test = "OverlapSphere", iterations, totalMs = stopwatch.ElapsedMilliseconds, avgMs = stopwatch.ElapsedMilliseconds / (float)iterations });
                    break;

                case "find":
                    stopwatch.Start();
                    for (int i = 0; i < iterations; i++)
                    {
                        UnityEngine.Object.FindObjectsOfType<GameObject>();
                    }
                    stopwatch.Stop();
                    results.Add(new { test = "FindObjectsOfType<GameObject>", iterations, totalMs = stopwatch.ElapsedMilliseconds, avgMs = stopwatch.ElapsedMilliseconds / (float)iterations });

                    stopwatch.Restart();
                    for (int i = 0; i < iterations; i++)
                    {
                        GameObject.Find("Player");
                    }
                    stopwatch.Stop();
                    results.Add(new { test = "GameObject.Find", iterations, totalMs = stopwatch.ElapsedMilliseconds, avgMs = stopwatch.ElapsedMilliseconds / (float)iterations });
                    break;

                default:
                    return new ErrorResponse("Unknown target. Valid: physics, find");
            }

            return new SuccessResponse($"Stress test completed: {target}", new { target, iterations, results });
        }
        #endregion

        #region Validate Scene
        private static object ValidateScene()
        {
            var result = ValidateSceneInternal();

            return new SuccessResponse($"Scene validation: {result.bugs.Count} bugs, {result.warnings.Count} warnings", new
            {
                passed = result.passed,
                bugs = result.bugs.Select(b => new { b.severity, b.category, b.message, b.location }).ToList(),
                warnings = result.warnings
            });
        }

        private static TestResult ValidateSceneInternal()
        {
            var result = new TestResult();

            // Check scene basics
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            result.passed.Add($"Active scene: {sceneName}");

            // Check for essential objects
            if (Camera.main != null)
                result.passed.Add("MainCamera present");
            else
                result.bugs.Add(new BugReport { severity = "high", category = "missing_essential", message = "No MainCamera in scene", location = sceneName });

            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (lights.Length > 0)
                result.passed.Add($"{lights.Length} lights in scene");
            else
                result.warnings.Add("No lights in scene - everything will be dark");

            // Check for player
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                result.passed.Add("Player object found");
            else
                result.warnings.Add("No object tagged 'Player'");

            // Check hierarchy depth
            var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            int maxDepth = 0;
            Transform deepest = null;
            foreach (var t in allTransforms)
            {
                int depth = 0;
                var current = t;
                while (current.parent != null)
                {
                    depth++;
                    current = current.parent;
                }
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                    deepest = t;
                }
            }
            if (maxDepth > 15)
                result.warnings.Add($"Deep hierarchy detected: {maxDepth} levels ({deepest?.name})");

            // Check for duplicate names at same level
            var duplicates = allTransforms
                .Where(t => t.parent != null)
                .GroupBy(t => new { parent = t.parent, name = t.name })
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.parent.name}/{g.Key.name} ({g.Count()}x)")
                .ToList();

            if (duplicates.Count > 0)
                result.warnings.Add($"Duplicate names found: {string.Join(", ", duplicates.Take(5))}");

            return result;
        }
        #endregion

        #region Find Bugs
        private static object FindBugs()
        {
            var bugs = new List<BugReport>();

            // 1. Missing References
            var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();

                // Missing scripts
                if (components.Any(c => c == null))
                {
                    bugs.Add(new BugReport
                    {
                        severity = "critical",
                        category = "missing_script",
                        message = "GameObject has missing script",
                        location = go.name
                    });
                }

                // Disabled Rigidbody with Collider issues
                var rb = go.GetComponent<Rigidbody>();
                var colliders = go.GetComponents<Collider>();
                if (rb != null && !rb.isKinematic && colliders.Length == 0)
                {
                    bugs.Add(new BugReport
                    {
                        severity = "medium",
                        category = "physics",
                        message = "Non-kinematic Rigidbody without Collider",
                        location = go.name
                    });
                }

                // Check for overlapping colliders at same position
                foreach (var comp in components.Where(c => c != null))
                {
                    try
                    {
                        var so = new SerializedObject(comp);
                        var prop = so.GetIterator();

                        while (prop.NextVisible(true))
                        {
                            // NaN/Infinity checks
                            if (prop.propertyType == SerializedPropertyType.Float)
                            {
                                if (float.IsNaN(prop.floatValue))
                                {
                                    bugs.Add(new BugReport
                                    {
                                        severity = "critical",
                                        category = "invalid_value",
                                        message = $"NaN value in {prop.name}",
                                        location = $"{go.name}.{comp.GetType().Name}"
                                    });
                                }
                                else if (float.IsInfinity(prop.floatValue))
                                {
                                    bugs.Add(new BugReport
                                    {
                                        severity = "critical",
                                        category = "invalid_value",
                                        message = $"Infinity value in {prop.name}",
                                        location = $"{go.name}.{comp.GetType().Name}"
                                    });
                                }
                            }

                            // Vector3 NaN checks
                            if (prop.propertyType == SerializedPropertyType.Vector3)
                            {
                                var v = prop.vector3Value;
                                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z))
                                {
                                    bugs.Add(new BugReport
                                    {
                                        severity = "critical",
                                        category = "invalid_value",
                                        message = $"NaN in Vector3 {prop.name}",
                                        location = $"{go.name}.{comp.GetType().Name}"
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Check for broken prefab connections
            foreach (var go in allObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var status = PrefabUtility.GetPrefabInstanceStatus(go);
                    if (status == PrefabInstanceStatus.MissingAsset)
                    {
                        bugs.Add(new BugReport
                        {
                            severity = "high",
                            category = "broken_prefab",
                            message = "Prefab instance has missing asset",
                            location = go.name
                        });
                    }
                }
            }

            // Summary
            var grouped = bugs.GroupBy(b => b.category)
                .Select(g => new { category = g.Key, count = g.Count() })
                .ToList();

            return new SuccessResponse($"Bug scan: {bugs.Count} bugs found", new
            {
                totalBugs = bugs.Count,
                bySeverity = new
                {
                    critical = bugs.Count(b => b.severity == "critical"),
                    high = bugs.Count(b => b.severity == "high"),
                    medium = bugs.Count(b => b.severity == "medium"),
                    low = bugs.Count(b => b.severity == "low")
                },
                byCategory = grouped,
                bugs = bugs.Select(b => new { b.severity, b.category, b.message, b.location }).ToList()
            });
        }
        #endregion

        #region Performance Profile
        private static object PerformanceProfile()
        {
            var result = AnalyzePerformance();

            return new SuccessResponse("Performance profile complete", new
            {
                objectCounts = result.data["objectCounts"],
                componentCounts = result.data["componentCounts"],
                memoryEstimates = result.data["memoryEstimates"],
                warnings = result.warnings,
                recommendations = result.data["recommendations"]
            });
        }

        private static TestResult AnalyzePerformance()
        {
            var result = new TestResult();
            result.data = new Dictionary<string, object>();

            // Count objects
            var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            var activeObjects = allObjects.Where(g => g.activeInHierarchy).Count();

            result.data["objectCounts"] = new
            {
                total = allObjects.Length,
                active = activeObjects,
                inactive = allObjects.Length - activeObjects
            };

            // Count components by type
            var rigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();

            result.data["componentCounts"] = new
            {
                rigidbodies = rigidbodies.Length,
                colliders = colliders.Length,
                renderers = renderers.Length,
                lights = lights.Length,
                audioSources = audioSources.Length
            };

            // Memory estimates
            var textures = UnityEngine.Resources.FindObjectsOfTypeAll<Texture>();
            var meshes = UnityEngine.Resources.FindObjectsOfTypeAll<Mesh>();
            var materials = UnityEngine.Resources.FindObjectsOfTypeAll<Material>();

            result.data["memoryEstimates"] = new
            {
                textureCount = textures.Length,
                meshCount = meshes.Length,
                materialCount = materials.Length
            };

            // Warnings
            if (rigidbodies.Length > 100)
                result.warnings.Add($"High rigidbody count ({rigidbodies.Length}) may impact physics performance");

            if (lights.Length > 8)
                result.warnings.Add($"Many lights ({lights.Length}) - consider baking or reducing");

            int realtimeLights = lights.Count(l => l.type != LightType.Rectangle && l.lightmapBakeType == LightmapBakeType.Realtime);
            if (realtimeLights > 4)
                result.warnings.Add($"Many realtime lights ({realtimeLights}) - expensive for mobile");

            // Recommendations
            var recommendations = new List<string>();

            if (allObjects.Length - activeObjects > 100)
                recommendations.Add("Consider destroying unused inactive objects instead of disabling");

            if (colliders.Length > rigidbodies.Length * 10)
                recommendations.Add("Many static colliders - ensure they're marked as static for optimization");

            result.data["recommendations"] = recommendations;

            return result;
        }
        #endregion

        #region Helpers
        private static int CalculateHealthScore(int bugs, int warnings, int passed)
        {
            int score = 100;
            score -= bugs * 15;
            score -= warnings * 3;
            score += passed * 2;
            return Mathf.Clamp(score, 0, 100);
        }

        private class TestResult
        {
            public List<BugReport> bugs = new List<BugReport>();
            public List<string> warnings = new List<string>();
            public List<string> passed = new List<string>();
            public Dictionary<string, object> data = new Dictionary<string, object>();
        }

        private class BugReport
        {
            public string severity; // critical, high, medium, low
            public string category; // missing_reference, invalid_value, missing_script, etc.
            public string message;
            public string location;
        }
        #endregion
    }
}
