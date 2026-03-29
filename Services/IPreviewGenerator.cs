using StructureSnap.Models;

namespace StructureSnap.Services
{
    /// <summary>
    /// Контракт для генератора превью форматов.
    /// и тестировать генерацию превью без реальной отрисовки графики.
    /// </summary>
    public interface IPreviewGenerator
    {
        /// <summary>
        /// Генерирует превью для указанного формата экспорта.
        /// </summary>
        /// <param name="format">Формат экспорта (JSON, PNG, Tree, CSV)</param>
        /// <param name="tree">Дерево проекта для генерации превью</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Данные превью для отображения в карточке</returns>
        Task<CardPreviewData> GeneratePreviewAsync(
            ExportFormat format,
            List<ProjectNode> tree,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Очищает временные файлы, созданные при генерации PNG-превью.
                /// </summary>
        void CleanupTempFiles();
    }
}