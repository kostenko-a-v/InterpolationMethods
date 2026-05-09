using System;
using System.Collections.Generic;
namespace InterpolationApp
{
    public class AitkenInterpolation
    {
        public static double Evaluate(List<InterpolationPoint> inputNodes, double targetX, out long operationCount)
        {
            ValidateNodes(inputNodes);
            int n = inputNodes.Count;
            operationCount = 0;
            double[,] P = new double[n, n];

            for (int i = 0; i < n; i++)
                P[i, 0] = inputNodes[i].Y;

            for (int j = 1; j < n; j++)
            {
                for (int i = 0; i < n - j; i++)
                {
                    double xi = inputNodes[i].X;
                    double xij = inputNodes[i + j].X;
                    double denominator = xij - xi;
                    if (Math.Abs(denominator) < 1e-15)
                        throw new InvalidOperationException(
                            $"Вузли x[{i}]={xi} та x[{i+j}]={xij} збігаються.");

                    P[i, j] = ((targetX - xi) * P[i + 1, j - 1] - (targetX - xij) * P[i, j - 1]) / denominator;
                    operationCount += 6;
                }
            }
            return P[0, n - 1];
        }

        public static double[,] GetAitkenTable(List<InterpolationPoint> inputNodes, double targetX)
        {
            ValidateNodes(inputNodes);
            int n = inputNodes.Count;
            double[,] P = new double[n, n];

            for (int i = 0; i < n; i++)
                P[i, 0] = inputNodes[i].Y;

            for (int j = 1; j < n; j++)
                for (int i = 0; i < n - j; i++)
                {
                    double xi = inputNodes[i].X;
                    double xij = inputNodes[i + j].X;
                    double denominator = xij - xi;
                    if (Math.Abs(denominator) < 1e-15)
                        throw new InvalidOperationException(
                            $"Вузли x[{i}]={xi} та x[{i+j}]={xij} збігаються.");
                    P[i, j] = ((targetX - xi) * P[i + 1, j - 1] - (targetX - xij) * P[i, j - 1]) / denominator;
                }
            return P;
        }

        public static Polynomial BuildPolynomial(List<InterpolationPoint> inputNodes)
        {
            ValidateNodes(inputNodes);
            int n = inputNodes.Count;
            Polynomial[,] P = new Polynomial[n, n];

            for (int i = 0; i < n; i++)
                P[i, 0] = new Polynomial(new double[] { inputNodes[i].Y });

            for (int j = 1; j < n; j++)
                for (int i = 0; i < n - j; i++)
                {
                    double xi = inputNodes[i].X;
                    double xij = inputNodes[i + j].X;
                    double denominator = xij - xi;
                    if (Math.Abs(denominator) < 1e-15)
                        throw new InvalidOperationException(
                            $"Вузли x[{i}]={xi} та x[{i+j}]={xij} збігаються.");

                    var xMinusXi = new Polynomial(new double[] { -xi, 1.0 });
                    var xMinusXij = new Polynomial(new double[] { -xij, 1.0 });
                    var numer = (xMinusXi * P[i + 1, j - 1]) - (xMinusXij * P[i, j - 1]);
                    P[i, j] = (1.0 / denominator) * numer;
                }
            return P[0, n - 1];
        }

        public static InterpolationResult Interpolate(
            List<InterpolationPoint> inputNodes, double targetX, int plotPointCount = 200)
        {
            ValidateNodes(inputNodes);
            var result = new InterpolationResult
            {
                MethodName = "Схема Ейткена",
                InputNodes = new List<InterpolationPoint>(inputNodes),
                TargetX = targetX,
                TheoreticalComplexity = "O(n²)"
            };

            result.InterpolatedValue = Evaluate(inputNodes, targetX, out long opCount);
            result.OperationCount = opCount;
            result.Polynomial = BuildPolynomial(inputNodes);
            return result;
        }

        private static void ValidateNodes(List<InterpolationPoint> inputNodes)
        {
            if (inputNodes == null || inputNodes.Count == 0)
                throw new ArgumentException("Список вузлів інтерполяції не може бути порожнім.");
            if (inputNodes.Count < 2)
                throw new ArgumentException("Для інтерполяції потрібно щонайменше 2 вузли.");

            var xSet = new HashSet<double>();
            for (int i = 0; i < inputNodes.Count; i++)
            {
                if (!inputNodes[i].IsValid())
                    throw new ArgumentException($"Вузол {i+1} має некоректні координати.");
                foreach (double ex in xSet)
                    if (Math.Abs(ex - inputNodes[i].X) < 1e-12)
                        throw new ArgumentException(
                            $"Знайдено дублікат x = {inputNodes[i].X}. Усі x-координати мають бути різними.");
                xSet.Add(inputNodes[i].X);
            }
        }
    }
}
