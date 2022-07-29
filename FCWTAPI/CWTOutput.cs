using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCWTNET
{
    public class CWTOutput
    {
        public double[,] RealArray { get; private set; }
        public double[,] ImagArray { get; private set; }
        public int Columns { get; }
        public int Rows { get; }

        public CWTOutput(double[][] real, double[][] imag)
        {
            RealArray = FCWTAPI.ToTwoDArray(real);
            ImagArray = FCWTAPI.ToTwoDArray(imag);
            Columns = RealArray.GetLength(1);
            Rows = RealArray.GetLength(0);
        }

        /// <summary>
        /// Method to calculate the modulus of the CWT
        /// </summary>
        /// <returns name="outputArray">double[,] containing the result of the calculation</returns>
        public double[,] ModulusCalculation()
        {
            double[,] output = new double[Rows, Columns];
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    output[i, j] = Math.Sqrt(Math.Pow(RealArray[i, j], 2) + Math.Pow(ImagArray[i, j], 2));
                }
            }
            return output;
        }
        /// <summary>
        /// Method to calculate the phase of the CWT
        /// </summary>
        /// <returns></returns>
        public double[,] PhaseCalculation()
        {
            double[,] output = new double[Rows, Columns];
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    double realImRatio = ImagArray[i, j] / RealArray[i, j];
                    output[i, j] = Math.Atan(realImRatio);
                }
            }
            return output;
        }
    }
}
