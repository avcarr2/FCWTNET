using FCWTNET;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using OxyPlot; 

namespace TestFCWTAPI
{
    public class CWTObjectTests
    {
        [SetUp]
        public static void Setup()
        {

        }
        [Test]
        public static void testPerformCWT()
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
            Assert.AreEqual(cosineCWT.OutputCWT.Rows, 1200);
            Assert.AreEqual(cosineCWT.OutputCWT.Columns, 1000);
        }
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
            // The axis holds normalised centre frequencies (cycles per sample):
            //   f[i] = c0 / (2*pi * 2^(psoctave + (N-1-i)/nvoices)),  N = noctaves*nvoices
            // Previously this divided c0 by the scale without the 2*pi, which made every
            // "true frequency" derived from it 2*pi too large — well above Nyquist for
            // real transients — and it ignored psoctave entirely.
            // C0 is a float on CWTObject, so build the expectation from the same value.
            double c0 = (float)(2 * Math.PI);
            const int Nvoices = 200;
            const int Psoctave = 1;
            int n = (6 - Psoctave + 1) * Nvoices;
            double Expected(int index) =>
                c0 / (2 * Math.PI * Math.Pow(2, Psoctave + (double)(n - 1 - index) / Nvoices));
            Assert.AreEqual(Expected(n - 5), cosCWT.FrequencyAxis.WaveletCenterFrequencies[^5], 1e-12);
            Assert.AreEqual(Expected(0), cosCWT.FrequencyAxis.WaveletCenterFrequencies[0], 1e-12);
            // c0 = 2*pi with psoctave = 1 puts the smallest scale exactly at Nyquist
            // (2*pi / (2*pi * 2^1) = 0.5); allow for float rounding in C0.
            Assert.LessOrEqual(cosCWT.FrequencyAxis.WaveletCenterFrequencies[^1], 0.5 + 1e-6);
            Assert.AreEqual(cosCWT.OutputCWT.Rows, cosCWT.FrequencyAxis.Length);
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
            Assert.AreEqual(cosCWT.OutputCWT.Columns, cosCWT.TimeAxis.Length);
            Assert.AreEqual(cosCWT.TimeAxis[9], 0.9, 0.01);

        }
        [Test]
        public void TestGenerateHeatMapCWTObject()
        {
            double[] cosine = Enumerable.Range(1, 1000)
                                        .Select(i => FunctionGenerator.GenerateCosineWave((double)i * 0.08 * Math.PI))
                                        .ToArray();
            string testWorkingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            CWTObject cosCWT = new(cosine, 1, 6, 200, 50, 4, false, 1000, 7.5e12, workingPath: testWorkingPath);
            string testDataName = "TestCos";
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.PerformCWT();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.CalculateTimeAxis();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.CalculateFrequencyAxis();
            cosCWT.FrequencyAxis.CalculateTrueFrequencies();
            cosCWT.FrequencyAxis.CalculateMZValues();
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "cosTestCWTHeatmap.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName);
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "cosTestCWTHeatmapTrueFreq.pdf", CWTFrequencies.FrequencyUnits.TrueFrequency, testDataName);
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "cosTestCWTHeatmapMZVals.pdf", CWTFrequencies.FrequencyUnits.MZValues, testDataName);
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "cosTestCWTHeatmapWindowed.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName, 0.1, 0.3, 1.5, 2.5);
            Assert.Throws<ArgumentException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.xlsx", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            // Exercises the >100000-column time-axis compression path in GenerateHeatMap,
            // so the signal has to stay longer than that. The scale count does not affect
            // what is under test, and 6 octaves x 200 voices over 150000 samples needs
            // roughly 10 GB once the jagged arrays, their 2D copies, the modulus and the
            // transposed copy all coexist. Two octaves x 20 voices covers the same path.
            double[] longCosine = Enumerable.Range(1, 150000)
                            .Select(i => FunctionGenerator.GenerateCosineWave((double)i * 0.08 * Math.PI))
                            .ToArray();
            CWTObject longCosCWT = new(longCosine, 1, 2, 20, 50, 4, false, 1000, 7.5e12, workingPath: testWorkingPath);
            longCosCWT.PerformCWT();
            longCosCWT.CalculateTimeAxis();
            longCosCWT.CalculateFrequencyAxis();
            longCosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "longCosTestCWTHeatmap.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName);


        }
        [Test]
        public void TestGenerateXYPlotCWTObject()
        {
            double[] cosine = Enumerable.Range(1, 1000)
                                        .Select(i => FunctionGenerator.GenerateCosineWave((double)i * 0.08 * Math.PI))
                                        .ToArray();
            string testWorkingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            CWTObject cosCWT = new(cosine, 1, 6, 200, 50, 4, false, 100000, 7.5e12, workingPath: testWorkingPath);
            string testDataName = "TestCos";
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName));
            cosCWT.PerformCWT();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName));
            cosCWT.CalculateTimeAxis();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName));
            cosCWT.CalculateFrequencyAxis();
            cosCWT.FrequencyAxis.CalculateTrueFrequencies();
            cosCWT.FrequencyAxis.CalculateMZValues();
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName);
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolutionTimeWindowed.pdf", PlottingUtils.XYPlotOptions.Evolution,
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName, 0.003, 0.007);
            // Tests composite summing a limited number of steps
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTComposite_10Steps.pdf", PlottingUtils.XYPlotOptions.Composite, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName);
            // Test composite summing all steps in the range
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTComposite_AllSteps.pdf", PlottingUtils.XYPlotOptions.Composite, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 100000000, testDataName);
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTSingle.pdf",  PlottingUtils.XYPlotOptions.Single, CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.97);
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 2));
            Assert.Throws<ArgumentException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.xlsx", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName));
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolutionTrueFreq.pdf", PlottingUtils.XYPlotOptions.Evolution,
                CWTFrequencies.FrequencyUnits.TrueFrequency, 180000, 240000, 10, testDataName);
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolutionMZVals.pdf", PlottingUtils.XYPlotOptions.Evolution,
                CWTFrequencies.FrequencyUnits.MZValues, 231.489, 130.208, 10, testDataName);
        }
        [Test]
        public void TestCosine()
        {
            PlotModel pm = new PlotModel();
            var line = new OxyPlot.Series.LineSeries();
            double[] vals = Enumerable.Range(1, 500)
                .Select(i => FunctionGenerator.GenerateCosineWave((double)i/57.2958))
                .ToArray();
            double[] xaxis = Enumerable.Range(1, 500).Select(i => (double)i).ToArray(); 
            for(int i = 0; i < vals.Length; i++)
            {
                line.Points.Add(new DataPoint(xaxis[i], vals[i])); 
            }
            pm.Series.Add(line);
            string writingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "CosinePlotF2pi.pdf");
            PlottingUtils.ExportPlotPDF(pm, writingPath);
            
        }
        

    }  
}