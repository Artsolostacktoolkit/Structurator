using StructureSnap.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace StructureSnap.Services
{
    /// <summary>
    /// Сервис экспорта данных в различные форматы.
    /// независима от генерации превью и парсинга проекта.
    /// </summary>
    public class ExportService : IExportService
    {
        // Максимальный размер PNG изображения
                private const int MaxPngSize = 4096;

        public async Task<ExportResult> ExportAsync(
            ExportFormat format,
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return ExportResult.Fail("Путь к файлу не указан");
                }

                                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    return ExportResult.Fail($"Папка не существует: {directory}");
                }

                                var result = format.Id.ToLower() switch
                {
                    "json" => await ExportJsonAsync(tree, outputPath, cancellationToken),
                    "png" => await ExportPngAsync(tree, outputPath, cancellationToken),
                    "tree" => await ExportTreeAsync(tree, outputPath, cancellationToken),
                    "csv" => await ExportCsvAsync(tree, outputPath, cancellationToken),
                    _ => ExportResult.Fail($"Неподдерживаемый формат: {format.Id}")
                };

                return result;
            }
            catch (OperationCanceledException)
            {
                                Debug.WriteLine("[ExportService] Экспорт отменён пользователем");
                return ExportResult.Fail("Экспорт отменён");
            }
            catch (Exception ex)
            {
                                Debug.WriteLine($"[ExportService] Ошибка экспорта: {ex.Message}");
                return ExportResult.Fail($"Ошибка экспорта: {ex.Message}");
            }
        }

        public string GetDefaultFileName(ExportFormat format, string solutionName)
        {
                        var safeName = string.Concat(solutionName.Split(Path.GetInvalidFileNameChars()));
            return $"{safeName}_structure{format.FileExtension}";
        }

        public string GetFileFilter(ExportFormat format)
        {
                        return $"{format.DisplayName}|*{format.FileExtension}";
        }

        private async Task<ExportResult> ExportJsonAsync(
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(tree, options);

                        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            var fileSize = new FileInfo(outputPath).Length;
            return ExportResult.Ok(outputPath, fileSize);
        }

        private async Task<ExportResult> ExportPngAsync(
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                                var treeText = BuildTreeText(tree, int.MaxValue, int.MaxValue);

                using var font = new Font("Consolas", 10);
                using var brush = Brushes.Black;

                                using var tempBmp = new Bitmap(1, 1);
                using var gTemp = Graphics.FromImage(tempBmp);
                var textSize = gTemp.MeasureString(treeText, font);

                                int width = Math.Min((int)textSize.Width + 40, MaxPngSize);
                int height = Math.Min((int)textSize.Height + 40, MaxPngSize);

                using var bitmap = new Bitmap(width, height);
                using var g = Graphics.FromImage(bitmap);

               g.Clear(Color.White);

               g.DrawString(treeText, font, brush, new PointF(20, 20));

                                bitmap.Save(outputPath, ImageFormat.Png);
            }, cancellationToken);

            var fileSize = new FileInfo(outputPath).Length;
            return ExportResult.Ok(outputPath, fileSize);
        }

        private async Task<ExportResult> ExportTreeAsync(
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var treeText = BuildTreeText(tree, int.MaxValue, int.MaxValue);

                        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteAsync(treeText.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            var fileSize = new FileInfo(outputPath).Length;
            return ExportResult.Ok(outputPath, fileSize);
        }

        private async Task<ExportResult> ExportCsvAsync(
            List<ProjectNode> tree,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Type,Name,Path,Depth");

            var items = FlattenTree(tree);
            foreach (var item in items)
            {
                                var safePath = EscapeCsvField(item.FullPath);
                var safeName = EscapeCsvField(item.Name);

                sb.AppendLine($"{item.ItemType},{safeName},{safePath},{item.Depth}");
            }

                        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteAsync(sb.ToString().AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            var fileSize = new FileInfo(outputPath).Length;
            return ExportResult.Ok(outputPath, fileSize);
        }

        /// <summary>
        /// Экранирует поле для CSV формата.
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        /// <summary>
        /// Строит текстовое представление дерева с символами └─, ├─.
        /// </summary>
        private string BuildTreeText(List<ProjectNode> nodes, int maxDepth, int maxItems, string prefix = "")
        {
            var sb = new StringBuilder();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (maxDepth != int.MaxValue && node.Depth > maxDepth) continue;

                var isLast = (i == nodes.Count - 1);
                var icon = node.IsFolder ? "📁" : "📄";

                sb.AppendLine($"{prefix}{(isLast ? "└─" : "├─")} {icon} {node.Name}");

                if (node.Children.Any() && (maxDepth == int.MaxValue || node.Depth < maxDepth))
                {
                    sb.Append(BuildTreeText(
                        node.Children,
                        maxDepth,
                        maxItems,
                        prefix + (isLast ? "   " : "│  ")));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Преобразует дерево в плоский список для CSV.
        /// </summary>
        private List<ProjectNode> FlattenTree(List<ProjectNode> nodes)
        {
            var result = new List<ProjectNode>();
            foreach (var node in nodes)
            {
                result.Add(node);
                result.AddRange(FlattenTree(node.Children));
            }
            return result;
        }
    }
}