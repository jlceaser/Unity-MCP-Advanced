# Unity MCP Vibe

**Native C# MCP Server for Unity Editor** - Zero Python, Maximum Performance

## Overview

Unity MCP Vibe is a high-performance Model Context Protocol (MCP) server that runs entirely in C# within the Unity Editor. It enables AI assistants like Claude to directly interact with your Unity project through a standardized protocol.

### Key Features

- **Native C# Server** - No Python dependency, runs directly in Unity
- **Auto-Restart** - Survives domain reloads and recompilation
- **Focus-Free Operation** - Works even when Unity is not focused
- **84+ MCP Tools** - Comprehensive Unity API coverage
- **Super Tools** - Execute arbitrary C# code, manage packages

## Architecture

```
Assets/Scripts/Editor/MCP/
├── Core/
│   ├── MCPNativeServer.cs       # Main server orchestrator
│   ├── MCPSystemWindow.cs       # Control Panel UI
│   ├── MCPAutoCompileSystem.cs  # Focus-free compilation
│   ├── MCPVibeSystem.cs         # Project vibe tools
│   └── MCPReferenceAssigner.cs  # Reference assignment tool
├── Helpers/
│   └── MCPDependencyChecker.cs  # Dependency utilities
├── NativeServer/
│   ├── Core/
│   │   ├── MCPToolRegistry.cs   # Tool registration
│   │   └── MCPResourceRegistry.cs # Resource registration
│   ├── Transport/
│   │   └── MCPHttpServer.cs     # HTTP + SSE transport
│   └── Protocol/
│       └── MCPTypes.cs          # JSON-RPC types
├── Systems/
│   ├── MCPTesterSystem.cs       # Testing tools
│   └── MCPGenericValidator.cs   # Project validation
└── Tools/
    ├── MCPSuperTool.cs          # execute_csharp (SUPER)
    ├── MCPUnityPackageTool.cs   # unity_package
    ├── MCPCoreTools.cs          # time, camera, render, scene
    ├── MCPAdvancedTools.cs      # Auto dialog handler
    ├── MCPMasterTools.cs        # Timeline control
    ├── MCPUltimateTools.cs      # Audio mixer
    ├── MCPCompleteTools.cs      # NavMesh control
    ├── MCPEditorTools.cs        # Editor control
    ├── MCPEssentialTools.cs     # Screenshot
    ├── MCPProjectTools.cs       # Project settings
    ├── MCPUtilityTools.cs       # Object selection
    ├── MCPWorkflowTools.cs      # Transform tools
    └── MCPUnityReset.cs         # Emergency recovery
```

## Super Tools

### execute_csharp
Execute arbitrary C# code directly in Unity Editor context.

```json
{
  "action": "execute",
  "code": "Time.timeScale = 0.5f;"
}
```

Actions: `execute`, `evaluate`, `batch`, `query`, `set_property`, `call_method`, `create`, `destroy`

### unity_package
Manage .unitypackage files safely.

```json
{
  "action": "analyze",
  "path": "C:/Downloads/MyPackage.unitypackage"
}
```

Actions: `analyze`, `list_contents`, `import`, `import_selective`, `detect_conflicts`, `organize`, `fix_references`, `find_packages`, `export`

## Available Tools

| Tool | Description |
|------|-------------|
| `execute_csharp` | Execute C# code in Unity context |
| `unity_package` | Manage .unitypackage files |
| `project_validator` | Scan project for issues |
| `time_control` | Control Time.timeScale, fixedDeltaTime |
| `render_settings` | Modify RenderSettings |
| `camera_control` | Control cameras |
| `multi_scene` | Multi-scene management |
| `script_runner` | Run MonoBehaviour methods |
| `timeline_control` | Timeline/Playables control |
| `audio_mixer` | AudioMixer control |
| `navmesh_control` | NavMesh baking and control |
| `editor_control` | Force refresh, recompile |
| `screenshot` | Capture screenshots |
| `project_settings` | Manage project settings |
| `select_objects` | Select objects in hierarchy |
| `transform_tools` | Align, distribute, snap |
| `unity_reset` | Emergency recovery |
| `assign_reference` | Assign component references |
| `get_project_vibe` | Get project overview |
| `quick_vibe` | Quick project status |

## Installation

1. Copy the `MCP` folder to `Assets/Scripts/Editor/`
2. Open Unity - server starts automatically
3. Connect your MCP client to `http://localhost:8080`

## Configuration

Open **System > MCP Control Panel** in Unity menu.

| Setting | Default | Description |
|---------|---------|-------------|
| Auto-Start | On | Start server on Unity launch |
| Auto-Restart | On | Restart after domain reload |
| HTTP Port | 8080 | Server port |

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Health check |
| `/health` | GET | Server status |
| `/mcp` | POST | JSON-RPC requests |
| `/mcp` | GET | Server info |
| `/sse` | GET | SSE connection |

## Protocol

Uses **MCP 2024-11-05** with **JSON-RPC 2.0**.

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "execute_csharp",
    "arguments": {
      "action": "execute",
      "code": "Debug.Log(\"Hello MCP!\");"
    }
  }
}
```

## Requirements

- Unity 2021.3+ (tested on Unity 6)
- .NET Standard 2.1
- Newtonsoft.Json (included in Unity)

## Version

- **Server**: Unity-MCP-Vibe-Native-JET
- **Version**: 3.0.0-JET
- **Protocol**: MCP 2024-11-05

## License

MIT License

---

Built with Claude Code
