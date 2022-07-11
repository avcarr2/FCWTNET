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
        /// Generates a double[1000,1000] containing a 2D Gaussian function
        /// </summary>
        /// <returns></returns>
        public static double[,] GenerateGaussian()
        {
            // Generate 1D normal distribution
            var singleData = new double[1000];
            for (int x = 0; x < 1000; ++x)
            {
                singleData[x] = Math.Exp((-1.0 / 2.0) * Math.Pow(((double)x - 500.0) / 200, 2.0));
            }

            // Generate 2D normal distribution
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
        /// Creates a heat map plot from a 2D array
        /// Functionality will be added to scale axes with time and frequency info from CWT
        /// </summary>
        /// <param name="data"> Input double[,] to plot</param>
        /// <param name="title">Plot title</param>
        /// <returns></returns>
        public static PlotModel GenerateHeatMap(double[,] data, string title)
        {
            int x0 = 0;
            int x1 = data.GetLength(0) - 1;
            int y0 = 0;
            int y1 = data.GetLength(1) - 1;
            var model = new PlotModel { Title = title };
            model.Axes.Add(new LinearColorAxis { Palette = OxyPalettes.Rainbow(100)});
            model.Axes.Add(new LinearAxis 
            { 
                Position = AxisPosition.Bottom, 
                Title = "Time (units)",
                FontSize = 14,
                TitleFontSize = 16
                
            });
            model.Axes.Add(new LinearAxis 
            { 
                Position = AxisPosition.Left, 
                Title = "f_{0} (units)",
                FontSize = 14,
                TitleFontSize = 16
            });
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
        /// Functionality to incorporate time and frequency information later will be added
        /// </summary>
        /// <param name="data"></param>
        /// <param name="rowIndices">int[] of rows to plot</param>
        /// the "rowIndices" parameter will likely change later to allow time point selection
        /// <param name="mode">Set the operation mode from the XYPlotOptions Enumerable</param>
        /// Composite averages a set of rows to create a single composite plot
        /// Evolution plots all selected rows in the same plot
        /// Single plots a single row
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static PlotModel GenerateXYPlot(double[,] data, int[] rowIndices, XYPlotOptions mode)
        {
            string plotTitle = string.Format("{0} plot", mode.ToString());
            var plotModel = new PlotModel { Title = plotTitle };
            plotModel.Axes.Add(new LinearAxis 
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0.05, 
                MaximumPadding = 0.05,
                Title = "Time (units)",
                FontSize = 14,
                TitleFontSize = 16
            });
            plotModel.Axes.Add(new LinearAxis 
            { 
                Position = AxisPosition.Left, 
                MinimumPadding = 0.05, 
                MaximumPadding = 0.05,
                Title = "f_{0} (units)",
                FontSize = 14,
                TitleFontSize = 16
            });
            if(mode == XYPlotOptions.Composite)
            {
                var compSeries = new LineSeries();
                
                for (int x = 0; x < data.GetLength(0); x++)
                {
                    double compValue = 0;
                    for(int i = 0; i < rowIndices.Length; i++ )
                        {
                            compValue += data[rowIndices[i], x];
                        }
                    compSeries.Points.Add(new DataPoint(x, compValue));
                }
                compSeries.Title = string.Format("Composite from f0 = {0} to f0 = {1}", rowIndices[0].ToString(), rowIndices[rowIndices.Length -1]);
                plotModel.Series.Add(compSeries);
                var legend = new OxyPlot.Legends.Legend
                {
                    LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
                    LegendPosition = OxyPlot.Legends.LegendPosition.TopRight
                };
                plotModel.Legends.Add(legend);

                return plotModel;
            }
            else if(mode == XYPlotOptions.Evolution || mode == XYPlotOptions.Single)
            {
                LineSeries[] lineSeriesArray = new LineSeries[rowIndices.Length];
                for (int i = 0; i < rowIndices.Length; i++)
                {
                    var indivSer = new LineSeries();
                    for (int x = 0; x < data.GetLength(0); x++)
                    {
                        indivSer.Points.Add(new DataPoint(x, data[rowIndices[i], x]));
                    }
                    indivSer.Title = "f_{0} = " + rowIndices[i].ToString();
                    lineSeriesArray[i] = indivSer;
                }
                for (int i = 0; i < lineSeriesArray.Length; i++)
                {
                    plotModel.Series.Add(lineSeriesArray[i]);
                }
                var legend = new OxyPlot.Legends.Legend
                {
                    LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
                    LegendPosition = OxyPlot.Legends.LegendPosition.RightMiddle,
                    LegendFontSize = 14,
                };
                plotModel.Legends.Add(legend);
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
