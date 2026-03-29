using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using StructureSnap.Models;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;
using System.Linq;


namespace StructureSnap.Services
{
    public class ProjectParser : IProjectParser
    {
        private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".vs", ".git", "packages", "node_modules", "bower_components"
        };

        private static readonly HashSet<string> ExcludedItemTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "KnownFrameworkReference",
            "KnownRuntimePack",
            "KnownAppHostPack",
            "KnownCrossgen2Pack",
            "KnownILCompilerPack",
            "KnownILLinkPack",
            "KnownWebAssemblySdkPack",
            "WindowsSdkSupportedTargetPlatformVersion",
            "SdkSupportedTargetPlatformVersion",
            "SdkSupportedTargetPlatformIdentifier",
            "SourceLinkGitHubHost",
            "SourceLinkGitLabHost",
            "SourceLinkAzureReposGitHost",
            "SourceLinkBitbucketGitHost",
            "_KnownRuntimeIdentiferPlatforms",
            "_ExcludedKnownRuntimeIdentiferPlatforms",
            "SupportedTargetFramework",
            "_UnsupportedNETCoreAppTargetFramework",
            "_UnsupportedNETStandardTargetFramework",
            "_UnsupportedNETFrameworkTargetFramework",
            "_EolNetCoreTargetFrameworkVersions",
            "ProjectCapability",
            "CompilerVisibleProperty",
            "RuntimeHostConfigurationOption",
            "PackageConflictOverrides",
            "GlobalAnalyzerConfigFiles",
            "_AllDirectoriesAbove",
        };

        
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Исходный код
            ".cs", ".xaml", ".cshtml", ".razor", ".vb", ".fs",
            // Конфигурация
            ".config", ".editorconfig", ".globalconfig", ".json", ".xml", ".props", ".targets", ".tasks",
            // Ресурсы
            ".resx", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".bmp", ".webp",
            // Документы
            ".txt", ".md", ".rtf", ".html", ".htm", ".css", ".js", ".ts",
            ".scss", ".less", ".sass", ".sql", ".ps1", ".bat", ".cmd", ".sh",
            ".yml", ".yaml", ".toml", ".ini", ".cfg", 
            // Git
            ".gitignore", ".gitattributes",
            // Проекты (для ссылок)
            ".csproj", ".vbproj", ".fsproj"
        };

        private static readonly Regex ProjectPatternRegex = new(
            @"Project\(""[^""]+""\)\s*=\s*""[^""]+"",\s*""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        static ProjectParser()
        {
            try
            {
                if (!MSBuildLocator.CanRegister) return;

                var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                if (instances.Any())
                {
                    var latest = instances.OrderByDescending(i => i.Version).First();
                    MSBuildLocator.RegisterInstance(latest);
                }
                else
                {
                    MSBuildLocator.RegisterDefaults();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProjectParser] Ошибка: {ex.Message}");
                // Не выбрасываем исключение — позволяем приложению запуститься
            }
        }

        // Вспомогательный метод для получения версии VS
        private static string GetVisualStudioVersion()
        {
            try
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                return instances.Any()
                    ? string.Join(", ", instances.Select(i => $"{i.Name} {i.Version}"))
                    : "Не найдено";
            }
            catch
            {
                return "Неизвестно";
            }
        }

        public bool IsValidSolution(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!File.Exists(path)) return false;

            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var firstLine = File.ReadLines(path).FirstOrDefault();
                return firstLine?.Contains("Microsoft Visual Studio Solution File") == true;
            }

            if (path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(path);
                    return doc.DocumentElement?.Name == "Solution";
                }
                catch { return false; }
            }
            return false;
        }

        public async Task<List<ProjectNode>> LoadSolutionAsync(
            string solutionPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
                throw new ArgumentException("Путь к решению не может быть пустым", nameof(solutionPath));

            var rootNodes = new List<ProjectNode>();
            var solutionDir = Path.GetFullPath(Path.GetDirectoryName(solutionPath) ?? string.Empty);

            Debug.WriteLine($"[ProjectParser] ════════════════════════════════════════");
            Debug.WriteLine($"[ProjectParser] Начало парсинга решения");
            Debug.WriteLine($"[ProjectParser] Путь: {solutionPath}");
            Debug.WriteLine($"[ProjectParser] Директория решения: {solutionDir}");
            Debug.WriteLine($"[ProjectParser] Формат: {(solutionPath.EndsWith(".slnx") ? "SLNX" : "SLN")}");
            Debug.WriteLine($"[ProjectParser] ════════════════════════════════════════");

            // ДИАГНОСТИКА: Проверяем содержимое файла
            DiagnosticSolutionParse(solutionPath);

            using var projectCollection = new ProjectCollection();
            var projectPaths = ExtractProjectPathsFromSolution(solutionPath);

            if (!projectPaths.Any())
            {
                Debug.WriteLine($"[ProjectParser] ⚠ Проекты не найдены в решении");
                Debug.WriteLine($"[ProjectParser] Попробуйте прямой поиск .csproj файлов");

                // Альтернативный метод: ищем все .csproj в директории решения
                var fallbackPaths = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !IsExcludedPath(p))
                    .ToList();

                Debug.WriteLine($"[ProjectParser] Найдено .csproj файлов в директории: {fallbackPaths.Count}");
                projectPaths.AddRange(fallbackPaths);
            }

            Debug.WriteLine($"[ProjectParser] ✓ Найдено проектов: {projectPaths.Count}");
            foreach (var pp in projectPaths)
            {
                Debug.WriteLine($"[ProjectParser]   - {pp}");
            }

            var totalProjects = projectPaths.Count;
            var processedProjects = 0;

            foreach (var projectPath in projectPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    Debug.WriteLine($"[ProjectParser] ─────────────────────────────────────");
                    Debug.WriteLine($"[ProjectParser] Загрузка проекта: {Path.GetFileName(projectPath)}");

                    if (!File.Exists(projectPath))
                    {
                        Debug.WriteLine($"[ProjectParser]   ✗ Файл проекта не существует: {projectPath}");
                        continue;
                    }

                    var project = projectCollection.LoadProject(projectPath);
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    var projectDir = Path.GetFullPath(Path.GetDirectoryName(projectPath) ?? string.Empty);

                    Debug.WriteLine($"[ProjectParser]   Директория проекта: {projectDir}");
                    Debug.WriteLine($"[ProjectParser]   ВСЕГО элементов в проекте: {project.Items.Count()}");

                    // Выводим первые 10 типов элементов для диагностики
                    var topItemTypes = project.Items
                        .GroupBy(i => i.ItemType)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(10)
                        .ToList();

                    Debug.WriteLine($"[ProjectParser]   Топ-10 типов элементов:");
                    foreach (var type in topItemTypes)
                    {
                        Debug.WriteLine($"[ProjectParser]     {type.Type}: {type.Count}");
                    }

                    var projectNode = new ProjectNode
                    {
                        Name = projectName,
                        FullPath = projectPath,
                        IsFolder = false,
                        ItemType = "Project",
                        Depth = 0,
                        Children = new List<ProjectNode>()
                    };

                    var allItems = project.Items.ToList();
                    var filteredItems = new List<ProjectNode>();

                    // Статистика фильтрации
                    var stats = new Dictionary<string, int>
                    {
                        { "Всего", allItems.Count },
                        { "Пустой EvaluatedInclude", 0 },
                        { "ExcludedItemType", 0 },
                        { "ExcludedFolder", 0 },
                        { "OutsideSolution", 0 },
                        { "NotAllowedExtension", 0 },
                        { "FileNotFound", 0 },
                        { "Прошло фильтр", 0 }
                    };

                    foreach (var item in allItems)
                    {
                        // Проверка 1: Пустой EvaluatedInclude
                        if (string.IsNullOrEmpty(item.EvaluatedInclude))
                        {
                            stats["Пустой EvaluatedInclude"]++;
                            continue;
                        }

                        // Проверка 2: ExcludedItemType
                        if (ExcludedItemTypes.Contains(item.ItemType))
                        {
                            stats["ExcludedItemType"]++;
                            continue;
                        }

                        // Проверка 3: ExcludedFolder
                        if (IsExcludedPath(item.EvaluatedInclude))
                        {
                            stats["ExcludedFolder"]++;
                            continue;
                        }

                        // Формируем полный путь
                        string fullPath;
                        try
                        {
                            fullPath = Path.IsPathRooted(item.EvaluatedInclude)
                                ? Path.GetFullPath(item.EvaluatedInclude)
                                : Path.GetFullPath(Path.Combine(projectDir, item.EvaluatedInclude));
                        }
                        catch
                        {
                            continue;
                        }

                        // Проверка 4: Файл существует
                        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                        {
                            stats["FileNotFound"]++;
                            continue;
                        }

                        // Проверка 5: Вне директории решения
                        if (!IsInSolutionDirectory(fullPath, solutionDir))
                        {
                            stats["OutsideSolution"]++;
                            continue;
                        }

                        // Проверка 6: AllowedExtensions
                        var extension = Path.GetExtension(fullPath);
                        if (AllowedExtensions.Count > 0 &&
                            !string.IsNullOrEmpty(extension) &&
                            !AllowedExtensions.Contains(extension))
                        {
                            stats["NotAllowedExtension"]++;
                            continue;
                        }

                        // Элемент прошёл все фильтры
                        stats["Прошло фильтр"]++;

                        filteredItems.Add(new ProjectNode
                        {
                            Name = Path.GetFileName(item.EvaluatedInclude),
                            FullPath = fullPath,
                            IsFolder = false,
                            ItemType = item.ItemType,
                            Depth = 1,
                            Children = new List<ProjectNode>()
                        });
                    }

                    // Статистика фильтрации
                    Debug.WriteLine($"[ProjectParser]   ═════ СТАТИСТИКА ФИЛЬТРАЦИИ ═════");
                    Debug.WriteLine($"[ProjectParser]   Всего элементов: {stats["Всего"]}");
                    Debug.WriteLine($"[ProjectParser]   Пустой EvaluatedInclude: {stats["Пустой EvaluatedInclude"]}");
                    Debug.WriteLine($"[ProjectParser]   ExcludedItemType: {stats["ExcludedItemType"]}");
                    Debug.WriteLine($"[ProjectParser]   ExcludedFolder: {stats["ExcludedFolder"]}");
                    Debug.WriteLine($"[ProjectParser]   FileNotFound: {stats["FileNotFound"]}");
                    Debug.WriteLine($"[ProjectParser]   OutsideSolution: {stats["OutsideSolution"]}");
                    Debug.WriteLine($"[ProjectParser]   NotAllowedExtension: {stats["NotAllowedExtension"]}");
                    Debug.WriteLine($"[ProjectParser]   ✓ Прошло фильтр: {stats["Прошло фильтр"]}");
                    Debug.WriteLine($"[ProjectParser]   ═══════════════════════════════════");

                    // Строим дерево папок
                    var folderTree = BuildFolderTree(filteredItems, projectDir);
                    Debug.WriteLine($"[ProjectParser]   Построено папок: {folderTree.Count}");

                    projectNode.Children.AddRange(folderTree);
                    rootNodes.Add(projectNode);

                    Debug.WriteLine($"[ProjectParser]   ✓ Проект загружен успешно");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProjectParser]   ✗ {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[ProjectParser]   StackTrace: {ex.StackTrace}");
                }

                processedProjects++;
                if (totalProjects > 0)
                {
                    progress?.Report((int)((processedProjects / (double)totalProjects) * 100));
                }
            }

            Debug.WriteLine($"[ProjectParser] ════════════════════════════════════════");
            Debug.WriteLine($"[ProjectParser] Итого проектов загружено: {rootNodes.Count}");
            Debug.WriteLine($"[ProjectParser] ════════════════════════════════════════");
            return rootNodes;
        }

        private void DiagnosticSolutionParse(string solutionPath)
        {
            try
            {
                Debug.WriteLine("========== ДИАГНОСТИКА ПАРСИНГА РЕШЕНИЯ ==========");
                Debug.WriteLine($"Файл: {solutionPath}");
                Debug.WriteLine($"Существует: {File.Exists(solutionPath)}");

                var fileInfo = new FileInfo(solutionPath);
                Debug.WriteLine($"Размер: {fileInfo.Length} байт");
                Debug.WriteLine($"Последнее изменение: {fileInfo.LastWriteTime}");

                if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    var content = File.ReadAllText(solutionPath);
                    Debug.WriteLine($"Содержимое .slnx (первые 500 символов):");
                    Debug.WriteLine(content.Substring(0, Math.Min(500, content.Length)));

                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(solutionPath);

                    var allNodes = xmlDoc.SelectNodes("//*");
                    Debug.WriteLine($"Всего XML элементов: {allNodes?.Count ?? 0}");

                    if (allNodes != null)
                    {
                        foreach (XmlNode node in allNodes)
                        {
                            if (node.Name.Contains("Project") ||
                                (node.Attributes != null && node.Attributes.Cast<XmlAttribute>()
                                    .Any(a => a.Value.Contains(".csproj"))))
                            {
                                Debug.WriteLine($"Найден потенциальный проект: {node.OuterXml}");
                            }
                        }
                    }
                }
                else
                {
                    var lines = File.ReadLines(solutionPath).Take(20).ToList();
                    Debug.WriteLine($"Первые 20 строк .sln:");
                    for (int i = 0; i < lines.Count; i++)
                    {
                        Debug.WriteLine($"  {i + 1}: {lines[i]}");

                        if (lines[i].Contains("Project("))
                        {
                            var match = ProjectPatternRegex.Match(lines[i]);
                            Debug.WriteLine($"    → Regex совпадение: {match.Success}");
                            if (match.Success)
                            {
                                Debug.WriteLine($"    → Извлечённый путь: {match.Groups[1].Value}");
                            }
                        }
                    }
                }

                Debug.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка диагностики: {ex.Message}");
            }
        }

        private List<string> ExtractProjectPathsFromSolution(string solutionPath)
        {
            Debug.WriteLine($"[ExtractProjectPaths] Парсинг решения: {solutionPath}");

            if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[ExtractProjectPaths]   Используем парсер .slnx");
                return ExtractProjectPathsFromSlnx(solutionPath);
            }

            Debug.WriteLine($"[ExtractProjectPaths]   Используем парсер .sln");
            return ExtractProjectPathsFromSlnLegacy(solutionPath);
        }

        private List<string> ExtractProjectPathsFromSlnx(string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionDir = Path.GetDirectoryName(solutionPath);

            if (string.IsNullOrEmpty(solutionDir))
            {
                Debug.WriteLine($"[ExtractProjectPaths] ⚠ Не удалось получить директорию решения");
                return projectPaths;
            }

            solutionDir = Path.GetFullPath(solutionDir);
            Debug.WriteLine($"[ExtractProjectPaths] Директория решения: {solutionDir}");

            try
            {
                var doc = new XmlDocument();
                doc.Load(solutionPath);

                Debug.WriteLine($"[ExtractProjectPaths] Корневой элемент: {doc.DocumentElement?.Name}");

                // Множество вариантов XPath запросов для поиска проектов
                var xpathQueries = new[]
                {
                    "//Project",
                    "//*[local-name()='Project']",
                    "//Projects/Project",
                    "//*[@Path]",
                    "//*[@path]",
                    "//*[contains(@Path, '.csproj')]",
                    "//*[contains(@path, '.csproj')]",
                    "//*[contains(text(), '.csproj')]"
                };

                foreach (var xpath in xpathQueries)
                {
                    var nodes = doc.SelectNodes(xpath);
                    if (nodes != null && nodes.Count > 0)
                    {
                        Debug.WriteLine($"[ExtractProjectPaths] XPath '{xpath}' нашёл {nodes.Count} узлов");

                        foreach (XmlNode node in nodes)
                        {
                            ProcessProjectNode(node, solutionDir, projectPaths);
                        }
                    }
                }

                
                if (projectPaths.Count == 0)
                {
                    Debug.WriteLine($"[ExtractProjectPaths] Поиск по всем атрибутам");
                    var allNodes = doc.SelectNodes("//*");
                    if (allNodes != null)
                    {
                        foreach (XmlNode node in allNodes)
                        {
                            if (node.Attributes != null)
                            {
                                foreach (XmlAttribute attr in node.Attributes)
                                {
                                    if (attr.Value.Contains(".csproj") ||
                                        attr.Value.Contains(".vbproj") ||
                                        attr.Value.Contains(".fsproj"))
                                    {
                                        Debug.WriteLine($"[ExtractProjectPaths] Найдено в атрибуте {attr.Name}: {attr.Value}");
                                        ProcessProjectPath(attr.Value, solutionDir, projectPaths);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtractProjectPaths] Ошибка: {ex.Message}");
            }

            Debug.WriteLine($"[ExtractProjectPaths] Итого проектов: {projectPaths.Count}");
            return projectPaths;
        }

        private void ProcessProjectNode(XmlNode node, string solutionDir, List<string> projectPaths)
        {
            Debug.WriteLine($"[ProcessProjectNode] Обработка узла: {node.Name}");

           
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    Debug.WriteLine($"[ProcessProjectNode]   Атрибут: {attr.Name} = {attr.Value}");

                    if (attr.Value.Contains(".csproj") ||
                        attr.Value.Contains(".vbproj") ||
                        attr.Value.Contains(".fsproj"))
                    {
                        ProcessProjectPath(attr.Value, solutionDir, projectPaths);
                    }
                }
            }

           
            if (!string.IsNullOrWhiteSpace(node.InnerText) &&
                (node.InnerText.Contains(".csproj") ||
                 node.InnerText.Contains(".vbproj") ||
                 node.InnerText.Contains(".fsproj")))
            {
                ProcessProjectPath(node.InnerText.Trim(), solutionDir, projectPaths);
            }
        }

        private void ProcessProjectPath(string pathValue, string solutionDir, List<string> projectPaths)
        {
            var relativePath = pathValue.Trim().Replace('/', Path.DirectorySeparatorChar);

            
            var possiblePaths = new List<string>();

            if (Path.IsPathRooted(relativePath))
            {
                possiblePaths.Add(Path.GetFullPath(relativePath));
            }
            else
            {
                possiblePaths.Add(Path.GetFullPath(Path.Combine(solutionDir, relativePath)));
                possiblePaths.Add(Path.GetFullPath(Path.Combine(solutionDir, "..", relativePath)));
                possiblePaths.Add(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath)));
            }

            foreach (var fullPath in possiblePaths.Distinct())
            {
                Debug.WriteLine($"[ProcessProjectPath] Проверка пути: {fullPath}");

                if (File.Exists(fullPath))
                {
                    if (!projectPaths.Contains(fullPath))
                    {
                        projectPaths.Add(fullPath);
                        Debug.WriteLine($"[ProcessProjectPath]   ✓ Добавлен: {fullPath}");
                    }
                    return;
                }
            }

            Debug.WriteLine($"[ProcessProjectPath]   ✗ Файл не найден по ни одному из путей");
        }

        private List<string> ExtractProjectPathsFromSlnLegacy(string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionDir = Path.GetDirectoryName(solutionPath) ?? string.Empty;

            foreach (var line in File.ReadLines(solutionPath))
            {
                var match = ProjectPatternRegex.Match(line);
                if (match.Success)
                {
                    var relativePath = match.Groups[1].Value;
                    var fullPath = Path.GetFullPath(relativePath, solutionDir);

                    if (fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        fullPath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                        fullPath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(fullPath))
                        {
                            projectPaths.Add(fullPath);
                        }
                    }
                }
            }

            return projectPaths;
        }

        private List<ProjectNode> BuildFolderTree(List<ProjectNode> items, string projectDir)
        {
            var rootFolders = new List<ProjectNode>();
            var folderMap = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var relativePath = Path.GetRelativePath(projectDir, item.FullPath);
                var directories = Path.GetDirectoryName(relativePath);

                if (string.IsNullOrEmpty(directories))
                {
                    rootFolders.Add(item);
                }
                else
                {
                    directories = directories.Replace('/', Path.DirectorySeparatorChar);
                    var parts = directories.Split(Path.DirectorySeparatorChar);

                    ProjectNode? currentFolder = null;
                    var currentPath = projectDir;

                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        currentPath = Path.Combine(currentPath, part);

                        if (!folderMap.TryGetValue(currentPath, out var folder))
                        {
                            folder = new ProjectNode
                            {
                                Name = part,
                                FullPath = currentPath,
                                IsFolder = true,
                                ItemType = "Folder",
                                Depth = i + 1,
                                Children = new List<ProjectNode>()
                            };

                            folderMap[currentPath] = folder;

                            if (i == 0)
                            {
                                rootFolders.Add(folder);
                            }
                            else
                            {
                                var parentPath = Path.GetDirectoryName(currentPath)!;
                                if (folderMap.TryGetValue(parentPath, out var parent))
                                {
                                    parent.Children.Add(folder);
                                }
                            }
                        }

                        currentFolder = folder;
                    }

                    currentFolder?.Children.Add(item);
                }
            }

            return rootFolders;
        }

        private bool IsInSolutionDirectory(string filePath, string solutionDir)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(solutionDir))
                return false;

            try
            {
                var normalizedPath = Path.GetFullPath(filePath);
                var normalizedDir = Path.GetFullPath(solutionDir);
                return normalizedPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool IsExcludedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            var pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return pathParts.Any(part => ExcludedFolders.Contains(part));
        }
    }
}