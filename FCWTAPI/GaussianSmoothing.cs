
namespace FCWTNET
{
    /// <summary>
    /// Code Derived from http://haishibai.blogspot.com/2009/09/image-processing-c-tutorial-4-gaussian.html
    /// </summary>
    public class GaussianSmoothing
    {
        
        /// <summary>
        /// Function to calculate a 1D gaussian kernal 
        /// </summary>
        /// <param name="deviation"></param><summary>Standard deviation of the gaussian</summary>
        /// <param name="size"></param><summary>Size of the 1D gaussian kernal array</summary>
        /// <returns name="ret"> 1D gaussian kernal array</returns>
        public static double[,] Calculate1DSampleKernel(double deviation, int size)
        {
            double[,] ret = new double[size, 1];
            double sum = 0;
            int half = size / 2;
            for (int i = 0; i < size; i++)
            {
                ret[i, 0] = 1 / (Math.Sqrt(2 * Math.PI) * deviation) * Math.Exp(-(i - half) * (i - half) / (2 * deviation * deviation));
                sum += ret[i, 0];
            }
            return ret;
        }
        /// <summary>
        /// Overload of Calculat1DSampleKernal which automatically sets the array size of the kernal to Ceiling(deviation * 3) * 2 + 1
        /// </summary>
        /// <param name="deviation"></param>
        /// <returns></returns>
        public static double[,] Calculate1DSampleKernel(double deviation)
        {
            int size = (int)Math.Ceiling(deviation * 3) * 2 + 1;
            return Calculate1DSampleKernel(deviation, size);
        }
        /// <summary>
        /// Calculates a normalized 1D gaussian kernal
        /// </summary>
        /// <param name="deviation"></param>
        /// <returns></returns>
        public static double[,] CalculateNormalized1DSampleKernel(double deviation)
        {
            return NormalizeMatrix(Calculate1DSampleKernel(deviation));
        }
        /// <summary>
        /// Normalizes any double[,] array such that the sum of all elements is equal to 1
        /// </summary>
        /// <param name="matrix"> double[,] array to be normalized </param>
        /// <returns name="ret"> normalized double[,] array</returns>
        public static double[,] NormalizeMatrix(double[,] matrix)
        {
            double[,] ret = new double[matrix.GetLength(0), matrix.GetLength(1)];
            double sum = 0;
            for (int i = 0; i < ret.GetLength(0); i++)
            {
                for (int j = 0; j < ret.GetLength(1); j++)
                    sum += matrix[i, j];
            }
            if (sum != 0)
            {
                for (int i = 0; i < ret.GetLength(0); i++)
                {
                    for (int j = 0; j < ret.GetLength(1); j++)
                        ret[i, j] = matrix[i, j] / sum;
                }
            }
            return ret;
        }
        /// <summary>
        /// Method to preform the 2D gaussian smoothing
        /// This method applies the smoothing operation by first smoothing in the x direction, then the y direction using a symmetric 1D kernal
        /// </summary>
        /// <param name="matrix">double[,] array to be smoothed </param>
        /// <param name="deviation">deviation of the gaussian smoothing function</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Exception to prevent input matrix from being smaller than the convolving function</exception>
        public static double[,] GaussianConvolution(double[,] matrix, double deviation)
        {
            if (matrix.GetLength(0) < (deviation * 6 + 1) || matrix.GetLength(1) < (deviation * 6 + 1))
            {
                throw new ArgumentException("Matrix may not be smaller than the convoluting gaussian kernal");
            }
            double[,] kernel = CalculateNormalized1DSampleKernel(deviation); // Calculates the 1D kernal to be used for smoothing
            double[,] res1 = new double[matrix.GetLength(0), matrix.GetLength(1)];
            double[,] res2 = new double[matrix.GetLength(0), matrix.GetLength(1)];
            //x-direction smoothing
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                // Calculates each point in the x direction
                for (int j = 0; j < matrix.GetLength(1); j++)
                    res1[i, j] = ProcessPoint(matrix, i, j, kernel, 0);
            }
            //y-direction smoothing
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                //Calculates each point in the y direction from the x smoothed array res 1
                for (int j = 0; j < matrix.GetLength(1); j++)
                    res2[i, j] = ProcessPoint(res1, i, j, kernel, 1);
            }
            return res2;
        }
        /// <summary>
        /// Method to process each individual point of a given double[,] array 
        /// </summary>
        /// <param name="matrix"> double[,] array meant to be processed</param>
        /// <param name="x">x coordinate of the point being processed</param>
        /// <param name="y">y coordinate of the point being processed</param>
        /// <param name="kernel">Normalized 1D gaussian kernal</param>
        /// <param name="direction">Direction to apply the gausian kernal to a given point 0 corresponds to x, 1 corresponds to y</param>
        /// <returns></returns>
        public static double ProcessPoint(double[,] matrix, int x, int y, double[,] kernel, int direction)
        {
            double res = 0;
            int half = kernel.GetLength(0) / 2;

            for (int i = 0; i < kernel.GetLength(0); i++)
            {
                int cox = direction == 0 ? x + i - half : x; // Sets the x coordinate of the point being multiplied by the kernal to contribute to the final value of the point being processed
                int coy = direction == 1 ? y + i - half : y; // Sets the y coordinate of the point being multiplied by the kernal to contribute to the final value of the point being processed
                // The direction determines which direction the kernal is applied in for each point
                if (cox >= 0 && cox < matrix.GetLength(0) && coy >= 0 && coy < matrix.GetLength(1)) // If the kernal extends beyond the matrix, those values are treated as zero leading to mild "darkening"
                {
                    res += matrix[cox, coy] * kernel[i, 0]; // Sums up the final value of the kernal
                }
            }
            return res;
        }
    }
}
