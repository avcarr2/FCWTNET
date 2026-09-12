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
            // The wavelet frequency axis is now normalised (cycles per sample) instead of
            // carrying a spurious factor of 2*pi, and it honours psoctave, so the whole
            // axis has shifted. Both the old and new axes are geometric with the same
            // common ratio 2^(1/nvoices), so rescaling every probe frequency by the ratio
            // of the two axis minima maps each probe onto exactly the same index as
            // before — which keeps all the index expectations below meaningful.
            double oldAxisMinimum = (2 * Math.PI) / Math.Pow(2, 7);
            double freqScale = cosCWT.FrequencyAxis.WaveletCenterFrequencies![0] / oldAxisMinimum;
            // m/z goes as A/f^2, so it rescales by the inverse square.
            double mzScale = 1 / (freqScale * freqScale);
            Assert.Throws<NullReferenceException>(() => nulledCWTFrequencies.CalculateIndicesForFrequencyRange(0.8, 1.4));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.01 * freqScale, 2 * freqScale));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.09 * freqScale, 28 * freqScale));
            Assert.Throws<ArgumentException>(() => cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.09 * freqScale, 0.08 * freqScale));
            var frequencyIndexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(0.04909 * freqScale, 0.06278 * freqScale);
            Assert.AreEqual(0, frequencyIndexRange.Item1);
            Assert.AreEqual(71, frequencyIndexRange.Item2);
            var endindexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(3.1199 * freqScale, 3.13 * freqScale);
            Assert.AreEqual(1198, endindexRange.Item1);
            Assert.AreEqual(1199, endindexRange.Item2);
            var mzIndexRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(309108.469 * mzScale, 294468.634 * mzScale, CWTFrequencies.FrequencyUnits.MZValues);
            Assert.AreEqual(1, mzIndexRange.Item1);
            Assert.AreEqual(9, mzIndexRange.Item2);
            var trueFrequencyRange = cosCWT.FrequencyAxis.CalculateIndicesForFrequencyRange(4925.781 * freqScale, 5242.714 * freqScale, CWTFrequencies.FrequencyUnits.TrueFrequency);
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
        [Test]
        public static void TestFrequencyWindowing()
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
            cosCWT.PerformCWT();
            cosCWT.CalculateFrequencyAxis();
            cosCWT.FrequencyAxis.CalculateTrueFrequencies();
            cosCWT.FrequencyAxis.CalculateMZValues();
            double[] testPlotFrequencyAxis = cosCWT.FrequencyAxis.WaveletCenterFrequencies;
            double[,] testCWTData = cosCWT.OutputCWT.ModulusCalculation();
            double[,] testFreqWindowedData;
            double[] testWindowedFreqAxis;
            // Probe frequencies rescaled onto the corrected axis; see the comment in
            // TestCalculateIndicesForFrequencyRange for why this preserves the indices.
            double freqScale = testPlotFrequencyAxis[0] / ((2 * Math.PI) / Math.Pow(2, 7));
            cosCWT.FrequencyAxis.FrequencyWindowing(0.04909 * freqScale, 0.06278 * freqScale, testPlotFrequencyAxis, testCWTData,
                CWTFrequencies.FrequencyUnits.WaveletFrequency, out testWindowedFreqAxis, out testFreqWindowedData);
            Assert.AreEqual(72, testFreqWindowedData.GetLength(0));
            Assert.AreEqual(testCWTData[70, 5], testFreqWindowedData[70, 5]);
            Assert.AreEqual(72, testWindowedFreqAxis.Length);
            double[] testEndWindowedFreqAxis;
            double[,] testEndFreqWindowedData;
            cosCWT.FrequencyAxis.FrequencyWindowing(3.1199 * freqScale, 3.13 * freqScale, testPlotFrequencyAxis, testCWTData,
                CWTFrequencies.FrequencyUnits.WaveletFrequency, out testEndWindowedFreqAxis, out testEndFreqWindowedData);
            Assert.AreEqual(testCWTData[1199, 5], testEndFreqWindowedData[1, 5]);
            Assert.AreEqual(2, testEndWindowedFreqAxis.Length);
            Assert.AreEqual(testPlotFrequencyAxis[1198], testEndWindowedFreqAxis[0]);
            Assert.AreEqual(testPlotFrequencyAxis[1199], testEndWindowedFreqAxis[1]);

        }
    }
}