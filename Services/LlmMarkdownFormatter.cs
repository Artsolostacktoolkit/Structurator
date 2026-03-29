using System.Text;
using StructureSnap.Models;

namespace StructureSnap.Services;

public class LlmMarkdownFormatter : ICodeFormatter
{
    public string Format(IReadOnlyList<CollectedCodeFile> files, string solutionName)
    {
        var output = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        output.AppendLine("# 📦 StructureSnap Export");
        output.AppendLine($"> Solution: `{solutionName}` | Files: {files.Count} | Generated: {timestamp}");
        output.AppendLine();
        output.AppendLine("## 🗂 Manifest");

        foreach (var file in files)
        {
            var hint = !string.IsNullOrEmpty(file.RoleHint) ? $" ← {file.RoleHint}" : string.Empty;
            output.AppendLine($"- `{file.RelativePath}` ({file.LineCount} lines){hint}");
        }
        output.AppendLine();
        output.AppendLine("---");
        output.AppendLine();

        foreach (var file in files)
        {
            output.AppendLine($"## File: `{file.RelativePath}` ({file.LineCount} lines)");
            output.AppendLine($"```{file.LanguageHint}");
            output.AppendLine(file.Content);
            output.AppendLine("```");
            output.AppendLine();
        }
        return output.ToString();
    }
}