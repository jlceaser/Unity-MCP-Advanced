#nullable disable
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.NativeServer.Protocol;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Tool handler delegate
    /// </summary>
    public delegate Task<MCPToolResult> MCPToolHandler(JObject arguments);

    /// <summary>
    /// Synchronous tool handler delegate
    /// </summary>
    public delegate object MCPSyncToolHandler(JObject arguments);

    /// <summary>
    /// Tool registration info
    /// </summary>
    public class MCPToolRegistration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject InputSchema { get; set; }
        public MCPToolHandler Handler { get; set; }
        public bool RequiresMainThread { get; set; } = true;
        public string Category { get; set; }
        public DateTime RegisteredAt { get; set; }
        public int CallCount { get; set; }
        public double TotalExecutionTime { get; set; }
    }

    /// <summary>
    /// High-performance tool registry for MCP.
    /// Automatically discovers and registers tools from assemblies.
    /// </summary>
    public class MCPToolRegistry
    {
        private readonly ConcurrentDictionary<string, MCPToolRegistration> _tools;
        private readonly MCPResponseCache _responseCache;
        private readonly MCPCircuitBreaker _circuitBreaker;
        private readonly object _lock = new object();

        public int ToolCount => _tools.Count;
        public IEnumerable<string> ToolNames => _tools.Keys;
        public MCPResponseCache ResponseCache => _responseCache;
        public MCPCircuitBreaker CircuitBreaker => _circuitBreaker;

        public event Action<string> OnToolRegistered;
        public event Action<string, double> OnToolExecuted;
        public event Action<string, Exception> OnToolError;
        public event Action<string> OnCacheHit;

        public MCPToolRegistry()
        {
            _tools = new ConcurrentDictionary<string, MCPToolRegistration>(StringComparer.OrdinalIgnoreCase);
            _responseCache = new MCPResponseCache();
            _circuitBreaker = new MCPCircuitBreaker();
        }

        #region Registration

        /// <summary>
        /// Register a tool with async handler
        /// </summary>
        public void RegisterTool(string name, string description, MCPToolHandler handler, JObject inputSchema = null, string category = null)
        {
            var registration = new MCPToolRegistration
            {
                Name = name,
                Description = description,
                Handler = handler,
                InputSchema = inputSchema ?? CreateDefaultSchema(),
                Category = category ?? "general",
                RegisteredAt = DateTime.UtcNow
            };

            _tools[name] = registration;
            OnToolRegistered?.Invoke(name);
        }

        /// <summary>
        /// Register a tool with sync handler (will be wrapped in Task)
        /// </summary>
        public void RegisterTool(string name, string description, MCPSyncToolHandler syncHandler, JObject inputSchema = null, string category = null)
        {
            MCPToolHandler asyncHandler = (args) =>
            {
                object result = syncHandler(args);
                return Task.FromResult(ConvertToToolResult(result));
            };

            RegisterTool(name, description, asyncHandler, inputSchema, category);
        }

        /// <summary>
        /// Register a tool with Func<JObject, object> handler
        /// </summary>
        public void RegisterTool(string name, string description, Func<JObject, object> handler, JObject inputSchema = null, string category = null)
        {
            RegisterTool(name, description, (MCPSyncToolHandler)(args => handler(args)), inputSchema, category);
        }

        /// <summary>
        /// Unregister a tool
        /// </summary>
        public bool UnregisterTool(string name)
        {
            return _tools.TryRemove(name, out _);
        }

        /// <summary>
        /// Check if tool exists
        /// </summary>
        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        /// <summary>
        /// Get tool registration
        /// </summary>
        public MCPToolRegistration GetTool(string name)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }

        #endregion

        #region Auto-Discovery

        /// <summary>
        /// Auto-discover and register tools from assemblies using McpForUnityTool attribute
        /// </summary>
        public int DiscoverTools()
        {
            int count = 0;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    if (assembly.IsDynamic) continue;
                    if (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("mscorlib")) continue;

                    count += DiscoverToolsInAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCPToolRegistry] Failed to scan assembly {assembly.FullName}: {ex.Message}");
                }
            }

            Debug.Log($"[MCPToolRegistry] Discovered {count} tools from assemblies");
            return count;
        }

        private int DiscoverToolsInAssembly(Assembly assembly)
        {
            int count = 0;

            try
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // Look for McpForUnityTool attribute
                    var toolAttr = type.GetCustomAttributes()
                        .FirstOrDefault(a => a.GetType().Name == "McpForUnityToolAttribute");

                    if (toolAttr != null)
                    {
                        if (RegisterToolFromType(type, toolAttr))
                        {
                            count++;
                        }
                    }
                }
            }
            catch { }

            return count;
        }

        private bool RegisterToolFromType(Type type, Attribute toolAttr)
        {
            try
            {
                // Get Name and Description from attribute
                var nameProperty = toolAttr.GetType().GetProperty("Name");
                var descProperty = toolAttr.GetType().GetProperty("Description");

                string name = nameProperty?.GetValue(toolAttr) as string ?? type.Name.ToLower();
                string description = descProperty?.GetValue(toolAttr) as string ?? $"Tool: {type.Name}";

                // Find HandleCommand method
                var handleMethod = type.GetMethod("HandleCommand", BindingFlags.Public | BindingFlags.Static);
                if (handleMethod == null)
                {
                    Debug.LogWarning($"[MCPToolRegistry] Tool {name} has no HandleCommand method");
                    return false;
                }

                // Create handler
                MCPSyncToolHandler handler = (args) =>
                {
                    try
                    {
                        return handleMethod.Invoke(null, new object[] { args });
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw ex.InnerException ?? ex;
                    }
                };

                RegisterTool(name, description, handler, category: "discovered");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCPToolRegistry] Failed to register tool from {type.Name}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Execution

        /// <summary>
        /// Execute a tool by name
        /// </summary>
        public async Task<MCPToolResult> ExecuteTool(string name, JObject arguments)
        {
            if (!_tools.TryGetValue(name, out var registration))
            {
                return new MCPToolResult
                {
                    IsError = true,
                    Content = new List<MCPContent>
                    {
                        MCPContent.TextContent($"Tool not found: {name}")
                    }
                };
            }

            // Check circuit breaker - prevent execution if circuit is open
            if (!_circuitBreaker.AllowRequest(name))
            {
                var circuitState = _circuitBreaker.GetState(name);
                return new MCPToolResult
                {
                    IsError = true,
                    Content = new List<MCPContent>
                    {
                        MCPContent.TextContent($"Tool '{name}' is temporarily unavailable (circuit {circuitState}). Please try again later.")
                    }
                };
            }

            // Check response cache first (for read-only tools)
            if (_responseCache.TryGetCachedResponse(name, arguments, out var cachedResult, out _))
            {
                OnCacheHit?.Invoke(name);
                _circuitBreaker.RecordSuccess(name); // Cache hits count as success
                return cachedResult;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                MCPToolResult result;

                if (registration.RequiresMainThread)
                {
                    // Execute on main thread
                    result = await ExecuteOnMainThread(registration.Handler, arguments);
                }
                else
                {
                    result = await registration.Handler(arguments);
                }

                // Update stats
                registration.CallCount++;
                registration.TotalExecutionTime += (DateTime.UtcNow - startTime).TotalMilliseconds;

                // Cache the response (only for successful read-only tools)
                _responseCache.CacheResponse(name, arguments, result);

                // Record success for circuit breaker
                if (!result.IsError)
                {
                    _circuitBreaker.RecordSuccess(name);
                }
                else
                {
                    _circuitBreaker.RecordFailure(name, "Tool returned error");
                }

                OnToolExecuted?.Invoke(name, (DateTime.UtcNow - startTime).TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure(name, ex.Message);
                OnToolError?.Invoke(name, ex);

                return new MCPToolResult
                {
                    IsError = true,
                    Content = new List<MCPContent>
                    {
                        MCPContent.TextContent($"Tool execution failed: {ex.Message}")
                    }
                };
            }
        }

        // Priority queue for main thread execution
        private static readonly MCPPriorityQueue _mainThreadQueue = new MCPPriorityQueue();
        private static bool _updateRegistered = false;

        /// <summary>
        /// Get priority queue statistics
        /// </summary>
        public static Dictionary<string, object> GetQueueStats() => _mainThreadQueue.GetStats();

        private Task<MCPToolResult> ExecuteOnMainThread(MCPToolHandler handler, JObject arguments, MCPPriority priority = MCPPriority.Normal, string toolName = null)
        {
            var tcs = new TaskCompletionSource<MCPToolResult>();

            // Ensure update callback is registered
            EnsureUpdateRegistered();

            // Queue the work with priority
            _mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    var task = handler(arguments);
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            tcs.SetException(t.Exception.InnerException ?? t.Exception);
                        else
                            tcs.SetResult(t.Result);
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority, toolName);

            return tcs.Task;
        }

        private void EnsureUpdateRegistered()
        {
            if (_updateRegistered) return;
            _updateRegistered = true;

            UnityEditor.EditorApplication.update += ProcessMainThreadQueue;

            // Also register for quitting to cleanup
            UnityEditor.EditorApplication.quitting += () => _updateRegistered = false;
        }

        /// <summary>
        /// Force Unity to process queued requests (call from HTTP handler)
        /// </summary>
        public static void RequestUpdate()
        {
            // Try multiple methods to wake up Unity
            try
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { }

            // Also use delayCall as backup
            UnityEditor.EditorApplication.delayCall += () => { };
        }

        private const int MaxItemsPerFrame = 50; // Increased from 10 for better throughput
        private const int MaxHighPriorityPerFrame = 100; // Process more high-priority items

        private static void ProcessMainThreadQueue()
        {
            int processed = 0;

            // First, process ALL high-priority items (up to limit)
            while (_mainThreadQueue.HasHighPriority && processed < MaxHighPriorityPerFrame)
            {
                if (_mainThreadQueue.TryDequeue(MCPPriority.High, out var highItem))
                {
                    try
                    {
                        highItem.Action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MCPToolRegistry] Main thread execution error (High): {ex.Message}");
                    }
                    processed++;
                }
                else break;
            }

            // Then process other priorities up to the normal limit
            while (processed < MaxItemsPerFrame)
            {
                var item = _mainThreadQueue.Dequeue();
                if (item == null) break;

                try
                {
                    item.Action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MCPToolRegistry] Main thread execution error ({item.Priority}): {ex.Message}");
                }
                processed++;
            }
        }

        #endregion

        #region Batch Execution

        /// <summary>
        /// Batch request structure
        /// </summary>
        public class BatchRequest
        {
            public string Name { get; set; }
            public JObject Arguments { get; set; }
            public string Id { get; set; }
        }

        /// <summary>
        /// Batch response structure
        /// </summary>
        public class BatchResponse
        {
            public string Id { get; set; }
            public MCPToolResult Result { get; set; }
            public double ExecutionTimeMs { get; set; }
        }

        /// <summary>
        /// Execute multiple tools in a batch
        /// </summary>
        public async Task<List<BatchResponse>> ExecuteBatch(List<BatchRequest> requests, bool parallel = true)
        {
            if (requests == null || requests.Count == 0)
            {
                return new List<BatchResponse>();
            }

            var responses = new List<BatchResponse>();

            if (parallel)
            {
                // Execute tools in parallel (where possible)
                var tasks = new List<Task<BatchResponse>>();

                foreach (var request in requests)
                {
                    tasks.Add(ExecuteSingleBatchRequest(request));
                }

                var results = await Task.WhenAll(tasks);
                responses.AddRange(results);
            }
            else
            {
                // Execute tools sequentially
                foreach (var request in requests)
                {
                    var response = await ExecuteSingleBatchRequest(request);
                    responses.Add(response);
                }
            }

            return responses;
        }

        private async Task<BatchResponse> ExecuteSingleBatchRequest(BatchRequest request)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await ExecuteTool(request.Name, request.Arguments ?? new JObject());
            sw.Stop();

            return new BatchResponse
            {
                Id = request.Id ?? request.Name,
                Result = result,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }

        #endregion

        #region List Tools

        /// <summary>
        /// Get all tools as MCP tool definitions
        /// </summary>
        public List<MCPTool> GetAllTools()
        {
            return _tools.Values.Select(t => new MCPTool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList();
        }

        /// <summary>
        /// Get tools by category
        /// </summary>
        public List<MCPTool> GetToolsByCategory(string category)
        {
            return _tools.Values
                .Where(t => t.Category == category)
                .Select(t => new MCPTool
                {
                    Name = t.Name,
                    Description = t.Description,
                    InputSchema = t.InputSchema
                }).ToList();
        }

        /// <summary>
        /// Get tool statistics
        /// </summary>
        public Dictionary<string, object> GetStats()
        {
            return new Dictionary<string, object>
            {
                ["total_tools"] = _tools.Count,
                ["total_calls"] = _tools.Values.Sum(t => t.CallCount),
                ["categories"] = _tools.Values.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.Count()),
                ["most_used"] = _tools.Values.OrderByDescending(t => t.CallCount).Take(10).Select(t => new { t.Name, t.CallCount }).ToList()
            };
        }

        #endregion

        #region Helpers

        private MCPToolResult ConvertToToolResult(object result)
        {
            if (result == null)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent> { MCPContent.TextContent("null") }
                };
            }

            if (result is MCPToolResult toolResult)
            {
                return toolResult;
            }

            if (result is string str)
            {
                return new MCPToolResult
                {
                    Content = new List<MCPContent> { MCPContent.TextContent(str) }
                };
            }

            // FAST PATH: Direct type checks for known response types (no reflection)
            if (result is MCPForUnity.Editor.Helpers.SuccessResponse successResp)
            {
                return new MCPToolResult
                {
                    IsError = false,
                    Content = new List<MCPContent> { MCPContent.JsonContent(result) }
                };
            }

            if (result is MCPForUnity.Editor.Helpers.ErrorResponse errorResp)
            {
                return new MCPToolResult
                {
                    IsError = true,
                    Content = new List<MCPContent> { MCPContent.JsonContent(result) }
                };
            }

            if (result is MCPForUnity.Editor.Helpers.PendingResponse)
            {
                return new MCPToolResult
                {
                    IsError = false,
                    Content = new List<MCPContent> { MCPContent.JsonContent(result) }
                };
            }

            // Check IMcpResponse interface (still fast, no property reflection)
            if (result is MCPForUnity.Editor.Helpers.IMcpResponse mcpResponse)
            {
                return new MCPToolResult
                {
                    IsError = !mcpResponse.Success,
                    Content = new List<MCPContent> { MCPContent.JsonContent(result) }
                };
            }

            // SLOW PATH: Use cached property accessors (Expression tree compiled delegates)
            bool isError = false;

            // Check "success" property using cached accessor
            var (successFound, successValue) = CachedPropertyAccessor.TryGetBoolProperty(result, "success");
            if (successFound)
            {
                isError = !successValue;
            }

            // Check "error" property using cached accessor
            var (errorFound, errorValue) = CachedPropertyAccessor.TryGetStringProperty(result, "error");
            if (errorFound && !string.IsNullOrEmpty(errorValue))
            {
                isError = true;
            }

            return new MCPToolResult
            {
                IsError = isError,
                Content = new List<MCPContent>
                {
                    MCPContent.JsonContent(result)
                }
            };
        }

        private JObject CreateDefaultSchema()
        {
            return JObject.FromObject(new
            {
                type = "object",
                properties = new { },
                additionalProperties = true
            });
        }

        #endregion
    }
}
