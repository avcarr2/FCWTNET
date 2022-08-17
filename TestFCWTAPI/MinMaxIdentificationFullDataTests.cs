using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;
using System.IO;
using NUnit.Framework;

namespace TestFCWTAPI
{
    public class MinMaxIdentificationFullDataTests
    {
        public CWTObject MyoglobinTestCWT { get; set; }
        public CWTObject ComplexTestCWT { get; set; }
        [OneTimeSetUp]
        public void TestDataSetup()
        {
            // Create test data from real transients
            string myoZ13TransientPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "SampleTransient_Myo_z13.csv");
            TransientData myoZ13Transient = new(myoZ13TransientPath);
            myoZ13Transient.ImportTransientData(800000);
            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            var myoZ13CWT = new CWTObject(myoZ13Transient.Data, 2, 3, 125, (float)(2 * Math.Tau), 4, false, myoZ13Transient.SamplingRate, myoZ13Transient.CalibrationFactor, testPath);
            myoZ13CWT.PerformCWT();
            myoZ13CWT.CalculateTimeAxis();
            myoZ13CWT.CalculateFrequencyAxis();
            MyoglobinTestCWT = myoZ13CWT;
            // Plot modulus and real heatmap for basic visualization 
            string complexTransientPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "ComplexTransient_protein_mixture_512ms.csv");
            TransientData complexTransient = new(complexTransientPath);
            complexTransient.ImportTransientData(700000);
            var complexTransientCWT = new CWTObject(complexTransient.Data, 2, 3, 125, (float)(15 * Math.Tau), 4, false, complexTransient.SamplingRate, complexTransient.CalibrationFactor, testPath);
            complexTransientCWT.PerformCWT();
            complexTransientCWT.CalculateTimeAxis();
            complexTransientCWT.CalculateFrequencyAxis();
            ComplexTestCWT = complexTransientCWT;
        }
        [Ignore("Long test time")]
        [Test]
        // Test method for generating plots to carefully examine the CWT of the sample MS transient data
        public void PlotGeneration()
        {
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "myoZ13_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13");
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "myoZ13_modulus_Zoomed.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13", 0.075, 0.095, 2.1, 3);
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "myoZ13_modulus_HiZoom.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13", 0.082, 0.083, 2.4, 2.5);
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Real, "myoZ13_Real.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13");
            ComplexTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "complexTransient_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "complexTransient");
            ComplexTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "zoomed_Band2complexTransient_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "complexTransient", 0.06, 0.08, 19, 30);
            ComplexTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "zoomed_Band1complexTransient_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "complexTransient", 0.02, 0.04, 19, 30);
        }
        [Ignore("Long test time")]
        [Test]
        // Test method to play around with smoothing
        public void SmoothingTests()
        {
            double[,] data = ComplexTestCWT.OutputCWT.ModulusCalculation();
            double[,] timeWindowedData;
            double[] dummyTimeAx;
            CWTExtensions.TimeWindowing(0.05, 0.09, ComplexTestCWT.TimeAxis, data, out dummyTimeAx, out timeWindowedData);
            data = timeWindowedData;
            double[,] freqWindowedData;
            double[] windowedFreqAxis;
            ComplexTestCWT.FrequencyAxis.FrequencyWindowing(21, 23.5, ComplexTestCWT.FrequencyAxis.WaveletCenterFrequencies, data, CWTFrequencies.FrequencyUnits.WaveletFrequency, out windowedFreqAxis, out freqWindowedData);
            data = freqWindowedData;
            double[,] smoothedData = GaussianSmoothing.EllipticGaussianConvolution(data, 1, 700);
            //double[,] smoothedData = data;
            double[,] xyReflectedData = new double[smoothedData.GetLength(1), smoothedData.GetLength(0)];
            for (int i = 0; i < smoothedData.GetLength(0); i++)
            {
                for (int j = 0; j < smoothedData.GetLength(1); j++)
                {
                    xyReflectedData[j, i] = smoothedData[i, j];
                }
            }
            var smoothedHeatMap = PlottingUtils.GenerateCWTHeatMap(xyReflectedData, "SmoothedBand2");
            string heatMapFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "smoothedHeatMap1freq700time.pdf");
            PlottingUtils.ExportPlotPDF(smoothedHeatMap, heatMapFilePath);
            string beatInBeatFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "beatinbeatSlice1f700t.pdf");
            int[] targetBeatInBeatIndex = new int[] { -1 * Array.BinarySearch(windowedFreqAxis, 22.2) - 1 };
            var xyPlotBeatInBeat = PlottingUtils.GenerateXYPlotCWT(xyReflectedData, targetBeatInBeatIndex, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single, "Slice at 22.2");
            PlottingUtils.ExportPlotPDF(xyPlotBeatInBeat, beatInBeatFilePath);
            string beatStdBeatFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "standardBeatSlice1f700t.pdf");
            int[] targetStandardBeatIndex = new int[] { -1 * Array.BinarySearch(windowedFreqAxis, 22.6) - 1 };
            var xyPlotStandardBeat = PlottingUtils.GenerateXYPlotCWT(xyReflectedData, targetStandardBeatIndex, PlottingUtils.PlotTitles.Custom, PlottingUtils.XYPlotOptions.Single, "Slice at 22.6");
            PlottingUtils.ExportPlotPDF(xyPlotStandardBeat, beatStdBeatFilePath);

        }
        [Test]
        public void SlicePlottingTest()
        {
            double[,] data = ComplexTestCWT.OutputCWT.ModulusCalculation();
            double[,] timeWindowedData;
            double[] dummyTimeAx;
            CWTExtensions.TimeWindowing(0.04, 0.11, ComplexTestCWT.TimeAxis, data, out dummyTimeAx, out timeWindowedData);
            data = timeWindowedData;
            double[,] freqWindowedData;
            double[] windowedFreqAxis;
            ComplexTestCWT.FrequencyAxis.FrequencyWindowing(21, 23.5, ComplexTestCWT.FrequencyAxis.WaveletCenterFrequencies, data, CWTFrequencies.FrequencyUnits.WaveletFrequency, out windowedFreqAxis, out freqWindowedData);
            data = freqWindowedData;
            int targetBeatInBeatIndex = -1 * Array.BinarySearch(windowedFreqAxis, 22.2) - 1;
            int targetStandardBeatIndex = -1 * Array.BinarySearch(windowedFreqAxis, 22.6) - 1;
            double[] rawBeatingSlice = new double[data.GetLength(1)];
            double[] rawStandardSlice = new double[data.GetLength(1)];
            for (int i = 0; i < data.GetLength(1); i++)
            {
                rawStandardSlice[i] = data[targetStandardBeatIndex, i];
                rawBeatingSlice[i] = data[targetBeatInBeatIndex, i];
            }
            double[] smoothedBeatingSlice = GaussianSmoothing.SliceEllipticGaussianConvolution(data, 1, 700, targetBeatInBeatIndex, 0);
            double[] smoothedStandardSlice = GaussianSmoothing.SliceEllipticGaussianConvolution(data, 1, 700, targetStandardBeatIndex, 0);
            var smoothedBeatingSlicePlot = PlottingUtils.Plot1DArray(smoothedBeatingSlice);
            var smoothedStandardSlicePlot = PlottingUtils.Plot1DArray(smoothedStandardSlice);
            string beatingSmoothSliceFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "beatingSliceSmoothed.pdf");
            string standardSmoothSliceFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "standardSliceSmoothed.pdf");
            PlottingUtils.ExportPlotPDF(smoothedStandardSlicePlot, standardSmoothSliceFilePath);
            PlottingUtils.ExportPlotPDF(smoothedBeatingSlicePlot, beatingSmoothSliceFilePath);
            
            double[] beatingSliceDeriv = MinMaxIdentification.StandardArrayDerivative(smoothedBeatingSlice, 5);
            var beatingSliceDerivPlot = PlottingUtils.Plot1DArray(beatingSliceDeriv);
            string beatingSliceDerivFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "beatingSliceDeriv5dist.pdf");
            PlottingUtils.ExportPlotPDF(beatingSliceDerivPlot, beatingSliceDerivFilePath);

            double[] standardSliceDeriv = MinMaxIdentification.StandardArrayDerivative(smoothedStandardSlice, 5);
            var standardSliceDerivPlot = PlottingUtils.Plot1DArray(standardSliceDeriv);
            string standardSliceDerivFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "standardSliceDeriv5dist.pdf");
            PlottingUtils.ExportPlotPDF(standardSliceDerivPlot, standardSliceDerivFilePath);
            double[] smoothStdSliceDeriv = GaussianSmoothing.GaussianSmoothing1D(standardSliceDeriv, 1000);
            var stdSmoothSliceDerivPlot = PlottingUtils.Plot1DArray(smoothStdSliceDeriv);
            string smoothStdSliceDerivPath = Path.Combine(ComplexTestCWT.WorkingPath, "smooth1000StDSliceDeriv5dist.pdf");
            PlottingUtils.ExportPlotPDF(stdSmoothSliceDerivPlot, smoothStdSliceDerivPath);

        }
        [Test]
        public void TestDerivitiveAnalysis()
        {
            double[,] data = ComplexTestCWT.OutputCWT.ModulusCalculation();
            double[,] timeWindowedData;
            double[] dummyTimeAx;
            CWTExtensions.TimeWindowing(0.04, 0.11, ComplexTestCWT.TimeAxis, data, out dummyTimeAx, out timeWindowedData);
            data = timeWindowedData;
            double[,] freqWindowedData;
            double[] windowedFreqAxis;
            ComplexTestCWT.FrequencyAxis.FrequencyWindowing(21, 23.5, ComplexTestCWT.FrequencyAxis.WaveletCenterFrequencies, data, CWTFrequencies.FrequencyUnits.WaveletFrequency, out windowedFreqAxis, out freqWindowedData);
            data = freqWindowedData;
            int targetBeatInBeatIndex = -1 * Array.BinarySearch(windowedFreqAxis, 22.2) - 1;
            int targetStandardBeatIndex = -1 * Array.BinarySearch(windowedFreqAxis, 22.6) - 1;
            double[] rawBeatingSlice = new double[data.GetLength(1)];
            double[] rawStandardSlice = new double[data.GetLength(1)];
            for (int i = 0; i < data.GetLength(1); i++)
            {
                rawStandardSlice[i] = data[targetStandardBeatIndex, i];
                rawBeatingSlice[i] = data[targetBeatInBeatIndex, i];
            }
            double[] smoothedStandardSlice = GaussianSmoothing.SliceEllipticGaussianConvolution(data, 1, 700, targetStandardBeatIndex, 0);
            var intFilteredStandardSlice = MinMaxIdentification.IndexLinkedIntensityFiltering(smoothedStandardSlice, 0.2);
            var intFiltStdPlot = PlottingUtils.PlotSortedPointDictionary(intFilteredStandardSlice);
            string standardSliceFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "intFiltStdSlicePlot.pdf");
            PlottingUtils.ExportPlotPDF(intFiltStdPlot, standardSliceFilePath);
            var downsampledStdDeriv = MinMaxIdentification.DownsampledSliceDerivative(intFilteredStandardSlice, 200);
            var dsampledDerivPlot = PlottingUtils.PlotSortedPointDictionary(downsampledStdDeriv);
            string standardDerivFilePath = Path.Combine(ComplexTestCWT.WorkingPath, "StdSliceDownsampledDeriv200Deriv.pdf");
            PlottingUtils.ExportPlotPDF(dsampledDerivPlot, standardDerivFilePath);
            var smoothedStdDeriv = MinMaxIdentification.DownsampledDerivativeSmoothing(downsampledStdDeriv, 1000, 200);
            var smoothedDerivPlot = PlottingUtils.PlotSortedPointDictionary(smoothedStdDeriv);
            var smoothedStdDerivFile = Path.Combine(ComplexTestCWT.WorkingPath, "smoothed1000dsampled200derivStd.pdf");
            PlottingUtils.ExportPlotPDF(smoothedDerivPlot, smoothedStdDerivFile);
            var secondStdDeriv = MinMaxIdentification.StandardDerivative(smoothedStdDeriv, 200);
            var secondDerivPlot = PlottingUtils.PlotSortedPointDictionary(secondStdDeriv);
            var secondDerivFile = Path.Combine(ComplexTestCWT.WorkingPath, "smoothedDerivandsecondderiv.pdf");
            PlottingUtils.ExportPlotPDF(secondDerivPlot, secondDerivFile);            
        }
    }
}
