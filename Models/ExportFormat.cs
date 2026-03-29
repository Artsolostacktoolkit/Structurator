namespace StructureSnap.Models
{
    /// <summary>
    /// Описание формата экспорта для карточки на дашборде.
    /// </summary>
    public class ExportFormat
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Символ-иконка для карточки (эмодзи или Unicode-символ).
       /// </summary>
        public string IconGlyph { get; set; } = "📄";

        public string FileExtension { get; set; } = ".txt";

        /// <summary>
        /// MIME-тип для корректного фильтра в диалоге сохранения.
        /// </summary>
        public string MimeType { get; set; } = "text/plain";

        public ExportFormat() { }

        /// <summary>
        /// Предустановленные форматы для дашборда.
        /// </summary>
        public static List<ExportFormat> GetPresetFormats()
        {
            return new List<ExportFormat>
            {
                new ExportFormat
                {
                    Id = "json",
                    DisplayName = "JSON Структура",
                    Description = "Машиночитаемый формат для скриптов и API",
                    IconGlyph = "📋",
                    FileExtension = ".json",
                    MimeType = "application/json"
                },
                new ExportFormat
                {
                    Id = "png",
                    DisplayName = "PNG Визуализация",
                    Description = "Изображение дерева для документации",
                    IconGlyph = "🖼️",
                    FileExtension = ".png",
                    MimeType = "image/png"
                },
                new ExportFormat
                {
                    Id = "tree",
                    DisplayName = "Текстовое дерево",
                    Description = "Классический вид как команда tree",
                    IconGlyph = "🌲",
                    FileExtension = ".txt",
                    MimeType = "text/plain"
                },
                new ExportFormat
                {
                    Id = "csv",
                    DisplayName = "CSV Таблица",
                    Description = "Табличный формат для Excel и отчётов",
                    IconGlyph = "📊",
                    FileExtension = ".csv",
                    MimeType = "text/csv"
                }
            };
        }
    }
}