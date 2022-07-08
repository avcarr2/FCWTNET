using System;

namespace FCWTNET
{
    public static class FunctionGenerator
    {
        public static void GenerateSineWave(ref double[] vals, double amplitude, double period, double phase)
        {
            for(int i = 0; i < vals.Length; i++)
            {
                vals[i] = amplitude * Math.Sin(period * (vals[i] + phase)); 
            }
        }
        public static double[] TransformValues(double[] vals, Func<double, double> function)
        {
            double[] results = new double[vals.Length];
            for(int i = 0; i < vals.Length; i++)
            {
                results[i] = function(vals[i]); 
            }
            return results; 
        }
        public static double GenerateCosineWave(double value)
        {
            return Math.Cos(value); 
        }
    }
}
