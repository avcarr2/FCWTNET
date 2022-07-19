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
        [Test]
        public void TestGenerateCWTHeatMap()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            double[] timeAxis = new double[1000];
            double[] freqAxis = new double[1000];
            for (int i = 0; i < testData.GetLength(0); i++)
            {
                timeAxis[i] = (double)i * 2;
                freqAxis[i] = Math.Pow(2, (double)i / 80);
            }
            var testHeatMap = PlottingUtils.GenerateCWTHeatMap(testData, "Test Heatmap", timeAxis, freqAxis);
            PlottingUtils.ExportPlotPDF(testHeatMap, "testCWTheatmap.pdf");
        }
        [Test]
        public void TestGenerateXYPlotCWT()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            int[] testMultiple = new int[] { 300, 400, 450, 500, 530, 620, 680 };
            int[] testSingle = new int[] { 500 };
            double[] timeAxis = new double[1000];
            double[] freqAxis = new double[1000];
            for (int i = 0; i < testData.GetLength(0); i++)
            {
                timeAxis[i] = (double)i * 2;
                freqAxis[i] = Math.Pow(2, (double)i / 80);
            }
            var testCompositePlot = PlottingUtils.GenerateXYPlotCWT(testData, testMultiple, timeAxis, freqAxis, PlottingUtils.PlotTitles.Composite, PlottingUtils.XYPlotOptions.Composite);
            var testEvolutionPlot = PlottingUtils.GenerateXYPlotCWT(testData, testMultiple, timeAxis, freqAxis, PlottingUtils.PlotTitles.Evolution, PlottingUtils.XYPlotOptions.Evolution);
            var testSinglePlot = PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, freqAxis, PlottingUtils.PlotTitles.Single, PlottingUtils.XYPlotOptions.Evolution);
            var testCustomTitle = PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, freqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single, "Custom Title");
            Assert.Throws<ArgumentNullException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, freqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            double[] badTimeAxis = new double[980];
            double[] badFreqAxis = new double[980];
            Assert.Throws<ArgumentException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, badTimeAxis, freqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            Assert.Throws<ArgumentException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, badFreqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            PlottingUtils.ExportPlotPDF(testCompositePlot, "testcompositeCWT.pdf");
            PlottingUtils.ExportPlotPDF(testEvolutionPlot, "testevolutionCWT.pdf");
            PlottingUtils.ExportPlotPDF(testSinglePlot, "testsingleCWT.pdf");
            PlottingUtils.ExportPlotPDF(testCustomTitle, "customtitleCWT.pdf");

        }
    }

    
}