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
        /// <summary>
        /// Method to take derivatives of a 1D array with Δx based on the index
        /// </summary>
        /// <param name="inputSlice">input 1D array</param>
        /// <param name="derivativeDistance">distance on each side of the target point to select initial and final points</param>
        /// <returns></returns>
        public static double[] StandardArrayDerivative(double[] inputSlice, int derivativeDistance)
        {
            if (inputSlice.Length / 2 < derivativeDistance)
            {
                throw new ArgumentException("derivativeDistance must be at least less than half the length of inputSlice", nameof(derivativeDistance));
            }
            double[] outputArray = new double[inputSlice.Length];
            for (int i = 0; i < inputSlice.Length; i++)
            {
                double intensityDifference;
                double axisDifference;
                // Edge case where the target point is closer than 1 derivativeDistance to the left edge
                if ( i - derivativeDistance < 0)
                {
                    intensityDifference = inputSlice[i + derivativeDistance] - inputSlice[0];
                    axisDifference = i + derivativeDistance;
                }
                // Edge case where the target point is closer than 1 derivativeDistance to the right edge
                else if (i + derivativeDistance >= inputSlice.Length)
                {
                    intensityDifference = inputSlice[^1] - inputSlice[i - derivativeDistance];
                    axisDifference = inputSlice.Length - i + derivativeDistance - 1;
                }
                // Standard case
                else
                {
                    intensityDifference = inputSlice[i + derivativeDistance] - inputSlice[i - derivativeDistance];
                    axisDifference = 2 * derivativeDistance;
                }
                outputArray[i] = intensityDifference / axisDifference;
            }
            return outputArray;
        }

        /// <summary>
        /// Method for taking a downsampled derivative of data in the SortedDictionary format
        /// This takes a derivative at 1 point for every 2 * derivativeDistance Points
        /// Do not use this method multiple times in a row to take higher derivatives,
        /// use it once if downsampling is required, then use StandardDerivative
        /// This method is likely not parallelizable
        /// </summary>
        /// <param name="filteredData"></param>
        /// <param name="derivativeDistance">distance on each side of the target point to select initial and final points</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static SortedDictionary<int, double> DownsampledSliceDerivative(SortedDictionary<int, double> filteredData, int derivativeDistance)
        {
            int prevIndex = -1;
            int counter = -1;
            SortedDictionary<int, double> derivativePoints = new SortedDictionary<int, double>();
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
                        double derivativeValue = (endValue - startValue) / (2 * derivativeDistance);
                        int derivativeIndex = point.Key - derivativeDistance;
                        derivativePoints.Add(derivativeIndex, derivativeValue);
                    }
                }
                else
                {
                    prevIndex = -1;
                    counter = -1;
                }
            }
            if (derivativePoints.Count == 0)
            {
                throw new Exception("No derivative points could be calculated, derivativeDistance may be too large relative to the data");
            }
            return derivativePoints;
        }
        /// <summary>
        /// Method to take a derivative of data in the SortedDictionary format without downsampling.
        /// This loses a single point at the edge of each coherent packet which may need to be fixed
        /// however, it won't cause problems for the application of peak identification.
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="originalDerivativeDistance">If the data was previously downsampled during differentiation, the derivativeDistance used must be given here</param>
        /// <returns></returns>
        public static SortedDictionary<int, double> StandardDerivative(SortedDictionary<int, double> inputData, int? originalDerivativeDistance = null)
        {
            int prevIndex = -1;
            SortedDictionary<int, double> derivativePoints = new SortedDictionary<int, double>();
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
                // Checks that we're in a packet of evenly spaced data with the anticipated spacing between indices
                if (prevIndex == -1 || prevIndex == point.Key - indexSpacing)
                {
                    prevIndex = point.Key;
                    // Checks that there is a point to the left and right of oneEarlierPoint
                    // the point which we are taking the derivative at.
                    if (oneEarlierPoint.Key != -1 && twoEarlierPoint.Key != -1)
                    {
                        double derivativeValue = (point.Value - twoEarlierPoint.Value) / (point.Key - twoEarlierPoint.Key);
                        int derivativeIndex = oneEarlierPoint.Key;
                        derivativePoints.Add(derivativeIndex, derivativeValue);
                    }
                    // Updates oneEarlierPoint and twoEarlierPoint
                    oneEarlierPoint = point;
                    twoEarlierPoint = oneEarlierPoint;
                }
                // If we're not in a packet of evenly spaced data, resets to begin identifying a new packet
                else
                {
                    prevIndex = -1;
                    oneEarlierPoint = new KeyValuePair<int, double>(-1, 0);
                    twoEarlierPoint = new KeyValuePair<int, double>(-1, 0);
                }
            if (derivativePoints.Count == 0)
                {
                    throw new ArgumentException("No regions of properly spaced data were identified, originalDerivativeDistance is likely incorrect", nameof(originalDerivativeDistance));
                }
            }
            return derivativePoints;
            
        }
        /// <summary>
        /// Smooths a downsampledDerivative using a smoothing deviation scaled with that downsampling
        /// This method is designed to work with downsampled and intensity filtered data 
        /// If a sequential region does not have enough points for the Gaussian smoothing, it is not smoothed and is left alone. 
        /// </summary>
        /// <param name="downsampledDerivative">Input downsampled derivative</param>
        /// <param name="derivativeSmoothingDeviation">Deviation of the Gaussian kernel used to smooth in terms of the original size of the data</param>
        /// <param name="derivativeDistance">Derivitative distance used when calculating the downsampled derivative</param>
        /// <returns></returns>
        public static SortedDictionary<int, double> DownsampledDerivativeSmoothing(SortedDictionary<int, double> downsampledDerivative, double derivativeSmoothingDeviation, int derivativeDistance)
        {
            int prevIndex = -1;
            SortedDictionary<int, double> smoothedDerivativePoints = new SortedDictionary<int, double>();
            List<int> clusterIndexList = new List<int>();
            List<double> clusterValueList = new List<double>();
            bool smoothingAppliedOnce = false;
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
                        double[] derivativeArray = clusterValueList.ToArray();
                        double[] smoothedDerivative = GaussianSmoothing.GaussianSmoothing1D(derivativeArray, derivativeSmoothingDeviation/(1 + 2 * (double)derivativeDistance));
                        for (int i = 0; i < smoothedDerivative.Length; i++)
                        {
                            smoothedDerivativePoints.Add(clusterIndexList[i], smoothedDerivative[i]);
                        }
                        if (!smoothingAppliedOnce)
                        {
                            smoothingAppliedOnce = true;
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
            if (smoothedDerivativePoints.Count == 0)
            {
                throw new ArgumentException("No regions of properly spaced data were identified, derivativeDistance is likely incorrect", nameof(derivativeDistance));
            }
            if (!smoothingAppliedOnce)
            {
                throw new ArgumentException("No sequential regions exist large enough for smoothing to be applied. Smoothing Kernel is too big", nameof(derivativeSmoothingDeviation));
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
            if(intensityThreshold > 1 || intensityThreshold < 0)
            {
                throw new ArgumentException("intensityThreshold must be greater than 0 and less than or equal to 1", nameof(intensityThreshold));
            }
            double maxValue = inputSlice.Max();
            double minValue = inputSlice.Min();
            double minIntensity = Math.Abs(maxValue) > Math.Abs(minValue) ? Math.Abs(maxValue) * intensityThreshold: Math.Abs(minValue) * intensityThreshold;
            SortedDictionary<int, double> intensityFilteredData = new SortedDictionary<int, double>();
            for (int i = 0; i < inputSlice.Length; i++)
            {
                if(Math.Abs(inputSlice[i]) > minIntensity)
                {
                    intensityFilteredData.Add(i, inputSlice[i]);
                }
            }
            return intensityFilteredData;
        }
    }
}
