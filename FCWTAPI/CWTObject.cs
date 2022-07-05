using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWT.NET;

namespace FCWT.NET
{
    public class CWTObject
    {
        public double[] inputData { get; }
        public int psoctave { get; }
        public int pendoctave { get; }
        public int pnbvoice { get; }
        public float c0 { get; }
        public int nthreads { get; }
        public bool use_optimization_schemes { get; }
        public float[,] outputCWT { get; private set; }
        public CWTObject(double[] inputData, int psoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes)
        {
            this.inputData = inputData;
            this.psoctave = psoctave;
            this.pendoctave = pendoctave;
            this.pnbvoice = pnbvoice;
            this.c0 = c0;
            this.nthreads = nthreads;
            this.use_optimization_schemes = use_optimization_schemes;
            this.outputCWT = FCWTAPI.ToTwoDArray(FCWTAPI.CWT(inputData, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes));
        }

    }
}
