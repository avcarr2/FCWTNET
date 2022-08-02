using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FCWTNET;
using OxyPlot;

namespace FCWTNET
{
    /// <summary>
    /// Class to encapsulate the CWT and all relevant parameters
    /// Currently includes:
    /// CWT Calculation
    /// Separation of real and imaginary components of the CWT
    /// Calculation of the modulus of the CWT
    /// Calculation of the phase of the CWT
    /// </summary>
    public class CWTObject
    {
        public double[] InputData { get; }
        public int Psoctave { get; }
        public int Pendoctave { get; }
        public int Pnbvoice { get; }
        public float C0 { get; }
        public int Nthreads { get; }
        public bool Use_Optimization_Schemes { get; }
        public int? SamplingRate { get; }
        public double? CalibrationCoefficient { get; private set; }

        public CWTOutput? OutputCWT { get; private set; }
        public CWTFrequencies? FrequencyAxis { get; private set; }
        public double[]? TimeAxis { get; private set; }
        public string? WorkingPath { get; }

        public CWTObject(double[] inputData, int psoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes, 
            int? samplingRate = null, double? calibrationCoefficient = null, string? workingPath = null)
        {
            InputData = inputData;
            Psoctave = psoctave;
            Pendoctave = pendoctave;
            Pnbvoice = pnbvoice;
            C0 = c0;
            Nthreads = nthreads;
            Use_Optimization_Schemes = use_optimization_schemes;
            SamplingRate = samplingRate;
            CalibrationCoefficient = calibrationCoefficient;
            OutputCWT = null;
            FrequencyAxis = null;
            TimeAxis = null;
            WorkingPath = workingPath;
        }
        /// <summary>
        /// Function to perform the calculation of the CWT and return it as a double[,] in the CWTObject class called OutputCWT
        /// Inverts the original CWT output
        /// </summary>
        public void PerformCWT()
        {
            FCWTAPI.CWT(InputData, Psoctave, Pendoctave, Pnbvoice, C0, Nthreads, Use_Optimization_Schemes, 
                out double[][] flippedReal, out double[][] flippedImag);
            double[][] real = new double[flippedReal.Length][];
            double[][] imag = new double[flippedImag.Length][];
            for (int i = 1; i <= flippedReal.Length; i++)
            {
                real[^i] = flippedReal[i - 1];
                imag[^i] = flippedImag[i - 1];
            }
            OutputCWT = new CWTOutput(real, imag); 
        }
        
