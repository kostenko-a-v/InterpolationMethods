using System;
using System.Collections.Generic;

namespace InterpolationApp
{
    public class InterpolationResult
    {
        public string MethodName { get; set; }
        public Polynomial Polynomial { get; set; }
        public double InterpolatedValue { get; set; }
        public double TargetX { get; set; }
        public List<InterpolationPoint> InputNodes { get; set; }

        public long OperationCount { get; set; }
        public string TheoreticalComplexity { get; set; }

        public InterpolationResult()
        {
            InputNodes = new List<InterpolationPoint>();

        }
    }
}


