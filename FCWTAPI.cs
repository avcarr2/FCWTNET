using System.Runtime.InteropServices; 
namespace FCWT.NET
{
    public class FCWTAPI
    {
        [DllImport("fCWT.dll", EntryPoint = "?cwt@fcwt@@YAXPEAMH0HHHMH_N@Z")]
        private static extern void _cwt([In] float[] input, int inputsize, [In,Out] float[] output, 
            int pstoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes);
        
        public static float[][] CWT(float[] input, int psoctave, int pendoctave, 
            int pnbvoice, float c0, int nthreads, bool use_optimization_schemes)
        {
            int inputSize = input.Length;
            int noctaves = pendoctave - psoctave + 1; 
            float[] output = GenerateOutputArray(inputSize, noctaves, pnbvoice); 
            _cwt(input, inputSize, output, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes);
            float[][] results = FixOutputArray(output, inputSize, noctaves, pnbvoice); 
            return results;
        }
        public static float[][] CWT(double[] input, int psoctave, int pendoctave, 
            int pnbvoice, float c0, int nthreads, bool use_optimization_schemes)
        {
            float[] fInput = ConvertDoubleToFloat(input);
            return CWT(fInput, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes);
        }
        public static float[] ConvertDoubleToFloat(double[] dArray)
        {
            float[] fArray = new float[dArray.Length];   
            for(int i = 0; i < dArray.Length; i++)
            {
                fArray[i] = ToFloat(dArray[i]);
            }
            return fArray; 
        }
        private static float ToFloat(double value)
        {
            return (float)value; 
        }
        private static float[] GenerateOutputArray(int size, int noctave, int nvoice)
        {
            return new float[size * noctave * nvoice * 2]; 
        }
        public static float[][] FixOutputArray(float[] array1D, int size, int noctave, int nvoice)
        {
            // from the original fCWT library code 
            int numberRows = noctave * nvoice * 2; 

            float[][] fixedResults = new float[numberRows][];
            
            for(int i = 0; i < numberRows; i++)
            {
                float[] temp = new float[size]; 
                for(int j = 0; j < size; j++)
                {
                    temp[j] = array1D[i + j]; 
                }
                fixedResults[i] = temp;
            }
            return fixedResults;
        }
        
    }
}