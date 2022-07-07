using NUnit.Framework;
using System;
using FCWT.NET;
using System.Linq; 


namespace TestFCWTAPI
{
    public class Tests
    {
        [SetUp]
        public static void Setup()
        {
            
        }
        [Test]
        public static void TestFCWT()
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
        public static void TestConvertDoubleToFloat()
        {
            double[] testArray = new double[] { 0D, 1D, 2D };
            float[] fArray = FCWTAPI.ConvertDoubleToFloat(testArray);
            Assert.AreEqual((float)testArray[1], fArray[1]); 
        }
        [Test]
        public static void TestFixOutputArray()
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
        public static void TestToTwoDArray()
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
        public static void ConvertFloat2DtoDouble()
        {
            float[,] test2DArray = new float[,]
            {
                {1F, 2F, 3F, 4F, 5F },
                {6F, 7F, 8F, 9F, 10F }
            };
            double[,] converted2DArray = FCWTAPI.ConvertFloat2DtoDouble(test2DArray);
            Assert.AreEqual((float)converted2DArray[1, 2], test2DArray[1, 2]);
        }
        [Test]
        public static void TestSplitIntoRealAndImaginary()
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
        public static void TestCalculatePhase()
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
        public static void TestCalculateModulus()
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
        public static void testPreformCWT()
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
        public static void TestSplitRealAndImaginary() // Test to split out the real and imaginary components from CWT in the CWTObject
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
            double[,] realCWT = cosineCWT.SplitRealAndImaginary(true);
            double[,] imCWT = cosineCWT.SplitRealAndImaginary(false);
            Assert.AreEqual(imCWT[0, 5], cosineCWT.OutputCWT[1, 5]);
            Assert.AreEqual(realCWT[0, 21], cosineCWT.OutputCWT[0, 21]);
            CWTObject noCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            Assert.Throws<ArgumentNullException>(() => noCWT.SplitRealAndImaginary(true));
        }
        [Test]
        public static void TestModulusCalculation() // Test to calculate the modulus of the CWT in the CWTObject
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            CWTObject noCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            Assert.Throws<ArgumentNullException>(() => noCWT.ModulusCalculation());
            CWTObject cosineCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            cosineCWT.PerformCWT();
            double[,] realCWT = cosineCWT.SplitRealAndImaginary(true);
            double[,] imCWT = cosineCWT.SplitRealAndImaginary(false);
            double[,] testModulus = cosineCWT.ModulusCalculation();
            double testPoint = Math.Sqrt(realCWT[25, 25] * realCWT[25, 25] + imCWT[25, 25] * imCWT[25, 25]);
            Assert.AreEqual(testPoint, testModulus[25,25], 0.001);
        }
        [Test]
        public static void TestPhaseCalculaton() // Test to calculate the phase of the CWT in the CWTObject
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            CWTObject noCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            Assert.Throws<ArgumentNullException>(() => noCWT.PhaseCalculation());
            CWTObject cosineCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            cosineCWT.PerformCWT();
            double[,] realCWT = cosineCWT.SplitRealAndImaginary(true);
            double[,] imCWT = cosineCWT.SplitRealAndImaginary(false);
            double[,] testPhase = cosineCWT.PhaseCalculation();
            double testPoint = Math.Atan(imCWT[32, 32] / realCWT[32, 32]);
            Assert.AreEqual(testPoint, testPhase[32, 32], 0.001);
        }
    }
}