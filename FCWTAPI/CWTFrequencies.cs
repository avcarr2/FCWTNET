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
        public int Length { get; private set; }
        public int Pnbvoice { get; private set; }
        // Knowing C0 will likely be necessary downstream for knowing the actual frequencies
        public float C0 {  get; private set; }
        public CWTFrequencies(double[] centerFrequencies, int pnbvoice, float c0)
        {
            WaveletCenterFrequencies = centerFrequencies;
            Length = centerFrequencies.Length;
            Pnbvoice = pnbvoice;
            C0 = c0;
        }
        public CWTFrequencies()
        {
            WaveletCenterFrequencies = null; 
        }
        public void ReplaceFrequencies(double[] centerFrequencies)
        {
            WaveletCenterFrequencies = centerFrequencies; 
        }
        public double[] ToMZValues(double calibrationCoefficient)
        {
            // calibration formula: m/z = A/f^2
            if (WaveletCenterFrequencies.Equals(null))
            {
                throw new NullReferenceException("Error: WaveletCenterFrequencies null."); 
            }
            double[] mzValues = new double[WaveletCenterFrequencies.Length];    
            for(int i = 0; i < WaveletCenterFrequencies.Length; i++)
            {
                mzValues[i] = calibrationCoefficient / (WaveletCenterFrequencies[i] * WaveletCenterFrequencies[i]); 
            }
            return mzValues; 
        }
        /// <summary>
        /// Method to get starting and ending indices for a particular frequency range.
        /// This is intentionally greedy in the sense that the index range will always include the desired start and end frequencies
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
        public void CalculateFrequencyFromScale()
        {

        }

    }
}
