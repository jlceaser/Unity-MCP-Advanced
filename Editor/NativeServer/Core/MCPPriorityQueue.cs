using System;
using System.Collections.Generic;
using System.Threading;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Priority levels for main thread execution
    /// </summary>
    public enum MCPPriority
    {
        /// <summary>Immediate execution - user-initiated actions</summary>
        High = 0,

        /// <summary>Normal priority - standard tool calls</summary>
        Normal = 1,

        /// <summary>Low priority - background tasks, telemetry</summary>
        Low = 2,

        /// <summary>Idle - only process when nothing else is pending</summary>
        Idle = 3
    }

    /// <summary>
    /// Priority-based action item for main thread execution
    /// </summary>
    public class PriorityAction
    {
        public Action Action { get; set; }
        public MCPPriority Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public string ToolName { get; set; }
    }

    /// <summary>
    /// Thread-safe priority queue for main thread execution.
    /// High priority items are processed first.
    /// </summary>
    public class MCPPriorityQueue
    {
        // Separate queues for each priority level
        private readonly Queue<PriorityAction> _highQueue = new();
        private readonly Queue<PriorityAction> _normalQueue = new();
        private readonly Queue<PriorityAction> _lowQueue = new();
        private readonly Queue<PriorityAction> _idleQueue = new();

        private readonly object _lock = new();

        // Stats
        private int _totalEnqueued;
        private int _totalProcessed;
        private int _highProcessed;
        private int _normalProcessed;
        private int _lowProcessed;
        private int _idleProcessed;

        /// <summary>
        /// Total pending items across all priorities
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _highQueue.Count + _normalQueue.Count + _lowQueue.Count + _idleQueue.Count;
                }
            }
        }

        /// <summary>
        /// Enqueue an action with the specified priority
        /// </summary>
        public void Enqueue(Action action, MCPPriority priority = MCPPriority.Normal, string toolName = null)
        {
            if (action == null) return;

            var item = new PriorityAction
            {
                Action = action,
                Priority = priority,
                QueuedAt = DateTime.UtcNow,
                ToolName = toolName
            };

            lock (_lock)
            {
                switch (priority)
                {
                    case MCPPriority.High:
                        _highQueue.Enqueue(item);
                        break;
                    case MCPPriority.Normal:
                        _normalQueue.Enqueue(item);
                        break;
                    case MCPPriority.Low:
                        _lowQueue.Enqueue(item);
                        break;
                    case MCPPriority.Idle:
                        _idleQueue.Enqueue(item);
                        break;
                }
                _totalEnqueued++;
            }
        }

        /// <summary>
        /// Dequeue the next highest-priority action.
        /// Returns null if no items are available.
        /// </summary>
        public PriorityAction Dequeue()
        {
            lock (_lock)
            {
                if (_highQueue.Count > 0)
                {
                    _highProcessed++;
                    _totalProcessed++;
                    return _highQueue.Dequeue();
                }
                if (_normalQueue.Count > 0)
                {
                    _normalProcessed++;
                    _totalProcessed++;
                    return _normalQueue.Dequeue();
                }
                if (_lowQueue.Count > 0)
                {
                    _lowProcessed++;
                    _totalProcessed++;
                    return _lowQueue.Dequeue();
                }
                if (_idleQueue.Count > 0)
                {
                    _idleProcessed++;
                    _totalProcessed++;
                    return _idleQueue.Dequeue();
                }
                return null;
            }
        }

        /// <summary>
        /// Try to dequeue an item within a specified priority range.
        /// Useful for processing only high-priority items in tight loops.
        /// </summary>
        public bool TryDequeue(MCPPriority maxPriority, out PriorityAction item)
        {
            lock (_lock)
            {
                if (maxPriority >= MCPPriority.High && _highQueue.Count > 0)
                {
                    _highProcessed++;
                    _totalProcessed++;
                    item = _highQueue.Dequeue();
                    return true;
                }
                if (maxPriority >= MCPPriority.Normal && _normalQueue.Count > 0)
                {
                    _normalProcessed++;
                    _totalProcessed++;
                    item = _normalQueue.Dequeue();
                    return true;
                }
                if (maxPriority >= MCPPriority.Low && _lowQueue.Count > 0)
                {
                    _lowProcessed++;
                    _totalProcessed++;
                    item = _lowQueue.Dequeue();
                    return true;
                }
                if (maxPriority >= MCPPriority.Idle && _idleQueue.Count > 0)
                {
                    _idleProcessed++;
                    _totalProcessed++;
                    item = _idleQueue.Dequeue();
                    return true;
                }

                item = null;
                return false;
            }
        }

        /// <summary>
        /// Check if there are any high-priority items pending
        /// </summary>
        public bool HasHighPriority
        {
            get
            {
                lock (_lock)
                {
                    return _highQueue.Count > 0;
                }
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public Dictionary<string, object> GetStats()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>
                {
                    ["pending_high"] = _highQueue.Count,
                    ["pending_normal"] = _normalQueue.Count,
                    ["pending_low"] = _lowQueue.Count,
                    ["pending_idle"] = _idleQueue.Count,
                    ["total_pending"] = Count,
                    ["total_enqueued"] = _totalEnqueued,
                    ["total_processed"] = _totalProcessed,
                    ["processed_by_priority"] = new Dictionary<string, int>
                    {
                        ["high"] = _highProcessed,
                        ["normal"] = _normalProcessed,
                        ["low"] = _lowProcessed,
                        ["idle"] = _idleProcessed
                    }
                };
            }
        }

        /// <summary>
        /// Clear all queued items
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _highQueue.Clear();
                _normalQueue.Clear();
                _lowQueue.Clear();
                _idleQueue.Clear();
            }
        }
    }
}
