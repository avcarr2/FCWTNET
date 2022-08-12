using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;

namespace fCWT.NET
{
    public class MinMaxIdentification
    {
        public double[,] InputData;
        public double FrequencySmoothingWidth;
        public double TimeSmoothingWidth;
        //public double MinFrequencyWidth;
        //public int? RowSpacings;
        public int TimeDerivativeDistance;
        //public double IntensityThreshold;
        public string WorkingPath;
        public MinMaxIdentification(double[,] inputData, int timeDerivativeDistance, string workingPath)
        {
            InputData = inputData;
            TimeDerivativeDistance = timeDerivativeDistance;
            WorkingPath = workingPath;
        }
        //public static void 
    }
}
