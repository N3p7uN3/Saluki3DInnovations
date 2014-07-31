using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{   
    /*
     * This class holds the basic information of each G command.
     * */
    public class GCommand
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public GCommandType CommandType;

        public GCommand(string command)
        {
            //parse the command.
            //G1 X97.253 Y96.943 E38.31017 F360.000
            string[] param = command.Split(' ');
            if (param[1].Substring(0,1) == "X")
            {
                if (command.IndexOf("E") != -1)
                    CommandType = GCommandType.XYExtrude;
                else
                    CommandType = GCommandType.XYPosition;

                X = Double.Parse(param[1].Substring(1));
                Y = Double.Parse(param[2].Substring(1));
            }
            else if (param[1].Substring(0, 1) == "Z")
            {
                //G1 Z1.000 F7800.000
                CommandType = GCommandType.ZMovement;

                Z = Double.Parse(param[1].Substring(1));

            }


        }
    }

    public enum GCommandType
    {
        XYPosition,
        ZMovement,
        XYExtrude
    };
}
