using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * 
     * This class simply reads a list of commands and estimates how long the whole process would complete.  In effect, it estimates the length of a print in time.
     * */
    public class PrintTimeEstimator
    {
        private List<PacketTime> _packets;
        private int _baud;
        private PrinterComm _pc;
        private volatile PrinterSettings _settings;

        private long dx, dy, dsource, dbed;

        public PrintTimeEstimator(List<PacketHolder> thePackets, int baudrate, PrinterSettings theSettings)
        {
            _baud = baudrate;

            _pc = new PrinterComm();

            dx = dy = dsource = dbed = 0;

            Packets.Translate t;
            long transdistance;
            _settings = theSettings;

            _packets = new List<PacketTime>();

            foreach (PacketHolder packet in thePackets)
            {
                PacketTime pt = new PacketTime(packet, _pc.GetPayload(packet), _baud);
                
                switch (packet.PacketType)
                {
                    
                    case Packets.PacketTypes.TranslateBed:
                        t = (Packets.Translate)packet.PacketData;
                        transdistance = Convert.ToInt64(Math.Abs(t.Position - dbed));
                        pt.SetTransTime(_settings.platformsTransSpeed, _settings.platformsTransAccel, transdistance);
                        dbed = t.Position;
                        break;

                    case Packets.PacketTypes.TranslateSource:
                        t = (Packets.Translate)packet.PacketData;
                        transdistance = Convert.ToInt64(Math.Abs(t.Position - dsource));
                        pt.SetTransTime(_settings.platformsTransSpeed, _settings.platformsTransAccel, transdistance);
                        dsource =t.Position;
                        break;

                    case Packets.PacketTypes.TranslateX:
                        t = (Packets.Translate)packet.PacketData;
                        transdistance = Convert.ToInt64(Math.Abs(t.Position - dx));
                        if (t.Printing)
                            pt.SetTransTime(_settings.xPrintSpeed, _settings.xPrintAccel, transdistance);
                        else
                            pt.SetTransTime(_settings.xTransSpeed, _settings.xTransAccel, transdistance);

                        dx = t.Position;
                        break;

                    case Packets.PacketTypes.TranslateY:
                        t = (Packets.Translate)packet.PacketData;
                        transdistance = Convert.ToInt64(Math.Abs(t.Position - dy));
                        pt.SetTransTime(_settings.yTransSpeed, _settings.yTransAccel, transdistance);
                        break;

                    case Packets.PacketTypes.PrintLineInfo:
                        pt.MicrocontrollerOverhead = 75;
                        break;

                    default:
                        pt.MicrocontrollerOverhead = 50;
                        break;
                 }
                _packets.Add(pt);


            }

                
        }

        public double GetPrintTime()
        {
            double total = 0;

            int curStart = 0;
            int curEnd = 0;
            int i = -1;

            bool needToSearch = true;
            bool signalDone = false;

            double[] times = new double[5];
            /*
             * 0    xmit and overhead
             * 1    xtime
             * 2    ytime
             * 3    source time
             * 4    bed time
             * 
             * */

            while ((i < _packets.Count) && (!signalDone))
            {

                while (needToSearch)
                {
                    if (i < (_packets.Count - 1))
                        ++i;


                    if (i == (_packets.Count - 1))
                    {
                        signalDone = true;
                        break;
                    }

                    if (_packets[i].Holder.WaitForPrev)
                    {
                        needToSearch = false;
                    }
                    else
                    {
                        
                    }
                }

                curEnd = i;

                for (int k = 0; k < 5; ++k)
                    times[k] = (double)0;

                for (int j = curStart; j < curEnd; ++j)
                {
                    times[0] += _packets[j].packetXmitTime;
                    times[0] += _packets[j].MicrocontrollerOverhead;

                    switch (_packets[j].Holder.PacketType)
                    {
                        case Packets.PacketTypes.TranslateBed:
                            times[4] += _packets[j].TransTime;
                            break;

                        case Packets.PacketTypes.TranslateSource:
                            times[3] += _packets[j].TransTime;
                            break;

                        case Packets.PacketTypes.TranslateX:
                            times[1] += _packets[j].TransTime;
                            break;
                        
                        case Packets.PacketTypes.TranslateY:
                            times[2] += _packets[j].TransTime;
                            break;
                    }
                }

                double largestVal = 0;
                for (int k = 0; k < 5; ++k)
                {
                    if (largestVal < times[k])
                        largestVal = times[k];
                }

                total += largestVal;

                curStart = curEnd;

                needToSearch = true;

                
                //++i;

            }
            

            return total;
        }

        private class PacketTime
        {
            public double packetXmitTime { get; set; }
            public PacketHolder Holder { get; set; }
            public double TransTime { get; set; }  //in milliseconds
            public double MicrocontrollerOverhead { get; set; }

            public PacketTime(PacketHolder theHolder, string payload, int baud)
            {
                packetXmitTime = ((double)(((double)(payload.Length * 7 ) ) / (double)baud)) * 1000;
                Holder = theHolder;

            }

            private void ComputeActionTime()
            {
                
            }

            public void SetTransTime(long speed, long accel, long distance)
            {
                //first determine if we're going to hit the max speed.
                double timeToGetMaxSpeed = (double)((double) speed / (double)accel);
                double distanceMoved = (double)0.5 * (double)accel * Math.Pow(timeToGetMaxSpeed, (double)2);
                distanceMoved = (double)(distanceMoved * (double)2);

                if ((double) distance > distanceMoved)
                {
                    //we do reach max speed.  calculate this way
                    double distanceRemaining = (double)(distanceMoved - (double)distance);
                    double maxspeedtime = (double)(distanceRemaining / (double)speed);
                    TransTime = Convert.ToInt64((double)(((double)((double)(timeToGetMaxSpeed*(double)2) + maxspeedtime)) * (double)1000));
                }
                else
                {
                    double distancehalf = (double)distance/2;
                    TransTime = Convert.ToInt64(((double)((Math.Sqrt(((double)((double)(2 * distancehalf) / (double)accel)))) * (double)2))*1000);
                }
            }
        }

    }
}
