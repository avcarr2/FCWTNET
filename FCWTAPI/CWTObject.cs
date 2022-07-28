using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;

namespace FCWTNET
{
    /// <summary>
    /// Class to encapsulate the CWT and all relevant parameters
    /// Currently includes:
    /// CWT Calculation
    /// Separation of real and imaginary components of the CWT
    /// Calculation of the modulus of the CWT
    /// Calculation of the phase of the CWT
    /// </summary>
    public class CWTObject
    {
        public double[] InputData { get; }
        public int Psoctave { get; }
        public int Pendoctave { get; }
        public int Pnbvoice { get; }
        public float C0 { get; }
        public int Nthreads { get; }
        public bool Use_Optimization_Schemes { get; }
        public int? SamplingRate { get; }

        public CWTOutput? OutputCWT { get; private set; }
        public CWTFrequencies? FrequencyAxis { get; private set; }
        public double[]? TimeAxis { get; private set; }

        public CWTObject(double[] inputData, int psoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes, int? samplingRate = null)
        {
            InputData = inputData;
            Psoctave = psoctave;
            Pendoctave = pendoctave;
            Pnbvoice = pnbvoice;
            C0 = c0;
            Nthreads = nthreads;
            Use_Optimization_Schemes = use_optimization_schemes;
            SamplingRate = samplingRate;
            OutputCWT = null;
            FrequencyAxis = null;
            TimeAxis = null;
        }
        /// <summary>
        /// Function to perform the calculation of the CWT and return it as a double[,] in the CWTObject class called OutputCWT
        /// </summary>
        public void PerformCWT()
        {
            FCWTAPI.CWT(InputData, Psoctave, Pendoctave, Pnbvoice, C0, Nthreads, Use_Optimization_Schemes, 
                out double[][] real, out double[][] imag);
            OutputCWT = new CWTOutput(real, imag); 
        }
        
        /// <summary>
        /// Method to calculate the modulus of the CWT
        /// </summary>
        /// <returns name="outputArray">double[,] containing the result of the calculation</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public double[,] ModulusCalculation()
        {
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before operating on it");
            }
            int rows = OutputCWT.RealArray.GetLength(0);
            int cols = OutputCWT.ImagArray.GetLength(1); 
            double[,] output = new double[rows, cols]; 
            for(int i = 0; i < rows; i++)
            {
                for(int j = 0; j < cols; j++)
                {
                    output[i, j] = Math.Sqrt(Math.Pow(OutputCWT.RealArray[i, j], 2) + Math.Pow(OutputCWT.ImagArray[i, j], 2)); 
                }
            }
            return output;
        }
        /// <summary>
        /// Method to calculate the phase of the CWT
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public double[,] PhaseCalculation()
        {
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before performing an operation on it");
            }
            int rows = OutputCWT.RealArray.GetLength(0);
            int cols = OutputCWT.RealArray.GetLength(1);

            double[,] output = new double[rows, cols]; 
            for(int i = 0; i < rows; i++)
            {
                for(int j = 0; j < cols; j++)
                {
                    double realImRatio = OutputCWT.RealArray[i,j] / OutputCWT.ImagArray[i,j];
                    output[i, j] = Math.Atan(realImRatio); 
                }
            }
            return output; 
        }
        public enum CWTComponent
        {
            Real, 
            Imaginary, 
            Both
        }
        public void CalculateFrequencyAxis()
        {
            int octaveNum = 1 + Pendoctave - Psoctave;
            double deltaA = 1 / Convert.ToDouble(Pnbvoice);
            double[] freqArray = new double[octaveNum * Pnbvoice];
            for (int i = 0 ; i < octaveNum * Pnbvoice; i++)
            {
                freqArray[i] = C0 / Math.Pow(2, (1 + (i + 1) * deltaA));
            }
            FrequencyAxis = new CWTFrequencies(freqArray);            
        }
        public void CalculateTimeAxis()
        {
            if(SamplingRate == null)
            {
                throw new ArgumentNullException("SamplingRate", "SamplingRate must be provided to calculate a time axis");
            }
            if (SamplingRate <= 0)
            {
                throw new ArgumentException("SamplingRate", "SamplingRate must be a positive, non-zero integer");

            }
            if(OutputCWT == null)
            {
                throw new ArgumentNullException("OutputCWT", "Output CWT must be calculated prior to calculating a time axis for it");
            }
            double [] timeArray = new double[OutputCWT.RealArray.GetLength(1)];
            double timeStep = 1 / (double)SamplingRate;
            double currentTime = 0;
            for (int i = 0; i < OutputCWT.RealArray.GetLength(1); i++)
            {
                timeArray[i] = currentTime;
                currentTime += timeStep;
            }
            TimeAxis = timeArray;
        }
    }
}
