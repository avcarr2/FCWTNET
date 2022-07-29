using NUnit.Framework;
using System;
using FCWTNET;

namespace TestFCWTAPI
{
    public class CWTOutputTests
    {
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
            CWTObject cosineCWT = new CWTObject(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false);
            cosineCWT.PerformCWT();
            double[,] testModulus = cosineCWT.OutputCWT.ModulusCalculation();
            // Tests points in all 4 quadrants, none of which would remain at the same coordinates under reflection or rotation
            double testPoint1 = Math.Sqrt(Math.Pow(cosineCWT.OutputCWT.RealArray[10, 25], 2) + Math.Pow(cosineCWT.OutputCWT.ImagArray[10, 25], 2));
            double testPoint2 = Math.Sqrt(Math.Pow(cosineCWT.OutputCWT.RealArray[60, 800], 2) + Math.Pow(cosineCWT.OutputCWT.ImagArray[60, 800], 2));
            double testPoint3 = Math.Sqrt(Math.Pow(cosineCWT.OutputCWT.RealArray[900, 27], 2) + Math.Pow(cosineCWT.OutputCWT.ImagArray[900, 27], 2));
            double testPoint4 = Math.Sqrt(Math.Pow(cosineCWT.OutputCWT.RealArray[900, 700], 2) + Math.Pow(cosineCWT.OutputCWT.ImagArray[900, 700], 2));
            Assert.AreEqual(testPoint1, testModulus[10, 25], 0.001);
            Assert.AreEqual(testPoint2, testModulus[60, 800], 0.001);
            Assert.AreEqual(testPoint3, testModulus[900, 27], 0.001);
            Assert.AreEqual(testPoint4, testModulus[900, 700], 0.001);
        }
        [Test]
        public static void TestPhaseCalculation()
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
            double[,] testPhase = cosineCWT.OutputCWT.PhaseCalculation();
            double testPoint = Math.Atan(cosineCWT.OutputCWT.ImagArray[32, 32] / cosineCWT.OutputCWT.RealArray[32, 32]);
            Assert.AreEqual(testPoint, testPhase[32, 32], 0.001);
        }
    }
}
