
namespace FCWTNET
{
    /// <summary>
    /// Code Derived from http://haishibai.blogspot.com/2009/09/image-processing-c-tutorial-4-gaussian.html
    /// </summary>
    public class GaussianSmoothing
    {
        
        /// <summary>
        /// Function to calculate a 1D Gaussian kernel 
        /// </summary>
        /// <param name="deviation"></param><summary>Standard deviation of the Gaussian</summary>
        /// <param name="size"></param><summary>Size of the 1D Gaussian kernel array</summary>
        /// <returns name="kernel1D"> 1D Gaussian kernel array</returns>
        public static double[,] Calculate1DSampleKernel(double deviation, int size)
        {
            double[,] kernel1D = new double[size, 1];
            double sum = 0;
            int half = size / 2;
            for (int i = 0; i < size; i++)
            {
                kernel1D[i, 0] = 1 / (Math.Sqrt(2 * Math.PI) * deviation) * Math.Exp(-(i - half) * (i - half) / (2 * deviation * deviation));
                sum += kernel1D[i, 0];
            }
            return kernel1D;
        }
        /// <summary>
        /// Overload of Calculat1DSampleKernel which only requires a deviation
        /// Sets the array size of the kernel to Ceiling(deviation * 3) * 2 + 1
        /// </summary>
        /// <param name="deviation"></param>
        /// <returns></returns>
        public static double[,] Calculate1DSampleKernel(double deviation)
        {
            int size = (int)Math.Ceiling(deviation * 3) * 2 + 1;
            return Calculate1DSampleKernel(deviation, size);
        }
        /// <summary>
        /// Calculates a normalized 1D Gaussian kernel
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
            double[,] normalizedMatrix = new double[matrix.GetLength(0), matrix.GetLength(1)];
            double sum = 0;
            for (int i = 0; i < normalizedMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < normalizedMatrix.GetLength(1); j++)
                    sum += matrix[i, j];
            }
            if (sum != 0)
            {
                for (int i = 0; i < normalizedMatrix.GetLength(0); i++)
                {
                    for (int j = 0; j < normalizedMatrix.GetLength(1); j++)
                        normalizedMatrix[i, j] = matrix[i, j] / sum;
                }
            }
            return normalizedMatrix;
        }

        /// <summary>
        /// Performs the 2D Gaussian smoothing on a given 2D array
        /// Applies the smoothing operation in x then y with 1D Kernels
        /// </summary>
        /// <param name="inputData">double[,] array to be smoothed </param>
        /// <param name="deviation">deviation of the Gaussian smoothing function</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double[,] GaussianConvolution(double[,] inputData, double deviation)
        {
            if (inputData.GetLength(0) < (deviation * 6 + 1) || inputData.GetLength(1) < (deviation * 6 + 1))
            {
                throw new ArgumentException("Matrix may not be smaller than the convoluting Gaussian kernel");
            }
            double[,] kernel = CalculateNormalized1DSampleKernel(deviation); 
            // Calculates the 1D kernel to be used for smoothing
            double[,] dim1Smoothing = new double[inputData.GetLength(0), inputData.GetLength(1)];
            double[,] dim2Smoothing = new double[inputData.GetLength(0), inputData.GetLength(1)];
            // x-direction smoothing
            for (int i = 0; i < inputData.GetLength(0); i++)
            {
                // Calculates each point in the x direction
                for (int j = 0; j < inputData.GetLength(1); j++)
                    dim1Smoothing[i, j] = ProcessPoint(inputData, i, j, kernel, 0);
            }
            //y-direction smoothing
            for (int i = 0; i < inputData.GetLength(0); i++)
            {
                //Calculates each point in the y direction from the x smoothed array res 1
                for (int j = 0; j < inputData.GetLength(1); j++)
                    dim2Smoothing[i, j] = ProcessPoint(dim1Smoothing, i, j, kernel, 1);
            }
            return dim2Smoothing;
        }
        /// <summary>
        /// Performs 2D Gaussian smoothing using 
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="frequencyDeviation"></param>
        /// <param name="timeDeviation"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double[,] EllipticGaussianConvolution(double[,] inputData, double frequencyDeviation, double timeDeviation)
        {
            if (inputData.GetLength(0) < (frequencyDeviation * 6 + 1) || inputData.GetLength(1) < (timeDeviation * 6 + 1))
            {
                throw new ArgumentException("Matrix may not be smaller than the convoluting Gaussian kernel");
            }
            double[,] frequencyKernel = CalculateNormalized1DSampleKernel(frequencyDeviation);
            double[,] timeKernel = CalculateNormalized1DSampleKernel(timeDeviation);
            // Calculates the 1D kernel to be used for smoothing
            double[,] frequencySmoothing = new double[inputData.GetLength(0), inputData.GetLength(1)];
            double[,] timeSmoothing = new double[inputData.GetLength(0), inputData.GetLength(1)];
            // x-direction smoothing
            for (int i = 0; i < inputData.GetLength(0); i++)
            {
                // Calculates each point in the x direction
                for (int j = 0; j < inputData.GetLength(1); j++)
                    frequencySmoothing[i, j] = ProcessPoint(inputData, i, j, frequencyKernel, 0);
            }
            //y-direction smoothing
            for (int i = 0; i < inputData.GetLength(0); i++)
            {
                //Calculates each point in the y direction from the x smoothed array res 1
                for (int j = 0; j < inputData.GetLength(1); j++)
                    timeSmoothing[i, j] = ProcessPoint(frequencySmoothing, i, j, timeKernel, 1);
            }
            return timeSmoothing;
        }
        public static double[] SliceEllipticGaussianConvolution(double[,] inputData, double frequencyDeviation, double timeDeviation, int sliceIndex, int dimension)
        {
            if (inputData.GetLength(0) < (frequencyDeviation * 6 + 1) || inputData.GetLength(1) < (timeDeviation * 6 + 1))
            {
                throw new ArgumentException("Matrix may not be smaller than the convoluting Gaussian kernel");
            }
            if (dimension != 0 && dimension != 1)
            {
                throw new ArgumentException("Dimension can only be 0 and 1", nameof(dimension));
            }
            double[,] frequencyKernel = CalculateNormalized1DSampleKernel(frequencyDeviation);
            double[,] timeKernel = CalculateNormalized1DSampleKernel(timeDeviation);
            double[,] sliceDirectionSmoothing;// = new double[inputData.GetLength(0), inputData.GetLength(1)];
            double[] smoothedSlice;// = new double[inputData.GetLength(0), inputData.GetLength(1)];
            // Calculates the 1D kernel to be used for smoothing
            if (dimension == 0)
            {
                // Calculates each point in the x direction
                int freqSize = (int)Math.Ceiling(frequencyDeviation * 3) * 2 + 1;
                sliceDirectionSmoothing = new double[freqSize, inputData.GetLength(1)];
                smoothedSlice = new double[inputData.GetLength(1)];
                for (int i = 0; i < freqSize; i++)
                {
                    int dataIndex = sliceIndex + i - freqSize / 2;
                    for (int j = 0; j < inputData.GetLength(1); j++)
                    {
                        sliceDirectionSmoothing[i, j] = ProcessPoint(inputData, dataIndex, j, frequencyKernel, 0);
                    }
                }
                for (int j = 0; j < inputData.GetLength(1); j++)
                {
                    smoothedSlice[j] = ProcessPoint(sliceDirectionSmoothing, freqSize / 2, j, timeKernel, 1);
                }
                return smoothedSlice;
            }
            else
            {
                // Calculates each point in the x direction
                int timeSize = (int)Math.Ceiling(timeDeviation * 3) * 2 + 1;
                sliceDirectionSmoothing = new double[inputData.GetLength(0), timeSize];
                smoothedSlice = new double[inputData.GetLength(0)];
                for (int i = 0; i < inputData.GetLength(0); i++)
                {
                    for (int j = 0; j < timeSize; j++)
                    {
                        int dataIndex = sliceIndex + j - timeSize / 2;
                        sliceDirectionSmoothing[i, j] = ProcessPoint(inputData, i, dataIndex, frequencyKernel, 0);
                    }
                }
                for (int i = 0; i < inputData.GetLength(0); i++)
                {
                    smoothedSlice[i] = ProcessPoint(sliceDirectionSmoothing, i, timeSize / 2, timeKernel, 1);
                }
                return smoothedSlice;
            }
        }

        /// <summary>
        /// Method to process each individual point of a given double[,] array 
        /// </summary>
        /// <param name="matrix"> double[,] array meant to be processed</param>
        /// <param name="x">x coordinate of the point being processed</param>
        /// <param name="y">y coordinate of the point being processed</param>
        /// <param name="kernel">Normalized 1D Gaussian kernel</param>
        /// <param name="direction">Direction to apply the Gausian kernel in 0 is x, 1 is y
        /// <returns></returns>
        public static double ProcessPoint(double[,] matrix, int x, int y, double[,] kernel, int direction)
        {
            double processedPoint = 0;
            int half = kernel.GetLength(0) / 2;

            for (int i = 0; i < kernel.GetLength(0); i++)
            {
                int influencePointX = direction == 0 ? x + i - half : x; 
                // Sets the x coordinate of the point being multiplied by the kernel
                // to contribute to the final value of the point being processed
                int influencePointY = direction == 1 ? y + i - half : y; 
                // Sets the y coordinate of the point being multiplied by the kernel
                // to contribute to the final value of the point being processed
                // The direction determines which direction the kernel is applied in for each point
                if (influencePointX >= 0 && influencePointX < matrix.GetLength(0) && influencePointY >= 0 && influencePointY < matrix.GetLength(1)) 
                // If the kernel extends beyond the matrix, exceeding values are treated as zero
                // This causes mild "darkening" and must be fixed in a future patch
                {
                    processedPoint += matrix[influencePointX, influencePointY] * kernel[i, 0]; 
                   // Sums up the final value of the kernel
                }
            }
            return processedPoint;
        }
        public static double[] GaussianSmoothing1D(double[] inputData, double deviation)
        {
            if (inputData.Length < (deviation * 6 + 1))
            {
                throw new ArgumentException("inputData may not be smaller than the convoluting Gaussian kernel", nameof(inputData));
            }
            double[,] kernel = CalculateNormalized1DSampleKernel(deviation);
            double[] smoothedData = new double[inputData.Length];
            for (int i = 0; i < inputData.GetLength(0); i++)
            {
                smoothedData[i] = ProcessPoint1D(inputData, i, kernel);
            }
            return smoothedData;
        }
        private static double ProcessPoint1D(double[] inputData, int x, double[,] kernel)
        {
            double processedPoint = 0;
            int half = kernel.GetLength(0) / 2;
           
            for (int i = 0; i < kernel.GetLength(0); i++)
            {
                int influencePoint = x + i - half;
                if (influencePoint >= 0 && influencePoint < inputData.Length)
                {
                    processedPoint += inputData[influencePoint] * kernel[i, 0];
                }
            }
            return processedPoint;
        }
    }
}
