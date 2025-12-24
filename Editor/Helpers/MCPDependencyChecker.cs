#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// MCPDependencyChecker - Detects and reports on required MCP dependencies
    /// Checks: Python, UV, Pip, MCP Server status
    /// </summary>
    public static class MCPDependencyChecker
    {
        public struct DependencyStatus
        {
            public string Name;
            public bool IsInstalled;
            public string Version;
            public string Path;
            public string Error;
            public string InstallUrl;
            public string InstallCommand;
        }

        private const int COMMAND_TIMEOUT_MS = 5000;
        private const int MCP_DEFAULT_PORT = 8080;

        #region Individual Checks

        public static DependencyStatus CheckPython()
        {
            var status = new DependencyStatus
            {
                Name = "Python",
                InstallUrl = "https://www.python.org/downloads/",
                InstallCommand = "winget install Python.Python.3.11"
            };

            // Try python first, then python3
            string[] commands = { "python", "python3" };

            foreach (var cmd in commands)
            {
                if (TryRunCommand(cmd, "--version", out string output, out string error))
                {
                    status.IsInstalled = true;
                    status.Version = ParseVersion(output, "Python");
                    status.Path = GetCommandPath(cmd);
                    return status;
                }
            }

            status.IsInstalled = false;
            status.Error = "Python not found in PATH";
            return status;
        }

        public static DependencyStatus CheckUV()
        {
            var status = new DependencyStatus
            {
                Name = "UV",
                InstallUrl = "https://docs.astral.sh/uv/getting-started/installation/",
                InstallCommand = "powershell -c \"irm https://astral.sh/uv/install.ps1 | iex\""
            };

            // Check common UV locations
            string[] uvPaths = GetPossibleUVPaths();

            foreach (var uvPath in uvPaths)
            {
                if (File.Exists(uvPath))
                {
                    if (TryRunCommand(uvPath, "--version", out string output, out _))
                    {
                        status.IsInstalled = true;
                        status.Version = ParseVersion(output, "uv");
                        status.Path = uvPath;
                        return status;
                    }
                }
            }

            // Try from PATH
            if (TryRunCommand("uv", "--version", out string pathOutput, out _))
            {
                status.IsInstalled = true;
                status.Version = ParseVersion(pathOutput, "uv");
                status.Path = GetCommandPath("uv");
                return status;
            }

            status.IsInstalled = false;
            status.Error = "UV not found. Install from: " + status.InstallUrl;
            return status;
        }

        public static DependencyStatus CheckPip()
        {
            var status = new DependencyStatus
            {
                Name = "Pip",
                InstallUrl = "https://pip.pypa.io/en/stable/installation/",
                InstallCommand = "python -m ensurepip --upgrade"
            };

            if (TryRunCommand("pip", "--version", out string output, out _))
            {
                status.IsInstalled = true;
                status.Version = ParseVersion(output, "pip");
                status.Path = GetCommandPath("pip");
                return status;
            }

            // Try pip3
            if (TryRunCommand("pip3", "--version", out output, out _))
            {
                status.IsInstalled = true;
                status.Version = ParseVersion(output, "pip");
                status.Path = GetCommandPath("pip3");
                return status;
            }

            status.IsInstalled = false;
            status.Error = "Pip not found";
            return status;
        }

        public static DependencyStatus CheckMCPServer(int port = MCP_DEFAULT_PORT)
        {
            var status = new DependencyStatus
            {
                Name = "MCP Server",
                InstallUrl = "https://github.com/anthropics/unity-mcp",
                InstallCommand = $"uvx mcp-for-unity --transport http --http-url http://localhost:{port}"
            };

            // Check if server is running on port
            bool isRunning = IsPortInUse(port);

            if (isRunning)
            {
                status.IsInstalled = true;
                status.Version = "Running";
                status.Path = $"http://localhost:{port}";
                return status;
            }

            status.IsInstalled = false;
            status.Error = $"Server not running on port {port}";
            return status;
        }

        #endregion

        #region Combined Check

        public static Dictionary<string, DependencyStatus> CheckAll()
        {
            return new Dictionary<string, DependencyStatus>
            {
                ["Python"] = CheckPython(),
                ["UV"] = CheckUV(),
                ["Pip"] = CheckPip(),
                ["MCP Server"] = CheckMCPServer()
            };
        }

        #endregion

        #region Troubleshooting

        public static List<string> GetTroubleshootingTips(Dictionary<string, DependencyStatus> status)
        {
            var tips = new List<string>();

            // UV installed but Unity doesn't see it
            if (status.TryGetValue("UV", out var uvStatus))
            {
                if (!uvStatus.IsInstalled)
                {
                    tips.Add("UV kurulu degil. Kurmak icin: " + uvStatus.InstallCommand);
                }
                else if (string.IsNullOrEmpty(uvStatus.Path) || !uvStatus.Path.Contains("\\"))
                {
                    tips.Add("UV kurulu ama PATH'te degil. Unity'yi kapatip acmayi deneyin.");
                }
            }

            // Python not found
            if (status.TryGetValue("Python", out var pyStatus) && !pyStatus.IsInstalled)
            {
                tips.Add("Python kurulu degil. Python 3.10+ kurun: " + pyStatus.InstallUrl);
            }

            // Server not running
            if (status.TryGetValue("MCP Server", out var serverStatus) && !serverStatus.IsInstalled)
            {
                tips.Add("MCP Server calismiyorsa: 'Start Server' butonuna basin.");
                tips.Add("Port 8080 baska bir uygulama tarafindan kullaniliyor olabilir.");
            }

            // General tips
            if (tips.Count == 0)
            {
                tips.Add("Tum bagimliliklar kurulu! Server baslatilabilir.");
            }
            else
            {
                tips.Add("---");
                tips.Add("Sorun devam ederse Unity'yi kapatip acin.");
                tips.Add("PATH degiskenlerinin dogru ayarlandigindan emin olun.");
            }

            return tips;
        }

        public static string GetOverallStatus(Dictionary<string, DependencyStatus> status)
        {
            int installed = 0;
            int total = 0;

            foreach (var kvp in status)
            {
                if (kvp.Key != "MCP Server") // Server is optional
                {
                    total++;
                    if (kvp.Value.IsInstalled) installed++;
                }
            }

            if (installed == total)
            {
                if (status.TryGetValue("MCP Server", out var server) && server.IsInstalled)
                    return "ALL_READY";
                return "READY_NO_SERVER";
            }

            if (installed == 0)
                return "NOT_READY";

            return "PARTIAL";
        }

        #endregion

        #region Helper Methods

        private static bool TryRunCommand(string command, string args, out string stdout, out string stderr)
        {
            stdout = null;
            stderr = null;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Application.dataPath
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null) return false;

                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();

                    process.WaitForExit(COMMAND_TIMEOUT_MS);
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return false;
            }
        }

        private static string GetCommandPath(string command)
        {
            try
            {
                string whereCmd = Application.platform == RuntimePlatform.WindowsEditor ? "where" : "which";
                if (TryRunCommand(whereCmd, command, out string output, out _))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return lines.Length > 0 ? lines[0].Trim() : null;
                }
            }
            catch { }
            return null;
        }

        private static string[] GetPossibleUVPaths()
        {
            var paths = new List<string>();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                paths.Add(Path.Combine(userProfile, ".local", "bin", "uv.exe"));
                paths.Add(Path.Combine(localAppData, "Programs", "uv", "uv.exe"));
                paths.Add(Path.Combine(userProfile, ".cargo", "bin", "uv.exe"));
            }
            else
            {
                paths.Add(Path.Combine(userProfile, ".local", "bin", "uv"));
                paths.Add("/usr/local/bin/uv");
                paths.Add("/opt/homebrew/bin/uv");
                paths.Add(Path.Combine(userProfile, ".cargo", "bin", "uv"));
            }

            return paths.ToArray();
        }

        private static string ParseVersion(string output, string prefix)
        {
            if (string.IsNullOrEmpty(output)) return "Unknown";

            // Try to extract version number
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                string line = lines[0].Trim();

                // Remove prefix if present
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    line = line.Substring(prefix.Length).Trim();
                }

                // Extract version number (first word that looks like a version)
                var parts = line.Split(' ');
                foreach (var part in parts)
                {
                    if (part.Length > 0 && (char.IsDigit(part[0]) || part[0] == 'v'))
                    {
                        return part.TrimStart('v');
                    }
                }

                return line.Length > 20 ? line.Substring(0, 20) + "..." : line;
            }

            return "Unknown";
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    if (TryRunCommand("cmd.exe", $"/c netstat -ano | findstr :{port}", out string output, out _))
                    {
                        return !string.IsNullOrEmpty(output) && output.Contains("LISTENING");
                    }
                }
                else
                {
                    if (TryRunCommand("lsof", $"-i :{port}", out string output, out _))
                    {
                        return !string.IsNullOrEmpty(output);
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion
    }
}
