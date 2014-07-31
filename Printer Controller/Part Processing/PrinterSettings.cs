using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This class holds all values and settings related to the printer.  Many objects get access to the same object of this class, so as to use the same values correctly.
     * 
     * */
    public class PrinterSettings
    {
        //Assume all distance are in millimeters

        public double DotWidth;

        public double XDistanceStepRatio { get; set; }
        public double YDistanceStepRatio { get; set; }

        private long _xStepsPerDot;
        private long _yStepsPer12Dots;

        private double _sourceDistanceStepRatio;
        private double _platformDistanceStepRatio;
        public double PrintThickness;
        public long PrintThicknessSteps { get; set; }
        public long PowderSourceSteps { get; set; }
        private double _powderRatio;    //the amount that the source platform will go up compared to the printing platform will go down.

        public int xTransSpeed { get; set; }
        public int xTransAccel { get; set; }
        public int xPrintSpeed { get; set; }
        public int xPrintAccel { get; set; }
        public int yTransSpeed { get; set; }
        public int yTransAccel { get; set; }
        public int platformsTransSpeed { get; set; }
        public int platformsTransAccel { get; set; }
        public bool xInv { get; set; }
        public bool yInv { get; set; }
        public bool bedsInv { get; set; }

        public double RollingMechanismDistanceStepRatio { get; set; }
        public int rollingMechanismSpeed { get; set; }
        public int rollingMechanismAccel { get; set; }
        public double RollingMechanismDistrSpeedRatio { get; set; }




        public delegate void NeedToSendPacketEventHandler(PacketHolder thePacket);
        public event NeedToSendPacketEventHandler NeedToSendPacket;

        public PrinterSettings(double widthOfDot)
        {
            DotWidth = widthOfDot;
        }

        private void _thePositioning_NeedToSendPacket(PacketHolder theAction)
        {
            NeedToSendPacket(theAction);
        }

        public void setXDistanceStepRatio(int steps, double distanceTraveled)
        {
            XDistanceStepRatio = (double)(distanceTraveled / ((double)steps));
        }

        public void setXDistanceStepRatio(double theRatio)
        {
            XDistanceStepRatio = theRatio;
        }

        //setXDistanceStepRatio must be called before calling this.
        public double getXStepsPerDotFloating()
        {
            return (double)(DotWidth / XDistanceStepRatio);
        }

        public long getXStepsPerDot()
        {
            return _xStepsPerDot;
        }

        public double getXBindingAgentDensity(int stepsBetweenSprays)
        {
            double XStepsPerDotRatio = getXStepsPerDotFloating();

            double overlappercent = (double)(((double)(XStepsPerDotRatio - (double)stepsBetweenSprays)) / XStepsPerDotRatio);
            //Add one to the number to get the total binding agent density in the X direction.
            return (double)(overlappercent + (double)1);

        }

        public void setXStepsPerDot(int steps)
        {
            _xStepsPerDot = steps;

            PacketHolder thePacket = new PacketHolder(Packets.PacketTypes.SetXDotDensity, false);
            Packets.DensityInfo theDensityInfo = new Packets.DensityInfo();
            theDensityInfo.StepsBetween = steps;
            thePacket.PacketData = theDensityInfo;

            NeedToSendPacket(thePacket);

        }

        //this function will return the amount of arrays in the X direction required to make a line the specified width,
        //using the steps / nozzle spray and the dot width values
        public int requestXDotsForWidth(double widthRequested)
        {
            double curWidth = 0;
            int dots = 1;

            while (curWidth < widthRequested)
            {
                curWidth += (double)((double)(_xStepsPerDot) * XDistanceStepRatio);
                ++dots;
            }

            return dots;
        }

        public void setYDistanceStepRatio(double ratio)
        {
            YDistanceStepRatio = ratio;
            _yStepsPer12Dots =  Convert.ToInt64(Math.Floor(getYStepsRawPer12Dots()));
        }

        private double getYStepsRawPer12Dots()
        {
            return (double)((DotWidth * (double)12) / YDistanceStepRatio);
        }

        public long getYStepsPer12Dots()
        {
            return _yStepsPer12Dots;
        }

        public int getYLinesRequiredForHeight(double heightRequested)
        {
            double curHeight = (double)_yStepsPer12Dots * YDistanceStepRatio;
            int lines = 1;

            while (curHeight < heightRequested)
            {
                curHeight = (double)((double)curHeight + (double)((double)_yStepsPer12Dots * YDistanceStepRatio));
                curHeight = (double)((double)curHeight - getArrayOverlap());
                ++lines;
            }

            return lines;
        }

        public void setPowderPlatformInfo(double sourceStepRatio, double platformStepRatio, double printThickness, double powderSourceRatio)
        {
            _sourceDistanceStepRatio = sourceStepRatio;
            _platformDistanceStepRatio = platformStepRatio;
            PrintThickness = printThickness;
            PrintThicknessSteps = Convert.ToInt32(Math.Ceiling((double)(printThickness / _platformDistanceStepRatio)));
            PowderSourceSteps = Convert.ToInt32(Math.Ceiling((double)(((double)(printThickness / _sourceDistanceStepRatio)) * powderSourceRatio)));


        }

        public int GetLayersRequiredForThickness(double requestedThickness)
        {
            int layers = 1;
            double cur = PrintThickness;

            while (cur < requestedThickness)
            {
                cur += PrintThickness;

                ++layers;
            }

            return layers;
        }

        public double getHeightOfArray()
        {
            return (double)(DotWidth * (double)12.0);
        }

        public double getArrayOverlap()
        {
            return (double)(getHeightOfArray() - ((double)((double)_yStepsPer12Dots * YDistanceStepRatio)));
        }

        public void CalculateRollingMechanismSpeed()
        {
            rollingMechanismSpeed = Convert.ToInt32((double)(((double)(YDistanceStepRatio * (double)yTransSpeed)) /  RollingMechanismDistanceStepRatio));
            rollingMechanismAccel = Convert.ToInt32((double)yTransAccel * (double)(YDistanceStepRatio / RollingMechanismDistanceStepRatio));
        }
    }

}
