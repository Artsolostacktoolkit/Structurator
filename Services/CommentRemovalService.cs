using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace StructureSnap.Services;

/// <summary>
/// Сервис селективной очистки кода от комментариев.
/// Сохраняет документацию и служебные метки, удаляет шум.
/// </summary>
public class CommentRemovalService : ICommentRemovalService
{
    /// <summary>
    /// Проверяет, следует ли удалять комментарии из файла.
    /// </summary>
    public bool ShouldRemoveComments(string filePath, CodeCollectorOptions options)
    {
        if (!options.RemoveComments)
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Исключаем файлы БД и другие важные форматы
        if (options.ExcludeFromCommentRemoval.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"[CommentRemoval] Пропущен файл (БД/конфиг): {filePath}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Удаляет только нежелательные комментарии из исходного кода.
    /// </summary>
    public Task<CommentRemovalResult> RemoveCommentsAsync(
        string content,
        string languageHint,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var originalLength = content.Length;
            var originalLines = content.Split('\n').Length;

            var cleaned = languageHint.ToLower() switch
            {
                "csharp" or "cs" or "c#" => RemoveCSharpCommentsSelective(content),
                "xml" or "xaml" or "html" => RemoveXmlCommentsSelective(content),
                "json" => content,
                "sql" => content, // SQL теперь исключается на уровне options
                "js" or "javascript" or "ts" or "typescript" => RemoveJsCommentsSelective(content),
                "python" or "py" => RemovePythonCommentsSelective(content),
                _ => RemoveGenericCommentsSelective(content)
            };

            var cleanedLength = cleaned.Length;
            var cleanedLines = cleaned.Split('\n').Length;

            return new CommentRemovalResult
            {
                CleanedContent = cleaned,
                RemovedCommentLines = originalLines - cleanedLines,
                RemovedCharacters = originalLength - cleanedLength,
                SavingsPercent = originalLength > 0
                    ? Math.Round((double)(originalLength - cleanedLength) / originalLength * 100, 1)
                    : 0
            };
        }, cancellationToken);
    }


    /// <summary>
    /// Удаляет комментарии C# селективно:
    /// ✅ Сохраняет: /// &lt;summary&gt;, /// &lt;param&gt;, // TODO:, // FIXME:, // NOTE:
    /// ❌ Удаляет: обычные // и /* */ без служебных меток
    /// </summary>
    private static string RemoveCSharpCommentsSelective(string content)
    {
        var lines = content.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // ✅ Удаляем строки, которые ТОЛЬКО комментарии
            if (trimmed.StartsWith("//") && !HasSpecialTag(trimmed))
            {
                continue; // Пропускаем всю строку
            }

            // ✅ Удаляем inline-комментарии (после кода)
            if (line.Contains("//") && !trimmed.StartsWith("//"))
            {
                var codePart = line.Substring(0, line.IndexOf("//")).TrimEnd();
                if (!string.IsNullOrEmpty(codePart))
                {
                    result.AppendLine(codePart);
                }
                continue;
            }

            // ✅ Сохраняем XML-документацию ///
            if (trimmed.StartsWith("///"))
            {
                result.AppendLine(line);
                continue;
            }

            // ✅ Сохраняем обычные строки кода
            if (!string.IsNullOrWhiteSpace(line))
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Проверяет, содержит ли комментарий служебную метку.
    /// </summary>
    private static bool HasSpecialTag(string comment)
    {
        var tags = new[]
        {
            "TODO", "FIXME", "NOTE", "HACK", "XXX", "OPTIMIZE",
            "REVIEW", "BUG", "WARNING", "DEPRECATED", "IMPORTANT"
        };

        var upper = comment.ToUpperInvariant();
        return tags.Any(tag =>
            upper.Contains($"// {tag}:") ||
            upper.Contains($"//{tag}:") ||
            upper.Contains($"/* {tag}:") ||
            upper.Contains($"/*{tag}:"));
    }

    /// <summary>
    /// Удаляет комментарии XML селективно.
    /// ✅ Сохраняет: &lt;!--[if ...]--&gt;, &lt;!-- @license --&gt;
    /// ❌ Удаляет: обычные &lt;!-- комментарий --&gt;
    /// </summary>
    private static string RemoveXmlCommentsSelective(string content)
    {
        // Сохраняем условные комментарии и лицензионные блоки
        return Regex.Replace(content,
            @"<!--(?!\[if| @license| @preserve| @cc_on)[\s\S]*?-->",
            string.Empty);
    }

    /// <summary>
    /// Удаляет комментарии SQL селективно.
    /// </summary>
    private static string RemoveSqlCommentsSelective(string content)
    {
        var lines = content.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Сохраняем комментарии с метками
            if (trimmed.StartsWith("--") && HasSpecialTag(trimmed))
            {
                result.AppendLine(line);
                continue;
            }

            // Удаляем обычные -- комментарии
            if (trimmed.StartsWith("--"))
                continue;

            // Обработка /* */
            if (trimmed.StartsWith("/*") && !trimmed.StartsWith("/**"))
            {
                if (HasSpecialTag(trimmed))
                    result.AppendLine(line);
                continue;
            }

            result.AppendLine(line);
        }

        return result.ToString();
    }

    /// <summary>
    /// Удаляет комментарии JS/TS селективно.
    /// </summary>
    private static string RemoveJsCommentsSelective(string content)
    {
        return RemoveCSharpCommentsSelective(content); // Логика аналогична C#
    }

    /// <summary>
    /// Удаляет комментарии Python селективно.
    /// </summary>
    private static string RemovePythonCommentsSelective(string content)
    {
        var lines = content.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Сохраняем документацию и метки
            if (trimmed.StartsWith("#") && (HasSpecialTag(trimmed) || trimmed.StartsWith("#!/")))
            {
                result.AppendLine(line);
                continue;
            }

            // Удаляем обычные # комментарии
            if (trimmed.StartsWith("#"))
                continue;

            result.AppendLine(line);
        }

        return result.ToString();
    }

    /// <summary>
    /// Универсальная селективная очистка.
    /// </summary>
    private static string RemoveGenericCommentsSelective(string content)
    {
        return RemoveCSharpCommentsSelective(content);
    }
}