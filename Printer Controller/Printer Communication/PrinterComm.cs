using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.Windows.Forms;

namespace Printer_Controller
{
    /*
     * This class handles processing a PacketHolder and formatting a string into a packet that the Arduino can parse.
     * */
    public class PrinterComm
    {
        private SerialComm _theSerialComm;

        public class PrinterCommEventMessage
        {
            public string Event { get; set; }
            public string Data { get; set; }

            public PrinterCommEventMessage(string eventname, string data)
            {
                Event = eventname;
                Data = data;
            }
        }
        public delegate void PrinterCommEventHandler(PrinterCommEventMessage theMessage);
        public event PrinterCommEventHandler PrinterCommEvent;
        public event PrinterCommEventHandler PacketReceived;

        
        public delegate void PacketFlowIndicatorEventHandler(PacketFlowInfo thePacketInfo);
        public event PacketFlowIndicatorEventHandler PacketFlowIndicator;


        private System.Timers.Timer _handshakeTimeout;

        private PacketVerificationSystem _theVerification;
        
        public PrinterComm()
        {
            _theSerialComm = new SerialComm("~");

            _theSerialComm.SerialCommEvent += new SerialComm.SerialCommEventHandler(_theSerialComm_SerialCommEvent);

            _theVerification = new PacketVerificationSystem(5);
            _theVerification.PacketReady += new PacketVerificationSystem.PacketReadyToBeSent(_theVerification_PacketReady);
        }

        private void _theVerification_PacketReady(string payloadWithStuff)
        {
            //Packet has proper headers and footers, send it.
            _theSerialComm.SendPacket(payloadWithStuff);
        }

