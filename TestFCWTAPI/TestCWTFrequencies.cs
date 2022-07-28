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
            
            int testNbVoices = testFrequencies.Length;
            float testC0 = 1.33F;
            double calibrationCoefficient = 7.5e12; 
            var cwtFrequencies = new CWTFrequencies(testFrequencies, testNbVoices, testC0);
            var badCwtFrequencies = new CWTFrequencies();

            Assert.Throws<NullReferenceException>(delegate { 
                badCwtFrequencies.ToMZValues(calibrationCoefficient);  
            });
            double trueValue = calibrationCoefficient / Math.Pow(testFrequencies[0], 2);
            double[] mzVals = cwtFrequencies.ToMZValues(calibrationCoefficient);
            Assert.AreEqual(trueValue, mzVals[0]); 
        }
        [Test]
        public void TestCalculateIndicesForFrequencyRange()
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
            var nulledCWTFrequencies = new CWTFrequencies();
            Assert.Throws<NullReferenceException>(() => nulledCWTFrequencies.CalculateIndicesForFrequencyRange(0.8, 1.4));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.01, 2));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.09, 28));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.09, 0.08));
            var frequencyIndexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.04909, 0.06278);
            Assert.AreEqual(0, frequencyIndexRange.Item1);
            Assert.AreEqual(71, frequencyIndexRange.Item2);
            var endindexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(3.1199, 3.13);
            Assert.AreEqual(1198, endindexRange.Item1);
            Assert.AreEqual(1199, endindexRange.Item2);

        }
    }
}