using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;

namespace FCWTNET
{
    public class CWTObject
    {
        public double[] InputData { get; }
        public int Psoctave { get; }
        public int Pendoctave { get; }
        public int Pnbvoice { get; }
        public float C0 { get; }
        public int Nthreads { get; }
        public bool Use_Optimization_Schemes { get; }
        public float[,]? OutputCWT { get; private set; }
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
        public void PerformCWT()
        {
            float[][] jaggedCWT = FCWTAPI.CWT(InputData, Psoctave, Pendoctave, Pnbvoice, C0, Nthreads, Use_Optimization_Schemes);
            OutputCWT = FCWTAPI.ToTwoDArray(jaggedCWT);
        }

    }
}
