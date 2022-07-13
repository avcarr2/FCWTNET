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
            var testCompositePlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, PlottingUtils.PlotTitles.Composite, PlottingUtils.XYPlotOptions.Composite);
            var testEvolutionPlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, PlottingUtils.PlotTitles.Evolution, PlottingUtils.XYPlotOptions.Evolution);
            var testSinglePlot = PlottingUtils.GenerateXYPlot(testData, testSingle, PlottingUtils.PlotTitles.Single, PlottingUtils.XYPlotOptions.Evolution);
            var testCustomTitle = PlottingUtils.GenerateXYPlot(testData, testSingle, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single, "Custom Title");
            Assert.Throws<ArgumentNullException>(() => PlottingUtils.GenerateXYPlot(testData, testSingle, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            PlottingUtils.ExportPlotPDF(testCompositePlot, "testcomposite.pdf");
            PlottingUtils.ExportPlotPDF(testEvolutionPlot, "testevolution.pdf");
            PlottingUtils.ExportPlotPDF(testSinglePlot, "testsingle.pdf");
            PlottingUtils.ExportPlotPDF(testCustomTitle, "customtitle.pdf");
        }
            
    }


    
}