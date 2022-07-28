using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using OxyPlot;
using System.IO;
using System.Text;


namespace TestFCWTAPI
{
    public class CWTObjectTests
    {
        [SetUp]
        public static void Setup()
        {

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
            Assert.AreEqual(cosineCWT.OutputCWT.GetLength(0), 1200);
            Assert.AreEqual(cosineCWT.OutputCWT.GetLength(1), 1000);
        }        
        [Test]
        public static void TestModulusCalculation()
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
            double[,] testModulus = cosineCWT.ModulusCalculation();
            double testPoint = Math.Sqrt(realCwt[25, 25] * realCwt[25, 25] + imagCwt[25, 25] * imagCwt[25, 25]);
            Assert.AreEqual(testPoint, testModulus[25, 25], 0.001);
        }
        //[Test]
        //public static void TestPhaseCalculaton()
        //{
        //    double[] testValues = new double[1000];
        //    double constant = 1D / 1000D * 2D * Math.PI;
        //    for (int i = 0; i < 1000; i++)
        //    {
        //        double val = (double)i * constant;
        //        testValues[i] = val;
        //    }
        //    double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
        //    CWTObject noCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
        //    Assert.Throws<ArgumentNullException>(() => noCWT.PhaseCalculation());
        //    CWTObject cosineCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
        //    cosineCWT.PerformCWT();
        //    cosineCWT.SplitRealAndImaginary(CWTObject.CWTComponent.Both, out double[,] realCwt, out double[,] imagCwt);
        //    double[,] testPhase = cosineCWT.PhaseCalculation();
        //    double testPoint = Math.Atan(imagCwt[32, 32] / realCwt[32, 32]);
        //    Assert.AreEqual(testPoint, testPhase[32, 32], 0.001);
        //}
        [Test]
        public static void TestCalculateFrequencyAxis()
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            CWTObject cosCWT = new(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            cosCWT.CalculateFrequencyAxis();
            cosCWT.PerformCWT();
            Assert.AreEqual(cosCWT.GetFrequencyAtIndex(4), (2 * Math.PI) / Math.Pow(2, 1.025), 0.001);
            Assert.AreEqual(cosCWT.OutputCWT.RealArray.GetLength(0), cosCWT.FrequencyAxis.Length);
        }
        [Test]
        public static void TestCalculateTimeAxis()
        {
            double[] testValues = new double[1000];
            double constant = 1D / 1000D * 2D * Math.PI;
            for (int i = 0; i < 1000; i++)
            {
                double val = (double)i * constant;
                testValues[i] = val;
            }
            double[] cosine = FunctionGenerator.TransformValues(testValues, FunctionGenerator.GenerateCosineWave);
            CWTObject cosCWT = new(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false, 10);
            cosCWT.PerformCWT();
            cosCWT.CalculateTimeAxis();
            Assert.AreEqual(cosCWT.OutputCWT.GetLength(1), cosCWT.TimeAxis.Length);
            Assert.AreEqual(cosCWT.TimeAxis[2], 0.2, 0.0001);
        }
    }  
}