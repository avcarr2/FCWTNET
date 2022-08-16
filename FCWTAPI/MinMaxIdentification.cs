using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;

namespace FCWTNET
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
        public static double[] StandardSliceDerivative(double[] inputSlice, int derivativeDistance)
        {
            double[] outputArray = new double[inputSlice.Length];
            for (int i = 0; i < inputSlice.Length; i++)
            {
                double intensityDifference;
                double axisDifference;

                if ( i - derivativeDistance < 0)
                {
                    intensityDifference = inputSlice[i + derivativeDistance] - inputSlice[0];
                    axisDifference = i + derivativeDistance;
                }
                else if(i + derivativeDistance >= inputSlice.Length)
                {
                    intensityDifference = inputSlice[^1] - inputSlice[i - derivativeDistance];
                    axisDifference = inputSlice.Length - i + derivativeDistance - 1;
                }
                else
                {
                    intensityDifference = inputSlice[i + derivativeDistance] - inputSlice[i - derivativeDistance];
                    axisDifference = 2 * derivativeDistance;
                }
                outputArray[i] = intensityDifference / axisDifference;
            }
            return outputArray;
        }
        // This method is likely not parallelizable
        // A parallelizable implementation may exist, but it seems to me that its not really feasable
        public static SortedDictionary<int, double> DownsampledSliceDerivitive(SortedDictionary<int, double> filteredData, int derivativeDistance)
        {
            int prevIndex = -1;
            int counter = -1;
            SortedDictionary<int, double> derivitivePoints = new SortedDictionary<int, double>();
            foreach (var point in filteredData)
            {
                if (prevIndex == -1 || prevIndex == point.Key - 1)
                {
                    counter++;
                    prevIndex = point.Key;
                    if (counter % (2 * derivativeDistance + 1) == 2 * derivativeDistance)
                    {
                        double startValue = filteredData[point.Key - (2 * derivativeDistance)];
                        double endValue = point.Value;
                        double derivitiveValue = (endValue - startValue) / (2 * derivativeDistance);
                        int derivitiveIndex = point.Key - derivativeDistance;
                        derivitivePoints.Add(derivitiveIndex, derivitiveValue);
                    }
                }
                else
                {
                    prevIndex = -1;
                    counter = -1;
                }
            }
            return derivitivePoints;
        }
        public static SortedDictionary<int, double> StandardDerivative(SortedDictionary<int, double> inputData, int? originalDerivativeDistance = null)
        {
            int prevIndex = -1;
            SortedDictionary<int, double> derivitivePoints = new SortedDictionary<int, double>();
            int indexSpacing;
            KeyValuePair<int, double> oneEarlierPoint = new KeyValuePair<int, double>(-1, 0);
            KeyValuePair<int, double> twoEarlierPoint = new KeyValuePair<int, double>(-1, 0);
            if (!originalDerivativeDistance.HasValue)
            {
                indexSpacing = 1;
            }
            else
            {
                indexSpacing = (originalDerivativeDistance.Value * 2) + 1;
            }
            foreach(var point in inputData)
            {
                // Checks that we're still in the same packet
                if (prevIndex == -1 || prevIndex == point.Key - indexSpacing)
                {
                    if (oneEarlierPoint.Key != -1 && twoEarlierPoint.Key != -1)
                    {
                        double derivitiveValue = (point.Value - twoEarlierPoint.Value) / (point.Key - twoEarlierPoint.Key);
                        int derivitiveIndex = oneEarlierPoint.Key;
                        derivitivePoints.Add(derivitiveIndex, derivitiveValue);
                    }
                    oneEarlierPoint = point;
                    twoEarlierPoint = oneEarlierPoint;
                }
                else
                {
                    prevIndex = -1;
                    oneEarlierPoint = new KeyValuePair<int, double>(-1, 0);
                    twoEarlierPoint = new KeyValuePair<int, double>(-1, 0);
                }
            }
            return derivitivePoints;
            
        }
        public static SortedDictionary<int, double> DownsampledDerivitiveSmoothing(SortedDictionary<int, double> downsampledDerivative, double derivativeSmoothingDeviation, int derivativeDistance)
        {
            int prevIndex = -1;
            SortedDictionary<int, double> smoothedDerivativePoints = new SortedDictionary<int, double>();
            List<int> clusterIndexList = new List<int>();
            List<double> clusterValueList = new List<double>();
            foreach(var point in downsampledDerivative)
            {
                if ((prevIndex == -1 || prevIndex == point.Key - (2 * derivativeDistance + 1)) && point.Key != downsampledDerivative.Keys.Last())
                {
                    clusterIndexList.Add(point.Key);
                    clusterValueList.Add(point.Value);
                    prevIndex = point.Key;
                }
                else
                {
                    if(clusterIndexList.Count > (6 * (derivativeSmoothingDeviation/ (1 + 2 * (double)derivativeDistance))) + 1)
                    {
                        double[] derivitiveArray = clusterValueList.ToArray();
                        double[] smoothedDerivative = GaussianSmoothing.GaussianSmoothing1D(derivitiveArray, derivativeSmoothingDeviation/(1 + 2 * (double)derivativeDistance));
                        for (int i = 0; i < smoothedDerivative.Length; i++)
                        {
                            smoothedDerivativePoints.Add(clusterIndexList[i], smoothedDerivative[i]);
                        }
                    }
                    else
                    {
                        for(int i = 0; i < clusterIndexList.Count; i++)
                        {
                            smoothedDerivativePoints.Add(clusterIndexList[i], clusterValueList[i]);
                        }
                    }
                    clusterIndexList = new List<int>();
                    clusterValueList = new List<double>();
                    prevIndex = -1;
                }
            }
            return smoothedDerivativePoints;
        }
        public int[] FrequencySliceMaxIdentification(double[] inputSlice, int derivativeDistance, double derivativeSmoothingDeviation, int peakWidth, double intensityThreshold)
        {
            throw new NotImplementedException();
            //double[] derivativeSlice = StandardSliceDerivative(inputSlice, derivativeDistance);
            //double[] smoothedDerivative = GaussianSmoothing.GaussianSmoothing1D(derivativeSlice, derivativeSmoothingDeviation);
            //for (int i = 0; i < inputSlice.Length - 1; i++)
            //{
            //    if (smoothedDerivative[i] > 0 && smoothedDerivative[i + 1] < 0 && inputSlice[i] > minIntensity)
            //    {

            //    }
            //}
            
        }
        public static SortedDictionary<int, double> IndexLinkedIntensityFiltering(double[] inputSlice, double intensityThreshold)
        {
            double minIntensity = inputSlice.Max() * intensityThreshold;
            SortedDictionary<int, double> intensityFilteredData = new SortedDictionary<int, double>();
            for (int i = 0; i < inputSlice.Length; i++)
            {
                if(inputSlice[i] > minIntensity)
                {
                    intensityFilteredData.Add(i, inputSlice[i]);
                }
            }
            return intensityFilteredData;
        }
    }
}
