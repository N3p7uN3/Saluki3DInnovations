using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This class holds the G code seperated by layers, so that specific G code commands can be attributed to their respective layers for later processing.
     * 
     * */
    public class GCodePart
    {
        public List<GCodePrintLayer> GCodePrintLayers;
        public double XSize { get; set; }
        public double YSize { get; set; }
        public double ZSize { get; set; }

        public GCodePart()
        {
            XSize = YSize = ZSize = (double)0;
            GCodePrintLayers = new List<GCodePrintLayer>();
        }
    }

    public class GCodePrintLayer
    {
        public List<GCommand> GCommands { get; set; }
        public int LayerNum { get; set; }

        public GCodePrintLayer(int layerNum)
        {
            GCommands = new List<GCommand>();
            LayerNum = layerNum;
        }

    }
}
