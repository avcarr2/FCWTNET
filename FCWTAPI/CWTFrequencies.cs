using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCWTNET
{
    public class CWTFrequencies
    {
        public double[]? WaveletCenterFrequencies { get; private set; }
        public double[]? TrueFrequencies { get; private set; }
        public double[]? MZValues { get; private set; }
        public int Length { get; private set; }
        public int Pnbvoice { get; private set; }
        public int? SamplingRate { get; private set; }
        public double? CalibrationCoefficient { get; private set; }
        public CWTFrequencies(double[] centerFrequencies, int pnbvoice, int? samplingRate = null, double? calibrationCoefficient = null)
        {
            WaveletCenterFrequencies = centerFrequencies;
            Length = centerFrequencies.Length;
            Pnbvoice = pnbvoice;
            SamplingRate = samplingRate;
            CalibrationCoefficient = calibrationCoefficient;
        }
        public CWTFrequencies()
        {
            WaveletCenterFrequencies = null; 
        }
        private void ReplaceFrequencies(double[] inputFrequencies, FrequencyUnits frequencyUnit)
        {
            switch (frequencyUnit)
            {
                case FrequencyUnits.WaveletFrequency:
                    WaveletCenterFrequencies = inputFrequencies;
                    break;
                case FrequencyUnits.TrueFrequency:
                    TrueFrequencies = inputFrequencies;
                    break ;
                case FrequencyUnits.MZValues:
                    MZValues = inputFrequencies;
                    break;
            }
            
        }
        public double TrueFreqToWaveletFreq(double trueFrequency)
        {
            if (SamplingRate.Equals(null))
            {
                throw new NullReferenceException("Error: SamplingRate is null");
            }
            return trueFrequency / (double)SamplingRate;
        }
        public double MZValueToWaveletFreq(double mzValue)
        {
            if (CalibrationCoefficient.Equals(null))
            {
                throw new NullReferenceException("Error: TrueFrequencies is null.");
            }
            double trueFrequency = Math.Sqrt((double)CalibrationCoefficient / mzValue);
            return TrueFreqToWaveletFreq(trueFrequency);
        }
        public void CalculateMZValues()
        {
            // calibration formula: m/z = A/f^2
            if (TrueFrequencies.Equals(null))
            {
                throw new NullReferenceException("Error: TrueFrequencies is null."); 
            }
            if (CalibrationCoefficient.Equals(null))
            {
                throw new NullReferenceException("Error: CalibrationCoefficient is null.");
            }
            double[] mzValues = new double[TrueFrequencies.Length];    
            for(int i = 0; i < TrueFrequencies.Length; i++)
            {
                mzValues[i] = (double)CalibrationCoefficient / (TrueFrequencies[i] * TrueFrequencies[i]); 
            }
            ReplaceFrequencies(mzValues, FrequencyUnits.MZValues);

        }
        public void CalculateTrueFrequencies()
        {
            // conversion is SamplingRate * WaveletFrequency based on fCWT readme 
            if (WaveletCenterFrequencies.Equals(null))
            {
                throw new NullReferenceException("Error: WaveletCenterFrequencies is null");
            }
            if (SamplingRate.Equals(null))
            {
                throw new NullReferenceException("Error: SamplingRate is null");
            }
            double[] trueFrequencies = new double[WaveletCenterFrequencies.Length];
            for ( int i = 0; i < WaveletCenterFrequencies.Length; i++)
            {
                trueFrequencies[i] = (double)SamplingRate * WaveletCenterFrequencies[i];
            }
            ReplaceFrequencies(trueFrequencies, FrequencyUnits.TrueFrequency);
        }
        /// <summary>
        /// Method to get starting and ending indices for a particular frequency range.
        /// This is intentionally greedy in the sense that the index range will always include the desired start and end frequencies
        /// This method can only be used when working with wavelet frequencies 
        /// </summary>
        /// <param name="startFrequency">Desired starting frequency</param>
        /// <param name="endFrequency">Desired ending frequency</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public (int, int) CalculateIndicesForFrequencyRange(double startFrequency, double endFrequency)
        {          

            if (WaveletCenterFrequencies == null)
            {
                throw new NullReferenceException("WaveletCenterFrequencies is null");
            }
            if (WaveletCenterFrequencies[0] > startFrequency)
            {
                throw new ArgumentException("startFrequency cannot be less than the minimum frequency", nameof(startFrequency));
            }
            if (startFrequency >= endFrequency)
            {
                throw new ArgumentException("endFrequency must be greater than startFrequency", nameof(endFrequency));
            }
            if (WaveletCenterFrequencies[^1] < endFrequency)
            {
                throw new ArgumentException("endFrequency must not be greater than the maximum CWT frequency", nameof(endFrequency));
            }
            else
            {
                int rawStartIndex = Array.BinarySearch(WaveletCenterFrequencies, startFrequency);
                double axisStartFrequency;
                int axisStartIndex;
                int positiveStartIndex;
                if (rawStartIndex < 0)
                {
                    positiveStartIndex = rawStartIndex * -1 - 1;
                }
                else
                {
                    positiveStartIndex = rawStartIndex;
                }

                if (WaveletCenterFrequencies[positiveStartIndex] > startFrequency)
                {
                    axisStartIndex = positiveStartIndex - 1;
                }
                else
                {
                    axisStartIndex = positiveStartIndex;
                }
                axisStartFrequency = WaveletCenterFrequencies[positiveStartIndex];
                double deltaA = 1 / Convert.ToDouble(Pnbvoice);
                int numFreqs = 1 + Convert.ToInt32(Math.Ceiling(Math.Log2(endFrequency / axisStartFrequency) / deltaA));
                int axisEndIndex = axisStartIndex + numFreqs;
                return (axisStartIndex, axisEndIndex);
            }
        }
        public (int, int) CalculateIndicesForFrequencyRange(double startValue, double endValue, FrequencyUnits frequencyUnit)
        {
            double startFrequency;
            double endFrequency;
            switch (frequencyUnit)
            {
                case FrequencyUnits.TrueFrequency:    
                    startFrequency = TrueFreqToWaveletFreq(startValue);
                    endFrequency = TrueFreqToWaveletFreq(endValue);
                    break;
                case FrequencyUnits.MZValues:
                    if (startValue <= endValue)
                    {
                        throw new ArgumentException("startValue must be > endValue when using m/z values", nameof(startValue));
                    }
                    startFrequency = MZValueToWaveletFreq(startValue);
                    endFrequency = MZValueToWaveletFreq(endValue);
                    break;
                // default case is where frequencyUnit is wavelet frequencies
                default:
                    startFrequency = startValue;
                    endFrequency = endValue;
                    break;
            }
            return CalculateIndicesForFrequencyRange(startFrequency, endFrequency);
        }

        public void FrequencyWindowing(double startFrequency, double endFrequency, double[] plotFrequencyAxis,
            double[,] data, FrequencyUnits frequencyUnits, out double[] windowedFrequencyAxis, out double[,] windowedData)
        {
            var frequencyIndexRange = CalculateIndicesForFrequencyRange(startFrequency, endFrequency, frequencyUnits);
            int indexDifference = frequencyIndexRange.Item2 - frequencyIndexRange.Item1;
            windowedData = new double[indexDifference + 1, data.GetLength(1)];
            windowedFrequencyAxis = new double[indexDifference + 1];

            for (int i = 0; i < indexDifference + 1; i++)
            {
                for (int j = 0; j < data.GetLength(1); j++)
                {
                    windowedData[i, j] = data[frequencyIndexRange.Item1 + i, j];
                }
                windowedFrequencyAxis[i] = plotFrequencyAxis[frequencyIndexRange.Item1 + i];
            }
        }
        public enum FrequencyUnits
        {
            WaveletFrequency,
            TrueFrequency,
            MZValues
        }
    }
}