        private void _theSerialComm_SerialCommEvent(SerialComm.SerialCommEventMessage theMessage)
        {
            if (theMessage.Event == "packetReceived")
            {
                ParsePacket(theMessage.Data);
                //Debug.Print("tryign to parse");
            }
            else if (theMessage.Event == "connected")
            {
                //We have established serial connectivity between the computer and the arduino, but we first need to verify communication
                //let's attempt the handshake:
                string packet = "hello?";
                _theSerialComm.SendPacket(packet);
                _handshakeTimeout = new System.Timers.Timer();
                _handshakeTimeout.Interval = 1000;
                _handshakeTimeout.Elapsed += new ElapsedEventHandler(_handshakeTimeout_Elapsed);
                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.To, new PacketHolder(Packets.PacketTypes.SendingHandshake, true), packet));

            }
            else if (theMessage.Event == "connectionFailed")
            {
                PrinterCommEvent(new PrinterCommEventMessage("connectionFailed", theMessage.Data));
            }
            else if (theMessage.Event == "notConnected")
            {
                PrinterCommEvent(new PrinterCommEventMessage("notConnected", theMessage.Data));
            }
        }

        private void _handshakeTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            PrinterCommEvent(new PrinterCommEventMessage("connectionFailed", "Handshake timed out."));
            Debug.Print("print connection timed out");
        }

        public void Start(string comPort, int baud)
        {
            _theSerialComm.Connect(comPort, baud);
        }

        private void ParsePacket(String packet)
        {
            Debug.Print("received packet '" + packet + "'");
            String[] s = packet.Split(' ');
            if (packet == "hello.")
            {
                //handshake successful!
                _handshakeTimeout.Stop();

                PrinterCommEvent(new PrinterCommEventMessage("connected", ""));
                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.From, new PacketHolder(Packets.PacketTypes.ReceivedHandshake, false), packet));
            }
            else if (packet == "c")
            {
                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.From, new PacketHolder(Packets.PacketTypes.ActionCompleted, false), packet));

            }
            else if (packet == "o")
            {

                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.From, new PacketHolder(Packets.PacketTypes.PacketOkay, false), packet));

            }
            else if (s[0] == "f")
            {
                Debug.Print("failure");
                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.From, new PacketHolder(Packets.PacketTypes.Failure, false), packet));

                if (_theVerification.PacketFailure((uint)Int16.Parse(packet.Substring(1))));
                {
                    PrinterCommEvent(new PrinterCommEventMessage("comIntegrityFailure", "Could not successfully send packet after multiple retries."));
                }
            }
            else if (s[0] == "freeRam")
            {
                Packets.StringResponse theResponse = new Packets.StringResponse();
                for (int i = 1; i < 10; ++i)
                {
                    theResponse.TheString = theResponse.TheString + Environment.NewLine + s[i];
                }

                PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.StackHeapResponse, false);
                theHolder.PacketData = theResponse;
                PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.From, theHolder, packet));
            }
        }

        public void SendPacket(PacketHolder thePacket)
        {

            string payload = GetPayload(thePacket);
            _theVerification.Enqueue(payload);
            PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.To, thePacket, payload));
           
            

        }

        public string GetPayload(PacketHolder thePacket)
        {
            string payload = "";

            Packets.Translate translateObj;
            Packets.PrintingMode printingMode;
            Packets.SetSpeedInfo speed;
            Packets.DensityInfo densityInfo;
            Packets.InitialPrintLineInfo printInit;
            Packets.PrintLine theLine;

            switch (thePacket.PacketType)
            {
                case Packets.PacketTypes.TranslateBed:   //done
                    translateObj = (Packets.Translate)thePacket.PacketData;
                    //payload = "translate2 " + translateObj.Position.ToString("00000000");
                    payload = "t2 " + translateObj.Position.ToString("00000000");
                    break;

                case Packets.PacketTypes.TranslateSource:    //done
                    translateObj = (Packets.Translate)thePacket.PacketData;
                    payload = "t1 " + translateObj.Position.ToString("00000000");
                    break;

                case Packets.PacketTypes.InitializePrintLine:
                    printInit = (Packets.InitialPrintLineInfo)thePacket.PacketData;

                    //pi lllllnnnnnnnnd
                    payload = "pi " + printInit.length.ToString("00000") + printInit.BeginAt.ToString("00000000");
                    if (printInit.direction)
                        payload += "1";
                    else
                        payload += "0";
                    break;

                case Packets.PacketTypes.PrintLineInfo:
                    theLine = (Packets.PrintLine)thePacket.PacketData;
                    payload = createPrintLineInfoString(theLine);
                    break;

                case Packets.PacketTypes.SetPowderPlatformsHome:  //not needed?
                    payload = "hb";
                    break;

                case Packets.PacketTypes.SetPrinterMode:  //not needed?
                    printingMode = (Packets.PrintingMode)thePacket.PacketData;
                    if (printingMode.Printing)
                        payload = "setPrinting";
                    else
                        payload = "setSetup";
                    break;

                case Packets.PacketTypes.SetAllXSpeedInfo:
                    Packets.XSpeedInfo xSpeedInfo = (Packets.XSpeedInfo)thePacket.PacketData;
                    payload = "xs ";
                    payload += xSpeedInfo.xTransSpeed.ToString("0000");
                    payload += xSpeedInfo.xTransAccel.ToString("0000");
                    payload += xSpeedInfo.xPrintSpeed.ToString("0000");
                    payload += xSpeedInfo.xPrintAccel.ToString("0000");
                    break;



                case Packets.PacketTypes.SetAllYSpeedInfo:
                    speed = (Packets.SetSpeedInfo)thePacket.PacketData;
                    payload = "ys " + speed.Speed.ToString("0000") + speed.Accel.ToString("0000");
                    break;

                case Packets.PacketTypes.SetBedSpeed:
                    speed = (Packets.SetSpeedInfo)thePacket.PacketData;
                    payload = "bs " + speed.Speed.ToString("0000");
                    break;

                case Packets.PacketTypes.SetBedAcceleration:
                    speed = (Packets.SetSpeedInfo)thePacket.PacketData;
                    payload = "ba " + speed.Speed.ToString("0000");
                    break;

                case Packets.PacketTypes.SetXDotDensity:
                    //Units are steps inbetween dots in the X direction.
                    densityInfo = (Packets.DensityInfo)thePacket.PacketData;
                    payload = "xd " + densityInfo.StepsBetween.ToString("00");
                    break;

                case Packets.PacketTypes.SetXYHome:
                    payload = "xyh";
                    break;

                case Packets.PacketTypes.TranslateX:
                    translateObj = (Packets.Translate)thePacket.PacketData;
                    if (translateObj.Printing)
                        payload = "t4p " + translateObj.Position.ToString("00000000");
                    //Translate and print the line that's stored in the microcontroller.
                    //if we can, overshoot such that we can decelerate nicely.
                    else
                        payload = "t4 " + translateObj.Position.ToString("00000000");
                    break;

                case Packets.PacketTypes.TranslateY:
                    translateObj = (Packets.Translate)thePacket.PacketData;
                    payload = "t5 " + translateObj.Position.ToString("00000000");
                    break;

                case Packets.PacketTypes.FireNozzlesOnce:
                    payload = "fireOnce";
                    break;

                case Packets.PacketTypes.StartFireNozzlesContinuously:
                    payload = "fireContinuously";
                    break;

                case Packets.PacketTypes.StopFireNozzlesContinuously:
                    payload = "stopContinuously";
                    break;

                case Packets.PacketTypes.AskStackHeapDistance:
                    payload = "stackHeapDistance";
                    break;

                case Packets.PacketTypes.SetInversionOfAxes:
                    Packets.AxisInversionInformation theInversion = (Packets.AxisInversionInformation)thePacket.PacketData;
                    payload = "inv ";
                    if (theInversion.XInverted)
                        payload += "1";
                    else
                        payload += "0";

                    if (theInversion.YInverted)
                        payload += "1";
                    else
                        payload += "0";

                    //if (theInversion.PlatformsInverted)
                    //    payload += "1";
                    //else
                    //    payload += "0";
                    //break;
                    if (theInversion.SourceInverted)
                        payload += "1";
                    else
                        payload += "0";

                    if (theInversion.PlatformInverted)
                        payload += "1";
                    else
                        payload += "0";

                    break;

                case Packets.PacketTypes.SetRollingMechanismSpeeds:
                    Packets.SetSpeedInfo s = (Packets.SetSpeedInfo)thePacket.PacketData;
                    payload = "rs ";
                    payload += s.Speed.ToString("0000");
                    payload += s.Accel.ToString("0000");
                    break;

                case Packets.PacketTypes.TranslateRollingMechanism:
                    translateObj = (Packets.Translate)thePacket.PacketData;
                    payload = "rt ";
                    payload += translateObj.Position.ToString("00000000");
                    break;


            }

            return payload;
        }

        private string createPrintLineInfoString(Packets.PrintLine thePrintLine)    //NO MORE THAN 12 ARRAYS AT ONCE
        {
            //printLineInfo <lineInfoPackeNum, 2 bytes><printLines, multiple of 3 bytes, no more than 500 total arrays at once>
            //can send 14 arrays at once
            //pli 00xxxxxxxx
            string payload = "pli ";
            

            payload += thePrintLine.num.ToString("000");

            for (int i = 0; i < thePrintLine.printLine.Count; ++i)
            {
                payload += (char)(((thePrintLine.printLine[i] & 0x0F00) >> 8) | 0x0050);
                payload += (char)(((thePrintLine.printLine[i] & 0x00F0) >> 4) | 0x0050);
                payload += (char)((thePrintLine.printLine[i] & 0x000F) | 0x0050); //least significant 4 bits
            }

            return payload;

        }

        public void SendEmergencyStop()
        {
            string packet = "sos";
            _theSerialComm.SendPacket(packet);
            PacketFlowIndicator(new PacketFlowInfo(PacketFlowInfo.PacketDirection.To, new PacketHolder(Packets.PacketTypes.EmergencyAllStop, false), packet));

        }
        public void Close()
        {
            try
            {
                _theSerialComm.Disconnect();
            }
            catch (NullReferenceException)
            {
                
                //do nothing
            }
        }

        public List<PacketHolder> GetLineDataPackets(long beginAt, bool direction, List<ushort> theArrays)
        {
            const int maxArraysPerPacket = 13;
            
            List<PacketHolder> packets = new List<PacketHolder>();
            
            PacketHolder theInit = new PacketHolder(Packets.PacketTypes.InitializePrintLine, false);
            Packets.InitialPrintLineInfo theInitialInfo = new Packets.InitialPrintLineInfo();
            theInitialInfo.BeginAt = beginAt;
            theInitialInfo.direction = direction;
            theInitialInfo.length = theArrays.Count;
            theInit.PacketData = theInitialInfo;

            packets.Add(theInit);

            PacketHolder theLinePacket;
            Packets.PrintLine theLineData;
            int i = 0;
            int k = 0;
            do
            {
                theLinePacket = new PacketHolder(Packets.PacketTypes.PrintLineInfo, false);
                theLineData = new Packets.PrintLine();
                theLineData.num = (ushort)k;

                if ((theArrays.Count - i) >= maxArraysPerPacket)
                {
                    for (int j = 0; j < maxArraysPerPacket; ++j)
                    {
                        theLineData.printLine.Add(theArrays[i + j]);
                    }

                    theLinePacket.PacketData = theLineData;

                    packets.Add(theLinePacket);
                }
                else
                {
                    for (int j = 0; j < (theArrays.Count - i); ++j)
                    {
                        theLineData.printLine.Add(theArrays[i + j]);
                    }

                    theLinePacket.PacketData = theLineData;

                    packets.Add(theLinePacket);
                }

                i += maxArraysPerPacket;
                ++k;

            } while (i < theArrays.Count);

            return packets;
        }

    }
}
