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
    public class MinMaxIdentificationTest
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
            var myoZ13CWT = new CWTObject(myoZ13Transient.Data, 2, 3, 100, (float)(2 * Math.Tau), 4, false, myoZ13Transient.SamplingRate, myoZ13Transient.CalibrationFactor, testPath);
            myoZ13CWT.PerformCWT();
            myoZ13CWT.CalculateTimeAxis();
            myoZ13CWT.CalculateFrequencyAxis();
            MyoglobinTestCWT = myoZ13CWT;
            // Plot modulus and real heatmap for basic visualization 
            string complexTransientPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "ComplexTransient_protein_mixture_512ms.csv");
            TransientData complexTransient = new(complexTransientPath);
            complexTransient.ImportTransientData(700000);
            var complexTransientCWT = new CWTObject(complexTransient.Data, 2, 3, 100, (float)(15 * Math.Tau), 4, false, complexTransient.SamplingRate, complexTransient.CalibrationFactor, testPath);
            complexTransientCWT.PerformCWT();
            complexTransientCWT.CalculateTimeAxis();
            complexTransientCWT.CalculateFrequencyAxis();
            ComplexTestCWT = complexTransientCWT;
        }
        [Test]
        public void PlotGeneration()
        {
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "myoZ13_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13");
            MyoglobinTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Real, "myoZ13_Real.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "myo z13");
            ComplexTestCWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "complexTransient_modulus.pdf", CWTFrequencies.FrequencyUnits.WaveletFrequency, "complexTransient");
        }
    }
}
