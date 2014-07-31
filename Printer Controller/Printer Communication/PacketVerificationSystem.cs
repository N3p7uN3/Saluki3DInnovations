using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This class is used after PrinterComm formats the command into packet format, adding both packet number and calculating the checksum of the packet payload.
     * It keeps tracks of each packet, and if the packet fails, this class handles the resending of the packet, and monitoring if the packet needs to be resent
     * more than the maximum amount of allowed time.  If this is the case, an event is raised, alerting the calling class that a packet simply could not be
     * sent successsfully.
     * */
    
    public class RawPacket
    {
        public string PacketContents { get; set; }
        public bool Success { get; set; }
        public ushort Attempts { get; set; }

        public RawPacket(string fullPayload)
        {
            PacketContents = fullPayload;
            Success = false;
            Attempts = 1;
        }
    }
    
    /*
     * This class will handle appending the checksum to the end, and handle any retry attemps, also alerting the calling class
     * of a packet that has exceeded its retry count.
     * */
    public class PacketVerificationSystem
    {
        private ushort _numOfRetries;
        private List<RawPacket> _packetsSent;

        public delegate void PacketReadyToBeSent(string payloadWithStuff);
        public event PacketReadyToBeSent PacketReady;

        public PacketVerificationSystem(ushort numOfRetries)
        {
            _numOfRetries = numOfRetries;
            _packetsSent = new List<RawPacket>();

        }

        public UInt32 Enqueue(String payload)
        {
            UInt32 packetNum = (UInt32) _packetsSent.Count;
            UInt32 checksum = calcChecksum(payload);
            string packetReady = packetNum.ToString("0000000000") + payload + checksum.ToString("00000");
            
            RawPacket newPacket = new RawPacket(packetReady);
            _packetsSent.Add(newPacket);

            PacketReady(packetReady);

            return packetNum;
        }

        public bool PacketFailure(UInt32 packeNum)
        {
            try
            {
                RawPacket failedPacket = _packetsSent[(int)packeNum];
                if (failedPacket.Attempts > _numOfRetries)
                {
                    //Exceeded number of retries, return true;
                    return true;
                }
                else
                {
                    failedPacket.Attempts += 1;

                    PacketReady(failedPacket.PacketContents);

                    return false;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        public void PacketSuccess(UInt32 packetNum)
        {
            //nothing yet.
        }

        private ushort calcChecksum(String cmd)
        {
	        int cs = 0;
            int x;
            bool j = false;
            for (int i = 0; i < cmd.Length; ++i)
            {
                if (j)
                {
                    x = cmd[i];
                    x = x << 8;
                    cs = cs ^ x;
                }
                else
                {
                    cs = cs ^ cmd[i];
                }
                j = !j;
            }
	        return (ushort)cs;
        }
    }
}
