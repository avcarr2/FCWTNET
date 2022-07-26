using NUnit.Framework;
using System;
using FCWTNET;



namespace TestFCWTAPI
{
    public class TestCWTFrequencies
    {
        [SetUp]
        public static void Setup()
        {

        }

        [Test]
        public void TestToMzValues()
        {
            double[] testFrequencies = new double[]
            {
                0.450e6,
                0.475e6
            };
            double calibrationCoefficient = 7.5e12; 
            var cwtFrequencies = new CWTFrequencies(testFrequencies);
            var badCwtFrequencies = new CWTFrequencies();

            Assert.Throws<NullReferenceException>(delegate { 
                badCwtFrequencies.ToMZValues(calibrationCoefficient);  
            });
            double trueValue = calibrationCoefficient / Math.Pow(testFrequencies[0], 2);
            double[] mzVals = cwtFrequencies.ToMZValues(calibrationCoefficient);
            Assert.AreEqual(trueValue, mzVals[0]); 
        }
    }
}