using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FCWTNET
{
    public class TransientData
    {
        public string Filepath { get; set; } 
        public int? Samplingrate { get; private set; }
        public double? Calibrationfactor { get; private set; }
 
        public double[]? Data { get; private set; }

        public TransientData(string filepath)
        {
            Filepath = filepath;
            Samplingrate = null;
            Calibrationfactor = null;
            Data = null;
        }
        
        /// <summary>
        /// Imports transient data into the TransientData class
        /// </summary>
        /// <param name="maxTimePoints">Maximum number of timepoints to read from the transient CSV file</param>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public void ImportTransientData(int maxTimePoints = int.MaxValue - 4)
        {
            if (Filepath.Substring(Filepath.Length - 4, 4) != ".csv")
            {
                throw new ArgumentException("Input file must be a .csv file");
            }
            List<double> dataList = new List<double>();
            try
            {                
                using (StreamReader dataReader = new StreamReader(Filepath))
                {
                    string line;
                    int LineCounter = 1;
                    while ((line = dataReader.ReadLine()) != null && LineCounter < maxTimePoints + 3)
                    {
                        
                        double currentNumber;
                        try
                        {
                            currentNumber = Convert.ToDouble(line);
                        }
                        catch (FormatException)
                        {
                            string invalidNumberLocation = "Invalid number at line " + LineCounter.ToString();
                            throw new FormatException(invalidNumberLocation);
                        }
                        if (LineCounter == 1)
                        {
                            Calibrationfactor = currentNumber;                               
                        }
                        else if (LineCounter == 2)
                        {
                            Samplingrate = Convert.ToInt32(currentNumber);                            
                        }
                        else
                        {
                            dataList.Add(currentNumber);
                        }                       
                        LineCounter++;
                    }

                }
            }
            catch(FileNotFoundException)
            {
                throw new FileNotFoundException("Transient data file could not be found");
            }
            double[] dataArray = new double[dataList.Count];
            dataArray = dataList.ToArray();
            Data = dataArray;
        }
    }
}
