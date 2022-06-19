using NUnit.Framework;
using System;
using FCWT.NET; 

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
    }
}