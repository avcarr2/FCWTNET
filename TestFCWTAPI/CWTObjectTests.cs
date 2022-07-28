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
            // Tests points in all 4 quadrants, none of which would remain at the same coordinates under reflection or rotation
            double testPoint1 = Math.Sqrt(realCwt[10, 25] * realCwt[10, 25] + imagCwt[10, 25] * imagCwt[10, 25]);
            double testPoint2 = Math.Sqrt(realCwt[60, 800] * realCwt[60, 800] + imagCwt[60, 800] * imagCwt[60, 800]);
            double testPoint3 = Math.Sqrt(realCwt[900, 27] * realCwt[900, 27] + imagCwt[900, 27] * imagCwt[900, 27]);
            double testPoint4 = Math.Sqrt(realCwt[900, 700] * realCwt[900, 700] + imagCwt[900, 700] * imagCwt[900, 700]);
            Assert.AreEqual(testPoint1, testModulus[10, 25], 0.001);
            Assert.AreEqual(testPoint2, testModulus[60, 800], 0.001);
            Assert.AreEqual(testPoint3, testModulus[900, 27], 0.001);
            Assert.AreEqual(testPoint4, testModulus[900, 700], 0.001);
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
            Assert.AreEqual(cosCWT.FrequencyAxis.WaveletCenterFrequencies[^5], (2 * Math.PI) / Math.Pow(2, 1.025), 0.001);
            Assert.AreEqual(cosCWT.FrequencyAxis.WaveletCenterFrequencies[0], (2 * Math.PI) / Math.Pow(2, 7), 0.00001);
            Assert.AreEqual(cosCWT.OutputCWT.GetLength(0) / 2, cosCWT.FrequencyAxis.Length);
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
            Assert.AreEqual(cosCWT.TimeAxis[2], 200, 0.01);
                       
        }
        [Test]
        public static void TestGetIndicesForFrequencyRange()
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
            Assert.Throws<ArgumentNullException>(() => cosCWT.GetIndicesForFrequencyRange(2, 2.6));
            cosCWT.CalculateFrequencyAxis();
            Assert.Throws<ArgumentException>(() => cosCWT.GetIndicesForFrequencyRange(0.001, 2));
            Assert.Throws<ArgumentException>(() => cosCWT.GetIndicesForFrequencyRange(0.06, 0.05));
            Assert.Throws<ArgumentException>(() => cosCWT.GetIndicesForFrequencyRange(0.06, 7));
            var testZeroStart = cosCWT.GetIndicesForFrequencyRange(0.04909, 0.0600);
            var testMiddleStart = cosCWT.GetIndicesForFrequencyRange(0.05, 0.0600);
            var testNearEndCase = cosCWT.GetIndicesForFrequencyRange(3.12, 3.13);
            var testAdjacentCase = cosCWT.GetIndicesForFrequencyRange(3.10, 3.11);
            Assert.AreEqual(testZeroStart, (0, 57));
            Assert.AreEqual(testMiddleStart, (5, 57));
            Assert.AreEqual(testNearEndCase, (1198, 1199)); 
            Assert.AreEqual(testAdjacentCase, (1196, 1197));            
        }

        [Test]
        public void TestGenerateHeatMapCWTObject()
        {
            double[] cosine = Enumerable.Range(1, 1000)
                                        .Select(i => FunctionGenerator.GenerateCosineWave((double)i / 57.2958))
                                        .ToArray();
            string testWorkingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            CWTObject cosCWT = new(cosine, 1, 16, 48, 300F, 4, false, 1000, workingPath: testWorkingPath);
            string testDataName = "TestCos";
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", testDataName));
            cosCWT.PerformCWT();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", testDataName));
            cosCWT.CalculateTimeAxis();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", testDataName));
            cosCWT.CalculateFrequencyAxis();
            cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.pdf", testDataName);
            Assert.Throws<ArgumentException>(() => cosCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "xyReflectedHeatmapc0-0.001.xlsx", testDataName));


        }
        [Test]
        public void TestGenerateXYPlotCWTObject()
        {
            double[] cosine = Enumerable.Range(1, 1000)
                                        .Select(i => FunctionGenerator.GenerateCosineWave((double)i / 57.2958))
                                        .ToArray();
            string testWorkingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            CWTObject cosCWT = new(cosine, 1, 16, 48, 300f, 4, false, 1000, workingPath: testWorkingPath);
            string testDataName = "TestCos";
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 0.09D, 100D, 10, testDataName));
            cosCWT.PerformCWT();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 0.09D, 100D, 10, testDataName));
            cosCWT.CalculateTimeAxis();
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 0.09D, 100D, 10, testDataName));
            cosCWT.CalculateFrequencyAxis();            
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 0.09D, 100D, 10, testDataName);
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTComposite.pdf", PlottingUtils.XYPlotOptions.Composite, 0.09D, 100D, 10, testDataName);
            cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTSingle.pdf", PlottingUtils.XYPlotOptions.Single, 0.09D);
            Assert.Throws<ArgumentNullException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.pdf", PlottingUtils.XYPlotOptions.Evolution, 0.09D));
            Assert.Throws<ArgumentException>(() => cosCWT.GenerateXYPlot(CWTObject.CWTFeatures.Modulus, "testcosCWTEvolution.xlsx", PlottingUtils.XYPlotOptions.Evolution, 0.09D, 100D, 10, testDataName));
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