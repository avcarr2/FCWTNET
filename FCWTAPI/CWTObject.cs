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

        public double[,]? OutputCWT { get; private set; }
        public double[]? FrequencyAxis { get; private set; }
        public double[]? TimeAxis { get; private set; }
        public string? WorkingPath { get; }

        public CWTObject(double[] inputData, int psoctave, int pendoctave, int pnbvoice, float c0, int nthreads, bool use_optimization_schemes, int? samplingRate = null, string? workingPath = null)
        {
            InputData = inputData;
            Psoctave = psoctave;
            Pendoctave = pendoctave;
            Pnbvoice = pnbvoice;
            C0 = c0;
            Nthreads = nthreads;
            Use_Optimization_Schemes = use_optimization_schemes;
            SamplingRate = samplingRate;
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
            float[][] rawJaggedCWT = FCWTAPI.CWT(InputData, Psoctave, Pendoctave, Pnbvoice, C0, Nthreads, Use_Optimization_Schemes);
            float[][] fixedJaggedCWT = new float[rawJaggedCWT.Length][];
            // Inverts frequency axis to make working with OutputCWT more intuitive
            for (int i = 1; i <= rawJaggedCWT.GetLength(0); i++)
            {
                fixedJaggedCWT[rawJaggedCWT.GetLength(0) - i] = rawJaggedCWT[i - 1];
            }
            float[,] floatCWT = FCWTAPI.ToTwoDArray(fixedJaggedCWT);
            OutputCWT = FCWTAPI.ConvertFloat2DtoDouble(floatCWT);

        }
        /// <summary>
        /// Function to separate the real and imaginary components of the CWT for complex wavelets
        /// </summary>
        /// <param name="real">If true, returns the real component of the CWT. If false, returns the imaginary component</param>
        /// <returns name="outputArray">double[,] containing the desired component of the CWT</returns>
        /// <exception cref="ArgumentNullException">Throws an error if the CWT has not been performed yet</exception>
        /// <exception cref="ArgumentException">Throws an error if there are an odd number of rows in OutputCWT</exception>
        public void SplitRealAndImaginary(CWTComponent comp, out double[,]? realCwt, out double[,]? imagCwt)
        {
            realCwt = null;
            imagCwt = null;
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before performing an operation on it");
            }
            switch (comp)
            {
                case CWTComponent.Real:
                    realCwt = GetComponent(CWTComponent.Real, OutputCWT);
                    break;
                case CWTComponent.Imaginary:
                    imagCwt = GetComponent(CWTComponent.Imaginary, OutputCWT);
                    break;
                case CWTComponent.Both:
                    GetBothComponents(OutputCWT, out double[,] real, out double[,] imag);
                    realCwt = real;
                    imagCwt = imag;
                    break;
                default:
                    break;
            }
        }
        private double[,] GetComponent(CWTComponent comp, double[,] originalArray)
        {
            int originalRowIndexer = 0;
            int rowNumber = originalArray.GetLength(0);
            int colNumber = originalArray.GetLength(1);
            double[,] outputArray = new double[rowNumber / 2, colNumber];
            if (comp == CWTComponent.Real)
            {
                originalRowIndexer = 0;
            }
            else if (comp == CWTComponent.Imaginary)
            {
                originalRowIndexer = 1;
            }
            else if (comp == CWTComponent.Both)
            {
                GetBothComponents(originalArray, out double[,] real, out double[,] imag);
            }
            for (int i = 0; i < rowNumber / 2; i++)
            {
                for (int j = 0; j < colNumber; j++)
                {
                    outputArray[i, j] = originalArray[originalRowIndexer, j];
                }
                originalRowIndexer += 2;
            }
            return outputArray;
        }
        private void GetBothComponents(double[,] originalArray, out double[,] real,
            out double[,] imag)
        {
            int originalRowIndexer = 0;
            int rowNumber = originalArray.GetLength(0);
            int colNumber = originalArray.GetLength(1);
            real = new double[rowNumber / 2, colNumber];
            imag = new double[rowNumber / 2, colNumber];

            for (int i = 0; i < real.GetLength(0) - 1; i++)
            {
                for (int j = 0; j < colNumber; j++)
                {
                    real[i, j] = originalArray[originalRowIndexer, j];
                    imag[i, j] = originalArray[originalRowIndexer + 1, j];
                }
                originalRowIndexer += 2;
            }
        }


        /// <summary>
        /// Method to calculate the modulus of the CWT
        /// </summary>
        /// <returns name="outputArray">double[,] containing the result of the calculation</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public double[,] ModulusCalculation()
        {
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before operating on it");
            }
            if (OutputCWT.GetLength(0) % 2 != 0)
            {
                throw new ArgumentException("Cannot extract Real and Imaginary components from a non complex CWT");
            }
            int originalIndex = 0;
            int outputRowLength = OutputCWT.GetLength(0) / 2;
            double[,] outputArray = new double[outputRowLength, OutputCWT.GetLength(1)];
            for (int i = 0; i < outputRowLength; i++)
            {
                int realRowIndex = originalIndex;
                int imagRowIndex = originalIndex + 1;
                for (int j = 0; j < OutputCWT.GetLength(1); j++)
                {
                    double modPoint = Math.Sqrt(OutputCWT[realRowIndex, j] * OutputCWT[realRowIndex, j] + OutputCWT[imagRowIndex, j] * OutputCWT[imagRowIndex, j]);
                    outputArray[i, j] = modPoint;
                }
                originalIndex += 2;

            }
            return outputArray;
        }
        /// <summary>
        /// Method to calculate the phase of the CWT
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public double[,] PhaseCalculation()
        {
            if (OutputCWT == null)
            {
                throw new ArgumentNullException("CWT must be performed before performing an operation on it");
            }
            if (OutputCWT.GetLength(0) % 2 != 0)
            {
                throw new ArgumentException("Cannot extract Real and Imaginary components from a non complex CWT");
            }
            int originalIndex = 0;
            int outputRowLength = OutputCWT.GetLength(0) / 2;
            double[,] outputArray = new double[outputRowLength, OutputCWT.GetLength(1)];
            for (int i = 0; i < outputRowLength - 1; i++)
            {
                int realRowIndex = originalIndex;
                int imagRowIndex = originalIndex + 1;
                for (int j = 0; j < OutputCWT.GetLength(1); j++)
                {
                    double realImRatio = OutputCWT[imagRowIndex, j] / OutputCWT[realRowIndex, j];
                    outputArray[i, j] = Math.Atan(realImRatio);
                }
                originalIndex += 2;
            }
            return outputArray;
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
            for (int i = 1; i <= octaveNum * Pnbvoice; i++)
            {
                double divisor = Math.Pow(2, Psoctave + i * deltaA);
                freqArray[octaveNum * Pnbvoice - i] = C0 / divisor;
            }
            FrequencyAxis = freqArray;
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
            double[] timeArray = new double[OutputCWT.GetLength(1)];
            double timeStep = 1000 / (double)SamplingRate;
            double currentTime = 0;
            for (int i = 0; i < OutputCWT.GetLength(1); i++)
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
        public void GenerateHeatMap(CWTFeatures cwtFeature, string fileName, string? dataName = null)
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
            double[,] data;
            if (cwtFeature == CWTFeatures.Imaginary)
            {
                data = GetComponent(CWTComponent.Imaginary, OutputCWT);
            }
            else if (cwtFeature == CWTFeatures.Real)
            {
                data = GetComponent(CWTComponent.Real, OutputCWT);
            }
            else if (cwtFeature == CWTFeatures.Modulus)
            {
                data = ModulusCalculation();
            }
            else
            {
                data = PhaseCalculation();
            }
            string title;
            if (cwtFeature == CWTFeatures.Imaginary || cwtFeature == CWTFeatures.Real)
            {
                title = cwtFeature.ToString() + "Component Plot";
            }
            else
            {
                title = cwtFeature.ToString() + "Plot";
            }
            if (dataName != null)
            {
                title = dataName + title;
            }
            PlotModel cwtPlot = PlottingUtils.GenerateCWTHeatMap(data, title, TimeAxis, FrequencyAxis);
            string filePath = Path.Combine(WorkingPath, fileName);
            PlottingUtils.ExportPlotPDF(cwtPlot, filePath);
        }
        
        // This method is not ready yet, but I want to talk to you about these before I go ahead and implement it.
        //public void GenerateXYPlot(CWTFeatures cwtFeature, string fileName, PlottingUtils.XYPlotOptions plotMode, double? startFrequency, double? endFrequency, int? sampleNumber, string? dataName = null)
        //{
        //    if (TimeAxis == null)
        //    {
        //        throw new ArgumentNullException(nameof(TimeAxis), "TimeAxis cannot be null");

        //    }
        //    if (OutputCWT == null)
        //    {
        //        throw new ArgumentNullException(nameof(OutputCWT), "OutputCWT cannot be null");
        //    }
        //    if (FrequencyAxis == null)
        //    {
        //        throw new ArgumentNullException(nameof(FrequencyAxis), "FrequencyAxis cannot be null");
        //    }
        //    if (Path.GetExtension(fileName) != ".pdf")
        //    {
        //        throw new ArgumentException(nameof(fileName), "fileName must have the .pdf extension");
        //    }
        //    double[,] data;
        //    if (cwtFeature == CWTFeatures.Imaginary)
        //    {
        //        data = GetComponent(CWTComponent.Imaginary, OutputCWT);
        //    }
        //    else if (cwtFeature == CWTFeatures.Real)
        //    {
        //        data = GetComponent(CWTComponent.Real, OutputCWT);
        //    }
        //    else if (cwtFeature == CWTFeatures.Modulus)
        //    {
        //        data = ModulusCalculation();
        //    }
        //    else
        //    {
        //        data = PhaseCalculation();
        //    }
        //    string title;
        //    if (cwtFeature == CWTFeatures.Imaginary || cwtFeature == CWTFeatures.Real)
        //    {
        //        title = cwtFeature.ToString() +  "Component " + plotMode.ToString() + " Plot";
        //    }
        //    else
        //    {
        //        title = cwtFeature.ToString() + " " + plotMode.ToString() + " Plot";
        //    }
        //    if (dataName != null)
        //    {
        //        title = dataName + title;
        //    }

        //}
        public (int, int) GetIndicesForFrequencyRange(double startFrequency, double endFrequency)
        {
            
            if (FrequencyAxis == null)
            {
                throw new ArgumentNullException(nameof(FrequencyAxis), "FrequencyAxis cannot be null");
            }
            if (FrequencyAxis[0] > startFrequency)
            {
                throw new ArgumentException(nameof(startFrequency), "startFrequency cannot be less than the minimum frequency");
            }
            if (startFrequency >= endFrequency)
            {
                throw new ArgumentException(nameof(endFrequency), "endFrequency must be greater than startFrequency");
            }
            if (FrequencyAxis[^1] < endFrequency)
            {
                throw new ArgumentException(nameof(endFrequency), "endFrequency must not be greater than the maximum CWT frequency");
            }
            if(FrequencyAxis[^2] <= startFrequency)
            {
                return (FrequencyAxis.Length - 2, FrequencyAxis.Length - 1);
            }
            //else
            //{
            //    int searchDivisor = 2;
            //    int midIndex = (FrequencyAxis.Length + 1) / searchDivisor;                
            //    while ((FrequencyAxis[midIndex] > startFrequency && FrequencyAxis[midIndex - 1] > startFrequency) || (FrequencyAxis[midIndex] < startFrequency && FrequencyAxis[midIndex + 1] < startFrequency))
            //    {
            //        searchDivisor = searchDivisor * 2;
            //        if (FrequencyAxis[midIndex] > startFrequency && FrequencyAxis[midIndex - 1] > startFrequency)
            //        {
            //            midIndex =+ (FrequencyAxis.Length + 1) / searchDivisor;
            //        }
            //        else
            //        {
            //            midIndex = +(FrequencyAxis.Length + 1) / searchDivisor;
            //        }
            //    }
            else
            {
                int rawStartIndex = Array.BinarySearch(FrequencyAxis, startFrequency);
                double axisStartFrequency;
                int axisStartIndex;
                int positiveStartIndex;
                if (rawStartIndex < 0)
                {
                    positiveStartIndex = rawStartIndex * -1 - 1;
                }
                else
                {
                    positiveStartIndex = rawStartIndex;
                }

                if (FrequencyAxis[positiveStartIndex] > startFrequency)
                {
                    axisStartIndex = positiveStartIndex - 1;
                }
                else
                {
                    axisStartIndex = positiveStartIndex;
                }
                axisStartFrequency = FrequencyAxis[positiveStartIndex];
                double deltaA = 1 / Convert.ToDouble(Pnbvoice);
                int numFreqs = Convert.ToInt32(Math.Ceiling(Math.Log2(endFrequency / axisStartFrequency) / deltaA));
                int axisEndIndex = axisStartIndex + numFreqs;
                return (axisStartIndex, axisEndIndex);
                
            }
            
        }           
    }
}
