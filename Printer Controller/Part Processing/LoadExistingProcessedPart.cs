using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;

namespace Printer_Controller
{
    public class LoadExistingProcessedPart
    {
        private FileInfo[] _theFiles;
        private string _path;
        private PowderPart _thePart;

        private BackgroundWorker _theWork;

        public delegate void PowderPartCompletedEventHandler(PowderPart thePart);
        public event PowderPartCompletedEventHandler PowderPartCompleted;

        public delegate void ProgressReportEventHandler(int max, int cur);
        public event ProgressReportEventHandler ProgressReport;

        private PrinterSettings _settings;

        private int _maxProgress;


        public LoadExistingProcessedPart(PrinterSettings settings)
        {
            // have user selected non compressed directory of powder layer text files
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog1.ShowDialog();

            _settings = settings;
            

            if (result == DialogResult.OK)
            {
                _path = folderBrowserDialog1.SelectedPath;
                DirectoryInfo selDir = new DirectoryInfo(folderBrowserDialog1.SelectedPath);
                _theFiles = selDir.GetFiles("*.txt", SearchOption.AllDirectories);

                _theWork = new BackgroundWorker();
                _theWork.WorkerReportsProgress = true;
                _theWork.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_theWork_RunWorkerCompleted);
                _theWork.ProgressChanged += new ProgressChangedEventHandler(_theWork_ProgressChanged);
                _theWork.DoWork += new DoWorkEventHandler(_theWork_DoWork);

                
            }
        }

        public void Start()
        {
            _theWork.RunWorkerAsync();
        }

        private void _theWork_DoWork(object sender, DoWorkEventArgs e)
        {
            List<PowderLayer> layers = new List<PowderLayer>();
            _thePart = new PowderPart();

            int k = 0;

            while (true)
            {
                try
                {
                    string text = File.ReadAllText(_path + "\\" + k.ToString() + ".txt");

                    PowderLayer layer = new PowderLayer(_settings.DotWidth);

                    _maxProgress = layer.size * _theFiles.Count();


                    string[] textLines = text.Split('\n');
                    for (int j = 0; j < (layer.size - 1); j++)
                    {
                        for (int i = 0; i < layer.size; i++)
                        {
                            int length = textLines[j].Length;

                            if (textLines[j][i] == '1')
                                layer.layer[i, j] = true;
                            else
                                layer.layer[i, j] = false;

                        }

                        //report dat status
                        double percent = (double)((double)(k * j) * (double)100 / (double)_maxProgress);

                        //_theWork.ReportProgress(Convert.ToInt32(percent));

                    }

                    layers.Add(layer);
                    k++;

                }
                catch (System.IO.FileNotFoundException)
                {

                    break;
                }
                
                

                


            }

            _thePart.Part = layers;


        }

        private void _theWork_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressReport(100, e.ProgressPercentage);
        }

        private void _theWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            PowderPartCompleted(_thePart);
        }
    }
}
