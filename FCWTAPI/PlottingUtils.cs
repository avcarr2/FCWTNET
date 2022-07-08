using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
namespace FCWTNET
{
    public class PlottingUtils
    {

        /// <summary>
        /// Example
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="includeContours"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static PlotModel GenerateGaussian(OxyPalette palette = null)
        {
            double x0 = 0;
            double x1 = 99;
            double y0 = 0;
            double y1 = 99;

            // generate 1d normal distribution
            var singleData = new double[100];
            for (int x = 0; x < 100; ++x)
            {
                singleData[x] = Math.Exp((-1.0 / 2.0) * Math.Pow(((double)x - 50.0) / 20.0, 2.0));
            }

            // generate 2d normal distribution
            var data = new double[100, 100];
            for (int x = 0; x < 100; ++x)
            {
                for (int y = 0; y < 100; ++y)
                {
                    data[y, x] = singleData[x] * singleData[(y + 30) % 100] * 100;
                }
            }


            var model = new PlotModel { Title = "Heatmap" };
            model.Axes.Add(new LinearColorAxis { Position = AxisPosition.Right, Palette = palette ?? OxyPalettes.Rainbow(100), HighColor = OxyColors.Gray, LowColor = OxyColors.Black });

            var hms = new HeatMapSeries
            {
                X0 = x0,
                X1 = x1,
                Y0 = y0,
                Y1 = y1,
                Data = data,
                RenderMethod = HeatMapRenderMethod.Bitmap
            };
            model.Series.Add(hms);

            return model;
        }
        public static PlotModel GenerateHeatMap(double[,] data)
        {
            int x0 = 0;
            int x1 = data.GetLength(0) - 1;
            int y0 = 0;
            int y1 = data.GetLength(1) - 1;
            var model = new PlotModel { Title = "Heatmap" };
            model.Axes.Add(new LinearColorAxis { Palette = OxyPalettes.Rainbow(100)});

            var hms = new HeatMapSeries
            {
                X0 = x0,
                X1 = x1,
                Y0 = y0,
                Y1 = y1,
                Data = data,
                RenderMethod = HeatMapRenderMethod.Bitmap
            };
            model.Series.Add(hms);

            return model;
        }
        /// <summary>
        /// Method to generate XY plots of individual, composite, or single rows of a 2D array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="rowIndices"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static PlotModel GenerateXYPlot(double[,] data, int[] rowIndices, XYPlotOptions mode)
        {
            string plotTitle = string.Format("{0} plot", mode.ToString());
            var plotModel = new PlotModel { Title = plotTitle };
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0.05, MaximumPadding = 0.05 });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, MinimumPadding = 0.05, MaximumPadding = 0.05 });
            if(mode == XYPlotOptions.Composite)
            {
                var compSeries = new LineSeries();
                
                for (int x = 0; x <= data.GetLength(0); x++)
                {
                    double compValue = 0;
                    for(int i = 0; i < rowIndices.Length; i++ )
                        {
                            compValue += data[rowIndices[i], x];
                        }
                    compSeries.Points.Add(new DataPoint(x, compValue));
                }
                compSeries.Title = string.Format("Composite from {0} to {1}", rowIndices[0].ToString());
                plotModel.Series.Add(compSeries);
                return plotModel;
            }
            else if(mode == XYPlotOptions.Evolution || mode == XYPlotOptions.Single)
            {
                LineSeries[] lineSeriesArray = new LineSeries[rowIndices.Length];
                for (int i = 0; i < rowIndices.Length; i++)
                {
                    var indivSer = new LineSeries();
                    for (int x = 0; x<= data.GetLength(0); x++)
                    {
                        indivSer.Points.Add(new DataPoint(x, data[rowIndices[i], x]));
                    }
                    indivSer.Title = String.Format("Voice {0}", rowIndices[i].ToString());
                    lineSeriesArray[i] = indivSer;
                }
                for (int i = 0; i < lineSeriesArray.Length; i++)
                {
                    plotModel.Series.Add(lineSeriesArray[i]);
                }
                return plotModel;
            }
            else
            {
                throw new ArgumentException("Invalid operation mode entered");
            }
        }
        public enum XYPlotOptions
        {
            Composite, 
            Evolution, 
            Single
        }
    }
}
