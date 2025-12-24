#nullable disable
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.NativeServer.Protocol;

namespace MCPForUnity.Editor.NativeServer.Transport
{
    /// <summary>
    /// High-performance HTTP server for MCP protocol - JET EDITION
    /// Supports both HTTP POST and SSE (Server-Sent Events) transports.
    /// Runs entirely in C# - no Python dependency - MAXIMUM PERFORMANCE!
    ///
    /// Performance Features:
    /// - Async/await throughout for non-blocking I/O
    /// - Parallel request handling via Task.Run
    /// - ConcurrentDictionary for thread-safe session management
    /// - Keep-alive connections for reduced latency
    /// - Minimal allocations in hot paths
    /// </summary>
    public class MCPHttpServer : IDisposable
    {
        public const string VERSION = "3.0.0-JET";
        public const string SERVER_NAME = "Unity-MCP-Vibe-Native-JET";

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenerTask;
        private readonly int _port;
        private readonly string _host;
        private bool _isRunning;
        private DateTime _startTime;
        private TimeSpan _uptime => DateTime.UtcNow - _startTime;

        // SSE sessions
        private readonly ConcurrentDictionary<string, SSESession> _sseSessions;

        // Request handler
        public Func<JsonRpcRequest, Task<JsonRpcResponse>> RequestHandler { get; set; }

        // Events
        public event Action OnServerStarted;
        public event Action OnServerStopped;
        public event Action<string> OnClientConnected;
        public event Action<string> OnClientDisconnected;
        public event Action<string, Exception> OnError;

        // Stats
        public int TotalRequests { get; private set; }
        public int ActiveConnections => _sseSessions.Count;
        public bool IsRunning => _isRunning;
        public string Url => $"http://{_host}:{_port}";

        public MCPHttpServer(int port = 8080, string host = "localhost")
        {
            _port = port;
            _host = host;
            _sseSessions = new ConcurrentDictionary<string, SSESession>();
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://{_host}:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");

                // Try to add wildcard for external access
                try { _listener.Prefixes.Add($"http://+:{_port}/"); } catch { }

                _listener.Start();
                _isRunning = true;
                _startTime = DateTime.UtcNow;

                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenAsync(_cts.Token));

                PrintBanner();
                OnServerStarted?.Invoke();

                Debug.Log($"[MCP Native] Server started on {Url}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Native] Failed to start server: {ex.Message}");
                OnError?.Invoke("start", ex);
                throw;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;
                _cts?.Cancel();

                // Close all SSE sessions
                foreach (var session in _sseSessions.Values)
                {
                    session.Close();
                }
                _sseSessions.Clear();

                _listener?.Stop();
                _listener?.Close();

