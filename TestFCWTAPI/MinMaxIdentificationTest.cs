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
        [OneTimeSetUp]
        public static void TestDataSetup()
        {
            // Create test data from real transients
            string myoZ13TransientPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "SampleTransient_Myo_z13.csv");
            TransientData myoZ13Transient = new(myoZ13TransientPath);
            myoZ13Transient.ImportTransientData(200000);
            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles");
            var myoZ13CWT = new CWTObject(myoZ13Transient.Data, 2, 3, 100, (float)(2 * Math.Tau), 4, false, myoZ13Transient.SamplingRate, testPath);
            myoZ13CWT.PerformCWT();
            myoZ13CWT.CalculateTimeAxis();
            myoZ13CWT.CalculateFrequencyAxis();

            // Plot modulus and real heatmap for basic visualization 
            myoZ13CWT.GenerateHeatMap(CWTObject.CWTFeatures.Modulus, "myoZ13_modulus.pdf", "myo z13");
            myoZ13CWT.GenerateHeatMap(CWTObject.CWTFeatures.Real, "myoZ13_Real.pdf", "myo z13");
        }
        [Test]
        public static void DummyTest()
        {
            int dummy = 3;
        }
    }
}
