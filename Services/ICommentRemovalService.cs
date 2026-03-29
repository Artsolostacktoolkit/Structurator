namespace StructureSnap.Services;

/// <summary>
/// Сервис очистки кода от комментариев.
/// Поддерживает несколько языков программирования.
/// </summary>
public interface ICommentRemovalService
{
    /// <summary>
    /// Удаляет комментарии из исходного кода.
    /// </summary>
    /// <param name="content">Исходный код</param>
    /// <param name="languageHint">Язык программирования (csharp, xml, json, sql и т.д.)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат обработки (очищенный код + статистика)</returns>
    Task<CommentRemovalResult> RemoveCommentsAsync(
        string content,
        string languageHint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат очистки кода от комментариев.
/// </summary>
public class CommentRemovalResult
{
    /// <summary>
    /// Очищенный код без комментариев
    /// </summary>
    public string CleanedContent { get; set; } = string.Empty;

    /// <summary>
    /// Количество удалённых строк с комментариями
    /// </summary>
    public int RemovedCommentLines { get; set; }

    /// <summary>
    /// Количество удалённых символов
    /// </summary>
    public int RemovedCharacters { get; set; }

    /// <summary>
    /// Процент экономии (для отображения пользователю)
    /// </summary>
    public double SavingsPercent { get; set; }
}