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
        [Test]
        public static void TestDownsampledDerivativeSmoothing()
        {
            double[] testArray = new double[128];
            for (int i = 0; i < testArray.Length; i++)
            {
                if (i < 32 || (i > 64 && i < 96))
                {
                    testArray[i] = Math.Sin(Math.PI * i / 32);
                }
                else
                {
                    testArray[i] = 0.3 * Math.Sin(Math.PI * i / 32);
                }
                
            }
            SortedDictionary<int, double> filteredPointSet = MinMaxIdentification.IndexLinkedIntensityFiltering(testArray, 0.2);
            int testDerivativeDistance = 1;
            SortedDictionary<int, double> downsampledDeriv = MinMaxIdentification.DownsampledSliceDerivative(filteredPointSet, testDerivativeDistance);
            List<double> SmoothableCluster = new List<double>();
            List<double> UnsmoothableCluster = new List<double>();

            foreach(var point in downsampledDeriv)
            {
                if (point.Key < 32)
                {
                    SmoothableCluster.Add(point.Value);
                }
                else if(point.Key < 64)
                {
                    UnsmoothableCluster.Add(point.Value);
                }
            }
            double[] smoothedCluster = GaussianSmoothing.GaussianSmoothing1D(SmoothableCluster.ToArray(), 1);
            SortedDictionary<int, double> testSmoothedDeriv = MinMaxIdentification.DownsampledDerivativeSmoothing(downsampledDeriv, 3, testDerivativeDistance);
            int firstSmoothableClusterCounter = 0;
            int secondSmoothableClusterCounter = 0;
            int firstUnsmoothedClusterCounter = 0;
            int secondUnsmoothedClusterCounter = 0;
            foreach (var point in testSmoothedDeriv)
            {
                if (point.Key < 32)
                {
                    Assert.AreEqual(smoothedCluster[firstSmoothableClusterCounter], point.Value, 0.00001);
                    firstSmoothableClusterCounter++;
                }
                else if (point.Key < 64)
                {
                    Assert.AreEqual(UnsmoothableCluster[firstUnsmoothedClusterCounter], point.Value, 0.00001);
                    firstUnsmoothedClusterCounter++;
                }
                else if (point.Key < 96)
                {
                    Assert.AreEqual(smoothedCluster[secondSmoothableClusterCounter], point.Value, 0.00001);
                    secondSmoothableClusterCounter++;
                }
                else
                {
                    Assert.AreEqual(UnsmoothableCluster[secondUnsmoothedClusterCounter], point.Value, 0.00001);
                    secondUnsmoothedClusterCounter++;
                }
            }
            
        }
        [Test]
        public static void TestStandardDerivative()
        {
            SortedDictionary<int, double> sampleNoDerivativeDistance = new SortedDictionary<int, double>();
            SortedDictionary<int, double> sampleWithDerivativeDistance = new SortedDictionary<int, double>();
            int derivativeDistance = 1;
            for(int i = 0; i < 10; i++)
            {
                sampleNoDerivativeDistance.Add(i, i * i);
                sampleWithDerivativeDistance.Add((i * (2 * derivativeDistance + 1)) + derivativeDistance, i * i);
            }
            for (int i = 13; i < 20; i++)
            {
                sampleNoDerivativeDistance.Add(i, i * i);
                sampleWithDerivativeDistance.Add((i * (2 * derivativeDistance + 1)) + derivativeDistance, i * i);
            }
            SortedDictionary<int, double> derivativeNoDerivativeDistance = MinMaxIdentification.StandardDerivative(sampleNoDerivativeDistance);
            SortedDictionary<int, double> derivativeWithDerivativeDistance = MinMaxIdentification.StandardDerivative(sampleWithDerivativeDistance, 1);
            Assert.AreEqual(sampleWithDerivativeDistance.Count - 4, derivativeWithDerivativeDistance.Count);
            Assert.AreEqual(sampleNoDerivativeDistance.Count - 4, derivativeNoDerivativeDistance.Count);
            foreach(var point in derivativeNoDerivativeDistance)
            {
                double expectedDerivative = (sampleNoDerivativeDistance[point.Key + 1] - sampleNoDerivativeDistance[point.Key - 1]) / 2;
                Assert.AreEqual(expectedDerivative, point.Value);
            }
            foreach(var point in derivativeWithDerivativeDistance)
            {
                double expectedDerivative = (sampleWithDerivativeDistance[point.Key + 2 * derivativeDistance + 1] - sampleWithDerivativeDistance[point.Key - (2 * derivativeDistance + 1)]) / (2 * (2 * derivativeDistance + 1));
                Assert.AreEqual(expectedDerivative, point.Value);
            }
        }

    }
}
