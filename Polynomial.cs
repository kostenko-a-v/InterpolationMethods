using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace InterpolationApp
{
    public class Polynomial
    {
        public double[] Coefficients { get; private set; }
        public int Degree => Coefficients.Length - 1;

        public Polynomial(double[] coefficients)
        {
            if (coefficients == null || coefficients.Length == 0)
                throw new ArgumentException("Масив коефіцієнтів не може бути порожнім.");

            Coefficients = (double[])coefficients.Clone();
            Trim();
        }
        public Polynomial()
        {
            Coefficients = new double[] { 0.0 };
        }
        public Polynomial(int degree)
        {
            if (degree < 0)
                throw new ArgumentException("Степінь полінома не може бути від'ємним.");

            Coefficients = new double[degree + 1];
        }

        private void Trim()
        {
            int lastNonZero = Coefficients.Length - 1;
            while (lastNonZero > 0 && Coefficients[lastNonZero] == 0.0)
            {
                lastNonZero--;
            }

            if (lastNonZero < Coefficients.Length - 1)
            {
                double[] trimmed = new double[lastNonZero + 1];
                Array.Copy(Coefficients, trimmed, lastNonZero + 1);
                Coefficients = trimmed;
            }
        }
        /// <summary>
        /// Обчислює значення полінома в заданій точці x за схемою Горнера.
        /// </summary>
        /// <param name="x">Значення аргументу.</param>
        /// <returns>Значення полінома в точці x.</returns>
        public double Evaluate(double x)
        {
            double result = 0;
            for (int i = Coefficients.Length - 1; i >= 0; i--)
            {
                result = result * x + Coefficients[i];
            }
            return result;
        }
        public static Polynomial operator +(Polynomial a, Polynomial b)
        {
            int maxLen = Math.Max(a.Coefficients.Length, b.Coefficients.Length);
            double[] result = new double[maxLen];

            for (int i = 0; i < maxLen; i++)
            {
                double ca = i < a.Coefficients.Length ? a.Coefficients[i] : 0;
                double cb = i < b.Coefficients.Length ? b.Coefficients[i] : 0;
                result[i] = ca + cb;
            }

            return new Polynomial(result);
        }

        public static Polynomial operator -(Polynomial a, Polynomial b)
        {
            int maxLen = Math.Max(a.Coefficients.Length, b.Coefficients.Length);
            double[] result = new double[maxLen];

            for (int i = 0; i < maxLen; i++)
            {
                double ca = i < a.Coefficients.Length ? a.Coefficients[i] : 0;
                double cb = i < b.Coefficients.Length ? b.Coefficients[i] : 0;
                result[i] = ca - cb;
            }

            return new Polynomial(result);
        }

        public static Polynomial operator *(Polynomial a, Polynomial b)
        {
            int newDegree = a.Degree + b.Degree;
            double[] result = new double[newDegree + 1];

            for (int i = 0; i <= a.Degree; i++)
            {
                for (int j = 0; j <= b.Degree; j++)
                {
                    result[i + j] += a.Coefficients[i] * b.Coefficients[j];
                }
            }

            return new Polynomial(result);
        }

        public static Polynomial operator *(double scalar, Polynomial p)
        {
            double[] result = new double[p.Coefficients.Length];
            for (int i = 0; i < p.Coefficients.Length; i++)
            {
                result[i] = scalar * p.Coefficients[i];
            }
            return new Polynomial(result);
        }

        public static Polynomial operator *(Polynomial p, double scalar)
        {
            return scalar * p;
        }

        public string ToSimplifiedString()
        {
            double maxCoeff = Coefficients.Length > 0 ? Coefficients.Max(c => Math.Abs(c)) : 0;
            if (maxCoeff < 1e-20)
                return "0";

            double noiseThreshold = Math.Max(1e-15, maxCoeff * 1e-12);

            StringBuilder sb = new StringBuilder();
            bool isFirst = true;

            for (int i = Degree; i >= 0; i--)
            {
                double coeff = Coefficients[i];

                if (Math.Abs(coeff) < noiseThreshold)
                    continue;

                double valToFormat = coeff;

                if (isFirst)
                {
                    if (i == 0)
                        sb.Append(FormatNumber(valToFormat));
                    else if (Math.Abs(valToFormat - 1.0) < 1e-12)
                        sb.Append(FormatPower(i));
                    else if (Math.Abs(valToFormat + 1.0) < 1e-12)
                    {
                        sb.Append("-");
                        sb.Append(FormatPower(i));
                    }
                    else
                    {
                        sb.Append(FormatNumber(valToFormat));
                        sb.Append(FormatPower(i));
                    }
                    isFirst = false;
                }
                else
                {
                    if (valToFormat > 0)
                    {
                        sb.Append(" + ");
                        if (i == 0)
                            sb.Append(FormatNumber(valToFormat));
                        else if (Math.Abs(valToFormat - 1.0) < 1e-12)
                            sb.Append(FormatPower(i));
                        else
                        {
                            sb.Append(FormatNumber(valToFormat));
                            sb.Append(FormatPower(i));
                        }
                    }
                    else
                    {
                        sb.Append(" - ");
                        double absVal = Math.Abs(valToFormat);
                        if (i == 0)
                            sb.Append(FormatNumber(absVal));
                        else if (Math.Abs(absVal - 1.0) < 1e-12)
                            sb.Append(FormatPower(i));
                        else
                        {
                            sb.Append(FormatNumber(absVal));
                            sb.Append(FormatPower(i));
                        }
                    }
                }
            }

            return sb.Length == 0 ? "0" : sb.ToString();
        }

        private string FormatNumber(double value)
        {
            return value.ToString("G6", CultureInfo.InvariantCulture);
        }

        private string FormatPower(int power)
        {
            if (power == 0) return "";
            if (power == 1) return "x";
            return $"x^{power}";
        }

        public override string ToString()
        {
            return ToSimplifiedString();
        }
        
        /// <summary>
        /// Перетворює поліном у форматований рядок з використанням Unicode-символів для ступенів.
        /// </summary>
        /// <returns>Рядок, що представляє поліном.</returns>
        public string ToUnicodeString()
        {
            string s = ToSimplifiedString();
            return System.Text.RegularExpressions.Regex.Replace(s, @"\^(\d+)", m =>
            {
                string result = "";
                foreach (char c in m.Groups[1].Value)
                {
                    result += c switch
                    {
                        '0' => '\u2070', '1' => '\u00B9', '2' => '\u00B2', '3' => '\u00B3',
                        '4' => '\u2074', '5' => '\u2075', '6' => '\u2076', '7' => '\u2077',
                        '8' => '\u2078', '9' => '\u2079', _ => c
                    };
                }
                return result;
            });
        }
    }
}


