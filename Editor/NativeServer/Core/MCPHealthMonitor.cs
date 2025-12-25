using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Health status levels
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// Health check result
    /// </summary>
    public class HealthCheckResult
    {
        public string Component { get; set; }
        public HealthStatus Status { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Details { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    /// <summary>
    /// Monitors MCP server health and provides diagnostics
    /// </summary>
    public class MCPHealthMonitor
    {
        private readonly MCPToolRegistry _toolRegistry;
        private readonly MCPCircuitBreaker _circuitBreaker;

        // Metrics
        private int _totalRequests;
        private int _failedRequests;
        private int _cacheHits;
        private DateTime _startTime;
        private DateTime _lastRequestTime;
        private double _totalExecutionTimeMs;

        // Thresholds
        public double ErrorRateThreshold { get; set; } = 0.1; // 10% error rate triggers degraded
        public double HighErrorRateThreshold { get; set; } = 0.25; // 25% triggers unhealthy
        public int OpenCircuitsThreshold { get; set; } = 3; // Number of open circuits for degraded
        public long MemoryThresholdMB { get; set; } = 500; // Memory threshold for warning

        public MCPHealthMonitor(MCPToolRegistry toolRegistry, MCPCircuitBreaker circuitBreaker)
        {
            _toolRegistry = toolRegistry;
            _circuitBreaker = circuitBreaker;
            _startTime = DateTime.UtcNow;

            // Subscribe to events
            if (_toolRegistry != null)
            {
                _toolRegistry.OnToolExecuted += OnToolExecuted;
                _toolRegistry.OnToolError += OnToolError;
                _toolRegistry.OnCacheHit += OnCacheHit;
            }
        }

        private void OnToolExecuted(string toolName, double executionTimeMs)
        {
            _totalRequests++;
            _lastRequestTime = DateTime.UtcNow;
            _totalExecutionTimeMs += executionTimeMs;
            _circuitBreaker?.RecordSuccess(toolName);
        }

        private void OnToolError(string toolName, Exception ex)
        {
            _failedRequests++;
            _circuitBreaker?.RecordFailure(toolName, ex.Message);
        }

        private void OnCacheHit(string toolName)
        {
            _cacheHits++;
        }

        /// <summary>
        /// Perform a full health check
        /// </summary>
        public Dictionary<string, object> GetHealthReport()
        {
            var checks = new List<HealthCheckResult>();

            // Check error rate
            checks.Add(CheckErrorRate());

            // Check circuit breakers
            checks.Add(CheckCircuitBreakers());

            // Check memory usage
            checks.Add(CheckMemory());

            // Check tool registry
            checks.Add(CheckToolRegistry());

            // Check response cache
            checks.Add(CheckResponseCache());

            // Determine overall status
            var overallStatus = HealthStatus.Healthy;
            foreach (var check in checks)
            {
                if (check.Status == HealthStatus.Unhealthy)
                {
                    overallStatus = HealthStatus.Unhealthy;
                    break;
                }
                if (check.Status == HealthStatus.Degraded && overallStatus != HealthStatus.Unhealthy)
                {
                    overallStatus = HealthStatus.Degraded;
                }
            }

            return new Dictionary<string, object>
            {
                ["status"] = overallStatus.ToString(),
                ["uptime_seconds"] = (DateTime.UtcNow - _startTime).TotalSeconds,
                ["last_request"] = _lastRequestTime,
                ["metrics"] = new Dictionary<string, object>
                {
                    ["total_requests"] = _totalRequests,
                    ["failed_requests"] = _failedRequests,
                    ["cache_hits"] = _cacheHits,
                    ["error_rate"] = _totalRequests > 0 ? (double)_failedRequests / _totalRequests : 0,
                    ["cache_hit_rate"] = _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0,
                    ["avg_execution_time_ms"] = _totalRequests > 0 ? _totalExecutionTimeMs / _totalRequests : 0
                },
                ["checks"] = checks
            };
        }

        /// <summary>
        /// Get a quick health status
        /// </summary>
        public HealthStatus GetQuickStatus()
        {
            // Quick checks without detailed analysis
            double errorRate = _totalRequests > 0 ? (double)_failedRequests / _totalRequests : 0;

            if (errorRate >= HighErrorRateThreshold)
                return HealthStatus.Unhealthy;

            if (errorRate >= ErrorRateThreshold)
                return HealthStatus.Degraded;

            var openCircuits = _circuitBreaker?.GetOpenCircuits();
            if (openCircuits != null && openCircuits.Count >= OpenCircuitsThreshold)
                return HealthStatus.Degraded;

            return HealthStatus.Healthy;
        }

        private HealthCheckResult CheckErrorRate()
        {
            double errorRate = _totalRequests > 0 ? (double)_failedRequests / _totalRequests : 0;
            HealthStatus status;
            string message;

            if (errorRate >= HighErrorRateThreshold)
            {
                status = HealthStatus.Unhealthy;
                message = $"High error rate: {errorRate:P1}";
            }
            else if (errorRate >= ErrorRateThreshold)
            {
                status = HealthStatus.Degraded;
                message = $"Elevated error rate: {errorRate:P1}";
            }
            else
            {
                status = HealthStatus.Healthy;
                message = $"Error rate normal: {errorRate:P1}";
            }

            return new HealthCheckResult
            {
                Component = "ErrorRate",
                Status = status,
                Message = message,
                Details = new Dictionary<string, object>
                {
                    ["total_requests"] = _totalRequests,
                    ["failed_requests"] = _failedRequests,
                    ["error_rate"] = errorRate
                },
                CheckedAt = DateTime.UtcNow
            };
        }

        private HealthCheckResult CheckCircuitBreakers()
        {
            if (_circuitBreaker == null)
            {
                return new HealthCheckResult
                {
                    Component = "CircuitBreakers",
                    Status = HealthStatus.Healthy,
                    Message = "Circuit breaker not configured",
                    CheckedAt = DateTime.UtcNow
                };
            }

            var openCircuits = _circuitBreaker.GetOpenCircuits();
            var stats = _circuitBreaker.GetStats();

            HealthStatus status;
            string message;

            if (openCircuits.Count >= OpenCircuitsThreshold)
            {
                status = HealthStatus.Degraded;
                message = $"{openCircuits.Count} circuits open";
            }
            else if (openCircuits.Count > 0)
            {
                status = HealthStatus.Healthy;
                message = $"{openCircuits.Count} circuits open (below threshold)";
            }
            else
            {
                status = HealthStatus.Healthy;
                message = "All circuits closed";
            }

            return new HealthCheckResult
            {
                Component = "CircuitBreakers",
                Status = status,
                Message = message,
                Details = stats,
                CheckedAt = DateTime.UtcNow
            };
        }

        private HealthCheckResult CheckMemory()
        {
            long memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            HealthStatus status;
            string message;

            if (memoryMB >= MemoryThresholdMB * 1.5)
            {
                status = HealthStatus.Unhealthy;
                message = $"Memory critical: {memoryMB}MB";
            }
            else if (memoryMB >= MemoryThresholdMB)
            {
                status = HealthStatus.Degraded;
                message = $"Memory high: {memoryMB}MB";
            }
            else
            {
                status = HealthStatus.Healthy;
                message = $"Memory normal: {memoryMB}MB";
            }

            return new HealthCheckResult
            {
                Component = "Memory",
                Status = status,
                Message = message,
                Details = new Dictionary<string, object>
                {
                    ["used_mb"] = memoryMB,
                    ["threshold_mb"] = MemoryThresholdMB
                },
                CheckedAt = DateTime.UtcNow
            };
        }

        private HealthCheckResult CheckToolRegistry()
        {
            if (_toolRegistry == null)
            {
                return new HealthCheckResult
                {
                    Component = "ToolRegistry",
                    Status = HealthStatus.Unhealthy,
                    Message = "Tool registry not available",
                    CheckedAt = DateTime.UtcNow
                };
            }

            int toolCount = _toolRegistry.ToolCount;
            return new HealthCheckResult
            {
                Component = "ToolRegistry",
                Status = toolCount > 0 ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = $"{toolCount} tools registered",
                Details = new Dictionary<string, object>
                {
                    ["tool_count"] = toolCount
                },
                CheckedAt = DateTime.UtcNow
            };
        }

        private HealthCheckResult CheckResponseCache()
        {
            var cache = _toolRegistry?.ResponseCache;
            if (cache == null)
            {
                return new HealthCheckResult
                {
                    Component = "ResponseCache",
                    Status = HealthStatus.Healthy,
                    Message = "Response cache not configured",
                    CheckedAt = DateTime.UtcNow
                };
            }

            var stats = cache.GetStats();
            return new HealthCheckResult
            {
                Component = "ResponseCache",
                Status = HealthStatus.Healthy,
                Message = $"{stats["total_entries"]} cached entries",
                Details = stats,
                CheckedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Reset all metrics
        /// </summary>
        public void ResetMetrics()
        {
            _totalRequests = 0;
            _failedRequests = 0;
            _cacheHits = 0;
            _totalExecutionTimeMs = 0;
            _startTime = DateTime.UtcNow;
        }
    }
}
