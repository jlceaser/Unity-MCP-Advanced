#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    #region Security Enums and Attributes

    /// <summary>
    /// Security level for MCP operations.
    /// </summary>
    public enum MCPSecurityLevel
    {
        /// <summary>All destructive operations require user approval.</summary>
        Strict,
        
        /// <summary>Only delete/write operations require approval (Default).</summary>
        Standard,
        
        /// <summary>No prompts - power user mode.</summary>
        Unrestricted
    }

    /// <summary>
    /// Marks a tool or action as potentially dangerous.
    /// When the security system is active, these operations will prompt for user approval.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class DangerousToolAttribute : Attribute
    {
        public string Description { get; }
        public DangerousOperationType OperationType { get; }

        public DangerousToolAttribute(string description, DangerousOperationType operationType = DangerousOperationType.Destructive)
        {
            Description = description;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// Types of dangerous operations.
    /// </summary>
    public enum DangerousOperationType
    {
        /// <summary>Deletes files, assets, or GameObjects.</summary>
        Destructive,
        
        /// <summary>Modifies critical settings or files.</summary>
        Modification,
        
        /// <summary>Executes arbitrary code.</summary>
        CodeExecution,
        
        /// <summary>Affects build or project settings.</summary>
        ProjectSettings
    }

    #endregion

    /// <summary>
    /// "GUARDIAN" - Security & Sandboxing System
    /// Provides approval mechanisms for destructive operations.
    /// Builds trust in the open-source community through safe-by-default design.
    /// </summary>
    [McpForUnityTool(
        name: "security_control",
        Description = "Security settings: get/set security level, approve/deny operations, view pending. Actions: get_level, set_level, get_pending, clear_session")]
    public static class MCPSecurity
    {
        // Current security level
        private static MCPSecurityLevel _currentLevel = MCPSecurityLevel.Standard;
        
        // Session-based approvals (cleared when Unity restarts)
        private static HashSet<string> _sessionApprovals = new HashSet<string>();
        
        // Pending operations waiting for approval
        private static Queue<PendingOperation> _pendingOperations = new Queue<PendingOperation>();

        // EditorPrefs key for persistent security level
        private const string SECURITY_LEVEL_KEY = "MCP_SecurityLevel";

        /// <summary>
        /// Current security level.
        /// </summary>
        public static MCPSecurityLevel CurrentLevel
        {
            get => _currentLevel;
            set
            {
                _currentLevel = value;
                EditorPrefs.SetInt(SECURITY_LEVEL_KEY, (int)value);
            }
        }

        /// <summary>
        /// Static constructor to load saved settings.
        /// </summary>
        static MCPSecurity()
        {
            if (EditorPrefs.HasKey(SECURITY_LEVEL_KEY))
            {
                _currentLevel = (MCPSecurityLevel)EditorPrefs.GetInt(SECURITY_LEVEL_KEY, (int)MCPSecurityLevel.Standard);
            }
        }

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "get_level";

            switch (action)
            {
                case "get_level":
                    return GetSecurityLevel();

                case "set_level":
                    return SetSecurityLevel(@params);

                case "get_pending":
                    return GetPendingOperations();

                case "clear_session":
                    return ClearSessionApprovals();

                case "approve_all":
                    return ApproveAllPending();

                case "get_stats":
                    return GetSecurityStats();

                default:
                    return new SuccessResponse("Security Control ready. Actions: get_level, set_level, get_pending, clear_session, approve_all, get_stats");
            }
        }

        #region Public API for Tool Integration

        /// <summary>
        /// Checks if an operation requires approval and prompts if necessary.
        /// Returns true if operation is allowed, false if denied.
        /// </summary>
        /// <param name="operationName">Name of the operation (e.g., "DeleteAsset")</param>
        /// <param name="details">Human-readable details about what will happen</param>
        /// <param name="operationType">Type of dangerous operation</param>
        public static bool RequestApproval(string operationName, string details, DangerousOperationType operationType = DangerousOperationType.Destructive)
        {
            // Unrestricted mode - always allow
            if (_currentLevel == MCPSecurityLevel.Unrestricted)
            {
                return true;
            }

            // Check session approvals
            string approvalKey = $"{operationName}:{operationType}";
            if (_sessionApprovals.Contains(approvalKey))
            {
                return true;
            }

            // In Strict mode, all operations need approval
            // In Standard mode, only Destructive and CodeExecution need approval
            bool needsApproval = _currentLevel == MCPSecurityLevel.Strict ||
                                 operationType == DangerousOperationType.Destructive ||
                                 operationType == DangerousOperationType.CodeExecution;

            if (!needsApproval)
            {
                return true;
            }

            // Show approval dialog
            string title = GetDialogTitle(operationType);
            string message = $"An AI assistant wants to perform the following operation:\n\n" +
                           $"Operation: {operationName}\n" +
                           $"Details: {details}\n\n" +
                           $"Do you want to allow this?";

            int result = EditorUtility.DisplayDialogComplex(
                title,
                message,
                "Allow",           // Option 0
                "Deny",            // Option 1
                "Allow All (Session)" // Option 2
            );

            switch (result)
            {
                case 0: // Allow
                    return true;
                    
                case 1: // Deny
                    return false;
                    
                case 2: // Allow All (Session)
                    _sessionApprovals.Add(approvalKey);
                    return true;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Simplified approval check that returns an error response if denied.
        /// Use this at the start of dangerous tool methods.
        /// </summary>
        public static object CheckApprovalOrError(string operationName, string details, DangerousOperationType operationType = DangerousOperationType.Destructive)
        {
            if (!RequestApproval(operationName, details, operationType))
            {
                return new ErrorResponse($"Operation denied by user: {operationName}");
            }
            return null; // Null means approved
        }

        /// <summary>
        /// Wraps a dangerous action with approval check.
        /// </summary>
        public static object ExecuteWithApproval<T>(
            string operationName,
            string details,
            DangerousOperationType operationType,
            Func<T> action) where T : class
        {
            var error = CheckApprovalOrError(operationName, details, operationType);
            if (error != null) return error;

            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Operation failed: {ex.Message}");
            }
        }

        #endregion

        #region Command Handlers

        private static object GetSecurityLevel()
        {
            return new SuccessResponse($"Current security level: {_currentLevel}", new
            {
                level = _currentLevel.ToString(),
                levelValue = (int)_currentLevel,
                description = GetLevelDescription(_currentLevel),
                sessionApprovals = _sessionApprovals.Count
            });
        }

        private static object SetSecurityLevel(JObject @params)
        {
            string levelStr = @params["level"]?.ToString()?.ToLower();

            MCPSecurityLevel newLevel;
            switch (levelStr)
            {
                case "strict":
                case "0":
                    newLevel = MCPSecurityLevel.Strict;
                    break;
                case "standard":
                case "1":
                    newLevel = MCPSecurityLevel.Standard;
                    break;
                case "unrestricted":
                case "2":
                    newLevel = MCPSecurityLevel.Unrestricted;
                    break;
                default:
                    return new ErrorResponse($"Invalid level: {levelStr}. Use: strict, standard, or unrestricted");
            }

            // Changing to unrestricted requires confirmation
            if (newLevel == MCPSecurityLevel.Unrestricted && _currentLevel != MCPSecurityLevel.Unrestricted)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Security Warning",
                    "Setting security to UNRESTRICTED will allow all MCP operations without prompts.\n\n" +
                    "This is recommended only for experienced users who trust the AI completely.\n\n" +
                    "Are you sure?",
                    "Yes, I understand",
                    "Cancel"
                );

                if (!confirm)
                {
                    return new ErrorResponse("Security level change cancelled by user");
                }
            }

            var oldLevel = _currentLevel;
            CurrentLevel = newLevel;

            return new SuccessResponse($"Security level changed: {oldLevel} → {newLevel}", new
            {
                previousLevel = oldLevel.ToString(),
                newLevel = newLevel.ToString(),
                description = GetLevelDescription(newLevel)
            });
        }

        private static object GetPendingOperations()
        {
            var pending = _pendingOperations.ToArray();
            return new SuccessResponse($"{pending.Length} pending operations", new
            {
                count = pending.Length,
                operations = pending
            });
        }

        private static object ClearSessionApprovals()
        {
            int count = _sessionApprovals.Count;
            _sessionApprovals.Clear();
            return new SuccessResponse($"Cleared {count} session approvals");
        }

        private static object ApproveAllPending()
        {
            int count = _pendingOperations.Count;
            _pendingOperations.Clear();
            return new SuccessResponse($"Approved {count} pending operations");
        }

        private static object GetSecurityStats()
        {
            return new SuccessResponse("Security statistics", new
            {
                currentLevel = _currentLevel.ToString(),
                sessionApprovals = _sessionApprovals.Count,
                pendingOperations = _pendingOperations.Count,
                approvedOperations = _sessionApprovals.ToArray()
            });
        }

        #endregion

        #region Helpers

        private static string GetDialogTitle(DangerousOperationType type)
        {
            switch (type)
            {
                case DangerousOperationType.Destructive:
                    return "⚠️ Destructive Operation";
                case DangerousOperationType.CodeExecution:
                    return "🔧 Code Execution Request";
                case DangerousOperationType.ProjectSettings:
                    return "⚙️ Project Settings Change";
                case DangerousOperationType.Modification:
                    return "✏️ Modification Request";
                default:
                    return "MCP Operation Request";
            }
        }

        private static string GetLevelDescription(MCPSecurityLevel level)
        {
            switch (level)
            {
                case MCPSecurityLevel.Strict:
                    return "All destructive operations require explicit user approval.";
                case MCPSecurityLevel.Standard:
                    return "Delete and code execution operations require approval. Modifications allowed.";
                case MCPSecurityLevel.Unrestricted:
                    return "No prompts. All operations execute immediately. Use with caution.";
                default:
                    return "Unknown";
            }
        }

        #endregion

        #region Helper Classes

        private class PendingOperation
        {
            public string OperationName { get; set; }
            public string Details { get; set; }
            public DangerousOperationType Type { get; set; }
            public DateTime RequestedAt { get; set; }
        }

        #endregion
    }
}
