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

        public CWTOutput(double[][] real, double[][] imag)
        {
            RealArray = FCWTAPI.ToTwoDArray(real);
            ImagArray = FCWTAPI.ToTwoDArray(imag); 
        }
    }
    public static class CWTOutputExtensions
    {
        public static int GetLength(this CWTOutput cwtoutput, int dimension)
        {
            switch (dimension)
            {
                case 0: return cwtoutput.RealArray.GetLength(0);
                case 1: return cwtoutput.RealArray.GetLength(1);
                default: return -1; 
            }
        }
    }
}
