using System.Runtime.InteropServices; 
namespace FCWTNET
{
    public class FCWTAPI
    {
        // TODO: If the size of the nvoices * noctaves * 2 is greater than max integer, split the 
        // calculation into multiple chunks. 
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
        /// <summary>
        /// Performs a fast continous wavelet transform with a morlet wavelet. 
        /// </summary>
        /// <param name="input"></param><summary>Input signal to be transformed.</summary>
        /// <param name="psoctave"></param><summary>Start octave (power of two) to calculate.</summary>
        /// <param name="pendoctave"></param><summary>Final octave. </summary>
        /// <param name="pnbvoice"></param><summary>Number of voices per octave.</summary>
        /// <param name="c0"></param><summary>Central frequency of the morlet wavelet.</summary>
        /// <param name="nthreads"></param><summary>Number of threads to use. </summary>
        /// <param name="use_optimization_schemes"></param><summary>fCWT optimization scheme to use.</summary>
        /// <returns></returns>
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
        /// <summary>
        /// Moves the 1D array to a 2D jagged array. 
        /// </summary>
        /// <param name="array1D"></param>
        /// <param name="size"></param>
        /// <param name="noctave"></param>
        /// <param name="nvoice"></param>
        /// <returns></returns>
        public static float[][] FixOutputArray(float[] array1D, int size, int noctave, int nvoice)
        {
            // from the original fCWT library code 
            int numberRows = noctave * nvoice * 2; 
            // Creates the final with freq as higher dim
            float[][] fixedResults = new float[numberRows][];
            for(int i = 0; i < numberRows; i++)
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

        
        //First element corresponds to the first jagged array dimension (voices), second element corresponds to the second dim (timepoints)
        /// <summary>
        /// Method to convert jagged 2D arrays to formal 2D arrays
        /// In the context of CWT the first dimension represents voices and the second represents timepoints
        /// </summary>
        /// <param name="JaggedTwoD"></param>
        /// <returns></returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static float[,] ToTwoDArray(float[][] JaggedTwoD)
        {

            int arrayCount = JaggedTwoD.Length;
            int arrayLength = JaggedTwoD[0].Length;
            float[,] twodOutput = new float[arrayCount, arrayLength];
            for (int i = 0; i < arrayCount; i++)
            {
                // If any array length is not equal to the previous sets arrayLength to a value which will throw an IndexOutOfRangeException
                if(i > 0)
                {
                    if (JaggedTwoD[i].Length != JaggedTwoD[i - 1].Length) { arrayLength = JaggedTwoD[i].Length + 1; }
                }
                try
                {
                    for (int j = 0; j < arrayLength; j++)
                    {
                        twodOutput[i, j] = JaggedTwoD[i][j];
                    }
                }
                catch(IndexOutOfRangeException)
                {
                    string rowError = String.Format("Invalid array length in row {0}", i);
                    throw new IndexOutOfRangeException(rowError);
                }
                
            }
            return twodOutput;
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
        /// <summary>
        /// Calculates the phase of the continuous wavelet transform output using a morlet wavelet. 
        /// </summary>
        /// <param name="realArray"></param><summary>The real part of the complex morlet CWT.</summary>
        /// <param name="imagArray"></param><summary>The imaginary part of the complex morlet CWT. </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static float[][] CalculatePhase(float[][] realArray, float[][] imagArray)
        {
            int realRows = realArray.GetLength(0);
            int imagRows = imagArray.GetLength(0);  

            int realCols = realArray[0].Length;
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
        /// <summary>
        /// Calculates the modulus of the complex morlet continuous wavelet transform. 
        /// </summary>
        /// <param name="realArray"></param>
        /// <param name="imagArray"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static float[][] CalculateModulus(float[][] realArray, float[][] imagArray)
        {
            int realRows = realArray.GetLength(0);
            int imagRows = imagArray.GetLength(0);

            int realCols = realArray[0].Length;
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