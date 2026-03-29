using StructureSnap.Models;

namespace StructureSnap.Services
{
    /// <summary>
    /// Контракт для сервиса экспорта данных.
    /// и тестировать ViewModel без реальной записи файлов на диск.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Экспортирует дерево проекта в указанный формат.
        /// </summary>
        /// <param name="format">Формат экспорта (JSON, PNG, Tree, CSV)</param>
        /// <param name="tree">Полное дерево проекта для экспорта</param>
        /// <param name="outputPath">Путь для сохранения файла</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат экспорта (успех/ошибка с сообщением)</returns>
        Task<ExportResult> ExportAsync(
            ExportFormat format,
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Генерирует имя файла по умолчанию для указанного формата.
                /// </summary>
        /// <param name="format">Формат экспорта</param>
        /// <param name="solutionName">Имя решения для подстановки</param>
        /// <returns>Рекомендуемое имя файла (например, "MySolution_structure.json")</returns>
        string GetDefaultFileName(ExportFormat format, string solutionName);

        /// <summary>
        /// Получает фильтр файлов для SaveFileDialog.
                /// </summary>
        /// <param name="format">Формат экспорта</param>
        /// <returns>Строка фильтра (например, "JSON files|*.json")</returns>
        string GetFileFilter(ExportFormat format);
    }

    /// <summary>
    /// Результат операции экспорта.
    /// но и сообщение, путь к файлу, размер для отображения в UI.
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public string? OutputPath { get; set; }

        public long FileSizeBytes { get; set; }

        public static ExportResult Ok(string path, long fileSize)
        {
            return new ExportResult
            {
                Success = true,
                OutputPath = path,
                FileSizeBytes = fileSize
            };
        }

        public static ExportResult Fail(string error)
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }
}