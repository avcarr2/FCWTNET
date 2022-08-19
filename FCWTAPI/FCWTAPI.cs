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

        public static void CWT(float[] input, int psoctave, int pendoctave,
            int pnbvoice, float c0, int nthreads, bool use_optimization_schemes, 
            out double[][] real, out double[][] imag)
        {
            int inputSize = input.Length;
            int noctaves = pendoctave - psoctave + 1;
            float[] output = GenerateOutputArray(inputSize, noctaves, pnbvoice);
            _cwt(input, inputSize, output, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes);
            SplitCWTOutput(output, inputSize, out double[][] realArray, out double[][] imagArray);
            real = realArray; imag = imagArray; 
        }
        public static float[] CWT_Base(float[] input, int psoctave, int pendoctave, 
            int pnbvoice, float c0, int nthreads, bool use_optimization_schemes)
        {
            float[] output = GenerateOutputArray(input.Length, pendoctave - psoctave + 1, pnbvoice);
            _cwt(input, input.Length, output, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes);
            return output; 
        }
        public static void SplitCWTOutput(float[] output, int signalLength, 
            out double[][] realArray, out double[][] imagArray)
        {
            double[] real1D = new double[output.Length / 2]; 
            double[] imag1D = new double[output.Length / 2];

            // convert the float to double
            double[] outputd = output.Select(i => (double)i).ToArray();

            int j = 0;
            int k = 0;

            for (int i = 0; i < outputd.Length; i++)
            {
                if (i % 2 == 0)
                {
                    real1D[j] = outputd[i];
                    j++;
                }
                else
                {
                    imag1D[k] = outputd[i];
                    k++;
                }
                if (k >= imag1D.Length)
                {
                    break;
                }
            }
            int rowsJagged = real1D.Length / signalLength;
            int offset = signalLength;
            int bytesToCopy = offset * sizeof(double);
            realArray = new double[rowsJagged][];
            imagArray = new double[rowsJagged][]; 

            for(int i = 0; i < rowsJagged; i++)
            {
                realArray[i] = new double[offset];
                imagArray[i] = new double[offset];

                Buffer.BlockCopy(real1D, offset * i * sizeof(double), realArray[i], 0, bytesToCopy);
                Buffer.BlockCopy(imag1D, offset * i * sizeof(double), imagArray[i], 0, bytesToCopy);
            }
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
        public static void CWT(double[] input, int psoctave, int pendoctave,
            int pnbvoice, float c0, int nthreads, bool use_optimization_schemes,
            out double[][] real, out double[][] imag)
        {
            float[] fInput = ConvertDoubleToFloat(input);
            CWT(fInput, psoctave, pendoctave, pnbvoice, c0, nthreads, use_optimization_schemes, 
                out double[][] realTemp, out double[][] imagTemp);
            real = realTemp; imag = imagTemp; 
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

    }
}