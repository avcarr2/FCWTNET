using NUnit.Framework;
using System;
using FCWT.NET;
using System.Linq; 


namespace TestFCWTAPI
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {

        }
        [Test]
        public void TestFCWT()
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            float[][] results = FCWTAPI.CWT(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            Assert.AreEqual(200 * 6 * 2, results.GetLength(0));
        }
        [Test]
        public void TestConvertDoubleToFloat()
        {
            double[] testArray = new double[] { 0D, 1D, 2D };
            float[] fArray = FCWTAPI.ConvertDoubleToFloat(testArray);
            Assert.AreEqual((float)testArray[1], fArray[1]);
        }
        [Test]
        public void TestFixOutputArray()
        {
            float[] testArray = new float[]
            {1F, 2F, 3F,
            4F, 5F, 6F,
            7F, 8F, 9F,
            10F, 11F, 12F};

            float[][] outputArray = FCWTAPI.FixOutputArray(testArray, 3, 1, 2);
            Assert.AreEqual(4, outputArray.GetLength(0));
        }


        [Test]
        public void TestToTwoDArray()
        {
            float[][] testJagged2d = new float[][]
            {
                new float[] {1, 2, 3, 4, 5, 6},
                new float[] {7, 8, 9, 10, 11, 12 },
                new float[] {11, 12, 13, 14, 15, 16}
            };
            float[,] test2DArray = FCWTAPI.ToTwoDArray(testJagged2d);
            Assert.AreEqual(3, test2DArray.GetLength(0));
            Assert.AreEqual(6, test2DArray.GetLength(1));
            float[][] badJaggedArray1 = new float[][]
            {
                new float[] {1, 2, 3, 4, 5, 6, 22},
                new float[] {7, 8, 9, 10, 11, 12 },
                new float[] {11, 12, 13, 14, 15, 16}
            };
            Assert.Throws<IndexOutOfRangeException>(() => FCWTAPI.ToTwoDArray(badJaggedArray1));
            float[][] badJaggedArray2 = new float[][]
            {
                new float[] {1, 2, 3, 4, 5, 6},
                new float[] {7, 8, 9, 10, 11, 12, 22 },
                new float[] {11, 12, 13, 14, 15, 16}
            };
            Assert.Throws<IndexOutOfRangeException>(() => FCWTAPI.ToTwoDArray(badJaggedArray2));
            float[][] badJaggedArray3 = new float[][]
            {
                new float[] {1, 2, 3, 4, 5, 6},
                new float[] {7, 8, 9, 10, 11, 12 },
                new float[] {11, 12, 13, 14, 15, 16, 22}
            };
            Assert.Throws<IndexOutOfRangeException>(() => FCWTAPI.ToTwoDArray(badJaggedArray3));
        }
        [Test]
        public void TestSplitIntoRealAndImaginary()
        {
            float[][] testArray = new float[][]
            {
                new float[] {1F, 2F, 3F, 4F },
                new float[] {5F, 6F, 7F, 8F }
            };

            FCWTAPI.SplitIntoRealAndImaginary(testArray, out float[][] realArray,
                out float[][] imaginaryArray);
            Assert.AreEqual(4, realArray[0].Length);
            Assert.AreEqual(4, imaginaryArray[0].Length);
        }
        [Test]
        public void TestCalculatePhase()
        {
            float[][] testArray = new float[][]
            {
                new float[] {1F, 1.2F, 1.3F, 1.4F },
                new float[] {1.5F, 1.6F, 1.7F, 1.8F }
            };

            float[][] phaseArray = FCWTAPI.CalculatePhase(testArray, testArray);

            Assert.AreEqual(1.273, phaseArray[0][0], 0.001);
        }
        [Test]
        public void TestCalculateModulus()
        {
            float[][] testArray = new float[][]
            {
                new float[] {1F, 2F, 3F, 4F },
                new float[] {5F, 6F, 7F, 8F }
            };

            float[][] modArray = FCWTAPI.CalculateModulus(testArray, testArray);
            Console.WriteLine(string.Join("; ", modArray[0].AsEnumerable()));
            Assert.AreEqual(1.41421, modArray[0][0], 0.001);

        }
        [Test]
        public void TestPreformCWT()
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            CWTObject cosineCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            cosineCWT.PerformCWT();
            Assert.AreEqual(cosineCWT.OutputCWT.GetLength(0), 200 * 6 * 2);
            Assert.AreEqual(cosineCWT.OutputCWT.GetLength(1), 1000);
        }
        [Test]
        public void TestCalculateSampleKernal()
        {
            double gaussDeviation = 1;
            int gaussSize = 7;
            int checkOne = 1;
            double checkPoint1 = 1 / (Math.Sqrt(2 * Math.PI) * 1) * Math.Exp(-(checkOne - gaussSize/2) * (checkOne - gaussSize/2) / (2 * gaussDeviation * gaussDeviation));
            int checkTwo = 5;
            double checkPoint2 = 1 / (Math.Sqrt(2 * Math.PI) * 1) * Math.Exp(-(checkTwo - gaussSize / 2) * (checkTwo - gaussSize / 2) / (2 * gaussDeviation * gaussDeviation));
            double[,] deviationKernal = GaussianSmoothing.Calculate1DSampleKernel(gaussDeviation);
            double[,] allparamsKernal = GaussianSmoothing.Calculate1DSampleKernel(gaussDeviation, gaussSize);
            Assert.AreEqual(deviationKernal, allparamsKernal);
            Assert.AreEqual(checkPoint1, deviationKernal[1, 0]);
            Assert.AreEqual(checkPoint2, deviationKernal[5, 0]);
        }
        [Test]
        public void TestNormalizeMatrix()
        {
            double[,] unnormalizedMatrix = new double[,] { { 5, 7, 9 }, { 3, 4, 7 }, { 5, 6, 7 } };
            double unnormalizedRatio1 = unnormalizedMatrix[0, 2] / unnormalizedMatrix[1, 3];
            double unnormalizedRatio2 = unnormalizedMatrix[1, 2] / unnormalizedMatrix[2, 3];
            double[,] normalizedMatrix = GaussianSmoothing.NormalizeMatrix(unnormalizedMatrix);
            double normalizedRatio1 = normalizedMatrix[0, 2] / normalizedMatrix[1, 3];
            double normalizedRatio2 = normalizedMatrix[1, 2] / normalizedMatrix[2, 3];
            Assert.AreEqual(normalizedRatio1, unnormalizedRatio1);
            Assert.AreEqual(normalizedRatio2, unnormalizedRatio2);
            double normalizedSum = 0;
            for(int i = 0; i < unnormalizedMatrix.GetLength(0); i++)
            {
                for(int j = 0; j < unnormalizedMatrix.GetLength(1); j++)
                {
                    normalizedSum = normalizedSum + normalizedMatrix[i, j];
                }
            }
            Assert.AreEqual(normalizedSum, unnormalizedRatio1, 0.001);
            
        }
        [Test]
        public void TestProcessPoint()
        {
            double[,] test2DArray = new double[51, 51];
            var pointArray = new (int, int, double)[]
            {
                (8, 8, 1),
                (27, 8, 1),
                (43, 2, 1),
                (50, 50, 1)
            };
            for(int i = 0; i < 51; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    for (int t = 0; t < 50; t++)
                        if (i == pointArray[t].Item1 && j == pointArray[t].Item2)
                        {
                            test2DArray[i, j] = pointArray[t].Item3;
                        }
                        else { test2DArray[i, j] = 0; }
                }
                
            }
            double[] valueArray = new double[4];
            double[,] test1dKernal = GaussianSmoothing.CalculateNormalized1DSampleKernel(1);
            for (int p = 0; p < 4; p++)
            {
                valueArray[p] = GaussianSmoothing.ProcessPoint(test2DArray, pointArray[p].Item1, pointArray[p].Item2, test1dKernal, 0);
            }
        }
    }


    
}