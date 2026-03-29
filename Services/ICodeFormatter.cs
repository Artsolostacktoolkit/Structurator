using StructureSnap.Models;

namespace StructureSnap.Services;

/// <summary>
/// Форматирует собранные файлы в целевой формат экспорта.
/// </summary>
public interface ICodeFormatter
{
    /// <summary>
    /// Преобразует коллекцию файлов в строку для экспорта.
    /// </summary>
    string Format(IReadOnlyList<CollectedCodeFile> files, string solutionName);
}