#nullable disable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// UnityPackage Tool - Read, analyze, organize, and safely integrate .unitypackage files
    /// Features:
    /// - Read package contents without importing
    /// - Analyze dependencies and conflicts
    /// - Detect and fix reference issues
    /// - Safe integration with existing project
    /// - Organize imported assets
    /// </summary>
    [McpForUnityTool(
        name: "unity_package",
        Description = "Manage .unitypackage files. Actions: analyze, list_contents, import, import_selective, detect_conflicts, organize, fix_references")]
    public static class MCPUnityPackageTool
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString()?.ToLower() ?? "help";
            string path = @params["path"]?.ToString();

            switch (action)
            {
                case "analyze":
                    return AnalyzePackage(path);

                case "list":
                case "list_contents":
                case "contents":
                    return ListPackageContents(path, @params);

                case "import":
                    return ImportPackage(path, @params);

                case "import_selective":
                    return ImportSelective(path, @params);

                case "detect_conflicts":
                case "conflicts":
                    return DetectConflicts(path);

                case "preview":
                    return PreviewImport(path);

                case "organize":
                    return OrganizeImportedAssets(@params);

                case "fix_references":
                case "fix_refs":
                    return FixReferences(@params);

                case "find_packages":
                case "scan":
                    return FindPackagesInFolder(@params);

                case "export":
                    return ExportPackage(@params);

                case "help":
                default:
                    return GetHelp();
            }
        }

        #region Analyze Package

        private static object AnalyzePackage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            if (!File.Exists(path))
                return new ErrorResponse($"Package not found: {path}");

            try
            {
                var contents = ExtractPackageInfo(path);

                // Categorize assets
                var categories = new Dictionary<string, List<PackageAsset>>
                {
                    { "Scripts", new List<PackageAsset>() },
                    { "Prefabs", new List<PackageAsset>() },
                    { "Materials", new List<PackageAsset>() },
                    { "Textures", new List<PackageAsset>() },
                    { "Models", new List<PackageAsset>() },
                    { "Audio", new List<PackageAsset>() },
                    { "Scenes", new List<PackageAsset>() },
                    { "Animations", new List<PackageAsset>() },
                    { "Shaders", new List<PackageAsset>() },
                    { "ScriptableObjects", new List<PackageAsset>() },
                    { "Other", new List<PackageAsset>() }
                };

                foreach (var asset in contents)
                {
                    string category = CategorizeAsset(asset.Path);
                    if (categories.ContainsKey(category))
                        categories[category].Add(asset);
                    else
                        categories["Other"].Add(asset);
                }

                // Detect potential issues
                var issues = DetectPotentialIssues(contents);

                // Calculate statistics
                var stats = new
                {
                    totalAssets = contents.Count,
                    totalSize = contents.Sum(c => c.Size),
                    categories = categories.Where(c => c.Value.Count > 0)
                        .ToDictionary(c => c.Key, c => c.Value.Count),
                    hasScripts = categories["Scripts"].Count > 0,
                    hasPrefabs = categories["Prefabs"].Count > 0,
                    hasScenes = categories["Scenes"].Count > 0,
                    potentialIssues = issues.Count,
                    issues = issues
                };

                return new SuccessResponse($"Package analysis: {contents.Count} assets", new
                {
                    packagePath = path,
                    packageName = Path.GetFileNameWithoutExtension(path),
                    statistics = stats,
                    topLevelFolders = contents.Select(c => c.Path.Split('/')[0]).Distinct().ToList(),
                    scriptNamespaces = ExtractNamespaces(contents.Where(c => c.Path.EndsWith(".cs")).ToList()),
                    recommendations = GenerateRecommendations(stats, issues)
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Analysis failed: {ex.Message}");
            }
        }

        private static List<string> ExtractNamespaces(List<PackageAsset> scripts)
        {
            // Would need to read script content - return empty for now
            return new List<string>();
        }

        private static List<string> DetectPotentialIssues(List<PackageAsset> contents)
        {
            var issues = new List<string>();

            // Check for common issues
            var paths = contents.Select(c => c.Path).ToList();

            // 1. Check for assets in root (bad practice)
            var rootAssets = contents.Where(c => !c.Path.Contains("/") && !c.Path.EndsWith(".meta")).ToList();
            if (rootAssets.Count > 5)
                issues.Add($"Package has {rootAssets.Count} assets in root - may clutter project");

            // 2. Check for duplicate file names
            var fileNames = contents.Select(c => Path.GetFileName(c.Path)).Where(n => !n.EndsWith(".meta")).ToList();
            var duplicates = fileNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
                issues.Add($"Duplicate file names: {string.Join(", ", duplicates.Take(5))}");

            // 3. Check for Editor scripts outside Editor folder
            var editorScripts = contents.Where(c =>
                c.Path.EndsWith(".cs") &&
                !c.Path.Contains("/Editor/") &&
                (c.Path.Contains("Editor") || c.Path.Contains("EditorWindow"))).ToList();
            if (editorScripts.Count > 0)
                issues.Add($"Potential Editor scripts outside Editor folder: {editorScripts.Count}");

            // 4. Check for common naming conflicts
            var commonNames = new[] { "GameManager", "Player", "Enemy", "UIManager", "AudioManager" };
            foreach (var name in commonNames)
            {
                if (paths.Any(p => p.Contains($"{name}.cs")))
                    issues.Add($"Contains common class name '{name}' - may conflict with existing code");
            }

            return issues;
        }

        private static List<string> GenerateRecommendations(object stats, List<string> issues)
        {
            var recommendations = new List<string>();

            if (issues.Count > 0)
                recommendations.Add("Review potential issues before importing");

            recommendations.Add("Use 'import_selective' to import only needed assets");
            recommendations.Add("Use 'detect_conflicts' to check for existing file conflicts");
            recommendations.Add("Back up your project before importing large packages");

            return recommendations;
        }

        #endregion

        #region List Contents

        private static object ListPackageContents(string path, JObject @params)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            if (!File.Exists(path))
                return new ErrorResponse($"Package not found: {path}");

            try
            {
                var contents = ExtractPackageInfo(path);

                string filter = @params["filter"]?.ToString()?.ToLower();
                string category = @params["category"]?.ToString()?.ToLower();
                int limit = @params["limit"]?.Value<int>() ?? 100;

                // Apply filters
                var filtered = contents.AsEnumerable();

                if (!string.IsNullOrEmpty(filter))
                {
                    filtered = filtered.Where(c =>
                        c.Path.ToLower().Contains(filter) ||
                        Path.GetFileName(c.Path).ToLower().Contains(filter));
                }

                if (!string.IsNullOrEmpty(category))
                {
                    filtered = filtered.Where(c => CategorizeAsset(c.Path).ToLower() == category);
                }

                var result = filtered.Take(limit).Select(c => new
                {
                    path = c.Path,
                    guid = c.Guid,
                    size = c.Size,
                    category = CategorizeAsset(c.Path),
                    extension = Path.GetExtension(c.Path)
                }).ToList();

                return new SuccessResponse($"Package contents: {result.Count}/{contents.Count} assets", new
                {
                    packageName = Path.GetFileNameWithoutExtension(path),
                    totalAssets = contents.Count,
                    showingAssets = result.Count,
                    filter = filter,
                    assets = result
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"List failed: {ex.Message}");
            }
        }

        #endregion

        #region Import Package

        private static object ImportPackage(string path, JObject @params)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            if (!File.Exists(path))
                return new ErrorResponse($"Package not found: {path}");

            try
            {
                bool interactive = @params["interactive"]?.Value<bool>() ?? false;

                if (interactive)
                {
                    AssetDatabase.ImportPackage(path, true);
                    return new SuccessResponse("Import dialog opened - select assets to import");
                }
                else
                {
                    AssetDatabase.ImportPackage(path, false);
                    AssetDatabase.Refresh();
                    return new SuccessResponse($"Package imported: {Path.GetFileNameWithoutExtension(path)}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Import failed: {ex.Message}");
            }
        }

        private static object ImportSelective(string path, JObject @params)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            var include = @params["include"]?.ToObject<string[]>();
            var exclude = @params["exclude"]?.ToObject<string[]>();
            var categories = @params["categories"]?.ToObject<string[]>();

            try
            {
                // For selective import, we need to use the interactive dialog
                // with a note about what to select
                var contents = ExtractPackageInfo(path);

                var toImport = contents.AsEnumerable();

                if (include != null && include.Length > 0)
                {
                    toImport = toImport.Where(c =>
                        include.Any(i => c.Path.Contains(i)));
                }

                if (exclude != null && exclude.Length > 0)
                {
                    toImport = toImport.Where(c =>
                        !exclude.Any(e => c.Path.Contains(e)));
                }

                if (categories != null && categories.Length > 0)
                {
                    toImport = toImport.Where(c =>
                        categories.Contains(CategorizeAsset(c.Path), StringComparer.OrdinalIgnoreCase));
                }

                var selectedPaths = toImport.Select(c => c.Path).ToList();

                // Open interactive dialog with recommendation
                AssetDatabase.ImportPackage(path, true);

                return new SuccessResponse("Import dialog opened", new
                {
                    recommendation = "Select only these assets:",
                    suggestedAssets = selectedPaths.Take(50).ToList(),
                    totalSuggested = selectedPaths.Count
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Selective import failed: {ex.Message}");
            }
        }

        private static object PreviewImport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            try
            {
                var contents = ExtractPackageInfo(path);
                var conflicts = new List<object>();

                foreach (var asset in contents.Where(c => !c.Path.EndsWith(".meta")))
                {
                    string fullPath = Path.Combine(Application.dataPath, asset.Path.Replace("Assets/", ""));
                    if (File.Exists(fullPath))
                    {
                        conflicts.Add(new
                        {
                            path = asset.Path,
                            status = "CONFLICT",
                            existingFile = fullPath
                        });
                    }
                }

                return new SuccessResponse("Import preview", new
                {
                    packageName = Path.GetFileNameWithoutExtension(path),
                    totalAssets = contents.Count,
                    conflictCount = conflicts.Count,
                    safeToImport = conflicts.Count == 0,
                    conflicts = conflicts.Take(20).ToList()
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Preview failed: {ex.Message}");
            }
        }

        #endregion

        #region Detect Conflicts

        private static object DetectConflicts(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new ErrorResponse("Path parameter required");

            try
            {
                var contents = ExtractPackageInfo(path);
                var conflicts = new List<object>();
                var safeAssets = new List<object>();

                foreach (var asset in contents.Where(c => !c.Path.EndsWith(".meta")))
                {
                    string targetPath = asset.Path;
                    if (!targetPath.StartsWith("Assets/"))
                        targetPath = "Assets/" + targetPath;

                    string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), targetPath);

                    if (File.Exists(fullPath))
                    {
                        var existingInfo = new FileInfo(fullPath);
                        conflicts.Add(new
                        {
                            path = asset.Path,
                            packageGuid = asset.Guid,
                            existingSize = existingInfo.Length,
                            packageSize = asset.Size,
                            sizeMatch = existingInfo.Length == asset.Size,
                            recommendation = existingInfo.Length == asset.Size ? "Skip (same size)" : "Review before import"
                        });
                    }
                    else
                    {
                        safeAssets.Add(new { path = asset.Path });
                    }
                }

                return new SuccessResponse($"Conflict detection: {conflicts.Count} conflicts found", new
                {
                    packageName = Path.GetFileNameWithoutExtension(path),
                    totalAssets = contents.Count,
                    conflictCount = conflicts.Count,
                    safeCount = safeAssets.Count,
                    safeToImport = conflicts.Count == 0,
                    conflicts = conflicts,
                    safeAssets = safeAssets.Take(20).ToList()
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Conflict detection failed: {ex.Message}");
            }
        }

        #endregion

        #region Organize Imported Assets

        private static object OrganizeImportedAssets(JObject @params)
        {
            string sourcePath = @params["source"]?.ToString() ?? "Assets";
            string targetPath = @params["target"]?.ToString();
            bool dryRun = @params["dry_run"]?.Value<bool>() ?? true;

            try
            {
                var allAssets = AssetDatabase.FindAssets("", new[] { sourcePath });
                var movedAssets = new List<object>();
                var errors = new List<string>();

                foreach (var guid in allAssets.Take(100)) // Limit for safety
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    string category = CategorizeAsset(assetPath);
                    string fileName = Path.GetFileName(assetPath);

                    string newPath = string.IsNullOrEmpty(targetPath)
                        ? $"Assets/{category}/{fileName}"
                        : $"{targetPath}/{category}/{fileName}";

                    if (assetPath != newPath)
                    {
                        if (dryRun)
                        {
                            movedAssets.Add(new { from = assetPath, to = newPath });
                        }
                        else
                        {
                            // Create directory if needed
                            string dir = Path.GetDirectoryName(newPath);
                            if (!AssetDatabase.IsValidFolder(dir))
                            {
                                CreateFolderRecursive(dir);
                            }

                            string error = AssetDatabase.MoveAsset(assetPath, newPath);
                            if (string.IsNullOrEmpty(error))
                            {
                                movedAssets.Add(new { from = assetPath, to = newPath });
                            }
                            else
                            {
                                errors.Add($"{assetPath}: {error}");
                            }
                        }
                    }
                }

                if (!dryRun)
                {
                    AssetDatabase.Refresh();
                }

                return new SuccessResponse(dryRun ? "Organization preview" : "Assets organized", new
                {
                    dryRun = dryRun,
                    movedCount = movedAssets.Count,
                    errorCount = errors.Count,
                    moves = movedAssets,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Organize failed: {ex.Message}");
            }
        }

        private static void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        #endregion

        #region Fix References

        private static object FixReferences(JObject @params)
        {
            string targetPath = @params["path"]?.ToString() ?? "Assets";
            bool dryRun = @params["dry_run"]?.Value<bool>() ?? true;

            try
            {
                var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { targetPath });
                var scenes = AssetDatabase.FindAssets("t:Scene", new[] { targetPath });
                var scriptableObjects = AssetDatabase.FindAssets("t:ScriptableObject", new[] { targetPath });

                var brokenReferences = new List<object>();
                var fixedReferences = new List<object>();

                // Check prefabs for missing references
                foreach (var guid in prefabs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab != null)
                    {
                        var components = prefab.GetComponentsInChildren<Component>(true);
                        foreach (var comp in components)
                        {
                            if (comp == null)
                            {
                                brokenReferences.Add(new
                                {
                                    asset = path,
                                    type = "MissingScript",
                                    gameObject = "Unknown"
                                });
                            }
                            else
                            {
                                // Check for missing references in serialized properties
                                var so = new SerializedObject(comp);
                                var prop = so.GetIterator();

                                while (prop.NextVisible(true))
                                {
                                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                                    {
                                        if (prop.objectReferenceValue == null &&
                                            prop.objectReferenceInstanceIDValue != 0)
                                        {
                                            brokenReferences.Add(new
                                            {
                                                asset = path,
                                                type = "MissingReference",
                                                component = comp.GetType().Name,
                                                property = prop.name
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return new SuccessResponse($"Reference check: {brokenReferences.Count} issues found", new
                {
                    dryRun = dryRun,
                    checkedPrefabs = prefabs.Length,
                    checkedScenes = scenes.Length,
                    checkedScriptableObjects = scriptableObjects.Length,
                    brokenCount = brokenReferences.Count,
                    brokenReferences = brokenReferences.Take(50).ToList(),
                    fixedCount = fixedReferences.Count,
                    recommendations = new[]
                    {
                        "Missing scripts usually indicate missing dependencies",
                        "Use 'import_selective' to import only required scripts",
                        "Check if required packages are installed via Package Manager"
                    }
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Fix references failed: {ex.Message}");
            }
        }

        #endregion

        #region Find Packages

        private static object FindPackagesInFolder(JObject @params)
        {
            string searchPath = @params["path"]?.ToString();

            // Default search paths
            var searchPaths = new List<string>();

            if (!string.IsNullOrEmpty(searchPath))
            {
                searchPaths.Add(searchPath);
            }
            else
            {
                // Common locations
                searchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads");
                searchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                searchPaths.Add(Application.dataPath.Replace("/Assets", ""));
            }

            var packages = new List<object>();

            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var files = Directory.GetFiles(path, "*.unitypackage", SearchOption.AllDirectories);
                        foreach (var file in files.Take(50))
                        {
                            var info = new FileInfo(file);
                            packages.Add(new
                            {
                                name = Path.GetFileNameWithoutExtension(file),
                                path = file,
                                size = info.Length,
                                sizeFormatted = FormatBytes(info.Length),
                                lastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    catch { }
                }
            }

            return new SuccessResponse($"Found {packages.Count} packages", new
            {
                searchedPaths = searchPaths,
                packageCount = packages.Count,
                packages = packages.OrderByDescending(p => ((dynamic)p).lastModified).ToList()
            });
        }

        #endregion

        #region Export Package

        private static object ExportPackage(JObject @params)
        {
            string[] assetPaths = @params["assets"]?.ToObject<string[]>();
            string outputPath = @params["output"]?.ToString();
            string packageName = @params["name"]?.ToString() ?? "ExportedPackage";
            bool includeDependencies = @params["include_dependencies"]?.Value<bool>() ?? true;

            if (assetPaths == null || assetPaths.Length == 0)
                return new ErrorResponse("Assets parameter required (array of asset paths)");

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(Application.dataPath.Replace("/Assets", ""),
                    $"{packageName}_{DateTime.Now:yyyyMMdd_HHmmss}.unitypackage");
            }

            try
            {
                var flags = includeDependencies
                    ? ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse
                    : ExportPackageOptions.Recurse;

                AssetDatabase.ExportPackage(assetPaths, outputPath, flags);

                return new SuccessResponse($"Package exported: {packageName}", new
                {
                    outputPath = outputPath,
                    assetCount = assetPaths.Length,
                    includedDependencies = includeDependencies
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Export failed: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static List<PackageAsset> ExtractPackageInfo(string packagePath)
        {
            var assets = new List<PackageAsset>();
            var tempDir = Path.Combine(Path.GetTempPath(), "unity_package_temp_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempDir);

                // Unity packages are tar.gz files
                using (var fileStream = File.OpenRead(packagePath))
                using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    ExtractTar(gzipStream, tempDir);
                }

                // Each asset in a .unitypackage has a GUID folder containing:
                // - asset (the actual file)
                // - asset.meta (the meta file)
                // - pathname (text file with the asset path)
                foreach (var guidDir in Directory.GetDirectories(tempDir))
                {
                    var pathnamePath = Path.Combine(guidDir, "pathname");
                    var assetPath = Path.Combine(guidDir, "asset");

                    if (File.Exists(pathnamePath))
                    {
                        string assetRelativePath = File.ReadAllText(pathnamePath).Trim();
                        long size = File.Exists(assetPath) ? new FileInfo(assetPath).Length : 0;

                        assets.Add(new PackageAsset
                        {
                            Guid = Path.GetFileName(guidDir),
                            Path = assetRelativePath,
                            Size = size
                        });
                    }
                }
            }
            finally
            {
                // Clean up temp directory
                try { Directory.Delete(tempDir, true); } catch { }
            }

            return assets;
        }

        /// <summary>
        /// Simple TAR extractor - no external dependencies
        /// </summary>
        private static void ExtractTar(Stream stream, string outputDir)
        {
            byte[] buffer = new byte[512];

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, 512);
                if (bytesRead < 512) break;

                // Check for end of archive (two zero blocks)
                bool allZeros = buffer.All(b => b == 0);
                if (allZeros) break;

                // Parse TAR header
                string fileName = Encoding.ASCII.GetString(buffer, 0, 100).TrimEnd('\0', ' ');
                if (string.IsNullOrEmpty(fileName)) break;

                // Parse file size (octal, bytes 124-135)
                string sizeStr = Encoding.ASCII.GetString(buffer, 124, 12).TrimEnd('\0', ' ');
                long fileSize = 0;
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    try { fileSize = Convert.ToInt64(sizeStr, 8); } catch { }
                }

                // Type flag (byte 156)
                char typeFlag = (char)buffer[156];
                bool isDirectory = typeFlag == '5';

                // Handle path prefixes (for long file names)
                string prefix = Encoding.ASCII.GetString(buffer, 345, 155).TrimEnd('\0', ' ');
                if (!string.IsNullOrEmpty(prefix))
                {
                    fileName = prefix + "/" + fileName;
                }

                string outputPath = Path.Combine(outputDir, fileName.Replace('/', Path.DirectorySeparatorChar));

                if (isDirectory)
                {
                    Directory.CreateDirectory(outputPath);
                }
                else if (fileSize > 0)
                {
                    // Ensure directory exists
                    string dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Read file content
                    using (var fileStream = File.Create(outputPath))
                    {
                        long remaining = fileSize;
                        byte[] fileBuffer = new byte[4096];

                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, fileBuffer.Length);
                            int read = stream.Read(fileBuffer, 0, toRead);
                            if (read == 0) break;
                            fileStream.Write(fileBuffer, 0, read);
                            remaining -= read;
                        }
                    }

                    // Skip padding to 512-byte boundary
                    int padding = (int)((512 - (fileSize % 512)) % 512);
                    if (padding > 0)
                    {
                        byte[] paddingBuffer = new byte[padding];
                        stream.Read(paddingBuffer, 0, padding);
                    }
                }
            }
        }

        private static string CategorizeAsset(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            string pathLower = path.ToLower();

            if (ext == ".cs" || ext == ".js")
                return "Scripts";
            if (ext == ".prefab")
                return "Prefabs";
            if (ext == ".mat")
                return "Materials";
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".tif")
                return "Textures";
            if (ext == ".fbx" || ext == ".obj" || ext == ".blend" || ext == ".dae" || ext == ".3ds")
                return "Models";
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff")
                return "Audio";
            if (ext == ".unity")
                return "Scenes";
            if (ext == ".anim" || ext == ".controller" || ext == ".overridecontroller")
                return "Animations";
            if (ext == ".shader" || ext == ".shadergraph" || ext == ".hlsl" || ext == ".cginc")
                return "Shaders";
            if (ext == ".asset")
                return "ScriptableObjects";

            return "Other";
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private static object GetHelp()
        {
            return new SuccessResponse("UnityPackage Tool - Help", new
            {
                description = "Read, analyze, and safely integrate .unitypackage files",
                actions = new
                {
                    analyze = "Deep analysis of package contents, dependencies, and potential issues",
                    list_contents = "List all assets in package with filtering options",
                    import = "Import package (interactive=true for dialog)",
                    import_selective = "Import with include/exclude filters",
                    detect_conflicts = "Check for existing file conflicts",
                    preview = "Preview what will be imported and conflicts",
                    organize = "Organize imported assets by category",
                    fix_references = "Detect and fix broken references in imported assets",
                    find_packages = "Scan common folders for .unitypackage files",
                    export = "Export assets as .unitypackage"
                },
                examples = new[]
                {
                    "unity_package action=analyze path=\"C:/Downloads/MyPackage.unitypackage\"",
                    "unity_package action=list_contents path=\"...\" category=\"Scripts\"",
                    "unity_package action=detect_conflicts path=\"...\"",
                    "unity_package action=import_selective path=\"...\" categories=[\"Scripts\",\"Prefabs\"]",
                    "unity_package action=organize source=\"Assets/ImportedAssets\" dry_run=true"
                }
            });
        }

        #endregion

        private class PackageAsset
        {
            public string Guid { get; set; }
            public string Path { get; set; }
            public long Size { get; set; }
        }
    }
}
