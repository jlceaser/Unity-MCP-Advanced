#nullable disable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.NativeServer.Protocol
{
    #region JSON-RPC 2.0 Base Types

    /// <summary>
    /// JSON-RPC 2.0 Request
    /// </summary>
    [Serializable]
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JToken Params { get; set; }

        public bool IsNotification => Id == null;
    }

    /// <summary>
    /// JSON-RPC 2.0 Response
    /// </summary>
    [Serializable]
    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError Error { get; set; }

        public static JsonRpcResponse Success(object id, object result)
        {
            return new JsonRpcResponse { Id = id, Result = result };
        }

        public static JsonRpcResponse Failure(object id, int code, string message, object data = null)
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = new JsonRpcError { Code = code, Message = message, Data = data }
            };
        }
    }

    /// <summary>
    /// JSON-RPC 2.0 Error
    /// </summary>
    [Serializable]
    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        // Standard JSON-RPC error codes
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
    }

    #endregion

    #region MCP Protocol Types

    /// <summary>
    /// MCP Server Capabilities
    /// </summary>
    [Serializable]
    public class MCPCapabilities
    {
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public ToolsCapability Tools { get; set; }

        [JsonProperty("resources", NullValueHandling = NullValueHandling.Ignore)]
        public ResourcesCapability Resources { get; set; }

        [JsonProperty("prompts", NullValueHandling = NullValueHandling.Ignore)]
        public PromptsCapability Prompts { get; set; }

        [JsonProperty("logging", NullValueHandling = NullValueHandling.Ignore)]
        public LoggingCapability Logging { get; set; }
    }

    [Serializable]
    public class ToolsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = true;
    }

    [Serializable]
    public class ResourcesCapability
    {
        [JsonProperty("subscribe")]
        public bool Subscribe { get; set; } = false;

        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = true;
    }

    [Serializable]
    public class PromptsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = false;
    }

    [Serializable]
    public class LoggingCapability { }

    /// <summary>
    /// MCP Server Info
    /// </summary>
    [Serializable]
    public class MCPServerInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

    /// <summary>
    /// MCP Initialize Result
    /// </summary>
    [Serializable]
    public class MCPInitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonProperty("capabilities")]
        public MCPCapabilities Capabilities { get; set; }

        [JsonProperty("serverInfo")]
        public MCPServerInfo ServerInfo { get; set; }

        [JsonProperty("instructions", NullValueHandling = NullValueHandling.Ignore)]
        public string Instructions { get; set; }
    }

    /// <summary>
    /// MCP Tool Definition
    /// </summary>
    [Serializable]
    public class MCPTool
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; set; }
    }

    /// <summary>
    /// MCP Tool Call Request
    /// </summary>
    [Serializable]
    public class MCPToolCallParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }
    }

    /// <summary>
    /// MCP Tool Call Result
    /// </summary>
    [Serializable]
    public class MCPToolResult
    {
        [JsonProperty("content")]
        public List<MCPContent> Content { get; set; } = new List<MCPContent>();

        [JsonProperty("isError")]
        public bool IsError { get; set; } = false;
    }

    /// <summary>
    /// MCP Content (Text, Image, etc.)
    /// </summary>
    [Serializable]
    public class MCPContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }

        public static MCPContent TextContent(string text)
        {
            return new MCPContent { Type = "text", Text = text };
        }

        public static MCPContent ImageContent(string base64Data, string mimeType = "image/png")
        {
            return new MCPContent { Type = "image", Data = base64Data, MimeType = mimeType };
        }

        public static MCPContent JsonContent(object data)
        {
            return new MCPContent { Type = "text", Text = JsonConvert.SerializeObject(data, Formatting.Indented) };
        }
    }

    /// <summary>
    /// MCP Resource Definition
    /// </summary>
    [Serializable]
    public class MCPResource
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }
    }

    /// <summary>
    /// MCP Resource Content
    /// </summary>
    [Serializable]
    public class MCPResourceContent
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("blob", NullValueHandling = NullValueHandling.Ignore)]
        public string Blob { get; set; }
    }

    /// <summary>
    /// MCP Resources Read Result
    /// </summary>
    [Serializable]
    public class MCPResourcesReadResult
    {
        [JsonProperty("contents")]
        public List<MCPResourceContent> Contents { get; set; } = new List<MCPResourceContent>();
    }

    #endregion

    #region List Results

    [Serializable]
    public class MCPToolsListResult
    {
        [JsonProperty("tools")]
        public List<MCPTool> Tools { get; set; } = new List<MCPTool>();
    }

    [Serializable]
    public class MCPResourcesListResult
    {
        [JsonProperty("resources")]
        public List<MCPResource> Resources { get; set; } = new List<MCPResource>();
    }

    #endregion
}
