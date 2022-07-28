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
    }
}
