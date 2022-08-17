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
    public class MinMaxIdentificationSmallDataTests
    {
        [Test]
        public static void StandardArrayDerivative()
        {
            double[] testArray = new double[15];
            for (int i = 1; i <= testArray.Length; i++)
            {
                testArray[i - 1] = i * i;
            }
            double[] testDerivative = MinMaxIdentification.StandardArrayDerivative(testArray, 2);
            Assert.AreEqual(12, testDerivative[5]);
            Assert.AreEqual(5, testDerivative[1]);
            Assert.AreEqual(28, testDerivative[14]);
            Assert.Throws<ArgumentException>(() => MinMaxIdentification.StandardArrayDerivative(testArray, 9));
        }
        [Test]
        public static void TestIndexLinkedIntensityFiltering()
        {
            double[] testArray = new double[16];
            for (int i = 0; i < testArray.Length; i++)
            {
                testArray[i] = Math.Sin(Math.PI * i / 8);                
            }
            SortedDictionary<int, double> testIntensityFiltering = MinMaxIdentification.IndexLinkedIntensityFiltering(testArray, 0.5);
            foreach(var pair in testIntensityFiltering)
            {
                Assert.IsTrue(Math.Abs(pair.Value) >= 0.5);
                Assert.AreEqual(testArray[pair.Key], pair.Value);
            }
            Assert.Throws<ArgumentException>(() => MinMaxIdentification.IndexLinkedIntensityFiltering(testArray, -1));
            Assert.Throws<ArgumentException>(() => MinMaxIdentification.IndexLinkedIntensityFiltering(testArray, 2));
        }
        [Test]
        public static void TestDownsampledSliceDerivative()
        {
            double[] testArray = new double[32];
            for (int i = 0; i < testArray.Length; i++)
            {
                testArray[i] = Math.Sin(Math.PI * i / 16);
            }
            SortedDictionary<int, double> filteredPointSet = MinMaxIdentification.IndexLinkedIntensityFiltering(testArray, 0.2);
            int testDerivativeDistance = 1;
            SortedDictionary<int, double> downsampledDeriv = MinMaxIdentification.DownsampledSliceDerivative(filteredPointSet, testDerivativeDistance);
            Assert.AreEqual(filteredPointSet.Count / (2 * testDerivativeDistance + 1), downsampledDeriv.Count);
            foreach(var point in downsampledDeriv)
            {
                double expectedDeriv = (double)(filteredPointSet[point.Key + 1] - filteredPointSet[point.Key - 1]) / 2;
                Assert.AreEqual(expectedDeriv, point.Value, 0.001);
            }
            Assert.Throws<Exception>(() => MinMaxIdentification.DownsampledSliceDerivative(filteredPointSet, 33));
        }
        public static void TestDownsampledDerivativeSmoothing()
        {

        }

    }
}
