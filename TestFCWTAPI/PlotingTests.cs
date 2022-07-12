using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using OxyPlot;
using System.IO;
using System.Text;


namespace TestFCWTAPI
{
    public class PlottingTests
    {
        [SetUp]
        public static void Setup()
        {

        }
        [Test]
        public void TestExportPlotPDF()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            var testPlot = PlottingUtils.GenerateHeatMap(testData, "Test Heatmap");
            Assert.Throws<ArgumentException>(() => PlottingUtils.ExportPlotPDF(testPlot, "testplot"));
            PlottingUtils.ExportPlotPDF(testPlot, "testplot.pdf");
        }
        [Test]
        public void TestGenerateHeatMap()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            var testHeatMap = PlottingUtils.GenerateHeatMap(testData, "Test Heatmap");
            PlottingUtils.ExportPlotPDF(testHeatMap, "testheatmap.pdf");
        }
        [Test]
        public void TestGenerateXYPlot()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            int[] testMultiple = new int[] { 300, 400, 450, 500, 530, 620, 680 };
            int[] testSingle = new int[] { 500 };
            var testCompositePlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, "Test Composite Plot", PlottingUtils.XYPlotOptions.Composite);
            var testEvolutionPlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, "Test Evolution Plot", PlottingUtils.XYPlotOptions.Evolution);
            var testSinglePlot = PlottingUtils.GenerateXYPlot(testData, testSingle, "Test Single Plot", PlottingUtils.XYPlotOptions.Evolution);
            PlottingUtils.ExportPlotPDF(testCompositePlot, "testcomposite.pdf");
            PlottingUtils.ExportPlotPDF(testEvolutionPlot, "testevolution.pdf");
            PlottingUtils.ExportPlotPDF(testSinglePlot, "testsingle.pdf");
        }
            
    }


    
}