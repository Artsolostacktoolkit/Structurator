namespace StructureSnap.Models
{
    /// <summary>
    /// Данные для превью внутри карточки на дашборде.
    /// </summary>
    public class CardPreviewData
    {
        /// <summary>
        /// Текстовое превью (для JSON, CSV, Tree).
        /// </summary>
        public string TextPreview { get; set; } = string.Empty;

        /// <summary>
        /// Путь к изображению превью (для PNG).
        /// </summary>
        public string ImagePreviewPath { get; set; } = string.Empty;

        /// <summary>
        /// Количество элементов в дереве (проекты + файлы).
        /// Нужно для отображения статистики на карточке.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Подсказка о размере результата (например, "120 проектов", "~25 KB").
        /// </summary>
        public string SizeHint { get; set; } = string.Empty;

        public CardPreviewData() { }

        /// <summary>
        /// Фабричный метод для текстовых превью (JSON, CSV, Tree).
        /// </summary>
        public static CardPreviewData CreateTextPreview(string text, int count, string sizeHint)
        {
            return new CardPreviewData
            {
                TextPreview = text,
                ItemCount = count,
                SizeHint = sizeHint
            };
        }

        /// <summary>
        /// Фабричный метод для графических превью (PNG).
        /// </summary>
        public static CardPreviewData CreateImagePreview(string imagePath, int count, string sizeHint)
        {
            return new CardPreviewData
            {
                ImagePreviewPath = imagePath,
                ItemCount = count,
                SizeHint = sizeHint
            };
        }
    }
}