using System;
using System.Collections.Generic;
using System.Globalization;

namespace InterpolationApp
{
    public static class DataValidator
    {
        public const int MaxNodes = 10;
        public const double MaxAbsValue = 1000;
        public const int MaxDecimalPlaces = 3;
        public const double MinXDistance = 0.1;

        public static bool TryParseNumber(string s, out double value)
        {
            value = 0;
            if (s == null) return false;
            s = s.Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool HasTooManyDecimals(string s)
        {
            s = s.Replace(',', '.');
            int dotIndex = s.IndexOf('.');
            if (dotIndex < 0) return false;
            string afterDot = s.Substring(dotIndex + 1);
            int eIndex = afterDot.IndexOfAny(new[] { 'e', 'E' });
            if (eIndex >= 0) afterDot = afterDot.Substring(0, eIndex);
            return afterDot.Length > MaxDecimalPlaces;
        }

        public static bool ExceedsMaxAbsValue(double value)
        {
            return Math.Abs(value) > MaxAbsValue;
        }
        
        public static string ValidateCellValue(string val)
        {
            if (string.IsNullOrEmpty(val))
                return "";

            if (!TryParseNumber(val, out double num))
                return "Некоректний формат числа";

            if (HasTooManyDecimals(val))
                return $"Помилка: більше {MaxDecimalPlaces} знаків після коми";

            if (ExceedsMaxAbsValue(num))
                return $"Перевищено ліміт {MaxAbsValue:G6} за модулем";

            return "";
        }

        public static (List<InterpolationPoint> nodes, string error) ValidateAndParseNodes(
            List<(string sx, string sy)> rawRows)
        {
            var nodes = new List<InterpolationPoint>();

            for (int i = 0; i < rawRows.Count; i++)
            {
                string sx = rawRows[i].sx?.Trim();
                string sy = rawRows[i].sy?.Trim();

                if (string.IsNullOrEmpty(sx) && string.IsNullOrEmpty(sy))
                    continue;

                if (string.IsNullOrEmpty(sx) || !TryParseNumber(sx, out double vx))
                    return (null, $"Рядок {i + 1}: некоректне значення X.");

                if (string.IsNullOrEmpty(sy) || !TryParseNumber(sy, out double vy))
                    return (null, $"Рядок {i + 1}: некоректне значення Y.");

                if (HasTooManyDecimals(sx))
                    return (null, $"Рядок {i + 1}: X має більше {MaxDecimalPlaces} знаків після коми.");

                if (HasTooManyDecimals(sy))
                    return (null, $"Рядок {i + 1}: Y має більше {MaxDecimalPlaces} знаків після коми.");

                if (ExceedsMaxAbsValue(vx))
                    return (null, $"Рядок {i + 1}: |X| = {Math.Abs(vx):G6} перевищує допустимий максимум {MaxAbsValue:G6} за модулем.");

                if (ExceedsMaxAbsValue(vy))
                    return (null, $"Рядок {i + 1}: |Y| = {Math.Abs(vy):G6} перевищує допустимий максимум {MaxAbsValue:G6} за модулем.");

                nodes.Add(new InterpolationPoint(vx, vy));
            }

            if (nodes.Count < 2)
                return (null, "Потрібно щонайменше 2 вузли для інтерполяції.");

            if (nodes.Count > MaxNodes)
                return (null, $"Забагато вузлів ({nodes.Count}). Максимум — {MaxNodes}.");

            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    double dist = Math.Abs(nodes[i].X - nodes[j].X);
                    if (dist == 0)
                        return (null, $"Вузли X = {nodes[i].X} та X = {nodes[j].X} збігаються. Всі значення X мають бути унікальними.");
                    if (dist < MinXDistance - 1e-9)
                        return (null, $"Вузли X = {nodes[i].X} та X = {nodes[j].X} занадто близькі (відстань {dist:G4}). Мінімальна відстань — {MinXDistance}.");
                }

            return (nodes, null);
        }

        public static (double value, string error) ValidateTargetX(string rawText)
        {
            string val = rawText?.Trim();

            if (!TryParseNumber(val, out double targetX))
                return (0, "Некоректне значення x.");

            if (HasTooManyDecimals(val))
                return (0, $"x має більше {MaxDecimalPlaces} знаків після коми.");

            if (ExceedsMaxAbsValue(targetX))
                return (0, $"|x| = {Math.Abs(targetX):G6} перевищує допустимий максимум {MaxAbsValue:G6} за модулем.");

            return (targetX, null);
        }

        public static bool IsResultOverflow(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value);
        }

    }
}