        public enum CWTComponent
        {
            Real,
            Imaginary,
            Both
        }
        /// <summary>
        /// Generates an array corresponding to the characteristic frequencies of the analyzing wavelet
        /// FrequencyAxis[0] corresponds to f for the analyzing wavelet at OutputCWT[0, ]
        /// </summary>
        public void CalculateFrequencyAxis()
        {
            int octaveNum = 1 + Pendoctave - Psoctave;
            double deltaA = 1 / Convert.ToDouble(Pnbvoice);
            double[] freqArray = new double[octaveNum * Pnbvoice];
            for (int i = 1 ; i <= octaveNum * Pnbvoice; i++)
            {
                freqArray[^i] = C0 / Math.Pow(2, (1 + (i) * deltaA));
            }
            FrequencyAxis = new CWTFrequencies(freqArray, Pnbvoice, SamplingRate, CalibrationCoefficient);            
        }
        /// <summary>
        /// Generates an array corresponding to the individual timepoints of the transient operated on by the CWT
        /// Timepoints are given in milliseconds
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void CalculateTimeAxis()
        {
            if (SamplingRate == null)
            {
                throw new ArgumentNullException("SamplingRate", "SamplingRate must be provided to calculate a time axis");
            }
            if (SamplingRate <= 0)
            {
                throw new ArgumentException("SamplingRate", "SamplingRate must be a positive, non-zero integer");

            }
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("OutputCWT", "Output CWT must be calculated prior to calculating a time axis for it");
            }
            double [] timeArray = new double[OutputCWT.RealArray.GetLength(1)];
            double timeStep = 1 / (double)SamplingRate;
            double currentTime = 0;
            for (int i = 0; i < OutputCWT.RealArray.GetLength(1); i++)
            {
                timeArray[i] = currentTime;
                currentTime += timeStep;
            }
            TimeAxis = timeArray;

        }
        public enum CWTFeatures
        {
            Imaginary,
            Real,
            Modulus,
            Phase
        }
        /// <summary>
        /// Generates a heatmap of a paticular CWT Feature
        /// </summary>
        /// <param name="cwtFeature">Enumerable to select whether the Modulus, Phase, Real or Imaginary component of CWT should be plotted</param>
        /// <param name="fileName">File to write the resulting plot to</param>
        /// <param name="dataName">Optional name of the data to pass into the title of the plot</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void GenerateHeatMap(CWTFeatures cwtFeature, string fileName, CWTFrequencies.FrequencyUnits 
            frequencyAxisUnits, string? dataName = null)
        {
            if (TimeAxis == null)
            {
                throw new ArgumentNullException(nameof(TimeAxis), "TimeAxis cannot be null");

            }
            if (OutputCWT == null)
            {
                throw new ArgumentNullException(nameof(OutputCWT), "OutputCWT cannot be null");
            }
            if (FrequencyAxis == null)
            {
                throw new ArgumentNullException(nameof(FrequencyAxis), "FrequencyAxis cannot be null");
            }
            if (Path.GetExtension(fileName) != ".pdf")
            {
                throw new ArgumentException(nameof(fileName), "fileName must have the .pdf extension");
            }
            double[] plotFrequencyAxis;
            switch (frequencyAxisUnits)
            {
                case CWTFrequencies.FrequencyUnits.TrueFrequency:
                    if (FrequencyAxis.TrueFrequencies.Equals(null))
                    {
                        throw new NullReferenceException("Error FrequencyAxis.TrueFrequencies is null");
                    }
                    plotFrequencyAxis = FrequencyAxis.TrueFrequencies;
                    break;
                case CWTFrequencies.FrequencyUnits.MZValues:
                    if (FrequencyAxis.MZValues.Equals(null))
                    {
                        throw new NullReferenceException("Error FrequencyAxis.MZValues is null");
                    }
                    plotFrequencyAxis = FrequencyAxis.MZValues;
                    break;
                default:
                    plotFrequencyAxis = FrequencyAxis.WaveletCenterFrequencies;
                    break;
            }
            double[,] data;
            if (cwtFeature == CWTFeatures.Imaginary)
            {
                data = OutputCWT.ImagArray;
            }
            else if (cwtFeature == CWTFeatures.Real)
            {
                data = OutputCWT.RealArray;
            }
            else if (cwtFeature == CWTFeatures.Modulus)
            {
                data = OutputCWT.ModulusCalculation();
            }
            else
            {
                data = OutputCWT.PhaseCalculation();
            }
            string title;
            if (cwtFeature == CWTFeatures.Imaginary || cwtFeature == CWTFeatures.Real)
            {
                title = cwtFeature.ToString() + " Component Plot";
            }
            else
            {
                title = cwtFeature.ToString() + " Plot";
            }
            if (dataName != null)
            {
                title = dataName + title;
            }
            // Reflects data about the xy axis to plot CWT data with Freqeuncy in the y-axis and time in the x-axis
            double[,] xyReflectedData = new double[data.GetLength(1), data.GetLength(0)];
            for (int i = 0; i < data.GetLength(0); i++)
            {
                for (int j = 0; j < data.GetLength(1); j++)
                {
                    xyReflectedData[j, i] = data[i, j];
                }
            }            
            PlotModel cwtPlot = PlottingUtils.GenerateCWTHeatMap(xyReflectedData, title, TimeAxis, plotFrequencyAxis);
            string filePath = Path.Combine(WorkingPath, fileName);
            PlottingUtils.ExportPlotPDF(cwtPlot, filePath);
        }

        /// <summary>
        /// Method to generate different 2D XY Plots from the CWT along frequency bands
        /// Allows for the generation of composite, evolution and single frequency band plots
        /// </summary>
        /// <param name="cwtFeature">Feature of the CWT to be plotted</param>
        /// <param name="fileName">File to write the plot to</param>
        /// <param name="plotMode">Specifies the type of plot to generate</param>
        /// <param name="startValue">Starting frequency to sample for the plot</param>
        /// <param name="endValue">End frequency to sample for the plot</param>
        /// <param name="sampleNumber">Number of frequencies to sample</param>
        /// <param name="dataName">Name of the data to enter</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void GenerateXYPlot(CWTFeatures cwtFeature, string fileName, PlottingUtils.XYPlotOptions plotMode, CWTFrequencies.FrequencyUnits frequencyAxisUnits,
            double startValue, double ? endValue = null, int? sampleNumber = null, string? dataName = null)
        {
            if (TimeAxis == null)
            {
                throw new ArgumentNullException(nameof(TimeAxis), "TimeAxis cannot be null");

            }
            if (OutputCWT == null)
            {
                throw new ArgumentNullException(nameof(OutputCWT), "OutputCWT cannot be null");
            }
            if (FrequencyAxis == null)
            {
                throw new ArgumentNullException(nameof(FrequencyAxis), "FrequencyAxis cannot be null");
            }
            if (Path.GetExtension(fileName) != ".pdf")
            {
                throw new ArgumentException(nameof(fileName), "fileName must have the .pdf extension");
            }
            double[] plotFrequencyAxis;
            switch (frequencyAxisUnits)
            {
                case CWTFrequencies.FrequencyUnits.TrueFrequency:
                    if (FrequencyAxis.TrueFrequencies.Equals(null))
                    {
                        throw new NullReferenceException("Error FrequencyAxis.TrueFrequencies is null");
                    }
                    plotFrequencyAxis = FrequencyAxis.TrueFrequencies;
                    break;
                case CWTFrequencies.FrequencyUnits.MZValues:
                    if (FrequencyAxis.MZValues.Equals(null))
                    {
                        throw new NullReferenceException("Error FrequencyAxis.MZValues is null");
                    }
                    plotFrequencyAxis = FrequencyAxis.MZValues;
                    break;
                default:
                    if (FrequencyAxis.WaveletCenterFrequencies.Equals(null))
                    {
                        throw new NullReferenceException("Error FrequencyAxis.WaveletCenterFrequencies is null");
                    }
                    plotFrequencyAxis = FrequencyAxis.WaveletCenterFrequencies;
                    break;
            }
            double[,] data;
            if (cwtFeature == CWTFeatures.Imaginary)
            {
                data = OutputCWT.ImagArray;
            }
            else if (cwtFeature == CWTFeatures.Real)
            {
                data = OutputCWT.RealArray;
            }
            else if (cwtFeature == CWTFeatures.Modulus)
            {
                data = OutputCWT.ModulusCalculation();
            }
            else
            {
                data = OutputCWT.PhaseCalculation();
            }
            string title;
            if (cwtFeature == CWTFeatures.Imaginary || cwtFeature == CWTFeatures.Real)
            {
                title = cwtFeature.ToString() + "Component " + plotMode.ToString() + " Plot";
            }
            else
            {
                title = cwtFeature.ToString() + " " + plotMode.ToString() + " Plot";
            }
            if (dataName != null)
            {
                title = dataName + title;
            }

            int[] indFrequencies;
            if (plotMode == PlottingUtils.XYPlotOptions.Evolution || plotMode == PlottingUtils.XYPlotOptions.Composite)
            {

                if(endValue != null && sampleNumber != null)
                {
                    (int, int) freqIndices = FrequencyAxis.CalculateIndicesForFrequencyRange((double)startValue, (double)endValue, frequencyAxisUnits);
                    int maxFrequencies = freqIndices.Item2 - freqIndices.Item1;                    
                    if (sampleNumber < (maxFrequencies))
                    {
                        indFrequencies = new int[(int)sampleNumber];
                        double virtualLocation = 0;
                        double stepSize = ((double)(freqIndices.Item2 - freqIndices.Item1) - 1) / ((double)sampleNumber - 1);
                        for (int i = 0; i < sampleNumber; i++)
                        {
                            if (i < sampleNumber - 1)
                            {
                                indFrequencies[i] = freqIndices.Item1 + Convert.ToInt32(Math.Floor(virtualLocation));
                            }
                            else
                            {
                                indFrequencies[i] = freqIndices.Item2;
                            }
                            virtualLocation += stepSize;
                        }
                    }
                    else
                    {
                        indFrequencies = new int[maxFrequencies];
                        for (int i = 0; i < maxFrequencies; i++)
                        {
                            indFrequencies[i] = freqIndices.Item1 + i;
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException("Neither startFreqeuncy nor endFrequency may be null");
                }
                
            }
            else
            {
                double singleFrequency;
                switch (frequencyAxisUnits)
                {
                    case CWTFrequencies.FrequencyUnits.TrueFrequency:
                        singleFrequency = FrequencyAxis.TrueFreqToWaveletFreq(startValue);
                        break;
                    case CWTFrequencies.FrequencyUnits.MZValues:
                        singleFrequency = FrequencyAxis.MZValueToWaveletFreq(startValue);
                        break;
                    default:
                        singleFrequency = startValue;
                        break;
                }
                int rawFrequencyIndex = Array.BinarySearch(FrequencyAxis.WaveletCenterFrequencies, singleFrequency);
                int frequencyIndex = rawFrequencyIndex < 0 ? -rawFrequencyIndex + 1 : rawFrequencyIndex;
                indFrequencies = new int[] {frequencyIndex};
            }
            double[,] xyReflectedData = new double[data.GetLength(1), data.GetLength(0)];
            for (int i = 0; i < data.GetLength(0); i++)
            {
                for (int j = 0; j < data.GetLength(1); j++)
                {
                    xyReflectedData[j, i] = data[i, j];
                }
            }
            PlotModel cwtPlot = PlottingUtils.GenerateXYPlotCWT(xyReflectedData, indFrequencies, TimeAxis, plotFrequencyAxis, PlottingUtils.PlotTitles.Custom, plotMode, title);

            string filePath = Path.Combine(WorkingPath, fileName);
            PlottingUtils.ExportPlotPDF(cwtPlot, filePath);

        }
               
    }
}
