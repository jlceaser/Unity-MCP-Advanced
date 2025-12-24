# ⚡ Unity MCP Vibe

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=for-the-badge&logo=unity)](https://unity.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Mac%20%7C%20Linux-gray?style=for-the-badge)](https://docs.unity3d.com/Manual/SupportedPlatforms.html)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)](LICENSE)
[![MCP](https://img.shields.io/badge/Protocol-MCP-blue?style=for-the-badge)](https://modelcontextprotocol.io)

> **The World's First Native C# Model Context Protocol Server for Unity.**  
> Give your AI Agent eyes, hands, and zero-latency reflexes inside the Unity Editor.

---

## 🌟 Why Unity MCP Vibe?

Unlike other Python-based bridges, **Unity MCP Vibe** runs entirely inside the Unity Editor via native C#. No external processes, no lag, direct memory access.

### 🧠 State-of-the-Art Capabilities

| Module | Codename | Description |
| :--- | :--- | :--- |
| **�️ Vision** | *The Eye* | **Multimodal AI Vision.** The first MCP that allows AI to *see* your Scene and Game identifiers via Base64 screenshots and UI analysis. |
| **⚡ Speed** | *Speed Demon* | **Zero-Latency Coding.** Execute, compile, and run C# code in-memory without triggering the slow "Domain Reload". |
| **🛡️ Security** | *The Guardian* | **Enterprise-Grade Safety.** Configurable permission levels (`Strict`, `Standard`, `Unrestricted`) with Unity Editor approval dialogs. |
| **🚀 Modern** | *Future Proof* | **Next-Gen Support.** Native tools for **UI Toolkit (UXML/USS)**, **Input System**, and **Package Manager**. |

---

## � Installation

### Option 1: Install via UPM (Recommended)

1. Open Unity.
2. Go to **Window > Package Manager**.
3. Click the **+** button and select **Add package from git URL...**
4. Paste the following URL:
   ```
   https://github.com/jlceaser/Unity-MCP-Vibe.git
   ```
   *(Note: Ensure you have Git installed and accessible in your PATH)*

### Option 2: Local Installation

1. Download or Clone this repository.
2. In Unity Package Manager, select **Add package from disk...**
3. Select the `package.json` file inside the `Packages/UnityMCP-Vibe` folder.

---

## 🚀 Quick Start

1. **Open the Control Panel**:  
   Menu: `System > MCP Control Panel`  
   *(Shortcut: Ctrl+Shift+M)*

2. **Start the Server**:  
   Click the green **START SERVER** button.

3. **Connect your AI**:  
   Configure your MCP Client (Claude Desktop, Cursor, etc.) to connect to the SSE endpoint:
   ```
   http://localhost:8080/mcp
   ```

---

## 🛠️ Toolset Overview

Includes **110+ Tools** covering every aspect of Unity development.

### 🎮 **Core & Scene**
- `manage_gameobject` - Create, modify, parent, find.
- `manage_scene` - Load, save, creating scenes.
- `vision_capture` - Take screenshots, analyze UI layout.

### 💻 **Code & Logic**
- `dynamic_compile` - **HOT!** Run C# instantly (No Reload).
- `manage_script` - Create, edit, and validate scripts.
- `security_control` - Manage AI permissions.

### 🎨 **Art & visual**
- `vision_capture` - Analyze colors, lighting, and composition.
- `manage_material` - Create/edit materials and shaders.
- `timeline_control` - Directors, tracks, and clips.

### 🔧 **Systems**
- `modern_unity` - Generate UI Toolkit UXML/USS.
- `upm_control` - Install/remove Unity packages.
- `git_control` - Commit, branch, and status check.

---

## ⚙️ Configuration & Security

### Security Levels
Protect your project from accidental damage.

- **🟢 Standard (Default)**: Dangerous operations (Delete/Overwrite) require confirmation dialog.
- **🔴 Strict**: ALL operations require confirmation.
- **⚡ Unrestricted**: No popups. For autonomous agents (Use with caution!)

Change this in `System > MCP Control Panel` or via the `security_control` tool.

### Networking
- **Port**: Default `8080`. Changeable via EditorPrefs (`MCP_Port`).
- **Auto-Restart**: Server automatically recovers if Unity crashes or reloads.

---

## 🤝 Contributing

We welcome contributions! Please fork the repository and submit a Pull Request.

## 📜 Credits

Built by **jlceaser** with components from:
- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (Core Architecture)
- [RICoder72/vibe-unity](https://github.com/RICoder72/vibe-unity) (Vibe Tools)

## 📄 License

**MIT License** - Free for commercial and personal use.