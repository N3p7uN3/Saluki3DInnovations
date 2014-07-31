using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This class processes the G commands and separates all the G commands, into their respective layers, where each layer has a class that contains it's respective G code commands.
     * 
     * */
    
    public class PartProcessing
    {
        List<GCommand> _gCommands;
        
        public PartProcessing(List<GCommand> gCommands)
        {
            _gCommands = gCommands;
        }

        public GCodePart GetGCodePart()
        {
            GCodePart part = new GCodePart();
            int j = 0;
            GCodePrintLayer layer = new GCodePrintLayer(j);

            

            //skip the first two commands, they are Z positioning commands we don't need

            for (int i = 2; i < _gCommands.Count; ++i)
            {
                if (_gCommands[i].CommandType != GCommandType.ZMovement)
                {
                    layer.GCommands.Add(_gCommands[i]);
                }
                else
                {
                    ++j;
                    part.GCodePrintLayers.Add(layer);
                    layer = new GCodePrintLayer(j);
                }
            }

            return part;

        }

        public double GetMinX()
        {
            double x = 200;     //initialize to a large value
            foreach (GCommand gcmd in _gCommands)
            {
                if ((gcmd.CommandType == GCommandType.XYExtrude) || (gcmd.CommandType == GCommandType.XYPosition))
                {
                    if (gcmd.X < x)
                        x = gcmd.X;
                }
            }

            return x;
        }

        public double GetMinY()
        {
            double x = 200;     //initialize to a large value
            foreach (GCommand gcmd in _gCommands)
            {
                if ((gcmd.CommandType == GCommandType.XYExtrude) || (gcmd.CommandType == GCommandType.XYPosition))
                {
                    if (gcmd.Y < x)
                        x = gcmd.Y;
                }
            }

            return x;
        }

        public double GetMaxX()
        {
            double x = 0;
            foreach (GCommand gcmd in _gCommands)
            {
                if ((gcmd.CommandType == GCommandType.XYExtrude) || (gcmd.CommandType == GCommandType.XYPosition))
                {
                    if (gcmd.X > x)
                        x = gcmd.X;
                }
            }

            return x;
        }

        public double GetMaxY()
        {
            double x = 0;
            foreach (GCommand gcmd in _gCommands)
            {
                if ((gcmd.CommandType == GCommandType.XYExtrude) || (gcmd.CommandType == GCommandType.XYPosition))
                {
                    if (gcmd.Y > x)
                        x = gcmd.Y;
                }
            }

            return x;
        }
    }
}
