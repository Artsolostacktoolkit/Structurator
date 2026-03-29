namespace StructureSnap.Services;

/// <summary>
/// Настройки сбора кода.
/// </summary>
public record CodeCollectorOptions
{
    /// <summary>
    /// Расширения файлов для включения.
    /// </summary>
    public IReadOnlyList<string> IncludeExtensions { get; init; } = new[]
    {
        ".cs", ".xaml", ".cshtml", ".razor", ".json", ".config", ".xml"
    };

    /// <summary>
    /// Папки для исключения.
    /// </summary>
    public IReadOnlyList<string> ExcludeFolders { get; init; } = new[]
    {
        "bin", "obj", ".git", "packages", "node_modules", ".vs"
    };

    /// <summary>
    /// Добавлять манифест в начало файла.
    /// </summary>
    public bool IncludeManifest { get; init; } = true;

    /// <summary>
    /// Добавлять подсказки о роли файлов.
    /// </summary>
    public bool IncludeRoleHints { get; init; } = false;

    /// <summary>
    /// Максимальный размер файла в байтах (0 = без лимита).
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>
    /// Удалять нежелательные комментарии из кода.
    /// </summary>
    public bool RemoveComments { get; init; } = false;
    public IReadOnlyList<string> ExcludeFromCommentRemoval { get; init; } = new[]
    {
        ".sql", ".db", ".mdb", ".sqlite", ".dbml", ".edmx", ".dacpac"
    };
}