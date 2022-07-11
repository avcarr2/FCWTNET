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
        public void TestGenerateHeatMap()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            var testHeatMap = PlottingUtils.GenerateHeatMap(testData, "Test Heatmap");
            using (var exportStream = File.Create(@"testheatmap.pdf"))
            {
                var pdfExport = new PdfExporter
                {
                    Width = 600,
                    Height = 600
                };
                pdfExport.Export(testHeatMap, exportStream);
            }
        }
        [Test]
        public void TestGenerateXYPlot()
        {
            double[,] testData = PlottingUtils.GenerateGaussian();
            int[] testMultiple = new int[] { 300, 400, 450, 500, 430, 380, 680 };
            int[] testSingle = new int[] { 500 };
            var testCompositePlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, PlottingUtils.XYPlotOptions.Composite);
            var testEvolutionPlot = PlottingUtils.GenerateXYPlot(testData, testMultiple, PlottingUtils.XYPlotOptions.Evolution);
            var testSinglePlot = PlottingUtils.GenerateXYPlot(testData, testSingle, PlottingUtils.XYPlotOptions.Evolution);
            using (var exportStream = File.Create(@"testcomposite.pdf"))
            {
                var pdfExport = new PdfExporter
                {
                    Width = 600,
                    Height = 600
                };
                pdfExport.Export(testCompositePlot, exportStream);
            }

            using (var exportStream = File.Create(@"testevolution.pdf"))
            {
                var pdfExport = new PdfExporter
                {
                    Width = 600,
                    Height = 600
                };
                pdfExport.Export(testEvolutionPlot, exportStream);
            }

            using (var exportStream = File.Create(@"testsingle.pdf"))
            {
                var pdfExport = new PdfExporter
                {
                    Width = 600,
                    Height = 600
                };
                pdfExport.Export(testSinglePlot, exportStream);
            }

        }
            
    }


    
}