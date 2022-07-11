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
        public double[,]? OutputCWT { get; private set; }
        public CWTObject(double[] inputData, int psoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes)
        {
            InputData = inputData;
            Psoctave = psoctave;
            Pendoctave = pendoctave;
            this.Pnbvoice = pnbvoice;
            C0 = c0;
            Nthreads = nthreads;
            Use_Optimization_Schemes = use_optimization_schemes;
            OutputCWT = null;
        }
        /// <summary>
        /// Function to perform the calculation of the CWT and return it as a double[,] in the CWTObject class called OutputCWT
        /// </summary>
        public void PerformCWT()
        {
            float[][] jaggedCWT = FCWTAPI.CWT(InputData, Psoctave, Pendoctave, Pnbvoice, C0, Nthreads, Use_Optimization_Schemes);
            float[,] floatCWT = FCWTAPI.ToTwoDArray(jaggedCWT);
            OutputCWT = FCWTAPI.ConvertFloat2DtoDouble(floatCWT);

        }
        /// <summary>
        /// Function to separate the real and imaginary components of the CWT for complex wavelets
        /// </summary>
        /// <param name="real">If true, returns the real component of the CWT. If false, returns the imaginary component</param>
        /// <returns name="outputArray">double[,] containing the desired component of the CWT</returns>
        /// <exception cref="ArgumentNullException">Throws an error if the CWT has not been preformed yet</exception>
        /// <exception cref="ArgumentException">Throws an error if there are an odd number of rows in OutputCWT</exception>
        public double[,] SplitRealAndImaginary(bool real)
        {
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be preformed before preforming an operation on it");
            }
            if (OutputCWT.GetLength(0) % 2 != 0)
            {
                throw new ArgumentException("Cannot extract Real and Imaginary components from a non complex CWT");
            }
            double[,] outputArray = new double[OutputCWT.GetLength(0) / 2, OutputCWT.GetLength(1)];
            for (int i = 0; i < OutputCWT.GetLength(0); i++)
            {
                if (real && i % 2 == 0)
                {
                    for (int j = 0; j < OutputCWT.GetLength(1); j++)
                    {
                        outputArray[i / 2, j] = OutputCWT[i, j];
                    }
                }
                else if (!real && i % 2 == 1)
                {
                    for (int j = 0; j < OutputCWT.GetLength(1); j++)
                    {
                        outputArray[(i - 1) / 2, j] = OutputCWT[i, j];
                    }
                }

            }
            return outputArray;
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
                throw new ArgumentNullException("CWT must be preformed before preforming an operation on it");
            }
            if (OutputCWT.GetLength(0) % 2 != 0)
            {
                throw new ArgumentException("Cannot extract Real and Imaginary components from a non complex CWT");
            }
            double[,] outputArray = new double[OutputCWT.GetLength(0) / 2, OutputCWT.GetLength(1)];
            for (int i = 0; i < OutputCWT.GetLength(0) / 2; i++)
            {
                for (int j = 0; j < OutputCWT.GetLength(1); j++)
                {
                    double modPoint = Math.Sqrt(OutputCWT[2 * i, j] * OutputCWT[2 * i, j] + OutputCWT[2 * i + 1, j] * OutputCWT[2 * i + 1, j]);
                    outputArray[i , j] = modPoint;
                }
                
            }
            return outputArray;
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
                throw new ArgumentNullException("CWT must be preformed before preforming an operation on it");
            }
            if (OutputCWT.GetLength(0) % 2 != 0)
            {
                throw new ArgumentException("Cannot extract Real and Imaginary components from a non complex CWT");
            }
            double[,] outputArray = new double[OutputCWT.GetLength(0) / 2, OutputCWT.GetLength(1)];
            for (int i = 0; i < OutputCWT.GetLength(0) / 2; i++)
            {
                for (int j = 0; j < OutputCWT.GetLength(1); j++)
                {
                    double realImRatio = OutputCWT[2 * i + 1, j] / OutputCWT[2 * i, j];
                    outputArray[i, j] = Math.Atan(realImRatio);
                }

            }
            return outputArray;
        }

    }
}
