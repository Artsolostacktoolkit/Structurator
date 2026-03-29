namespace StructureSnap.Models
{
    /// <summary>
    /// Представляет узел дерева проекта (файл или папку).
    /// </summary>
    public class ProjectNode
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public bool IsFolder { get; set; }

        /// <summary>
        /// Тип элемента с точки зрения MSBuild (Compile, Content, None и т.д.)
        
        /// </summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Коллекция дочерних элементов.
       
        /// </summary>
        public List<ProjectNode> Children { get; set; } = new();

        /// <summary>
        /// Уровень вложенности (0 = корень).
        /// Нужно для корректного отступа при визуализации и ограничения глубины превью.
        /// </summary>
        public int Depth { get; set; } = 0;

        public ProjectNode() { }

        public ProjectNode(string name, string path, bool isFolder, int depth = 0)
        {
            Name = name;
            FullPath = path;
            IsFolder = isFolder;
            Depth = depth;
        }
    }
}