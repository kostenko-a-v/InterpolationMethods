namespace InterpolationApp
{
    public class InterpolationPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public InterpolationPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X}; {Y})";
        }

        public bool IsValid()
        {
            return !double.IsNaN(X) && !double.IsInfinity(X)
                && !double.IsNaN(Y) && !double.IsInfinity(Y);
        }
    }
}
