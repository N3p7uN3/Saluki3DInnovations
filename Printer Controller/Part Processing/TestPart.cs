using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This class creates all the necessary packets required to print out the specified sized cube.
     * Before these actions are queued, we must:
     * 
     * Have the home set
     * Have all distance/stepper ratios set
     * X distance between fires set
     * rolling mechanism ends 1 and 2 set
     * Change speed/accel values if needed.
     * 
     * Once this is done, this class may be called and the requested actions to create the part will be provided.
     * */
    public class TestPart
    {
        private double _size;
        private volatile PrinterSettings _settings;
        private AxesPositioning _positioning;

        private List<PacketHolder> _packets;

        private long _x, _y, _yAxis, _z, _source, _bed, _rollingMechanism;
        private double _yHeightCur;
        private bool _printDirection;

        private bool _onlyOneLayer;
        private bool _doubleEachLayer;
        
        public TestPart(double size, PrinterSettings settings, AxesPositioning positioning, bool OnlyOneLayer, bool DoubleEachLayer)
        {
            _size = size;
            _settings = settings;
            _positioning = positioning;
            _packets = new List<PacketHolder>();

            _x = _y = _z = _source = _bed = _yAxis = _rollingMechanism = 0;
            _printDirection = false;

            _onlyOneLayer = OnlyOneLayer;
            _doubleEachLayer = DoubleEachLayer;
        }

        public List<PacketHolder> GetTestPart()
        {
            CreatePacketsForTestPart();

            //PostPacketProcessing();

            return _packets;
        }

        private void PostPacketProcessing()
        {
            for (int i = 0; i < (_packets.Count - 2); ++i)
            {
                //Look for translation commands that are right after one another.  Treat these as unwanted, and simply translate the second one.
                if ((_packets[i].PacketType == _packets[i + 2].PacketType)  && (_packets[i].PacketType == Packets.PacketTypes.TranslateY))
                {
                    //ytrans extra
                    //roll extra
                    //ytrans want
                    //roll want
                    _packets.RemoveAt(i);
                    _packets.RemoveAt(i + 1);
                }

            }
        }

        private void CreatePacketsForTestPart()
        {
            prePartTranslation();

            int layerCount;

            if (!_onlyOneLayer)
	        {
		        layerCount = _settings.GetLayersRequiredForThickness(_size); 
	        }
            else
            {
                //debugging purpose: only create enough commands to create ONE layer.
                layerCount = 1;
            }


            for (int i = 0; i < layerCount; ++i)
            {
                createLinesForLayer();

                TranslateY(_positioning.RollingMechanismEnd2, true, false); //translate to flatten out the currently printed layer

                //bookmark: add support for double layer here
                if (_doubleEachLayer)
                {
                    //reposition for next layer
                    TranslateY(0, true, false);
                    
                    createLinesForLayer();

                    TranslateY(_positioning.RollingMechanismEnd2, true, false); //translate to flatten out the currently printed layer
                }
                
                TranslateY(_positioning.RollingMechanismEnd1, true, false);

                _source += _settings.PowderSourceSteps;
                _bed -= _settings.PrintThicknessSteps;
                TranslateBed(_bed, true);
                TranslateSource(_source, false);

                //This the powder distribution part, need to do it accordingly!

                TranslateY(_positioning.RollingMechanismEnd2, true, true);

                //reposition for next layer
                TranslateY(0, true, false);
                //TranslateX(0, false, false);
            }
        }

        private void createLinesForLayer()
        {
            _yHeightCur = (double)(_size);//initialize the height such that, each time, we can
            //accout for the expected overlap in the Y axis
            int numOfFulllines = 0;

            while (_yHeightCur >= _settings.getHeightOfArray())
            {
                //subtract off the overlap.
                //on the first iteration of this, there is NO overlap
                _yHeightCur = (double)(_yHeightCur - _settings.getHeightOfArray());
                //_yHeightCur = (double)(_yHeightCur );
                addFullLine();
                numOfFulllines++;

            }

            //done adding full lines.  now we need to check how much height we have remaining, and add the appropriate
            //lines
            //double heightRemaining = (double)(_settings.getHeightOfArray() - _yHeightCur);
            if (_yHeightCur > 0)
            {
                //height remaining, 
                int dotsRemaining = Convert.ToInt32(Math.Ceiling((double)(((double)(_yHeightCur / _settings.getHeightOfArray())) * (double)12)));

                if (dotsRemaining > 0)
                    addPartialLine(dotsRemaining);
            }
        }

        private void addFullLine()
        {
            addLine(0x0FFF);
        }

        private void addPartialLine(int amount)
        {
            int array = 0;
            for (int i = 0; i < amount; ++i)
            {
                array = array | 0x0001;
                array = array << 1;

            }

            array = array >> 1;

            addLine((ushort)array);
        }

        private void addLine(ushort array)
        {
            //Assuming, at the beginning of this function, we are already in correct position for firing
            long numOfArrays = _settings.requestXDotsForWidth(_size);
            //101010101
            long translateAmount = (numOfArrays - (long)1) * _settings.getXStepsPerDot();
            long translateTo;
            long translateForBeginning;
            if (_printDirection)
            {
                translateForBeginning = _x + 10;
                translateTo = _x - translateAmount;
            }
            else
            {
                translateForBeginning = _x - 10;
                translateTo = _x + translateAmount;
            }

            //Translate the x a little past where it needs to begin, such that it begins properly
            TranslateX(translateForBeginning, false, false);

            Packets.InitialPrintLineInfo ip = new Packets.InitialPrintLineInfo();
            ip.BeginAt = _x;
            ip.direction = _printDirection;
            ip.length = numOfArrays;

            //add the packet
            PacketHolder h = new PacketHolder(Packets.PacketTypes.InitializePrintLine, false);
            h.PacketData = ip;
            _packets.Add(h);

            long arraysRemaining = numOfArrays;
            int count = 0;
            while (arraysRemaining > 0)
            {
                if (arraysRemaining >= 13)
                {
                    h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
                    Packets.PrintLine l = new Packets.PrintLine();
                    l.num = (ushort)count;
                    for (int i = 0; i < 13; ++i)
                    {
                        l.printLine.Add(array);
                    }
                    h.PacketData = l;

                    _packets.Add(h);

                    arraysRemaining -= 13;
                }
                else
                {
                    h = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
                    Packets.PrintLine l = new Packets.PrintLine();
                    l.num = (ushort)count;
                    for (int i = 0; i < arraysRemaining; ++i)
                    {
                        l.printLine.Add(array);
                    }
                    h.PacketData = l;

                    _packets.Add(h);

                    arraysRemaining -= arraysRemaining;
                }

                ++count;
            }

            

            //Send the X translation that will commence the print
            TranslateX(translateTo, true, true);

            //now, change the appropriate variables such that we can add more lines
            _x = translateTo;
            _printDirection = !_printDirection;

            //now, translate into position for the new line.
            //this will be a wait since we want to wait for the previous line to complete.

            TranslateY((_yAxis + _settings.getYStepsPer12Dots()), true, false);



        }

        private void prePartTranslation()
        {
            TranslateX(0, false, false);
            TranslateY(0, false, false);
        }

        private void TranslateX(long pos, bool wait, bool printing)
        {
            PacketHolder p = new PacketHolder(Packets.PacketTypes.TranslateX, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            t.Printing = printing;
            p.PacketData = t;

            _packets.Add(p);
        }

        private void TranslateY(long pos, bool wait, bool rollingMechanism)
        {
            PacketHolder p = new PacketHolder(Packets.PacketTypes.TranslateY, wait);
            Packets.Translate t = new Packets.Translate();
            t.Position = pos;
            p.PacketData = t;

            _packets.Add(p);



            long distanceMoving = pos - _yAxis;
            _yAxis = pos;

            //need to calculate how much to move the rolling mechanism bar

            //BEGIN COPY PASTE




            long stepsToMove = Convert.ToInt64((double)(((double)(distanceMoving * _settings.YDistanceStepRatio)) / _settings.RollingMechanismDistanceStepRatio));
            
           

            //now, we need the rolling mechanism to respond
            p = new PacketHolder(Packets.PacketTypes.TranslateRollingMechanism, false);
            t = new Packets.Translate();

            int prevSpeed = _settings.rollingMechanismSpeed;
            _settings.CalculateRollingMechanismSpeed();


            if (!rollingMechanism)  //Just track along with the Y mechanism, simply move along with it
            {
                if (prevSpeed != _settings.rollingMechanismSpeed)   //Need to check that the previous speed was a rolling mechanism speed, in which case, we need to reset it back to normal.
                {
                    SetRollingMechanismSpeed((double)1);
                }

                _rollingMechanism += stepsToMove;
            }
            else
            {                 //We are distributing powder, do that!
                _settings.CalculateRollingMechanismSpeed();
                SetRollingMechanismSpeed(_settings.RollingMechanismDistrSpeedRatio);
                stepsToMove = Convert.ToInt64((double)((double)stepsToMove * _settings.RollingMechanismDistrSpeedRatio));

                _rollingMechanism -= stepsToMove;

            }

            t.Position = _rollingMechanism;
            p.PacketData = t;
            _packets.Add(p);


            //END COPY PASTE

            
        }

        private void SetRollingMechanismSpeed(double ratio)
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
