using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading;
    
namespace Printer_Controller
{
    /*
     * This class implements the Windows serial communication port opening, enabling communication with the Arduino over a virtual serial port via USB cable.
     * */
    
    public class SerialComm
    {
        public delegate void SerialCommEventHandler(SerialCommEventMessage theMessage);
        public event SerialCommEventHandler SerialCommEvent;
        public class SerialCommEventMessage
        {
            public string Event { get; set; }
            public string Data { get; set; }

            public SerialCommEventMessage(string Eventname, string theData)
            {
                Event = Eventname;
                Data = theData;
            }
        }

        volatile SerialPort _serialPort;
        string _endOfPacketChar;
        string _buffer;

        SerialReadThread _theReadThread;
        
        public SerialComm(string endOfPacket)
        {
            _endOfPacketChar = endOfPacket;
            _buffer = "";
            _theReadThread = new SerialReadThread();
        }

        public void Connect(String comPort, int baud)
        {
            string error = "";
            
            if (_serialPort == null)
            {
                _serialPort = new SerialPort(comPort, baud);
            }

            try
            {
                _serialPort.Open();
            }
            catch (System.IO.IOException ioException)
            {
                SerialCommEvent(new SerialCommEventMessage("connectionFailed", ioException.Message));
                Debug.Print("connectionFailed: " + ioException.Message);
                error = ioException.Message;
            }
            

            //now check if it opened
            if (_serialPort.IsOpen)
            {
                SerialCommEvent(new SerialCommEventMessage("connected", ""));
                Connected();
            }
            else
            {
                if (error == "")
                    SerialCommEvent(new SerialCommEventMessage("connectionFailed", "Could not connect, unknown reason."));
                else
                    SerialCommEvent(new SerialCommEventMessage("connectionFailed", "Could not connect: " + error));
            }
        }

        private void Connected()
        {
            _serialPort.ErrorReceived += new SerialErrorReceivedEventHandler(_serialPort_ErrorReceived);

            _serialPort.DataBits = 8;

            _theReadThread.SetSerialPort(_serialPort);

            _theReadThread.PacketReceived += new SerialReadThread.PacketReceivedEventHandler(_theReadThread_PacketReceived);
            new Thread(_theReadThread.DoWork).Start();
        }

        private void  _theReadThread_PacketReceived(string packet)
        {
 	        SerialCommEvent(new SerialCommEventMessage("packetReceived", packet));
        }

        private void _serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            SerialCommEvent(new SerialCommEventMessage("error", e.ToString()));
            Debug.Print("serialerror: " + e.ToString());
        }

        public void SendPacket(string packet)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    packet = packet + _endOfPacketChar;
                    _serialPort.Write(packet);
                    Debug.Print("attempting to send '" + packet + "'");
                }
                else
                    SerialCommEvent(new SerialCommEventMessage("notConnected", "No longer connected."));
            }
            catch (Exception)
            {
                
                //do nothing
            }
        }

        public void Disconnect()
        {
            _theReadThread.RequestStop();
            try
            {
                _serialPort.Close();
            }
            catch (System.IO.IOException)
            {
                //do nothing
            }
            catch (NullReferenceException)
            {
                //do nothing
            }
        }

        private class SerialReadThread
        {
            public volatile SerialPort _serialPort;
            private bool _requestStop;

            public delegate void PacketReceivedEventHandler(string packet);
            public event PacketReceivedEventHandler PacketReceived;

            public SerialReadThread()
            {

                _requestStop = false;
            }

            public void SetSerialPort(SerialPort thePort)
            {
                _serialPort = thePort;
            }

            public void DoWork()
            {
                _serialPort.ReadTimeout = 50;

                String buff = "";

                while (!_requestStop)
                {
                    try
                    {
                        buff = _serialPort.ReadLine();

                        if (buff != "")
                        {
                            PacketReceived(String.Copy(buff));

                            buff = "";
                        }
                    }
                    catch (TimeoutException e)
                    {
                        //do nothing
                    }
                    catch (InvalidOperationException e2)
                    {
                        RequestStop();
                    }
                    catch (System.IO.IOException e3)
                    {
                        RequestStop();
                    }

                    Thread.Sleep(10);
                }
            }

            public void RequestStop()
            {
                _requestStop = true;
            }
        }
    }
}
