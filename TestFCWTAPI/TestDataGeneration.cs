using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestFCWTAPI
{
    public class TestDataGeneration
    {
        /// <summary>
        /// Generates a double[1000,1000] containing a 2D Gaussian function
        /// </summary>
        /// <returns></returns>
        public static double[,] GenerateGaussian()
        {
            // Generate 1D Gaussian distribution
            var singleData = new double[1000];
            for (int x = 0; x < 1000; ++x)
            {
                singleData[x] = Math.Exp((-1.0 / 2.0) * Math.Pow(((double)x - 500.0) / 200, 2.0));
            }

            // Generate 2D Gaussian distribution
            var data = new double[1000, 1000];
            for (int x = 0; x < 1000; ++x)
            {
                for (int y = 0; y < 1000; ++y)
                {
                    data[x, y] = singleData[x] * singleData[y] * 1000;
                }
            }
            return data;
        }
        /// <summary>
        /// Generates a double[1000,1000] containing asymmetrical gaussian data
        /// The first 400 x values are multiplied by 4 and the first 200 y values are multiplied by 3
        /// This is used to check that plotting functions preserve data orientation
        /// </summary>
        /// <returns></returns>
        public static double[,] GenerateAsymmetricalGaussian()
        {
            // Generate 1D Gaussian distribution
            var xsingleData = new double[1000];
            var ysingleData = new double[1000];
            for (int i = 0; i < 1000; ++i)
            {
                if (i < 400)
                {
                    xsingleData[i] = 4 * Math.Exp((-1.0 / 2.0) * Math.Pow(((double)i - 500.0) / 200, 2.0));
                }
                else
                {
                    xsingleData[i] = Math.Exp((-1.0 / 2.0) * Math.Pow(((double)i - 500.0) / 200, 2.0));
                }
                if (i < 200)
                {
                    ysingleData[i] = 3 * Math.Exp((-1.0 / 2.0) * Math.Pow(((double)i - 500.0) / 200, 2.0));
                }
                else
                {
                    ysingleData[i] = Math.Exp((-1.0 / 2.0) * Math.Pow(((double)i - 500.0) / 200, 2.0));
                }
            }
            // Generate 2D Gaussian distribution
            var data = new double[1000, 1000];
            for (int x = 0; x < 1000; ++x)
            {
                for (int y = 0; y < 1000; ++y)
                {
                    data[x, y] = xsingleData[x] * ysingleData[y] * 1000;
                }
            }
            return data;
        }
    }
}
