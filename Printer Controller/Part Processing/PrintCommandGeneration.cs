using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Printer_Controller
{
    /*
     * This class takes the processed layers and creates print commands that would print the processed part.
     * */
    
    public class PrintCommandGeneration
    {
        private PowderPart _part;
        private volatile PrinterSettings _settings;
        private List<PacketHolder> _packets;
        private AxesPositioning _positioning;

        private long _dy, _rollingMechanism, _source, _bed;

        private const int maxPrintLineSize = 2000;
        private const long beginAtOffset = 10;

        public delegate void PrintCommandsReadyEventHandler(List<PacketHolder> Packets);
        public event PrintCommandsReadyEventHandler PrintCommandsReady;

        public delegate void ProgressReportEventHandler(int max, int curPos);
        public event ProgressReportEventHandler ProgressReport;

        public delegate void TimeEtaEventHandler(string time);
        public event TimeEtaEventHandler TimeEtaReady;

        private ETAEstimation _eta;
        
        public PrintCommandGeneration(PowderPart Part, PrinterSettings Settings, AxesPositioning positioning)
        {
            _part = Part;
            _settings = Settings;
            _packets = new List<PacketHolder>();
            _positioning = positioning;
        }

        public void DoWork()
        {
            _eta = new ETAEstimation("Generating Print Commands");
            _eta.TimeReady += new ETAEstimation.TimeReadyEventHandler(_eta_TimeReady);
            _eta.Start();
            
            TranslateToOrigin(false);
            _source = _bed = _rollingMechanism =_dy = 0;



            bool printDirection = false;    //false == to the right

            for(int k = 0; k < _part.Part.Count; ++k)
            {
                int j = 0;
                List<ushort> printLine; 
                
                int index = 0;
                long[,] beginEndArr = new long[10, 2];

                while ((j + 12) <= _part.Part[k].size)
                {
                    int left, right;
                    left = getLeftEdge(k, j);
                    right = getRightEdge(k, j);

                    long xBeginAt;
                    long endAt = 0;

                    if (left > -1)
                    {
                        //we need to check for -1 because if -1, nothing was detected in the print line

                        //lets translate the Y into position
                        //wait is true because we are waiting for a previous print line command to complete
                        TranslateY(Convert.ToInt64(((double)(((double)j / (double)12) * (double)_settings.getYStepsPer12Dots()))), true, false);

                        int amntOfArrays = Int32.Parse(Math.Abs(right - left).ToString()) + 1;


                        //int dotWidth = Int32.Parse( Math.Abs(right - left).ToString()) + 1;    //number of arrays starting from beginning to end, not necessarily the actual amount of arrays sprayed
                        printLine = generatePrintLine(k, j, left, right, amntOfArrays, printDirection);

                        double xDist;

                        long translateToBeginAt;

                        if (!printDirection)
                        {
                            //translate to the left hand side of the line to be printed
                            xDist = (double)((double)left * _settings.DotWidth);
                            xBeginAt = Convert.ToInt64((double)(xDist / _settings.XDistanceStepRatio));
                            translateToBeginAt = xBeginAt - beginAtOffset;
                        }
                        else
                        {
                            xDist = (double)((double)right * _settings.DotWidth);
                            xBeginAt = Convert.ToInt64((double)(xDist / _settings.XDistanceStepRatio));
                            translateToBeginAt = xBeginAt + beginAtOffset;
                        }

                        if (printLine != null)
                        {
                            TranslateX(translateToBeginAt, false, false);
                            
                            if (printLine.Count <= maxPrintLineSize)
                            {
                                endAt = sendPrintLineStuff(printLine, xBeginAt, printDirection);

                                TranslateX((long)endAt, true, true);
                            }
                            else
                            {
                                int remaining = printLine.Count;
                                int i = 0;
                                List<ushort> printLineSplitUp;
                                while (remaining > 0)
                                {
                                    printLineSplitUp = new List<ushort>();

                                    if (remaining >= maxPrintLineSize)
                                    {
                                        for (int p = (i * maxPrintLineSize); p < ((i * maxPrintLineSize) + maxPrintLineSize); ++p)
                                        {
                                            printLineSplitUp.Add(printLine[p]);
                                        }

                                        remaining -= maxPrintLineSize;
                                    }
                                    else
                                    {
                                        for (int p = (i * maxPrintLineSize); p < ((i * maxPrintLineSize) + remaining); ++p)
                                        {
                                            printLineSplitUp.Add(printLine[p]);
                                        }

                                        remaining -= remaining;
                                    }

                                    endAt = sendPrintLineStuff(printLineSplitUp, xBeginAt, printDirection);

                                    TranslateX((long)endAt, true, true);

                                    xBeginAt = endAt;

                                    if (!printDirection)
                                    {
                                        translateToBeginAt = xBeginAt - beginAtOffset;
                                    }
                                    else
                                    {
                                        translateToBeginAt = xBeginAt + beginAtOffset;
                                    }

                                    TranslateX(translateToBeginAt, true, false);




                                    ++i;

                                }
                            }

                            printDirection = !printDirection;
                        } 
                    }


                    

                    j += 12;
                    ///we've printed the full line (or not)
                }

                //finished printing the layer.
 
                //before powder distribution, lets move the bar to flatten out the currently printed layer

                TranslateY(_positioning.RollingMechanismEnd2, true, false);

                //begin powder distribution

                TranslateY(_positioning.RollingMechanismEnd1, true, false); //get out of the way to move the platforms

                _source += _settings.PowderSourceSteps;
                _bed -= _settings.PrintThicknessSteps;
                TranslateBed(_bed, true);
                TranslateSource(_source, false);

                //This the powder distribution part, need to do it accordingly!

                TranslateY(_positioning.RollingMechanismEnd2, true, true);

                //now report progress
                ProgressReport(_part.Part.Count, k);
                _eta.SetValues(_part.Part.Count, k);
            }

            _eta.Stop();

            PrintCommandsReady(_packets);



        }

        private void _eta_TimeReady(string time)
        {
            TimeEtaReady(time);
        }

        private long sendPrintLineStuff(List<ushort> printLine, long initialFirePos, bool direction)
        {
            //initialize some stuff
            PacketHolder h = new PacketHolder(Packets.PacketTypes.InitializePrintLine, false);
            Packets.InitialPrintLineInfo ipl = new Packets.InitialPrintLineInfo();
            ipl.BeginAt = initialFirePos;
            ipl.direction = direction;
            ipl.length = (long)printLine.Count;
            h.PacketData = ipl;

            _packets.Add(h);

            long endAt;

            //now, the actual lines.
            //need to split them up into no more than 19 nozzle fires per packet
            //pli 00xxxxxxxx

            int maxArraysPerPacket = 13;

            int needToBeProcessed = printLine.Count;
            int k = 0;

            if (!direction)
                endAt = initialFirePos + (_settings.getXStepsPerDot() * (long)(printLine.Count - (long)1));
            else
                endAt = initialFirePos - (_settings.getXStepsPerDot() * (long)(printLine.Count - 1));

            while (needToBeProcessed >= maxArraysPerPacket)
            {

                h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
                Packets.PrintLine pl = new Packets.PrintLine();
                pl.num = (ushort)k;
                for (int i = (k * maxArraysPerPacket); i < ((k*maxArraysPerPacket) + maxArraysPerPacket); ++i)
                {
                    pl.printLine.Add(printLine[i]);
                }

                h.PacketData = pl;

                _packets.Add(h);

                ++k;
                needToBeProcessed -= maxArraysPerPacket;

            }

            if (needToBeProcessed > 0)
            {
                h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
                Packets.PrintLine pl = new Packets.PrintLine();
                pl.num = (ushort)k;
                for (int i = (k*maxArraysPerPacket); i < ((k*maxArraysPerPacket) + needToBeProcessed); ++i)
                {
                    pl.printLine.Add(printLine[i]);
                }

                h.PacketData = pl;

                _packets.Add(h);
            }

            //else
            //{
            //    endAt = initialFirePos - (_settings.getXStepsPerDot() * (long)(printLine.Count - 1));
                
            //    while (needToBeProcessed >= maxArraysPerPacket)
            //    {

            //        h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
            //        Packets.PrintLine pl = new Packets.PrintLine();
            //        pl.num = (ushort)k;
            //        for (int i = (printLine.Count - (k * maxArraysPerPacket) - 1); i >= (printLine.Count - ((k * maxArraysPerPacket) + maxArraysPerPacket)); --i)
            //        {
            //            pl.printLine.Add(printLine[i]);
            //        }

            //        h.PacketData = pl;

            //        _packets.Add(h);

            //        ++k;
            //        needToBeProcessed -= maxArraysPerPacket;

            //    }

            //    if (needToBeProcessed > 0)
            //    {
            //        h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
            //        Packets.PrintLine pl = new Packets.PrintLine();
            //        pl.num = (ushort)k;
            //        for (int i = (printLine.Count - (k * maxArraysPerPacket) - 1); i >= 0; --i)
            //        {
            //            pl.printLine.Add(printLine[i]);
            //        }

            //        h.PacketData = pl;

            //        _packets.Add(h);
            //    }
            //}

            return endAt;

        }

        private List<ushort> generatePrintLine(int layer, int jIndex, int left, int right, int amntOfArrays, bool printDirection)
        {
            double distInbetweenSprays = (double)((double)_settings.getXStepsPerDot() * _settings.XDistanceStepRatio);
            double totalDist = (double)((double)((double)amntOfArrays * distInbetweenSprays));
            //int totalSprays = (Math.Abs(right - left) + 1);
            List<ushort> printLine = new List<ushort>();

            bool foundNothing = true;

            double leftActual = (double)left * _settings.DotWidth;
            double rightActual = (double)right * _settings.DotWidth;

            double curPos = leftActual;
            int iCordinate;

            while (curPos <= rightActual)
            {
                //convert the curPos into an actual integer index that we can look up in the part
                iCordinate = Convert.ToInt32((double)(curPos / (double)_settings.DotWidth));

                int array = 0;

                for (int j = jIndex; j < (jIndex + 12); ++j)
                {
                    //array = array << 1;

                    if (_part.Part[layer].layer[iCordinate, j])
                    {
                        array = array | 0x8000;
                        foundNothing = false;
                    }

                    array = array >> 1;


                }

                array = array >> 4;

                printLine.Add((ushort)array);

                curPos = (double)(curPos + (double)((double)_settings.getXStepsPerDot() * _settings.XDistanceStepRatio));
            }

            if (foundNothing)
                return null;
            else
            {
                //need to take care of direction
                List<ushort> printLineCorrectedForDirection = new List<ushort>();

                if (printDirection)
                {
                    for (int i = (printLine.Count() - 1); i >= 0; i--)
                    {
                        printLineCorrectedForDirection.Add(printLine[i]);
                    }

                    return printLineCorrectedForDirection;
                }
                else
                {
                    return printLine;
                }
            }
        }

        private void TranslateToOrigin(bool wait)
        {
            TranslateX(0, wait, false);
            TranslateY(0, false, false);
        }

        private void TranslateX(long pos, bool wait, bool printing)
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.TranslateX, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            t.Printing = printing;
            h.PacketData = t;

            _packets.Add(h);
        }

        private void TranslateY(long pos, bool wait, bool rollingMechanism)
        {
            PacketHolder p = new PacketHolder(Packets.PacketTypes.TranslateY, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            p.PacketData = t;

            _packets.Add(p);


            //BEGIN PASTE FROM TestPart.CS line 266



            long stepsMovingInY = pos - _dy;
            _dy = pos;


            //now, we need the rolling mechanism to respond

            long stepsToMove = Convert.ToInt64((double)(((double)(stepsMovingInY * _settings.YDistanceStepRatio)) / _settings.RollingMechanismDistanceStepRatio));


            p = new PacketHolder(Packets.PacketTypes.TranslateRollingMechanism, false);
            t = new Packets.Translate();

            int prevSpeed = _settings.rollingMechanismSpeed;
            _settings.CalculateRollingMechanismSpeed();


            if (!rollingMechanism)  //Just track along with the Y mechanism, simply move along with it
            {
                if (prevSpeed != _settings.rollingMechanismSpeed)   //Need to check that the previous speed was a rolling mechanism speed, in which case, we need to reset it back to normal.
                {
                    SendRollingMechanismSpeed((double)1);
                }

                _rollingMechanism += stepsToMove;
            }
            else
            {   //We are distributing powder, do that!
                _settings.CalculateRollingMechanismSpeed();
                SendRollingMechanismSpeed(_settings.RollingMechanismDistrSpeedRatio);
                stepsToMove = Convert.ToInt64((double)((double)stepsToMove * (double)1.475 * _settings.RollingMechanismDistrSpeedRatio * (double)(_settings.YDistanceStepRatio / _settings.RollingMechanismDistanceStepRatio)));

                _rollingMechanism += stepsToMove;

            }

            t.Position = _rollingMechanism;
            p.PacketData = t;
            _packets.Add(p);


            //END PASTE FROM TestPart.cs

        }

        private void SendRollingMechanismSpeed(double ratio)
        {
            if (ratio != (double)1)
                _settings.rollingMechanismSpeed = Convert.ToInt32((double)(_settings.rollingMechanismSpeed * ratio));

            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetRollingMechanismSpeeds, false);
            Packets.SetSpeedInfo s = new Packets.SetSpeedInfo();
            s.Accel = (ushort)_settings.rollingMechanismAccel;
            s.Speed = (ushort)_settings.rollingMechanismSpeed;
            h.PacketData = s;

            _packets.Add(h);
        }

        private int getLeftEdge(int layer, int jIndex)
        {
            bool dotExists = false;
            int leftEdge = 0;

            for (int i = 0; i < _part.Part[layer].size; ++i)
            {
                for (int j = jIndex; j < (jIndex + 12); ++j)
                {
                    dotExists = _part.Part[layer].layer[i, j];

                    if (dotExists)
                        break;
                }

                if (dotExists)
                {
                    leftEdge = i;
                    break;
                }
            }

            if (dotExists)
                return leftEdge;
            else
                return -1;
        }

        private int getRightEdge(int layer, int jIndex)
        {
            bool dotExists;
            int rightEdge = 1000000;

            for (int i = (_part.Part[layer].size - 1); i >= 0; --i)
            {

                dotExists = false;
                for (int j = jIndex; j < (jIndex + 12); ++j)
                {
                    dotExists = _part.Part[layer].layer[i, j];

                    if (dotExists)
                        break;
                }

                if (dotExists)
                {
                    rightEdge = i;
                    break;
                }
            }

            return rightEdge;
        }

        private void TranslateSource(long pos, bool wait)
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.TranslateSource, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            h.PacketData = t;

            _packets.Add(h);
        }

        private void TranslateBed(long pos, bool wait)
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.TranslateBed, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            h.PacketData = t;

            _packets.Add(h);
        }
    }
}
