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
        public void SplitRealAndImaginary(CWTComponent comp, out double[,]? realCwt, out double[,]? imagCwt)
        {
            realCwt = null;
            imagCwt = null; 
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before performing an operation on it");
            }
            switch (comp)
            {
                case CWTComponent.Real:
                    realCwt = GetComponent(CWTComponent.Real, OutputCWT); 
                    break;
                case CWTComponent.Imaginary:
                    imagCwt = GetComponent(CWTComponent.Imaginary, OutputCWT);
                    break;
                case CWTComponent.Both:
                    GetBothComponents(OutputCWT, out double[,] real, out double[,] imag);
                    realCwt = real;
                    imagCwt = imag; 
                    break;
                default:
                    break;
            }
        }
        private double[,] GetComponent(CWTComponent comp, double[,] originalArray)
        {
            int originalRowIndexer = 0;
            int rowNumber = originalArray.GetLength(0); 
            int colNumber = originalArray.GetLength(1);
            double[,] outputArray = new double[rowNumber/2, colNumber]; 
            if(comp == CWTComponent.Real)
            {
                originalRowIndexer = 0; 
            }else if(comp == CWTComponent.Imaginary)
            {
                originalRowIndexer = 1; 
            }else if(comp == CWTComponent.Both)
            {
                GetBothComponents(originalArray, out double[,] real, out double[,] imag); 
            }

            // iterate over the output array
            for(int i = 0; i < rowNumber; i++)
            {
                for(int j = 0; j < colNumber; j++)
                {
                    outputArray[i, j] = originalArray[originalRowIndexer, j]; 
                }
                originalRowIndexer += 2; 
            }
            return outputArray; 
        }
        private void GetBothComponents(double[,] originalArray, out double[,] real, 
            out double[,] imag)
        {
            int originalRowIndexer = 0;
            int rowNumber = originalArray.GetLength(0);
            int colNumber = originalArray.GetLength(1);
            real = new double[rowNumber / 2, colNumber];
            imag = new double[rowNumber / 2, colNumber];

            for(int i = 0; i < real.GetLength(0) - 1; i++)
            {
                for(int j = 0; j < colNumber; j++)
                {
                    real[i, j] = originalArray[originalRowIndexer, j]; 
                    imag[i, j] = originalArray[originalRowIndexer + 1, j]; 
                }
                originalRowIndexer += 2; 
            }
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
                throw new ArgumentNullException("CWT must be performed before performing an operation on it");
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
            int originalIndex = 0;
            int outputRowLength = OutputCWT.GetLength(0) / 2;
            double[,] outputArray = new double[outputRowLength, OutputCWT.GetLength(1)];
            for (int i = 0; i < outputRowLength - 1; i++)
            {
                int realRowIndex = originalIndex;
                int imagRowIndex = originalIndex + 1; 
                for (int j = 0; j < OutputCWT.GetLength(1); j++)
                {
                    double realImRatio = OutputCWT[imagRowIndex, j] / OutputCWT[realRowIndex, j];
                    outputArray[i, j] = Math.Atan(realImRatio);
                }
                originalIndex += 2; 
            }
            return outputArray;
        }
        public enum CWTComponent
        {
            Real, 
            Imaginary, 
            Both
        }

    }
}
