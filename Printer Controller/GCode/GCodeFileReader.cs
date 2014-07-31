using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Printer_Controller
{
    /*
     * The purpose of this class is to simply open the G code file, and get all the important G code commands that this program will parse for the part processing.
     * 
     * */
    public class GCodeFileReader
    {
        private Stream _s;

        public delegate void TimeEtaEventHandler(string time);
        public event TimeEtaEventHandler TimeEta;

        ETAEstimation _eta;
        
        public GCodeFileReader()
        {
            _s = null;
            
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.InitialDirectory = Environment.CurrentDirectory;
            ofd.Filter = "GCode files (*.gcode)|*.gcode";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _s = ofd.OpenFile();
            }
        }

        public List<GCommand> GetGCode()
        {
            List<GCommand> gcommands = new List<GCommand>();

            TimeEta("Processing G Code file...");
            
            if (_s != null)
            {
                StreamReader sr = new StreamReader(_s);
                

                string line = "";
                while ((line = sr.ReadLine()) != null)
                {
                    if (line != "")
                    {
                        if (line.Substring(0, 2) == "G1")
                        {
                            if ((line.IndexOf("X") != -1) || (line.IndexOf("Z") != -1))
                                gcommands.Add(new GCommand(line));
                        }
                    }
                    
                }
            }

            if (gcommands.Count == 0)
                return null;
            else
                return gcommands;

            
        }

        private void _eta_TimeReady(string time)
        {
            TimeEta(time);
        }
    }
}
