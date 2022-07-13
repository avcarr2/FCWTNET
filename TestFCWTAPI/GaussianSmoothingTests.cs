using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using OxyPlot;
using System.IO;
using System.Text;


namespace TestFCWTAPI
{
    public class GaussianSmoothingTests
    {
        [SetUp]
        public static void Setup()
        {

        }

        [Test]
        public void TestCalculateSampleKernel()
        {
            double gaussDeviation = 1;
            int gaussSize = 7;
            int checkOne = 1;
            double checkPoint1 = 1 / (Math.Sqrt(2 * Math.PI) * 1) * Math.Exp(-(checkOne - gaussSize / 2) * (checkOne - gaussSize / 2) / (2 * gaussDeviation * gaussDeviation));
            int checkTwo = 5;
            double checkPoint2 = 1 / (Math.Sqrt(2 * Math.PI) * 1) * Math.Exp(-(checkTwo - gaussSize / 2) * (checkTwo - gaussSize / 2) / (2 * gaussDeviation * gaussDeviation));
            double[,] deviationKernel = GaussianSmoothing.Calculate1DSampleKernel(gaussDeviation);
            double[,] allparamsKernel = GaussianSmoothing.Calculate1DSampleKernel(gaussDeviation, gaussSize);
            Assert.AreEqual(deviationKernel, allparamsKernel);
            Assert.AreEqual(checkPoint1, deviationKernel[1, 0]);
            Assert.AreEqual(checkPoint2, deviationKernel[5, 0]);
        }
        [Test]
        public void TestNormalizeMatrix()
        {
            double[,] unnormalizedMatrix = new double[,] { { 5, 7, 9 }, { 3, 4, 7 }, { 5, 6, 7 } };
            double unnormalizedRatio1 = unnormalizedMatrix[0, 2] / unnormalizedMatrix[1, 2];
            double unnormalizedRatio2 = unnormalizedMatrix[1, 2] / unnormalizedMatrix[2, 2];
            double[,] normalizedMatrix = GaussianSmoothing.NormalizeMatrix(unnormalizedMatrix);
            double normalizedRatio1 = normalizedMatrix[0, 2] / normalizedMatrix[1, 2];
            double normalizedRatio2 = normalizedMatrix[1, 2] / normalizedMatrix[2, 2];
            Assert.AreEqual(normalizedRatio1, unnormalizedRatio1, 0.001);
            Assert.AreEqual(normalizedRatio2, unnormalizedRatio2, 0.001);
            double normalizedSum = 0;
            for (int i = 0; i < unnormalizedMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < unnormalizedMatrix.GetLength(1); j++)
                {
                    normalizedSum += normalizedMatrix[i, j];
                }
            }
            Assert.AreEqual(normalizedSum, 1, 0.001);

        }
        [Test]
        public void TestProcessPoint()
        {
            double[,] test2DArray = new double[51, 51];
            var pointArray = new (int, int, double)[] // List of points to add to a test array
            {
                (8, 8, 1), // Generic point
                (27, 8, -1), // Sample point with a negative value
                (2, 43, 1), // Sample point next to an edge
                (50, 50, 1) // Sample point in a corner
            };
            var neighborArray = new (int, int)[] 
            // Generates a list of coordinates to points adjacent to the points in pointArray on the x direction
            {
                (7, 8),
                (26, 8),
                (1, 43),
                (49, 50)
            };

            // Generates a 2D array containing all points from pointArray
            for (int i = 0; i < 51; i++)
            {
                for (int j = 0; j < 51; j++)
                {
                    for (int t = 0; t < 4; t++)
                        if (i == pointArray[t].Item1 && j == pointArray[t].Item2)
                        {
                            test2DArray[i, j] = pointArray[t].Item3;
                        }
                }

            }
            // Array to store transformed values of the pointArray points
            double[] valueArray = new double[4];
            // Array to store transformed values of the neighborArray points
            double[] neighborValueArray = new double[4];
            //Deviation of the Guassian kernel used
            double testDeviation = 1;
            // Size of the Gaussian kernel used
            int testSize = 7;
            double[,] test1dKernel = GaussianSmoothing.CalculateNormalized1DSampleKernel(testDeviation);
            // Point at the center of the Gaussian
            double gaussPoint4 = 1 / (Math.Sqrt(2 * Math.PI) * testDeviation) * Math.Exp(-(3 - testSize / 2) * (3 - testSize / 2) / (2 * testDeviation * testDeviation));
            // Point adjacent to the center of the Gaussian
            double gaussPoint3 = 1 / (Math.Sqrt(2 * Math.PI) * testDeviation) * Math.Exp(-(2 - testSize / 2) * (2 - testSize / 2) / (2 * testDeviation * testDeviation));
            //Checks that all values from the Process point function are what they should be
            for (int p = 0; p < 4; p++)
            {
                valueArray[p] = GaussianSmoothing.ProcessPoint(test2DArray, pointArray[p].Item1, pointArray[p].Item2, test1dKernel, 0);
                neighborValueArray[p] = GaussianSmoothing.ProcessPoint(test2DArray, neighborArray[p].Item1, neighborArray[p].Item2, test1dKernel, 0);
                if (p != 1)
                {
                    Assert.AreEqual(gaussPoint4, valueArray[p], 0.01);
                    Assert.AreEqual(gaussPoint3, neighborValueArray[p], 0.01);
                }
                else
                {
                    Assert.AreEqual(gaussPoint4 * -1, valueArray[p], 0.01);
                    Assert.AreEqual(gaussPoint3 * -1, neighborValueArray[p], 0.01);
                }

            }
        }
        [Test]
        public void TestGaussianConvolution()
        {
            double[,] invalid2DArray = new double[,]
            {
                {1, 2, 3, 4, 5 },
                {4, 5, 6, 7, 8 },
                {9, 10, 11, 12, 13 },
            };
            Assert.Throws<ArgumentException>(() => GaussianSmoothing.GaussianConvolution(invalid2DArray, 1));
            double[,] test2DArray = new double[51, 51];
            // List of points to add to a test array
            var pointArray = new (int, int, double)[] 
            {
                (8, 8, 1), // Generic point
                (27, 8, -1), // Sample point with a negative value
                (2, 43, 1), // Sample point next to an edge
                (50, 50, 1) // Sample point in a corner
            };
            // Generates a list of coordinates to points adjacent to the points in pointArray
            var adjacentArray = new (int, int)[]
            {
                (7, 8),
                (27, 9),
                (1, 43),
                (50, 49)
            };
            // Generates a list of coordinates to points diagonal to the points in pointArray
            var diagonalArray = new (int, int)[]
            {
                (9, 9),
                (28, 9),
                (3, 44),
                (49, 49)
            };
            // Generates a 2D array containing all points from pointArray
            for (int i = 0; i < 51; i++)
            {
                for (int j = 0; j < 51; j++)
                {
                    for (int t = 0; t < 4; t++)
                        if (i == pointArray[t].Item1 && j == pointArray[t].Item2)
                        {
                            test2DArray[i, j] = pointArray[t].Item3;
                        }
                }

            }
            // Deviation of our test 2D Gaussian
            double testDeviation = 1; 
            // Calculates expected value of a point at the center of the 2D Gaussian
            double centerPoint = 1 / (2 * Math.PI * testDeviation);
            // Calculates expected value of a point 1 unit off from the center of the 2D Gaussian in any direction
            double adjacentPoint = 1 / (2 * Math.PI * testDeviation) * Math.Exp(-(1 * 1) / (2 * testDeviation * testDeviation));
            // Calculates expected value of a point 1 unit off from the center in in x and y direction
            double diagonalPoint = 1 / (2 * Math.PI * testDeviation) * Math.Exp(-2 / (2 * testDeviation * testDeviation));
            // Calculats the Gaussian convolution of our test 2D Gaussian with tesd2DArray
            double[,] testBlurredArray = GaussianSmoothing.GaussianConvolution(test2DArray, testDeviation);             
            for (int p = 0; p < 4; p++)
            {
                // Checks all the center point values for correctness
                Assert.AreEqual(centerPoint * pointArray[p].Item3, testBlurredArray[pointArray[p].Item1, pointArray[p].Item2], 0.001);
                // Checks all the adjacent point values for correctness
                Assert.AreEqual(adjacentPoint * pointArray[p].Item3, testBlurredArray[adjacentArray[p].Item1, adjacentArray[p].Item2], 0.001);
                // Checks all the diagonal point values for correctness
                Assert.AreEqual(diagonalPoint * pointArray[p].Item3, testBlurredArray[diagonalArray[p].Item1, diagonalArray[p].Item2], 0.001);                 
            }
            /* 
             * Note: this guassian smoothing function does not preserve the the total "intensity" 
             * of the array it operates on, there exists a sort of edge darkening that should be 
             * fixed later. The unit test for this new function should include a module to ensure
             * that the sum of the original array equals the sum of the blurred array
             */

        }
    }  
}