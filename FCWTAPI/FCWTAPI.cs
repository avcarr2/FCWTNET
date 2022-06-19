using System.Runtime.InteropServices; 
namespace FCWT.NET
{
    public class FCWTAPI
    {
        [DllImport("fCWT.dll", EntryPoint = "?cwt@fcwt@@YAXPEAMH0HHHMH_N@Z")]
        private static extern void _cwt([In] float[] input, int inputsize, [In, Out] float[] output,
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
            for (int i = 0; i < dArray.Length; i++)
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

            for (int i = 0; i < numberRows; i++)
            {
                float[] temp = new float[size];
                for (int j = 0; j < size; j++)
                {
                    temp[j] = array1D[i + j];
                }
                fixedResults[i] = temp;
            }
            return fixedResults;
        }
        /// <summary>
        /// Used to split the imaginary and the real arrays when using a complex wavelet, i.e. the morlet. 
        /// </summary>
        /// <param name="combinedArray"></param>
        /// <param name="realArray"></param>
        /// <param name="imaginaryArray"></param>
        public static void SplitIntoRealAndImaginary(float[][] combinedArray, out float[][] realArray, out float[][] imaginaryArray)
        {
            // every other row is the opposite
            int rowNumber = combinedArray.GetLength(0);

            // row number will always be even when using a complex wavelet

            realArray = new float[rowNumber / 2][];
            imaginaryArray = new float[rowNumber / 2][];

            int realIndexer = 0;
            int imagIndexer = 0;
            int combinedIndexer = 0;
            while (combinedIndexer < rowNumber)
            {
                int rowLength = combinedArray[combinedIndexer].Length;
                if (combinedIndexer % 2 == 0)
                {
                    realArray[realIndexer] = new float[rowLength];
                    realArray[realIndexer] = combinedArray[combinedIndexer];
                    realIndexer++;
                }
                else
                {
                    imaginaryArray[imagIndexer] = new float[rowLength];
                    imaginaryArray[imagIndexer] = combinedArray[combinedIndexer];
                    imagIndexer++;
                }
                combinedIndexer++;
            }
        }
        public static float[][] CalculatePhase(float[][] realArray, float[][] imagArray)
        {
            int realRows = realArray.GetLength(0);
            int imagRows = imagArray.GetLength(0);  

            int realCols = realArray.GetLength(1);
            if(realRows != imagRows)
            {
                throw new ArgumentException("Real and imaginary arrays have unequal lengths"); 
            }

            float[][] phaseArray = new float[realRows][]; 

            for(int i = 0; i < realRows; i++)
            {
                float[] temp = new float[realCols]; 
                for(int j =0; j < realCols; j++)
                {
                    float val = imagArray[i][j] / realArray[i][j];
                    double v = (Math.Atan((double)val));
                    temp[j] = 1F / (float)v; 
                }
                phaseArray[i] = temp; 
            }
            return phaseArray; 
        }
        public static float[][] CalculateModulus(float[][] realArray, float[][] imagArray)
        {
            int realRows = realArray.GetLength(0);
            int imagRows = imagArray.GetLength(0);

            int realCols = realArray.GetLength(1);
            if (realRows != imagRows)
            {
                throw new ArgumentException("Real and imaginary arrays have unequal lengths");
            }

            float[][] modArray = new float[realRows][];

            for (int i = 0; i < realRows; i++)
            {
                float[] temp = new float[realCols];
                for (int j = 0; j < realCols; j++)
                {
                    float val = realArray[i][j]*realArray[i][j] + imagArray[i][j]*imagArray[i][j];
                    temp[j] = (float)Math.Sqrt((double)val); 
                }
                modArray[i] = temp; 
            }
            return modArray;
        }
        
    }
}