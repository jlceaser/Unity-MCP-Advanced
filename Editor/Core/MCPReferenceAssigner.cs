#nullable disable
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// MCP Tool for assigning component references between GameObjects.
    /// Solves the limitation of manage_gameobject not being able to set object references.
    /// </summary>
    [McpForUnityTool(
        name: "assign_reference",
        Description = "Assigns component references between GameObjects. Params: sourceObject, sourceComponent (optional), targetObject, targetComponent, targetProperty")]
    public static class MCPReferenceAssigner
    {
        public static object HandleCommand(JObject @params)
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
                    return new ErrorResponse("Required: sourceObject, targetObject, targetComponent, targetProperty. Optional: sourceComponent");
                }

                // Find source GameObject
                var sourceGO = GameObject.Find(sourceObjectName);
                if (sourceGO == null)
                {
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

                // Get source reference (component or GameObject)
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

                return new SuccessResponse(
                    $"Assigned '{sourceObjectName}.{sourceComponentName ?? "GameObject"}' to '{targetObjectName}.{targetComponentName}.{targetPropertyName}'",
                    new {
                        source = sourceObjectName,
                        sourceComponent = sourceComponentName ?? "GameObject",
                        target = targetObjectName,
                        targetComponent = targetComponentName,
                        property = targetPropertyName,
                        success = true
                    });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to assign reference: {e.Message}");
            }
        }
    }
}
