using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCWTNET
{
    public static  class CWTExtensions
    {
        public static double GetFrequencyAtIndex(this CWTObject cwt, int index)
        {
            if (!cwt.Equals(null))
            {
                return cwt.FrequencyAxis.WaveletCenterFrequencies[index];
            }
            else
            {
                throw new NullReferenceException("cwt frequency axis is null"); 
            }
        }
    }
}
