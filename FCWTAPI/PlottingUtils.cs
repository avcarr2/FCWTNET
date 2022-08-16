using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
namespace FCWTNET
{
    public class PlottingUtils
    {
        
        /// <summary>
        /// Creates a heat map plot from a 2D array with proper frequency and time axes
        /// </summary>
        /// <param name="data"> Input double[,] to plot</param>
        /// <param name="plotTitle">String containing the desired plot title</param>
        /// <param name="timeAxis">double[] containing CWT timepoints</param>
        /// <param name="freqAxis">double[] containing CWT frequencies</param>
        /// <returns></returns>
        public static PlotModel GenerateCWTHeatMap(double[,] data, string plotTitle, double[] timeAxis, double[] freqAxis)
        {
            double x0 = timeAxis[0];
            double x1 = timeAxis[^1];
            double y0 = freqAxis[0];
            double y1 = freqAxis[^1];
            var model = new PlotModel { Title = plotTitle };
            model.Axes.Add(new LinearColorAxis { Palette = OxyPalettes.Rainbow(100), Position = AxisPosition.Right});
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (ms)",
                FontSize = 14,
                TitleFontSize = 16
            });
            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "f_{0} (Hz)",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10
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
        public static PlotModel GenerateCWTHeatMap(double[,] data, string plotTitle)
        {
            double[] defaultTimeAxis = new double[data.GetLength(0)];
            double[] defaultFrequencyAxis = new double[data.GetLength(1)];
            for (int i = 0; i < data.GetLength(1); i++)
            {
                defaultFrequencyAxis[i] = Math.Pow(2, 1 + (0.001* i));
            }
            for (int i = 0; i < data.GetLength(0); i++)
            {
                defaultTimeAxis[i] = i;
            }
            return GenerateCWTHeatMap(data, plotTitle, defaultTimeAxis, defaultFrequencyAxis);
        }
        /// <summary>
        /// Creates a contour plot from a 2D array with proper frequency and time axis
        /// </summary>
        /// <param name="data"></param>
        /// <param name="plotTitle"></param>
        /// <param name="timeAxis"></param>
        /// <param name="freqAxis"></param>
        /// <returns></returns>
        public static PlotModel GenerateCWTContourPlot(double[,] data, string plotTitle, double[] timeAxis, double[] freqAxis)
        {

            var model = new PlotModel { Title = plotTitle };
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (ms)",
                FontSize = 14,
                TitleFontSize = 16
            });
            model.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "f_{0} (Hz)",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10
            });
            var contourSeries = new ContourSeries
            {
                ColumnCoordinates = timeAxis,
                RowCoordinates = freqAxis,
                Data = data,
            };
            model.Series.Add(contourSeries);

            return model;
        }
        public static PlotModel GenerateCWTContourPlot(double[,] data, string plotTitle)
        {
            double[] defaultTimeAxis = new double[data.GetLength(0)];
            double[] defaultFrequencyAxis = new double[data.GetLength(1)];
            for (int i = 0; i < data.GetLength(1); i++)
            {
                defaultFrequencyAxis[i] = Math.Pow(2, 1 + (0.001 * i));
            }
            for (int i = 0; i < data.GetLength(0); i++)
            {
                defaultTimeAxis[i] = i;
            }
            return GenerateCWTContourPlot(data, plotTitle, defaultTimeAxis, defaultFrequencyAxis);
        }
        public static PlotModel GenerateXYPlotCWT(double[,] data, int[] rowIndices, double[] timeAxis, double[] freqAxis, PlotTitles plotTitle, XYPlotOptions mode, string? customTitle = null)
        {
            string actualTitle;
            if (timeAxis.Length != data.GetLength(0))
            {
                throw new ArgumentException("timeAxis must have the same number of timepoints as the CWT", nameof(timeAxis));
            }
            if (freqAxis.Length != data.GetLength(1))
            {
                throw new ArgumentException("freqAxis must have the same number of timepoints as the CWT", nameof(freqAxis));
            }
            if (plotTitle == PlotTitles.Custom)
            {
                if (customTitle == null)
                {
                    throw new ArgumentNullException("If Custom plotTitle is used, a custom title must be entered", nameof(customTitle));
                }
                else
                {
                    actualTitle = customTitle;
                }

            }
            else { actualTitle = plotTitle.ToString() + " Plot"; }
            var plotModel = new PlotModel { Title = actualTitle };
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Time (ms)",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10,
                Minimum = timeAxis[0],
                Maximum = timeAxis[^1],
            });
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Intensity",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10
            });
            if (mode == XYPlotOptions.Composite)
            {
                var compSeries = new LineSeries();

                for (int x = 0; x < data.GetLength(0); x++)
                {
                    double compValue = 0;
                    for (int i = 0; i < rowIndices.Length; i++)
                    {
                        compValue += data[x, rowIndices[i]];
                    }
                    compSeries.Points.Add(new DataPoint(timeAxis[x], compValue));
                }
                compSeries.Title = "Composite from f_{0} = " + freqAxis[rowIndices[0]].ToString("G3") + " to f_{0} = " + freqAxis[rowIndices[rowIndices.Length - 1]].ToString("G3") + " Hz";
                plotModel.Series.Add(compSeries);
                compSeries.Color = OxyColors.Black;
                var legend = new OxyPlot.Legends.Legend
                {
                    LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
                    LegendPosition = OxyPlot.Legends.LegendPosition.TopRight,
                    LegendFontSize = 14
                };
                plotModel.Legends.Add(legend);
                if(mode == XYPlotOptions.Evolution)
                {
                    plotModel.DefaultColors = OxyPalettes.Rainbow(plotModel.Series.Count).Colors;
                }
                return plotModel;
            }
            else
            {
                LineSeries[] lineSeriesArray = new LineSeries[rowIndices.Length];
                for (int i = 0; i < rowIndices.Length; i++)
                {
                    var indivSer = new LineSeries();
                    for (int x = 0; x < data.GetLength(0); x++)
                    {
                        indivSer.Points.Add(new DataPoint(timeAxis[x], data[x, rowIndices[i]]));
                    }
                    indivSer.Title = "f_{0} = " + freqAxis[rowIndices[i]].ToString("G3") + " Hz";
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
        }
        public static PlotModel GenerateXYPlotCWT(double[,] data, int[] rowIndices, PlotTitles plotTitle, XYPlotOptions mode, string? customTitle = null)
        {
            double[] defaultTimeAxis = new double[data.GetLength(0)];
            double[] defaultFrequencyAxis = new double[data.GetLength(1)];
            for (int i = 0; i < data.GetLength(1); i++)
            {
                defaultFrequencyAxis[i] = Math.Pow(2, 1 + (0.001 * i));
            }
            for (int i = 0; i < data.GetLength(0); i++)
            {
                defaultTimeAxis[i] = i;
            }
            return GenerateXYPlotCWT(data, rowIndices, defaultTimeAxis, defaultFrequencyAxis, plotTitle, mode, customTitle);
        }
        public static PlotModel Plot1DArray(double[] data)
        {
            var plotModel = new PlotModel();
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Index",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10,
                Minimum = 0,
                Maximum = data.Length - 1,
            });
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Intensity",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10
            });
            LineSeries dataSeries = new LineSeries();
            for (int i = 0; i < data.Length; i++)
            {
                dataSeries.Points.Add(new DataPoint(i, data[i]));
            }
            plotModel.Series.Add(dataSeries);
            return plotModel;

        }
        public static PlotModel PlotSortedPointDictionary(SortedDictionary<int, double> data)
        {
            var plotModel = new PlotModel();
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Index",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10,
                Minimum = data.Keys.First(),
                Maximum = data.Keys.Last() - 1,
            });
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05,
                Title = "Intensity",
                FontSize = 14,
                TitleFontSize = 16,
                AxisTitleDistance = 10
            });
            LineSeries dataSeries = new LineSeries();
            foreach(var point in data)
            {
                dataSeries.Points.Add(new DataPoint(point.Key, point.Value));
            }
            plotModel.Series.Add(dataSeries);
            return plotModel;
        }

        /// <summary>
        /// Exports generated plots to PDF files
        /// Expansion to this method should include options for other file formats
        /// </summary>
        /// <param name="plotModel">Plot to export</param>
        /// <param name="filePath">File path to export the plot to, must contain .pdf extension</param>
        /// <param name="plotWidth">Width of the plot</param>
        /// <param name="plotHeight">Height of the plot</param>
        /// <exception cref="ArgumentException"></exception>

        public static void ExportPlotPDF(PlotModel plotModel, string filePath, int plotWidth = 700, int plotHeight = 600)
        {
            if (Path.GetExtension(filePath) == null)
            {
                filePath = filePath + ".pdf";
            }
            using (var exportStream = File.Create(filePath))
            {
                var pdfExport = new OxyPlot.SkiaSharp.PdfExporter
                {
                    Width = plotWidth,
                    Height = plotHeight
                };
                pdfExport.Export(plotModel, exportStream);
            }
        }
        public enum XYPlotOptions
        {
            Composite, 
            Evolution, 
            Single
        }
        public enum PlotTitles
        {
            Composite,
            Evolution,
            Single,
            Custom
              
        }
    }
}
