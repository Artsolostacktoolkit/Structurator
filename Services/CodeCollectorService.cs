using System.IO;
using System.Text;
using System.Diagnostics;
using StructureSnap.Models;

namespace StructureSnap.Services;

/// <summary>
/// Собирает исходный код из проектов решения для экспорта.
/// </summary>
public class CodeCollectorService
{
    private readonly CodeCollectorOptions _options;

    public CodeCollectorService(CodeCollectorOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Собирает все файлы кода из решения асинхронно.
    /// </summary>
    public async Task<IReadOnlyList<CollectedCodeFile>> CollectAsync(
        string solutionPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException("Не удалось определить директорию решения");

        var collectedFiles = new List<CollectedCodeFile>();
        var projectFiles = await FindProjectFilesAsync(solutionDir, cancellationToken);

        progress?.Report($"Найдено {projectFiles.Count} файлов...");

        using var semaphore = new SemaphoreSlim(4);
        var tasks = projectFiles.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var collected = await ReadAndFormatFileAsync(file, solutionDir, cancellationToken);
                if (collected != null)
                {
                    lock (collectedFiles) collectedFiles.Add(collected);
                    progress?.Report($"Обработано: {collected.RelativePath}");
                }
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        return collectedFiles.OrderBy(f => f.RelativePath).ToList().AsReadOnly();
    }

    private async Task<List<string>> FindProjectFilesAsync(string solutionDir, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        await Task.Run(() =>
        {
            foreach (var ext in _options.IncludeExtensions)
            {
                foreach (var file in Directory.GetFiles(solutionDir, $"*{ext}", SearchOption.AllDirectories))
                {
                    if (ShouldIncludeFile(file, solutionDir)) files.Add(file);
                }
            }
        }, cancellationToken);
        return files;
    }

    private bool ShouldIncludeFile(string fullPath, string solutionDir)
    {
        var relativePath = Path.GetRelativePath(solutionDir, fullPath);
        foreach (var part in relativePath.Split(Path.DirectorySeparatorChar))
        {
            if (_options.ExcludeFolders.Contains(part, StringComparer.OrdinalIgnoreCase)) return false;
        }
        try
        {
            var info = new FileInfo(fullPath);
            if (_options.MaxFileSizeBytes > 0 && info.Length > _options.MaxFileSizeBytes) return false;
        }
        catch { return false; }
        return true;
    }

    private async Task<CollectedCodeFile?> ReadAndFormatFileAsync(
        string fullPath,
        string solutionDir,
        CancellationToken cancellationToken)
    {
        try
        {
            var relativePath = Path.GetRelativePath(solutionDir, fullPath);
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var languageHint = GetLanguageHint(extension);

            // Чтение файла с определением кодировки
            var content = await ReadFileWithEncodingAsync(fullPath, cancellationToken);
            if (string.IsNullOrEmpty(content)) return null;

                        if (_options.RemoveComments && ShouldRemoveCommentsFromPath(fullPath))
            {
                var remover = new CommentRemovalService();
                var removalResult = await remover.RemoveCommentsAsync(content, languageHint, cancellationToken);
                content = removalResult.CleanedContent;

                if (removalResult.SavingsPercent > 5)
                {
                    Debug.WriteLine($"[CodeCollector] {relativePath}: удалено {removalResult.SavingsPercent}% комментариев");
                }
            }
            
            var lines = content.Split('\n').Length;
            var roleHint = _options.IncludeRoleHints ? GetRoleHint(relativePath) : null;

            return new CollectedCodeFile
            {
                RelativePath = relativePath,
                Content = content,
                LineCount = lines,
                LanguageHint = languageHint,
                RoleHint = roleHint
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Ошибка чтения файла {fullPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Проверяет, следует ли удалять комментарии из файла.
    /// Исключает файлы БД и конфигурации.
    /// </summary>
    private bool ShouldRemoveCommentsFromPath(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var excludedExtensions = new[]
        {
            ".sql", ".db", ".mdb", ".sqlite", ".dbml", ".edmx", ".dacpac"
        };

        if (excludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"[CommentRemoval] Пропущен файл (БД/конфиг): {Path.GetFileName(filePath)}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Читает файл с автоопределением кодировки.
    /// </summary>
    private static async Task<string> ReadFileWithEncodingAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var encodings = new[]
        {
            new UTF8Encoding(true),
            new UTF8Encoding(false),
            Encoding.UTF8,
            Encoding.Unicode,
            Encoding.Default
        };

        foreach (var encoding in encodings)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch { continue; }
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>
    /// Определяет язык программирования по расширению.
    /// </summary>
    private static string GetLanguageHint(string extension) => extension switch
    {
        ".cs" => "csharp",
        ".xaml" => "xml",
        ".cshtml" => "html",
        ".razor" => "razor",
        ".json" => "json",
        ".config" => "xml",
        ".xml" => "xml",
        ".sql" => "sql",
        ".js" => "javascript",
        ".ts" => "typescript",
        ".py" => "python",
        _ => "text"
    };

    /// <summary>
    /// Генерирует подсказку о роли файла на основе пути.
    /// </summary>
    private static string? GetRoleHint(string relativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        var directory = Path.GetDirectoryName(relativePath);

        if (fileName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase)) return "ViewModel";
        if (fileName.EndsWith("Model", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("Entity", StringComparison.OrdinalIgnoreCase)) return "Model";
        if (fileName.EndsWith("View", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("Window", StringComparison.OrdinalIgnoreCase)) return "View";
        if (fileName.Equals("App", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Program", StringComparison.OrdinalIgnoreCase)) return "Entry Point";
        if (directory?.Contains("Services", StringComparison.OrdinalIgnoreCase) == true) return "Service";
        if (directory?.Contains("Controllers", StringComparison.OrdinalIgnoreCase) == true) return "Controller";

        return null;
    }
}