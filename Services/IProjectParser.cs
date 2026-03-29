using StructureSnap.Models;

namespace StructureSnap.Services
{
    /// <summary>
    /// Контракт для парсера структуры проекта.
    /// и делает MainViewModel независимым от конкретной реализации MSBuild.
    /// </summary>
    public interface IProjectParser
    {
        /// <summary>
        /// Загружает структуру решения асинхронно с прогрессом.
        /// </summary>
        /// <param name="solutionPath">Путь к файлу .sln</param>
        /// <param name="progress">Прогресс выполнения (0-100)</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Список корневых узлов дерева (по одному на проект)</returns>
        Task<List<ProjectNode>> LoadSolutionAsync(
            string solutionPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Быстрая проверка валидности файла решения без загрузки MSBuild.
                /// </summary>
        /// <param name="path">Путь к файлу</param>
        /// <returns>True если файл является валидным .sln</returns>
        bool IsValidSolution(string path);
    }
}