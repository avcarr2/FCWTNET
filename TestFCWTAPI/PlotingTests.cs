using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
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
            double[,] testData = TestDataGeneration.GenerateGaussian();
            double[] xAxis = new double[1000];
            double[] yAxis = new double[1000];
            for (int i = 0; i < testData.GetLength(0); i++)
            {
                xAxis[i] = (double)i;
                yAxis[i] = (double)i *3.4;
            }
            var plotModel = new PlotModel { Title = "Test Plot" };
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "X-Axis",
                Minimum = xAxis[0],
                Maximum = xAxis[^1],
            });
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Y-Axis",
                Minimum = yAxis[0],
                Maximum = yAxis[^1],
            });
            var testSeries = new LineSeries();
            for (int i = 0; i < xAxis.Length; i++)
            {
                testSeries.Points.Add(new DataPoint(xAxis[i], yAxis[i]));
            }
            plotModel.Series.Add(testSeries);
            PlottingUtils.ExportPlotPDF(plotModel, "testExport.pdf");
        }
        [Test]
        public void TestGenerateCWTHeatMap()
        {
            double[,] testData = TestDataGeneration.GenerateAsymmetricalGaussian();
            double[] timeAxis = new double[1000];
            double[] freqAxis = new double[1000];
            for (int i = 0; i < testData.GetLength(0); i++)
            {
                timeAxis[i] = (double)i * 2;
                freqAxis[i] = Math.Pow(2, (double)i / 80);
            }
            var testHeatMap = PlottingUtils.GenerateCWTHeatMap(testData, "Test Heatmap", timeAxis, freqAxis);
            PlottingUtils.ExportPlotPDF(testHeatMap, "testCWTheatmap.pdf");
            var noAxesHeatMap = PlottingUtils.GenerateCWTHeatMap(testData, "Test Heatmap");
            PlottingUtils.ExportPlotPDF(noAxesHeatMap, "testNoAxesCWTheatmap.pdf");
        }
        [Test]
        public void TestGenerateCWTContourPlot()
        {
            double[,] testData = TestDataGeneration.GenerateAsymmetricalGaussian();
            double[] timeAxis = new double[1000];
            double[] freqAxis = new double[1000];
            for (int i = 0; i < testData.GetLength(0); i++)
            {
                timeAxis[i] = (double)i * 2;
                freqAxis[i] = Math.Pow(2, (double)i / 80);
            }
            var testContour = PlottingUtils.GenerateCWTContourPlot(testData, "Test Contour", timeAxis, freqAxis);
            PlottingUtils.ExportPlotPDF(testContour, "testCWTcontour.pdf");
            var noAxesContourMap = PlottingUtils.GenerateCWTContourPlot(testData, "Test Contour");
            PlottingUtils.ExportPlotPDF(testContour, "testNoAxesCWTcontour.pdf");
        }
        [Test]
        public void TestGenerateXYPlotCWT()
        {
            double[,] testData = TestDataGeneration.GenerateAsymmetricalGaussian();
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
            var testNoAxesEvolution = PlottingUtils.GenerateXYPlotCWT(testData, testMultiple, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Evolution, "NoAxesEvolution");
            Assert.Throws<ArgumentNullException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, freqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            double[] badTimeAxis = new double[980];
            double[] badFreqAxis = new double[980];
            Assert.Throws<ArgumentException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, badTimeAxis, freqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            Assert.Throws<ArgumentException>(() => PlottingUtils.GenerateXYPlotCWT(testData, testSingle, timeAxis, badFreqAxis, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single));
            PlottingUtils.ExportPlotPDF(testCompositePlot, "testcompositeCWT.pdf");
            PlottingUtils.ExportPlotPDF(testEvolutionPlot, "testevolutionCWT.pdf");
            PlottingUtils.ExportPlotPDF(testSinglePlot, "testsingleCWT.pdf");
            PlottingUtils.ExportPlotPDF(testCustomTitle, "customtitleCWT.pdf");
            PlottingUtils.ExportPlotPDF(testNoAxesEvolution, "No_Axes_XY_Plot.pdf");
        }
    }

    
}