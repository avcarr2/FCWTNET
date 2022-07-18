using NUnit.Framework;
using System;
using FCWTNET;
using System.Linq;
using OxyPlot;
using System.IO;
using System.Text;


namespace TestFCWTAPI
{
    public class TransientDataTests
    {
        [SetUp]
        public static void Setup()
        {

        }
        [Test]
        public static void TestTransientImport()
        {
            var testPoints = new (int, double)[]
            {
                (8, 755643.3),
                (27, -355027.38),
                (17, -405399.26),
            };
            string testDatapath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "SampleTransient_Myo_z13.csv");
            string badDatapath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestDataFiles", "badfile.csv");
            TransientData testTransient = new TransientData(testDatapath);
            int testPointnum = 40000;
            testTransient.ImportTransientData(testPointnum);
            for(int i = 0; i < testPoints.Length; i++)
            {
                Assert.AreEqual(testPoints[i].Item2, testTransient.Data[testPoints[i].Item1], 0.1);
            }
            Assert.AreEqual(testTransient.SamplingRate, 4000000);
            Assert.AreEqual((double)testTransient.CalibrationFactor, 211751487582899.97, 0.01);
            Assert.AreEqual(testTransient.Data.Length, testPointnum);
            TransientData nonexistantFile = new TransientData("notarealfile.csv");
            Assert.Throws<FileNotFoundException>(() => nonexistantFile.ImportTransientData());
            TransientData badData = new TransientData(badDatapath);
            Assert.Throws<FormatException>(() => badData.ImportTransientData());
            TransientData notCSV = new TransientData("notacsvfile.pdf");
            Assert.Throws<ArgumentException>(() => notCSV.ImportTransientData());
        }

    }


    
}