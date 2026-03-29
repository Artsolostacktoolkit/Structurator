using StructureSnap.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace StructureSnap.Services
{
    public class PreviewGenerator : IPreviewGenerator
    {
        private const int MaxPreviewDepth = 3;
        private const int MaxPreviewItems = 15;
        private static readonly List<string> _tempFiles = new();
        private static readonly object _lock = new();

        public async Task<CardPreviewData> GeneratePreviewAsync(
            ExportFormat format,
            List<ProjectNode> tree,
            CancellationToken cancellationToken = default)
        {
            return format.Id.ToLower() switch
            {
                "json" => await GenerateJsonPreviewAsync(tree, cancellationToken),
                "png" => await GeneratePngPreviewAsync(tree, cancellationToken),
                "tree" => await GenerateTreePreviewAsync(tree, cancellationToken),
                "csv" => await GenerateCsvPreviewAsync(tree, cancellationToken),
                _ => CreateDefaultPreview()
            };
        }

        public void CleanupTempFiles()
        {
            lock (_lock)
            {
                foreach (var filePath in _tempFiles)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch { }
                }
                _tempFiles.Clear();
            }
        }

        private async Task<CardPreviewData> GenerateJsonPreviewAsync(
            List<ProjectNode> tree,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield();

                // Простая сериализация без сложных типов
                var json = "{\"projects\": [" +
                    string.Join(", ", tree.Take(3).Select(p =>
                        $"{{\"name\": \"{p.Name}\", \"type\": \"{p.ItemType}\"}}")) +
                    "]}";

                var previewText = json.Length > 100 ? json.Substring(0, 100) + "...}" : json;

                return CardPreviewData.CreateTextPreview(
                    previewText,
                    CountAllItems(tree),
                    $"{tree.Count} проектов");
            }
            catch
            {
                return CardPreviewData.CreateTextPreview(
                    "{ \"projects\": [ ... ] }",
                    CountAllItems(tree),
                    $"{tree.Count} проектов");
            }
        }

        private async Task<CardPreviewData> GeneratePngPreviewAsync(
            List<ProjectNode> tree,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var treeText = BuildTreeText(tree, MaxPreviewDepth, MaxPreviewItems);

                    using var font = new Font("Consolas", 9);
                    using var tempBmp = new Bitmap(1, 1);
                    using var gTemp = Graphics.FromImage(tempBmp);
                    var textSize = gTemp.MeasureString(treeText, font);

                    int width = Math.Min((int)textSize.Width + 40, 250);
                    int height = Math.Min((int)textSize.Height + 40, 180);

                    using var bitmap = new Bitmap(width, height);
                    using var g = Graphics.FromImage(bitmap);
                    g.Clear(Color.FromArgb(250, 250, 250));
                    g.DrawString(treeText, font, Brushes.Black, new PointF(20, 20));

                    var tempPath = Path.Combine(Path.GetTempPath(),
                        $"structuresnap_preview_{Guid.NewGuid()}.png");

                    bitmap.Save(tempPath, ImageFormat.Png);
                    lock (_lock) { _tempFiles.Add(tempPath); }

                    return CardPreviewData.CreateImagePreview(tempPath, CountAllItems(tree), $"{tree.Count} проектов");
                }
                catch
                {
                    return CreateDefaultPreview();
                }
            }, cancellationToken);
        }

        private async Task<CardPreviewData> GenerateTreePreviewAsync(
            List<ProjectNode> tree,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var treeText = BuildTreeText(tree, MaxPreviewDepth, MaxPreviewItems);
            var lines = treeText.Split('\n').Take(10).ToArray();
            var previewText = string.Join('\n', lines) + (lines.Length < treeText.Split('\n').Length ? "\n..." : "");
            return CardPreviewData.CreateTextPreview(previewText, CountAllItems(tree), $"{tree.Count} проектов");
        }

        private async Task<CardPreviewData> GenerateCsvPreviewAsync(
            List<ProjectNode> tree,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var sb = new StringBuilder();
            sb.AppendLine("Type,Name,Path,Depth");
            foreach (var item in FlattenTree(tree).Take(3))
            {
                var safePath = item.FullPath.Contains(",") ? $"\"{item.FullPath}\"" : item.FullPath;
                sb.AppendLine($"{item.ItemType},{item.Name},{safePath},{item.Depth}");
            }
            return CardPreviewData.CreateTextPreview(sb.ToString(), CountAllItems(tree), $"{tree.Count} проектов");
        }

        private string BuildTreeText(List<ProjectNode> nodes, int maxDepth, int maxItems, string prefix = "")
        {
            var sb = new StringBuilder();
            var processed = 0;
            for (int i = 0; i < nodes.Count && processed < maxItems; i++)
            {
                var node = nodes[i];
                if (node == null || node.Depth > maxDepth) continue;
                var isLast = (i == nodes.Count - 1);
                var icon = node.IsFolder ? "📁" : "📄";
                sb.AppendLine($"{prefix}{(isLast ? "└─" : "├─")} {icon} {node.Name}");
                if (node.Children.Any() && node.Depth < maxDepth)
                {
                    processed++;
                    sb.Append(BuildTreeText(node.Children, maxDepth, maxItems - processed, prefix + (isLast ? "   " : "│  ")));
                }
                processed++;
            }
            return sb.ToString();
        }

        private List<ProjectNode> FlattenTree(List<ProjectNode> nodes)
        {
            var result = new List<ProjectNode>();
            if (nodes == null) return result;
            foreach (var node in nodes)
            {
                if (node != null)
                {
                    result.Add(node);
                    result.AddRange(FlattenTree(node.Children));
                }
            }
            return result;
        }

        private int CountAllItems(List<ProjectNode> nodes)
        {
            if (nodes == null) return 0;
            var count = nodes.Count;
            foreach (var node in nodes)
            {
                if (node != null) count += CountAllItems(node.Children);
            }
            return count;
        }

        private CardPreviewData CreateDefaultPreview()
        {
            return CardPreviewData.CreateTextPreview("Предпросмотр недоступен", 0, "—");
        }
    }
}