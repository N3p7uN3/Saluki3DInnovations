using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This holds the Packets classes for the communication protocol, including all packet types enumerated, along with the PacketHolder class, and each
     * command type's classes to hold each command's respective information required to complete such a command.
     * */
    public class Packets
    {
        public enum PacketTypes
        {
            TranslateX,
            TranslateY,
            SetXYHome,
            SetPowderPlatformsHome,
            SetAllXSpeedInfo,
            SetAllYSpeedInfo,
            SetBedSpeed,
            SetBedAcceleration,
            InitializePrintLine,
            PrintLineInfo,
            SetXDotDensity,
            DistributePowder,
            SetPrinterMode,
            SetPrintStartingLocation,
            FireNozzlesOnce,
            StartFireNozzlesContinuously,
            StopFireNozzlesContinuously,
            PacketOkay,
            ActionCompleted,
            Failure,
            SendingHandshake,
            ReceivedHandshake,
            EmergencyAllStop,
            AskStackHeapDistance,
            StackHeapResponse,
            SetInversionOfAxes,
            TranslateSource,
            TranslateBed,
            SetInversionOfPowderBeds,
            SetRollingMechanismSpeeds,
            TranslateRollingMechanism,
        };

        

        //Create classes that hold the packet data

        public class Translate
        {
            public long Position { get; set; } //translate TO this position
            public bool Printing { get; set; }

            public Translate()
            {
                Printing = false;
            }
        }

        public class SetSpeedInfo
        {
            public ushort Speed { get; set; }
            public ushort Accel { get; set; }
        }

        public class MiscCommand
        {
            //
        }

        public class PrintingMode
        {
            public bool Printing { get; set; }
        }

        public class InitialPrintLineInfo
        {
            public long BeginAt { get; set; }
            public bool direction { get; set; }
            public long length { get; set; }
        }

        public class PrintLine
        {
            public List<ushort> printLine { get; set; }
            public ushort num { get; set; }
            public PrintLine() { printLine = new List<ushort>(); }
        }

        public class DensityInfo
        {
            public int StepsBetween { get; set; }
        }

        public class StringResponse
        {
            public string TheString { get; set; }
        }

        public class AxisInversionInformation
        {
            public bool XInverted { get; set; }
            public bool YInverted { get; set; }

            public bool PlatformInverted { get; set; }
            public bool SourceInverted { get; set; }
        }

        public class XSpeedInfo
        {
            public int xTransSpeed { get; set; }
            public int xTransAccel { get; set; }
            public int xPrintSpeed { get; set; }
            public int xPrintAccel { get; set; }
        }




        

    }

    public class PacketHolder
    {
        public Packets.PacketTypes PacketType { get; set; }
        public object PacketData { get; set; }
        public bool WaitForPrev { get; set; }

        public PacketHolder(Packets.PacketTypes packetType, bool waitForPrev)
        {
            WaitForPrev = waitForPrev;
            PacketType = packetType;
        }
        
        public PacketHolder Clone()
        {
            PacketHolder clone = new PacketHolder(PacketType, WaitForPrev);
            clone.PacketData = PacketData;

            return clone;
        }

    }

    public class PacketFlowInfo
    {
        public enum PacketDirection
        {
            To,
            From
        };
        public PacketDirection Direction { get; set; }
        public PacketHolder Packet { get; set; }
        public string RawPacket { get; set; }

        public PacketFlowInfo(PacketDirection packetDirection, PacketHolder thePacketHolder, string rawPacket)
        {
            Direction = packetDirection;
            Packet = thePacketHolder;
            RawPacket = rawPacket;
        }
    }

    


}
