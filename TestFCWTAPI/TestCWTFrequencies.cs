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
        public void TestCalculateTrueFrequencies()
        {
            double[] testFrequencies = new double[]
            {
                0.450e6,
                0.475e6
            };
            int testNbVoices = testFrequencies.Length;
            int testSampleRate = 50000;
            var cwtFrequencies = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate);
            var noSampleRate = new CWTFrequencies(testFrequencies, testNbVoices);
            var badCwtFrequencies = new CWTFrequencies();
            Assert.Throws<NullReferenceException>(() => badCwtFrequencies.CalculateTrueFrequencies());
            Assert.Throws<NullReferenceException>(() => noSampleRate.CalculateTrueFrequencies());
            cwtFrequencies.CalculateTrueFrequencies();
            double trueValue = testSampleRate * testFrequencies[0];
            Assert.AreEqual(trueValue, cwtFrequencies.TrueFrequencies[0]);
            
        }
        [Test]
        public void TestCalculateMzValues()
        {
            double[] testFrequencies = new double[]
            {
                0.450e6,
                0.475e6
            };
            
            int testNbVoices = testFrequencies.Length;
            int testSampleRate = 50000;
            double calibrationCoefficient = 7.5e12; 
            var cwtFrequencies = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate, calibrationCoefficient);
            var noCalibration = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate);
            var badCwtFrequencies = new CWTFrequencies();
            Assert.Throws<NullReferenceException>(delegate { 
                badCwtFrequencies.CalculateMZValues();  
            });
            noCalibration.CalculateTrueFrequencies();
            Assert.Throws<NullReferenceException>(() => noCalibration.CalculateMZValues());
            double trueValue = calibrationCoefficient / Math.Pow((testFrequencies[0] * testSampleRate), 2);
            Assert.Throws<NullReferenceException>(() => cwtFrequencies.CalculateMZValues());
            cwtFrequencies.CalculateTrueFrequencies();
            cwtFrequencies.CalculateMZValues();
            Assert.AreEqual(trueValue, cwtFrequencies.MZValues[0]); 
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
            CWTObject cosCWT = new(cosine, 1, 6, 200, (float)(2 * Math.PI), 4, false, 100000, 7.5e12);
            cosCWT.CalculateFrequencyAxis();
            cosCWT.FrequencyAxis.CalculateTrueFrequencies();
            cosCWT.FrequencyAxis.CalculateMZValues();            
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
            var mzIndexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(309108.469, 294468.634, CWTFrequencies.FrequencyUnits.MZValues);
            Assert.AreEqual(1, mzIndexRange.Item1);
            Assert.AreEqual(9, mzIndexRange.Item2);
            var trueFrequencyRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(4925.781, 5242.714, CWTFrequencies.FrequencyUnits.TrueFrequency);
            Assert.AreEqual(1, trueFrequencyRange.Item1);
            Assert.AreEqual(19, trueFrequencyRange.Item2);



        }        
        [Test]
        public static void TestTrueFreqToWaveletFreq()
        {
            double testTrueFrequency = 5000;
            double[] testFrequencies = new double[]
            {
                0.450e6,
                0.475e6
            };
            int testNbVoices = testFrequencies.Length;
            int testSampleRate = 50000;
            double testWaveletFrequency = testTrueFrequency / testSampleRate;
            var cwtFrequencies = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate);
            var noSampleRate = new CWTFrequencies(testFrequencies, testNbVoices);
            Assert.Throws<NullReferenceException>(() => noSampleRate.TrueFreqToWaveletFreq(testTrueFrequency));
            Assert.AreEqual(cwtFrequencies.TrueFreqToWaveletFreq(testTrueFrequency), testWaveletFrequency);
        }
        [Test]
        public static void TestMZValueToWaveletFreq()
        {
            double testMZValue = 1000;
            double[] testFrequencies = new double[]
            {
                0.450e6,
                0.475e6
            };
            int testNbVoices = testFrequencies.Length;
            int testSampleRate = 50000;
            double testCalibrationCoefficient = 7.5e12;
            double testTrueFrequency = Math.Sqrt(testCalibrationCoefficient / testMZValue);
            double testWaveletFrequency = testTrueFrequency / testSampleRate;
            var cwtFrequencies = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate, testCalibrationCoefficient);
            var noCalibration = new CWTFrequencies(testFrequencies, testNbVoices, testSampleRate);
            Assert.Throws<NullReferenceException>(() => noCalibration.MZValueToWaveletFreq(testTrueFrequency));
            double waveletFrequency = cwtFrequencies.TrueFreqToWaveletFreq(testTrueFrequency);
            Assert.AreEqual(waveletFrequency, testWaveletFrequency);
        }
    }
}