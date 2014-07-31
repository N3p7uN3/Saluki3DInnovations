using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;

namespace Printer_Controller
{
    /*
     * This class does the actual layer processing using the GPolygons representing each layer
     * 
     * */
    public class LayerProcessing
    {
        private volatile GCodePrintLayer _layer;
        private volatile PowderLayer _layerProcessed;
        private double _extrusionWidth, _dotWidth;
        public string debugPoints;

        private BackgroundWorker RunAsync;

        public delegate void LayerProcessingDoneEventHandler(PowderLayer theLayer);
        public event LayerProcessingDoneEventHandler LayerIsDone;

        public delegate void ProgressReportEventHandler(int layernum, int progress);
        public event ProgressReportEventHandler ProgressChanged;

        private string _pathForSaving;
        bool _save;

        public LayerProcessing(GCodePrintLayer layer, double extrusionWidth, double dotWidth, string pathForSaving, bool save)
        {
            _layer = layer;

            
            _extrusionWidth = extrusionWidth;
            _dotWidth = dotWidth;
            _save = save;

            RunAsync = new BackgroundWorker();
            RunAsync.DoWork += new DoWorkEventHandler(RunAsync_DoWork);
            RunAsync.WorkerReportsProgress = RunAsync.WorkerSupportsCancellation = true;
            RunAsync.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunAsync_RunWorkerCompleted);
            RunAsync.ProgressChanged += new ProgressChangedEventHandler(RunAsync_ProgressChanged);

            _pathForSaving = pathForSaving;
        }

        private void RunAsync_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressChanged(_layer.LayerNum, e.ProgressPercentage);
        }

        void RunAsync_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _layerProcessed.debugPoints = debugPoints;
            LayerIsDone(_layerProcessed);
        }

        public void Start()
        {
            RunAsync.RunWorkerAsync();
        }

        void RunAsync_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
            
            _layerProcessed = new PowderLayer(_dotWidth);
            _layerProcessed.LayerNum = _layer.LayerNum;
            
            //first we need to generate all of the polygons
            List<GPolygon> polygons = new List<GPolygon>();
            double lastX, lastY;
            lastX = lastY = 0;

            double minX, minY, maxX, maxY;
            //initilize to some extreme value
            minX = _layerProcessed.size;
            maxX = 0;
            minY = _layerProcessed.size;
            maxY = 0;

            foreach (GCommand cmd in _layer.GCommands)
            {
                switch (cmd.CommandType)
                {
                    case GCommandType.XYPosition:
                        lastX = cmd.X;
                        lastY = cmd.Y;
                        break;

                    case GCommandType.XYExtrude:
                        if ((lastX != cmd.X) && (lastY != cmd.Y))
                        {
                            polygons.Add(new GPolygon(lastX, lastY, cmd.X, cmd.Y, _extrusionWidth));
                            lastX = cmd.X;
                            lastY = cmd.Y;

                            if (cmd.X > maxX)
                                maxX = cmd.X;
                            if (cmd.X < minX)
                                minX = cmd.X;
                            if (cmd.Y > maxY)
                                maxY = cmd.Y;
                            if (cmd.Y < minY)
                                minY = cmd.Y;

                             
                        }
                        break;
                }
            }

            int processMaxX, processMinX, processMaxY, processMinY;
            processMaxX = Convert.ToInt32((double)(maxX / _dotWidth)) + 1;
            processMaxY = Convert.ToInt32((double)(maxY / _dotWidth)) + 1;
            processMinX = Convert.ToInt32((double)(minX / _dotWidth)) - 1;
            processMinY = Convert.ToInt32((double)(minY / _dotWidth)) - 1;

            double xDist, yDist;

            int p = 0;
            int total = polygons.Count + _layerProcessed.size;

            foreach (GPolygon poly in polygons)
            {
                //find the min/max x y values for this polygon, and ONLY scan that area of the bed
                GPolygonMinMaxes minMaxes = poly.GetMinMaxes();
                int xMin = Convert.ToInt32((double)(Math.Floor((double)(minMaxes.XMin / _dotWidth)) - (double)1));
                int yMin = Convert.ToInt32((double)(Math.Floor((double)(minMaxes.YMin / _dotWidth)) - (double)1));
                int xMax = Convert.ToInt32((double)(Math.Ceiling((double)(minMaxes.XMax / _dotWidth)) + (double)1));
                int yMax = Convert.ToInt32((double)(Math.Ceiling((double)(minMaxes.YMax / _dotWidth)) + (double)1));

                for (int i = xMin; i <= xMax; ++i)
                {
                    for (int j = yMin; j <= yMax; ++j)
                    {
                        //first check it's not already marked as true
                        if (!_layerProcessed.layer[i, j])
                        {
                            xDist = _layerProcessed.getDist(i);
                            yDist = _layerProcessed.getDist(j);

                            if (poly.WithinPolygon(xDist, yDist))
                            {
                                _layerProcessed.layer[i, j] = true;
                                debugPoints += xDist.ToString() + "\t" + yDist.ToString() + "\t" + ((double)((double)_layer.LayerNum * (double)_extrusionWidth)).ToString() + ";\n\n";
                            }
                        }
                    }
                }

                ++p;

                RunAsync.ReportProgress(Convert.ToInt32((double)((double)((double)p / (double)total) * 100)));

                

            }

            //System.IO.File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\test.txt", debugPoints);

            if (_save)
	        {
		        string data = "";
                    
                    for (int j = 0; j < _layerProcessed.size; j++)
                    {
                        for (int i = 0; i < _layerProcessed.size; i++)
                        {
                            if (_layerProcessed.layer[i, j])
                                data += "1";
                            else
                                data += "0";
                        }

                        data += "\n";

                        RunAsync.ReportProgress(Convert.ToInt32((double)((double)((double)(polygons.Count + j) / (double)total) * 100)));
                    }

                    System.IO.File.WriteAllText(_pathForSaving + "\\" + _layerProcessed.LayerNum.ToString() + ".txt", data); 
	        }

            
            
        }
    }
}
