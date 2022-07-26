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
        public CWTFrequencies(double[] centerFrequencies, int pnbvoice)
        {
            WaveletCenterFrequencies = centerFrequencies;
            Length = centerFrequencies.Length;
            Pnbvoice = pnbvoice;
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
        public (int, int) CalculateIndicesForFrequencyRange(double startFrequency, double endFrequency)
        {          

            if (WaveletCenterFrequencies == null)
            {
                throw new ArgumentNullException(nameof(WaveletCenterFrequencies), "WaveletCenterFrequencies cannot be null");
            }
            if (WaveletCenterFrequencies[0] > startFrequency)
            {
                throw new ArgumentException(nameof(startFrequency), "startFrequency cannot be less than the minimum frequency");
            }
            if (startFrequency >= endFrequency)
            {
                throw new ArgumentException(nameof(endFrequency), "endFrequency must be greater than startFrequency");
            }
            if (WaveletCenterFrequencies[^1] < endFrequency)
            {
                throw new ArgumentException(nameof(endFrequency), "endFrequency must not be greater than the maximum CWT frequency");
            }
            if (WaveletCenterFrequencies[^2] <= startFrequency)
            {
                return (WaveletCenterFrequencies.Length - 2, WaveletCenterFrequencies.Length - 1);
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
                int numFreqs = Convert.ToInt32(Math.Ceiling(Math.Log2(endFrequency / axisStartFrequency) / deltaA));
                int axisEndIndex = axisStartIndex + numFreqs;
                return (axisStartIndex, axisEndIndex);
            }
        }
        public void CalculateFrequencyFromScale()
        {

        }

    }
}
