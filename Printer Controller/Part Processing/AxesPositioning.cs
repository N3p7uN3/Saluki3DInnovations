using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    //This class will hold all the stepper values, axis directions, current locations, etc.
    //During manual operation, this class will keep track of the current position of the axes, allowing the user to make
    //manual moveements with ease.


    public class AxesPositioning
    {
        public delegate void NeedToSendPacketEventHandler(PacketHolder theAction);
        public event NeedToSendPacketEventHandler NeedToSendPacket;
        
        public long XPosition { get; set; }

        public long YPosition { get; set; }

        public long SourcePosition { get; set; }
        public long BedPosition { get; set; }

        public long XMax { get; set; }
        public long YMax { get; set; }

        //Assuming the Y axis is the axis the axis that the Rolling Mechanism will traverse
        public long RollingMechanismEnd1 { get; set; }
        public long RollingMechanismEnd2 { get; set; }

        public long RollingMechanism { get; set; }

        private volatile PrinterSettings _settings;

        public AxesPositioning(PrinterSettings settings)
        {
            XPosition = YPosition = SourcePosition = BedPosition = 0;
            _settings = settings;
        }

        public void SetXYHome()
        {
            XPosition = 0;
            YPosition = 0;

            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.SetXYHome, true);

            NeedToSendPacket(theHolder);
        }

        public void translateX(long amount)
        {
            XPosition += amount;

            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateX, true);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = XPosition;
            theHolder.PacketData = theTranslation;

            NeedToSendPacket(theHolder);
        }

        public void translateY(long amount)
        {
            YPosition += amount;

            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateY, true);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = YPosition;
            theHolder.PacketData = theTranslation;

            NeedToSendPacket(theHolder);

            //BEGIN PASTE FROM TestPart.CS line 266



            //now, we need the rolling mechanism to respond

            long stepsToMove = Convert.ToInt64((double)(((double)(amount * _settings.YDistanceStepRatio)) / _settings.RollingMechanismDistanceStepRatio));


            PacketHolder p = new PacketHolder(Packets.PacketTypes.TranslateRollingMechanism, false);
            Packets.Translate t = new Packets.Translate();

            int prevSpeed = _settings.rollingMechanismSpeed;
            _settings.CalculateRollingMechanismSpeed();


            //Just track along with the Y mechanism, simply move along with it
            
            if (prevSpeed != _settings.rollingMechanismSpeed)   //Need to check that the previous speed was a rolling mechanism speed, in which case, we need to reset it back to normal.
            {
                SetRollingMechanismSpeed((double)1);
            }

            RollingMechanism += stepsToMove;
            

            t.Position = RollingMechanism;
            p.PacketData = t;

            NeedToSendPacket(p);

            //END PASTE FROM TestPart.cs
        }

        private void SetRollingMechanismSpeed(double ratio)
        {
            if (ratio != (double)1)
                _settings.rollingMechanismSpeed = Convert.ToInt32((double)(_settings.rollingMechanismSpeed * ratio));

            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetRollingMechanismSpeeds, true);
            Packets.SetSpeedInfo s = new Packets.SetSpeedInfo();
            s.Accel = (ushort)_settings.rollingMechanismAccel;
            s.Speed = (ushort)_settings.rollingMechanismSpeed;
            h.PacketData = s;

            NeedToSendPacket(h);
        }

        public void translateSource(long amount)
        {
            SourcePosition += amount;

            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateSource, true);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = SourcePosition;
            theHolder.PacketData = theTranslation;

            NeedToSendPacket(theHolder);

        }

        public void translateBed(long amount)
        {
            BedPosition += amount;

            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateBed, true);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = BedPosition;
            theHolder.PacketData = theTranslation;

            NeedToSendPacket(theHolder);
        }

        public void setXYMaxes()
        {
            XMax = XPosition;
            YMax = YPosition;
        }

        public void setDist1()
        {
            RollingMechanismEnd1 = YPosition; ;
        }

        public void setDist2()
        {
            RollingMechanismEnd2 = YPosition;
        }







        
    }
}
