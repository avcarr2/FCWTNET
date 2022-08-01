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
            Assert.AreEqual(cosCWT.FrequencyAxis.WaveletCenterFrequencies[^5], (2 * Math.PI) / Math.Pow(2, 1.025), 0.001);
            Assert.AreEqual(cosCWT.FrequencyAxis.WaveletCenterFrequencies[0], (2 * Math.PI) / Math.Pow(2, 7), 0.00001);
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
            CWTObject cosCWT = new(cosine, 1, 6, 200, 50, 4, false, 1000, workingPath: testWorkingPath);
            string testDataName = "TestCos";
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.PerformCWT();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.CalculateTimeAxis();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));
            cosCWT.CalculateFrequencyAxis();
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "cosTestCWTHeatmap.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName);
            Assert.Throws<ArgumentException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.xlsx", CWTFrequencies.FrequencyUnits.WaveletFrequency, testDataName));


        }
        [Test]
        public void TestGenerateXYPlotCWTObject()
        {
            double[] cosine = Enumerable.Range(1, 1000)
                                        .Select(i => FunctionGenerator.GenerateCosineWave((double)i * 0.08 * Math.PI))
                                        .ToArray();
            string testWorkingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            CWTObject cosCWT = new(cosine, 1, 6, 200, 50, 4, false, 1000, workingPath: testWorkingPath);
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
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 
                CWTFrequencies.FrequencyUnits.WaveletFrequency, 1.8, 2.4, 10, testDataName);
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