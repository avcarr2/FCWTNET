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
        
        public CWTFrequencies(double[] centerFrequencies)
        {
            WaveletCenterFrequencies = centerFrequencies;
            Length = centerFrequencies.Length;
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
        public void CalculateFrequencyFromScale()
        {

        }

    }
}
