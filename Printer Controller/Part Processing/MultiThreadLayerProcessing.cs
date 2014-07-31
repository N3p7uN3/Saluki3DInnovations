using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Windows.Forms;

namespace Printer_Controller
{
    /*
     * 
     * This class implements the multi threaded aspect of LayerProcessing.  It simply creates multiple threads of LayerProcessing, and monitors for their completion.
     * When ever a layer has completed, a new LayerProcessing object is created so as to make sure the user specified amount of LayerProcessing threads is always running.
     * */
    public class MultiThreadLayerProcessing
    {
        private volatile GCodePart _thePart;

        public delegate void ProcessingCompletedEventHandler(PowderPart Part);
        public event ProcessingCompletedEventHandler ProcessingCompleted;

        public delegate void ProgressReportEventHandler(int max, int curPos);
        public event ProgressReportEventHandler ProgressReport;

        public delegate void TimeEstimationEventHandler(string time);
        public event TimeEstimationEventHandler TimeEstimationReady;
        private int _numOfThreads;

        private double _extrusionWidth;

        private List<GCommand> _gCommands;
        private ETAEstimation _eta;

        private PrinterSettings _settings;
        private bool _save;

        public MultiThreadLayerProcessing(int numOfThreads, double extrusionWidth, PrinterSettings settings, bool save)
        {
            
            _numOfThreads = numOfThreads;
            _extrusionWidth = extrusionWidth;
            _settings = settings;
            _save = save;
            
        }

        void gcfr_TimeEta(string time)
        {
            TimeEstimationReady(time);
        }

        public void ReadFile()
        {
            GCodeFileReader gcfr = new GCodeFileReader();
            gcfr.TimeEta += new GCodeFileReader.TimeEtaEventHandler(gcfr_TimeEta);
            _gCommands = gcfr.GetGCode();

            if (_gCommands != null)
            {
                PartProcessing pp = new PartProcessing(_gCommands);
                _thePart = pp.GetGCodePart();

            }
        }

        public void Start()
        {
            if (_gCommands != null)
            {
                _eta = new ETAEstimation("Layer Processing");
                _eta.TimeReady += new ETAEstimation.TimeReadyEventHandler(_eta_TimeReady);
                _eta.Start();
                
                LayerProcessingThreads lpt = new LayerProcessingThreads(_numOfThreads, _thePart, _extrusionWidth, _settings.DotWidth, _save);
                lpt.ProcessingFinished += new LayerProcessingThreads.ProcessingFinishedEventHandler(lpt_ProcessingFinished);
                lpt.ProgressChanged += new LayerProcessingThreads.ProgressChangedEventHandler(lpt_ProgressChanged);
                new Thread(lpt.DoWork).Start(); 
            }
        }

        private void _eta_TimeReady(string time)
        {
            TimeEstimationReady(time);
        }

        private void lpt_ProgressChanged(int max, int cur)
        {
            ProgressReport(max, cur);
            _eta.SetValues(max, cur);
        }

        private void lpt_ProcessingFinished(PowderPart part)
        {
            //string saveToFile = "";
            //foreach (PowderLayer layer in part.Part)
            //{
            //    System.IO.File.WriteAllText(Environment.CurrentDirectory + "\\" + layer.LayerNum.ToString() + ".txt", layer.debugPoints);
            //}

            _eta.Stop();

            ProcessingCompleted(part);
        }

        private class LayerProcessingThreads
        {
            private int _numOfThreads;
            private volatile GCodePart _part;
            private PowderPart _processedPart;

            int layersRemaining;
            int curWorking;

            int[] _progress;

            private double _extrusionWidth;
            private double _dotWidth;

            public delegate void ProcessingFinishedEventHandler(PowderPart part);
            public event ProcessingFinishedEventHandler ProcessingFinished;

            public delegate void ProgressChangedEventHandler(int max, int cur);
            public event ProgressChangedEventHandler ProgressChanged;

            string _pathForSaving;
            bool _save;

            public LayerProcessingThreads(int numOfThreads, GCodePart thePart, double extrusionWidth, double dotWidth, bool save)
            {
                _numOfThreads = numOfThreads;
                _part = thePart;
                _processedPart = new PowderPart();
                _progress = new int[_part.GCodePrintLayers.Count];
                _extrusionWidth = extrusionWidth;
                _dotWidth = dotWidth;
                _save = save;

                DateTime now = DateTime.Now;

                _pathForSaving = Application.StartupPath + "\\" + now.ToFileTimeUtc().ToString();
                //System.IO.File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\test.txt", debugPoints);

                Directory.CreateDirectory(_pathForSaving);


            }

            public void DoWork()
            {
                layersRemaining = _part.GCodePrintLayers.Count;
                _progress = new int[layersRemaining];
                for (int i = 0; i < layersRemaining; ++i)
                {
                    _progress[i] = 0;
                }
                curWorking = 0;

                while (layersRemaining > 0)
                {
                    while (curWorking < _numOfThreads)
                    {
                        if (layersRemaining > 0)
                        {
                            //if here, do a layer
                            LayerProcessing lp = new LayerProcessing(_part.GCodePrintLayers[layersRemaining - 1], _extrusionWidth, _dotWidth, _pathForSaving, _save);
                            lp.LayerIsDone += new LayerProcessing.LayerProcessingDoneEventHandler(lp_LayerIsDone);
                            lp.ProgressChanged += new LayerProcessing.ProgressReportEventHandler(lp_ProgressChanged);
                            lp.Start();
                            ++curWorking;
                            --layersRemaining;
                        }
                        else
                        {
                            break;  
                        }
                    }

                    Thread.Sleep(5);
                }

                while (curWorking > 0)
                {
                    //waiting for the layers to finish
                    Thread.Sleep(10);
                }

                //sort
                _processedPart.Part.Sort();

                //if got here, all layers have finished
                ProcessingFinished(_processedPart);

                

            }

            private void lp_ProgressChanged(int layernum, int progress)
            {
                _progress[layernum] = progress;
                int sum = 0;
                for (int i = 0; i < _progress.Length; ++i)
                {
                    sum += _progress[i];
                }
                ProgressChanged((_progress.Length * 100), sum);
            }

            private void lp_LayerIsDone(PowderLayer theLayer)
            {
                _processedPart.Part.Add(theLayer);
                --curWorking;

            }
        }
    }
}
