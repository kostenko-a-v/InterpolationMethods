using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InterpolationApp
{
    public static class FileManager
    {

        public static void SaveResult(InterpolationResult result, string filePath, bool append = false)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Шлях до файлу не може бути порожнім.");

            var sb = new StringBuilder();
            sb.AppendLine($"РЕЗУЛЬТАТИ ІНТЕРПОЛЯЦІЇ — {result.MethodName}");
            sb.AppendLine();

            sb.AppendLine("Вузли інтерполяції:");
            sb.AppendLine($"  {"№",-5} {"x",12} {"y",12}");
            for (int i = 0; i < result.InputNodes.Count; i++)
            {
                var node = result.InputNodes[i];
                sb.AppendLine($"  {i + 1,-5} {node.X,12:G6} {node.Y,12:G6}");
            }
            sb.AppendLine();

            sb.AppendLine("Інтерполяційний поліном (спрощений вигляд):");
            sb.AppendLine($"  P(x) = {result.Polynomial?.ToSimplifiedString() ?? "Не обчислено"}");
            sb.AppendLine();
            sb.AppendLine("Обчислене значення:");
            sb.AppendLine($"  x = {result.TargetX:G7}");
            sb.AppendLine($"  P(x) = {result.InterpolatedValue:G10}");
            sb.AppendLine();
            sb.AppendLine("Складність алгоритму:");
            sb.AppendLine($"  Теоретична складність: {result.TheoreticalComplexity}");
            sb.AppendLine($"  Кількість операцій: {result.OperationCount}");
            sb.AppendLine();

            if (append && File.Exists(filePath))
                File.AppendAllText(filePath, Environment.NewLine + sb.ToString(), Encoding.UTF8);
            else
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static List<InterpolationPoint> LoadNodes(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл не знайдено.", filePath);

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            var nodes = new List<InterpolationPoint>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (i == 0 && (line.StartsWith("x", StringComparison.OrdinalIgnoreCase)))
                    continue;

                string[] parts = line.Split(new[] { ';', ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    throw new FormatException(
                        $"Рядок {i + 1}: очікується формат 'x;y' (числа будуть округлені за математичними правилами), отримано '{line}'.");

                string strX = parts[0].Trim().Replace(',', '.');
                string strY = parts[1].Trim().Replace(',', '.');

                if (!double.TryParse(strX, NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                    throw new FormatException(
                        $"Рядок {i + 1}: не вдалося розпізнати x = '{parts[0]}'.");

                if (!double.TryParse(strY, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                    throw new FormatException(
                        $"Рядок {i + 1}: не вдалося розпізнати y = '{parts[1]}'.");

                x = Math.Round(x, DataValidator.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                y = Math.Round(y, DataValidator.MaxDecimalPlaces, MidpointRounding.AwayFromZero);

                if (DataValidator.ExceedsMaxAbsValue(x))
                    throw new FormatException(
                        $"Рядок {i + 1}: |X| = {Math.Abs(x):G6} перевищує допустимий максимум {DataValidator.MaxAbsValue:G6}.");
                if (DataValidator.ExceedsMaxAbsValue(y))
                    throw new FormatException(
                        $"Рядок {i + 1}: |Y| = {Math.Abs(y):G6} перевищує допустимий максимум {DataValidator.MaxAbsValue:G6}.");

                foreach (var existingNode in nodes)
                {
                    if (existingNode.X == x)
                        throw new InvalidOperationException($"Вузол X = {x} вже існує. Всі значення X мають бути унікальними.");
                }

                nodes.Add(new InterpolationPoint(x, y));

                if (nodes.Count > DataValidator.MaxNodes)
                    throw new InvalidOperationException(
                        $"Файл містить більше {DataValidator.MaxNodes} вузлів. Максимум — {DataValidator.MaxNodes}.");
            }

            if (nodes.Count == 0)
                throw new InvalidOperationException("Файл не містить жодного вузла.");

            return nodes;
        }
    }
}
