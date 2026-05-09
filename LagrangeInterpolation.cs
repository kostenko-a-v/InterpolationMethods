using System;
using System.Collections.Generic;
namespace InterpolationApp
{
    public class LagrangeInterpolation
    {
        public static double Evaluate(List<InterpolationPoint> inputNodes, double targetX, out long operationCount)
        {
            ValidateNodes(inputNodes);

            int n = inputNodes.Count;
            double result = 0;
            operationCount = 0;

            for (int i = 0; i < n; i++)
            {
                double li = 1.0;

                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;

                    double numerator = targetX - inputNodes[j].X;
                    double denominator = inputNodes[i].X - inputNodes[j].X;

                    if (Math.Abs(denominator) < 1e-15)
                        throw new InvalidOperationException(
                            $"Вузли x[{i}]={inputNodes[i].X} та x[{j}]={inputNodes[j].X} збігаються. " +
                            "Усі вузли інтерполяції мають бути різними.");

                    li *= numerator / denominator;
                    operationCount += 3;
                }

                result += inputNodes[i].Y * li;
                operationCount += 2;
            }

            return result;
        }

        public static Polynomial BuildPolynomial(List<InterpolationPoint> inputNodes)
        {
            ValidateNodes(inputNodes);

            int n = inputNodes.Count;
            Polynomial result = new Polynomial();

            for (int i = 0; i < n; i++)
            {
                Polynomial li = new Polynomial(new double[] { 1.0 });

                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;

                    double denominator = inputNodes[i].X - inputNodes[j].X;

                    if (Math.Abs(denominator) < 1e-15)
                        throw new InvalidOperationException(
                            $"Вузли x[{i}]={inputNodes[i].X} та x[{j}]={inputNodes[j].X} збігаються.");

                    Polynomial factor = new Polynomial(new double[]
                    {
                        -inputNodes[j].X / denominator,
                        1.0 / denominator
                    });

                    li = li * factor;
                }

                result = result + (inputNodes[i].Y * li);
            }

            return result;
        }

        public static InterpolationResult Interpolate(
            List<InterpolationPoint> inputNodes,
            double targetX,
            int plotPointCount = 200)
        {
            ValidateNodes(inputNodes);

            var result = new InterpolationResult
            {
                MethodName = "Метод Лагранжа",
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

            var xValues = new HashSet<double>();
            for (int i = 0; i < inputNodes.Count; i++)
            {
                if (!inputNodes[i].IsValid())
                    throw new ArgumentException($"Вузол {i + 1} має некоректні координати.");

                foreach (double existing in xValues)
                {
                    if (Math.Abs(existing - inputNodes[i].X) < 1e-12)
                        throw new ArgumentException(
                            $"Знайдено дублікат x = {inputNodes[i].X}. " +
                            "Усі x-координати вузлів мають бути різними.");
                }
                xValues.Add(inputNodes[i].X);
            }
        }
    }
}


