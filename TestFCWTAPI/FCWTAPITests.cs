using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using Plotly.NET;
using Plotly.NET.LayoutObjects;
using Microsoft.FSharp.Core;
using System.Collections.Generic; 
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
            FCWTAPI.CWT(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false, 
                out double[][] real, out double[][] imag);
            Assert.AreEqual(200 * 6, real.GetLength(0));
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
        public void TestAPIResults()
        {
            float[] input = Enumerable.Range(0, 1000).Select(i => (float)Math.Cos(0.5 * Math.PI * (double)i)).ToArray();
            float[] output = FCWTAPI.CWT_Base(input, 1, 6, 10, 10f * 2f * (float)Math.PI, 4, false);

            FCWTAPI.SplitCWTOutput(output, input.Length, 
                out double[][] realArray, out double[][] imagArray); 

            string[] xlabels = Enumerable.Range(0, 1000).Select(i=> Convert.ToString(i)).ToArray();
            string[] ylabels = Enumerable.Range(1, 5 * 10).Select(i => Convert.ToString(i)).ToArray();

            IEnumerable<double[]> realEnumerable = realArray.AsEnumerable(); 

            GenericChart.GenericChart heatmap = Chart2D.Chart.Heatmap<double[], double, double>(realEnumerable,
                                                                                                             xlabels,
                                                                                                             ylabels);
            heatmap.Show(); 
        }
        [Test]
        public void TestSplitCWTResults()
        {
            float[] testArray = new float[]
            {
                1.0f, 2.0f,
                3.0f, 4.0f,
                5.0f, 6.0f, 
                7f, 8f, 
                9f, 10f, 
                11f, 12f
            };
            FCWTAPI.SplitCWTOutput(testArray, 3, 
                out double[][] realArray, out double[][] imagArray); 
            Assert.AreEqual(10.0d, imagArray[1][1]); 
        }
    }  
}