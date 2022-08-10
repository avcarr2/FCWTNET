using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCWTNET
{
    public static class CWTExtensions
    {
        public static double GetFrequencyAtIndex(this CWTObject cwt, int index)
        {
            if (!cwt.Equals(null))
            {
                return cwt.FrequencyAxis.WaveletCenterFrequencies[index];
            }
            else
            {
                throw new NullReferenceException("cwt frequency axis is null");
            }
        }
        /// <summary>
        /// Method for compressing CWT features along the time axis.
        /// Compresses the original double[,] to be the largest array with less columns than maxArrayColumns
        /// with the same number of rows as the original array.
        /// </summary>
        /// <param name="data">Original double[,]</param>
        /// <param name="compressedData">Output double[,]</param>
        /// <param name="maxArrayColumns">Maximum number of columns in the output array</param>
        public static void Time2DArrayCompression(double[,] data, out double[,] compressedData, int maxArrayColumns)
        {
            if (maxArrayColumns <= 0)
            {
                throw new ArgumentException("maxArrayColumns must be a positive integer", nameof(maxArrayColumns));
            }
            int compressionRatio = (int)Math.Ceiling(data.GetLength(1) / (double)maxArrayColumns);
            int subDvisionCount = (int)Math.Ceiling((double)data.GetLength(1) / compressionRatio);
            compressedData = new double[data.GetLength(0), subDvisionCount];
            for (int i = 0; i < data.GetLength(0); i++)
            {
                for (int j = 0; j < subDvisionCount - 1; j++)
                {
                    double pointSum = 0;
                    for (int k = 0; k < compressionRatio; k++)
                    {
                        pointSum += data[i, compressionRatio * j + k];
                    }
                    compressedData[i, j] = pointSum / compressionRatio;
                }
                double finalPointSum = 0;
                double finalPointCount = 0;
                for (int j = (subDvisionCount - 1) * compressionRatio; j < data.GetLength(1); j++)
                {
                    finalPointSum += data[i, j];
                    finalPointCount += 1;
                }
                compressedData[i, compressedData.GetLength(1) - 1] = finalPointSum / finalPointCount;
            }
        }
        /// <summary>
        /// Method for compressing CWT time axis.
        /// Compresses the original double[] to be the largest array with less elements than maxArrayColumns
        /// </summary>
        /// <param name="data">Original double[]</param>
        /// <param name="compressedData">Output double[]</param>
        /// <param name="maxArrayColumns">Maximum number of elements in the output array</param>
        public static void TimeAxisCompression(double[] data, out double[] compressedData, int maxArrayColumns)
        {
            if (maxArrayColumns <= 0)
            {
                throw new ArgumentException("maxArrayColumns must be a positive integer", nameof(maxArrayColumns));
            }
            int compressionRatio = (int)Math.Ceiling(data.Length / (double)maxArrayColumns);
            int subDvisionCount = (int)Math.Ceiling((double)data.Length / compressionRatio);
            compressedData = new double[subDvisionCount];
            for (int j = 0; j < subDvisionCount - 1; j++)
            {
                double pointSum = 0;
                for (int k = 0; k < compressionRatio; k++)
                {
                    pointSum += data[compressionRatio * j + k];
                }
                compressedData[j] = pointSum / compressionRatio;
            }
            double finalPointSum = 0;
            double finalPointCount = 0;
            for (int j = (subDvisionCount - 1) * compressionRatio; j < data.Length; j++)
            {
                finalPointSum += data[j];
                finalPointCount += 1;
            }
            compressedData[compressedData.Length - 1] = finalPointSum / finalPointCount;
        }
        public static void TimeWindowing(double startTime, double endTime, double[] timeAxis, double[,] data,
            out double[] windowedTimeAxis, out double[,] windowedData)
        {
            if (startTime > endTime)
            {
                throw new ArgumentException("endTime must be greater than startTime", nameof(startTime));
            }
            if (startTime < timeAxis[0] || endTime > timeAxis[^1])
            {
                throw new ArgumentOutOfRangeException("Start and end times must be within the time range of the data");
            }
            if (data.GetLength(1) != timeAxis.Length)
            {
                throw new ArgumentException("timeAxis length must match the number of columns in the data", nameof(timeAxis));
            }
            int rawStartIndex = Array.BinarySearch(timeAxis, startTime);
            int rawEndIndex = Array.BinarySearch(timeAxis, endTime);
            int startIndex = rawStartIndex < 0 ? -rawStartIndex - 1 : rawStartIndex;
            int endIndex = rawEndIndex < 0 ? -rawEndIndex - 1 : rawEndIndex;
            int indexDifference = endIndex - startIndex;
            windowedData = new double[data.GetLength(0), indexDifference + 1];
            windowedTimeAxis = new double[indexDifference + 1];
            for (int j = 0; j < indexDifference + 1; j++)
            {
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    windowedData[i, j] = data[i, startIndex + j];                    
                }
                windowedTimeAxis[j] = timeAxis[startIndex + j];
            }
        }
    }
}
