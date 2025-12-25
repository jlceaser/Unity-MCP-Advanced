using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Circuit breaker state
    /// </summary>
    public enum CircuitState
    {
        /// <summary>Normal operation, requests are allowed</summary>
        Closed,

        /// <summary>Failures exceeded threshold, requests are blocked</summary>
        Open,

        /// <summary>Testing if service has recovered</summary>
        HalfOpen
    }

    /// <summary>
    /// Circuit breaker for individual tools
    /// </summary>
    public class ToolCircuitBreaker
    {
        public string ToolName { get; }
        public CircuitState State { get; private set; } = CircuitState.Closed;
        public int FailureCount { get; private set; }
        public int SuccessCount { get; private set; }
        public DateTime LastFailure { get; private set; }
        public DateTime? OpenedAt { get; private set; }
        public string LastError { get; private set; }

        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly int _successThresholdForClose;

        public ToolCircuitBreaker(string toolName, int failureThreshold = 5, int openDurationSeconds = 60, int successThresholdForClose = 3)
        {
            ToolName = toolName;
            _failureThreshold = failureThreshold;
            _openDuration = TimeSpan.FromSeconds(openDurationSeconds);
            _successThresholdForClose = successThresholdForClose;
        }

        public bool AllowRequest()
        {
            switch (State)
            {
                case CircuitState.Closed:
                    return true;

                case CircuitState.Open:
                    // Check if enough time has passed to try again
                    if (DateTime.UtcNow - OpenedAt > _openDuration)
                    {
                        State = CircuitState.HalfOpen;
                        return true;
                    }
                    return false;

                case CircuitState.HalfOpen:
                    // Allow limited requests in half-open state
                    return true;

                default:
                    return true;
            }
        }

        public void RecordSuccess()
        {
            SuccessCount++;

            switch (State)
            {
                case CircuitState.HalfOpen:
                    // If enough successes in half-open, close the circuit
                    if (SuccessCount >= _successThresholdForClose)
                    {
                        State = CircuitState.Closed;
                        FailureCount = 0;
                        OpenedAt = null;
                    }
                    break;

                case CircuitState.Closed:
                    // Reset failure count on success
                    if (FailureCount > 0)
                    {
                        FailureCount--;
                    }
                    break;
            }
        }

        public void RecordFailure(string error)
        {
            FailureCount++;
            LastFailure = DateTime.UtcNow;
            LastError = error;
            SuccessCount = 0;

            switch (State)
            {
                case CircuitState.Closed:
                    if (FailureCount >= _failureThreshold)
                    {
                        State = CircuitState.Open;
                        OpenedAt = DateTime.UtcNow;
                    }
                    break;

                case CircuitState.HalfOpen:
                    // Any failure in half-open reopens the circuit
                    State = CircuitState.Open;
                    OpenedAt = DateTime.UtcNow;
                    break;
            }
        }

        public void Reset()
        {
            State = CircuitState.Closed;
            FailureCount = 0;
            SuccessCount = 0;
            OpenedAt = null;
            LastError = null;
        }
    }

    /// <summary>
    /// Circuit breaker manager for all tools
    /// </summary>
    public class MCPCircuitBreaker
    {
        private readonly ConcurrentDictionary<string, ToolCircuitBreaker> _circuits = new();

        // Configuration
        public int DefaultFailureThreshold { get; set; } = 5;
        public int DefaultOpenDurationSeconds { get; set; } = 60;
        public int DefaultSuccessThreshold { get; set; } = 3;

        /// <summary>
        /// Check if a tool request is allowed
        /// </summary>
        public bool AllowRequest(string toolName)
        {
            var circuit = GetOrCreateCircuit(toolName);
            return circuit.AllowRequest();
        }

        /// <summary>
        /// Record a successful tool execution
        /// </summary>
        public void RecordSuccess(string toolName)
        {
            var circuit = GetOrCreateCircuit(toolName);
            circuit.RecordSuccess();
        }

        /// <summary>
        /// Record a failed tool execution
        /// </summary>
        public void RecordFailure(string toolName, string error)
        {
            var circuit = GetOrCreateCircuit(toolName);
            circuit.RecordFailure(error);
        }

        /// <summary>
        /// Get circuit state for a tool
        /// </summary>
        public CircuitState GetState(string toolName)
        {
            return _circuits.TryGetValue(toolName, out var circuit) ? circuit.State : CircuitState.Closed;
        }

        /// <summary>
        /// Reset circuit for a tool
        /// </summary>
        public void ResetCircuit(string toolName)
        {
            if (_circuits.TryGetValue(toolName, out var circuit))
            {
                circuit.Reset();
            }
        }

        /// <summary>
        /// Reset all circuits
        /// </summary>
        public void ResetAll()
        {
            foreach (var circuit in _circuits.Values)
            {
                circuit.Reset();
            }
        }

        /// <summary>
        /// Get all open circuits
        /// </summary>
        public List<(string toolName, string lastError, DateTime? openedAt)> GetOpenCircuits()
        {
            var result = new List<(string, string, DateTime?)>();
            foreach (var kvp in _circuits)
            {
                if (kvp.Value.State == CircuitState.Open)
                {
                    result.Add((kvp.Key, kvp.Value.LastError, kvp.Value.OpenedAt));
                }
            }
            return result;
        }

        /// <summary>
        /// Get statistics for all circuits
        /// </summary>
        public Dictionary<string, object> GetStats()
        {
            int closed = 0, open = 0, halfOpen = 0;
            var toolStats = new Dictionary<string, object>();

            foreach (var kvp in _circuits)
            {
                var circuit = kvp.Value;
                switch (circuit.State)
                {
                    case CircuitState.Closed: closed++; break;
                    case CircuitState.Open: open++; break;
                    case CircuitState.HalfOpen: halfOpen++; break;
                }

                if (circuit.State != CircuitState.Closed || circuit.FailureCount > 0)
                {
                    toolStats[kvp.Key] = new
                    {
                        state = circuit.State.ToString(),
                        failures = circuit.FailureCount,
                        successes = circuit.SuccessCount,
                        lastError = circuit.LastError
                    };
                }
            }

            return new Dictionary<string, object>
            {
                ["total_circuits"] = _circuits.Count,
                ["closed"] = closed,
                ["open"] = open,
                ["half_open"] = halfOpen,
                ["problematic_tools"] = toolStats
            };
        }

        private ToolCircuitBreaker GetOrCreateCircuit(string toolName)
        {
            return _circuits.GetOrAdd(toolName, name =>
                new ToolCircuitBreaker(name, DefaultFailureThreshold, DefaultOpenDurationSeconds, DefaultSuccessThreshold));
        }
    }
}
