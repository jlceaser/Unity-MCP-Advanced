<img width="1024" height="559" alt="image" src="https://github.com/user-attachments/assets/bb47608f-f38d-443d-84c7-e496dbdb85bc" />

#  Unity MCP Vibe
### The World's First Native C# Model Context Protocol Server

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-000000?style=for-the-badge&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Mac%20%7C%20Linux-gray?style=for-the-badge)](https://docs.unity3d.com/Manual/SupportedPlatforms.html)
[![MCP Ready](https://img.shields.io/badge/MCP-1.0%20Ready-blue?style=for-the-badge&logo=anthropic)](https://modelcontextprotocol.io)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)](LICENSE)

<br>

> **Give your AI Agent eyes, hands, and zero-latency reflexes inside the Unity Editor.**

**Unity MCP Vibe** is not just a bridge; it's a native organ. Unlike Python-based wrappers that introduce lag and complexity, Vibe runs entirely inside Unity's memory space via native C#, enabling real-time control, visual analysis, and hot-reloading capabilities that were previously impossible.

[🚀 Quick Start](#-quick-start) • [📚 Documentation](docs/README.md) • [🔧 Toolset](#%EF%B8%8F-toolset-overview) • [🤝 Contribute](#-contributing)

</div>

---

## 🌟 Why Vibe? The Native Advantage

Most MCP servers for Unity rely on a slow Python middleman. **Unity MCP Vibe** eliminates the middleman.

| Feature | 🐍 Python Bridges | ⚡ Unity MCP Vibe |
| :--- | :--- | :--- |
| **Architecture** | External Process (Socket/HTTP) | **Native C# Assembly** |
| **Latency** | High (Process Switching) | **Zero (In-Memory)** |
| **Vision** | ❌ Impossible / Limited | **✅ Native GameView Capture** |
| **Compilation** | ❌ Triggers Domain Reload | **✅ Dynamic Hot-Execution** |
| **Setup** | Requires Python/Pip/Venv | **✅ Plug & Play (UPM)** |

---

## 🧠 State-of-the-Art Modules

We have re-engineered the core tools to support modern Unity workflows (DOTS, UI Toolkit, Input System).

### 👁️ **The Eye (Vision Module)**
**Multimodal AI starts here.** Vibe allows your Agent (Claude, Cursor, etc.) to *see* your Scene and Game View.
* **Context-Aware UI Analysis:** Detects buttons, labels, and layout issues in UI Toolkit.
* **Visual Debugging:** AI captures screenshots to diagnose shader or lighting errors instantly.

### ⚡ **Speed Demon (Dynamic Coding)**
**No more waiting for progress bars.**
* Execute C# logic instantly without triggering a full Domain Reload.
* Perfect for tweaking values, spawning test objects, or mass-renaming assets on the fly.

### 🛡️ **The Guardian (Security)**
**Enterprise-grade safety for autonomous agents.**
* **Strict Mode:** Requires human approval for every write operation.
* **Standard Mode:** Auto-approves reads, confirms writes/deletes.
* **God Mode:** Unrestricted access for fully autonomous loops.

---

## 📦 Installation

### Option 1: Unity Package Manager (Recommended)
1. Open Unity.
2. Go to **Window > Package Manager**.
3. Click **+ > Add package from git URL...**
4. Paste:
https://github.com/jlceaser/Unity-MCP-Vibe.git
### Option 2: `manifest.json` (For Power Users)
Add this line to your `Packages/manifest.json`:
```json
"com.jlceaser.unity-mcp-vibe": "[https://github.com/jlceaser/Unity-MCP-Vibe.git](https://github.com/jlceaser/Unity-MCP-Vibe.git)"
🚀 Quick Start Guide1. Initialize the ServerGo to System > MCP Control Panel (or press Ctrl+Shift+M).Click Start Server.Status will change to: Listening on http://localhost:8080/sse2. Connect Your AI ClientConfigure your MCP client (Claude Desktop, Cursor, etc.) with the following config:For Claude Desktop (claude_desktop_config.json):JSON{
  "mcpServers": {
    "unity-vibe": {
      "command": "cmd.exe", // or "sh" on Mac/Linux
      "args": [
        "/c",
        "curl -N http://localhost:8080/sse" 
      ]
    }
  }
}
(Note: Direct SSE support varies by client. Detailed connection guides are in the Wiki.)🛠️ Toolset OverviewUnity MCP Vibe comes with 110+ Production-Ready Tools.<details><summary><strong>🎮 Core & Hierarchy</strong></summary>ToolDescriptionmanage_gameobjectCreate, find, reparent, and destroy objects.inspect_componentRead and write public/private fields via Reflection.query_sceneSemantic search for objects (e.g., "Find all enemies near 0,0,0").</details><details><summary><strong>💻 Code & Compilation</strong></summary>ToolDescriptiondynamic_compile(Exclusive) Compile & run C# snippets in memory.script_architectGenerate robust MonoBehaviours with proper namespaces.refactor_assistantAnalyze script dependencies and suggest cleanups.</details><details><summary><strong>🎨 Graphics & Vision</strong></summary>ToolDescriptionvision_captureCapture Game/Scene view as Base64 for AI analysis.material_labCreate and modify URP/HDRP materials programmatically.light_studioAdjust lighting settings and bake handling.</details><details><summary><strong>🔧 Systems & DevOps</strong></summary>ToolDescriptiongit_controlCommit, push, and branch without leaving Unity.package_wizardInstall/Remove UPM packages via registry or git.log_analyzerRead Console logs and auto-suggest fixes for stack traces.</details>⚙️ ConfigurationLocated in ProjectSettings/MCPVibeSettings.asset or via the Control Panel.Port: Default 8080.Auto-Start: Start server automatically when Unity opens.Security Level:🟢 Standard (Recommended)🔴 Strict⚡ Unrestricted🤝 ContributingUnity MCP Vibe is an open ecosystem. We welcome Pull Requests!Fork the repo.Create your feature branch (git checkout -b feature/amazing-feature).Commit your changes.Push to the branch.Open a Pull Request.📜 Credits & LicenseUnity MCP Vibe is a Cedral Interactive initiative, developed by jlceaser.It stands on the shoulders of giants, integrating concepts from:CoplayDev/unity-mcp (Original Concept)RICoder72/vibe-unity (Vision Tools)Licensed under the MIT License. Use it commercially, modify it, build the future.<div align="center"><sub>Built with ❤️ for the Unity Community</sub></div>