                OnServerStopped?.Invoke();
                Debug.Log("[MCP Native] Server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Native] Error stopping server: {ex.Message}");
            }
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogWarning($"[MCP Native] Listener error: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // CORS headers
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept");

                // Performance headers
                response.KeepAlive = true;
                response.AddHeader("X-Server", SERVER_NAME);
                response.AddHeader("X-Version", VERSION);

                // Handle preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                string path = request.Url.AbsolutePath.ToLower();

                // Route requests
                switch (path)
                {
                    case "/":
                    case "/health":
                        await HandleHealthCheck(response);
                        break;

                    case "/mcp":
                    case "/mcp/":
                        if (request.HttpMethod == "POST")
                        {
                            await HandleMCPPost(request, response);
                        }
                        else
                        {
                            await HandleMCPInfo(response);
                        }
                        break;

                    case "/sse":
                    case "/mcp/sse":
                        await HandleSSEConnection(request, response);
                        break;

                    case "/message":
                    case "/mcp/message":
                        if (request.HttpMethod == "POST")
                        {
                            await HandleMCPPost(request, response);
                        }
                        break;

                    default:
                        response.StatusCode = 404;
                        await WriteJsonResponse(response, new { error = "Not found", path = path });
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Native] Request error: {ex.Message}");
                OnError?.Invoke(request.Url.AbsolutePath, ex);

                try
                {
                    response.StatusCode = 500;
                    await WriteJsonResponse(response, new { error = ex.Message });
                }
                catch { }
            }
        }

        private async Task HandleHealthCheck(HttpListenerResponse response)
        {
            var health = new
            {
                status = "healthy",
                server = SERVER_NAME,
                version = VERSION,
                uptime = _uptime.TotalSeconds,
                connections = ActiveConnections,
                requests = TotalRequests
            };

            await WriteJsonResponse(response, health);
        }

        private async Task HandleMCPInfo(HttpListenerResponse response)
        {
            var info = new
            {
                name = SERVER_NAME,
                version = VERSION,
                protocol = "MCP 2024-11-05",
                transport = "HTTP + SSE",
                endpoints = new
                {
                    mcp = "/mcp (POST)",
                    sse = "/sse (GET)",
                    health = "/health (GET)"
                }
            };

            await WriteJsonResponse(response, info);
        }

        private async Task HandleMCPPost(HttpListenerRequest request, HttpListenerResponse response)
        {
            TotalRequests++;

            // Read request body
            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(body))
            {
                response.StatusCode = 400;
                await WriteJsonResponse(response, JsonRpcResponse.Failure(null, JsonRpcError.InvalidRequest, "Empty request body"));
                return;
            }

            // Parse JSON-RPC request
            JsonRpcRequest rpcRequest;
            try
            {
                rpcRequest = JsonConvert.DeserializeObject<JsonRpcRequest>(body);
            }
            catch (Exception ex)
            {
                response.StatusCode = 400;
                await WriteJsonResponse(response, JsonRpcResponse.Failure(null, JsonRpcError.ParseError, ex.Message));
                return;
            }

            // Process request
            JsonRpcResponse rpcResponse;
            if (RequestHandler != null)
            {
                rpcResponse = await RequestHandler(rpcRequest);
            }
            else
            {
                rpcResponse = JsonRpcResponse.Failure(rpcRequest.Id, JsonRpcError.InternalError, "No request handler configured");
            }

            await WriteJsonResponse(response, rpcResponse);
        }

        private async Task HandleSSEConnection(HttpListenerRequest request, HttpListenerResponse response)
        {
            string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // SSE headers
            response.ContentType = "text/event-stream";
            response.AddHeader("Cache-Control", "no-cache");
            response.AddHeader("Connection", "keep-alive");
            response.AddHeader("X-Session-Id", sessionId);

            var session = new SSESession(sessionId, response);
            _sseSessions[sessionId] = session;

            OnClientConnected?.Invoke(sessionId);
            Debug.Log($"[MCP Native] SSE client connected: {sessionId}");

            try
            {
                // Send initial connection event
                await session.SendEvent("connected", new { sessionId, server = SERVER_NAME });

                // Keep connection alive
                while (!session.IsClosed && _isRunning)
                {
                    await session.SendEvent("ping", new { timestamp = DateTime.UtcNow.ToString("o") });
                    await Task.Delay(30000); // Ping every 30 seconds
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Native] SSE session {sessionId} error: {ex.Message}");
            }
            finally
            {
                _sseSessions.TryRemove(sessionId, out _);
                OnClientDisconnected?.Invoke(sessionId);
                Debug.Log($"[MCP Native] SSE client disconnected: {sessionId}");
            }
        }

        public async Task BroadcastEvent(string eventType, object data)
        {
            foreach (var session in _sseSessions.Values)
            {
                try
                {
                    await session.SendEvent(eventType, data);
                }
                catch { }
            }
        }

        private async Task WriteJsonResponse(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            string json = JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private void PrintBanner()
        {
            string banner = $@"
<color=cyan>╔═══════════════════════════════════════════════════════════════════════╗
║  MCP Unity Vibe JET  │  v{VERSION}  │  {Url}  │  READY  ║
╚═══════════════════════════════════════════════════════════════════════╝</color>";
            Debug.Log(banner);
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }

    /// <summary>
    /// SSE (Server-Sent Events) session
    /// </summary>
    public class SSESession
    {
        public string Id { get; }
        public bool IsClosed { get; private set; }

        private readonly HttpListenerResponse _response;
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();

        public SSESession(string id, HttpListenerResponse response)
        {
            Id = id;
            _response = response;
            _writer = new StreamWriter(response.OutputStream, Encoding.UTF8) { AutoFlush = true };
        }

        public Task SendEvent(string eventType, object data)
        {
            if (IsClosed) return Task.CompletedTask;

            try
            {
                string json = JsonConvert.SerializeObject(data);
                string message = $"event: {eventType}\ndata: {json}\n\n";

                lock (_lock)
                {
                    _writer.Write(message);
                }
            }
            catch
            {
                IsClosed = true;
            }
            
            return Task.CompletedTask;
        }

        public void Close()
        {
            IsClosed = true;
            try
            {
                _writer?.Close();
                _response?.Close();
            }
            catch { }
        }
    }
}
