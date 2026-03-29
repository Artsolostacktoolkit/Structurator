namespace StructureSnap.Models;

/// <summary>
/// Представляет файл с исходным кодом, готовый для экспорта.
/// </summary>
public record CollectedCodeFile
{
    /// <summary>
    /// Относительный путь от решения
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Полное содержимое файла
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Количество строк в файле
    /// </summary>
    public int LineCount { get; init; }

    /// <summary>
    /// Подсказка о роли файла (опционально)
    /// </summary>
    public string? RoleHint { get; init; }

    /// <summary>
    /// Язык программирования для подсветки синтаксиса
    /// </summary>
    public string LanguageHint { get; init; } = "csharp";
}