using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace Printer_Controller
{    
    /*
     * This class handles the queue of packets to be sent to the printer, including implemeting the wait of packets that require all previously sent
     * packets to complete.
     * */
    
    public class QueueManager
    {

        private volatile int _actionsBeingPerformed;
        private volatile int _maxActions;
        private volatile Queue<PacketHolder> _queuedPackets;

        private PrinterComm _theCom;

        public delegate void PrinterCommEventHandler(PrinterComm.PrinterCommEventMessage theMessage);
        public event PrinterCommEventHandler CommEvent;

        public delegate void PacketFlowIndicationEventHandler(PacketFlowInfo thePacketInfo);
        public event PacketFlowIndicationEventHandler PacketFlowInfo;

        private QueueProcessor _theProcessor;
        private Thread _theProcessorThread;
        
        public QueueManager(int MaxSimutaneousActions)
        {
            _queuedPackets = new Queue<PacketHolder>();
            _actionsBeingPerformed = 0;
            _maxActions = MaxSimutaneousActions;

            _theCom = new PrinterComm();
            _theCom.PrinterCommEvent += new PrinterComm.PrinterCommEventHandler(_theCom_PrinterCommEvent);
            _theCom.PacketFlowIndicator += new PrinterComm.PacketFlowIndicatorEventHandler(_theCom_PacketFlowIndicator);

            _theProcessor = new QueueProcessor(_queuedPackets, _actionsBeingPerformed, _maxActions);
            _theProcessor.DoAction += new QueueProcessor.DoActionEventHandler(_theProcessor_DoAction);

            _theProcessorThread = new Thread(_theProcessor.DoWork);
            _theProcessorThread.Name = "queueManagerThread";
            
        }

        private void _theCom_PacketFlowIndicator(PacketFlowInfo thePacketInfo)
        {
            PacketFlowInfo(thePacketInfo);

            switch (thePacketInfo.Packet.PacketType)
            {
                case Packets.PacketTypes.ActionCompleted:
                    _theProcessor.ActionHasCompleted();
                    break;

                case Packets.PacketTypes.PacketOkay:
                    _theProcessor.ReiceivedPacketOkay();
                    break;

                case Packets.PacketTypes.ReceivedHandshake:
                    
                    _theProcessorThread.Start();
                    break;
            }
        }

        private void _theProcessor_DoAction(PacketHolder theAction)
        {
            _theCom.SendPacket(theAction);
            _theProcessor.SetWaiting();
        }

        public void sendAllStop()
        {
            _theProcessor.RequestStop();
            _theCom.SendEmergencyStop();
        }

        private void _theCom_PrinterCommEvent(PrinterComm.PrinterCommEventMessage theMessage)
        {
            CommEvent(theMessage);

            if (theMessage.Event == "comIntegrityFailure")
            {
                _theProcessor.RequestStop();
            }

            if (theMessage.Event == "freeRam")
            {
                MessageBox.Show(theMessage.Data);
            }
        }

        public void StartCommunication(string comPort, int baudRate)
        {
            _theCom.Start(comPort, baudRate);
        }



        public void EnqueueAction(PacketHolder thePacket)
        {
            _queuedPackets.Enqueue(thePacket);

        }

        public void Pause()
        {
            _theProcessor.Pause();
        }

        private class QueueProcessor
        {
            private bool _requestStop;

            private volatile Queue<PacketHolder> _queuedPackets;
            private volatile PacketHolder _nextAction;
            private volatile int _actionsBeingPerformed;
            private volatile int _maxActions;

            private bool _currentlyWaiting;
            private bool _waitingForOk;

            public delegate void DoActionEventHandler(PacketHolder theAction);
            public event DoActionEventHandler DoAction;

            private bool _paused;
            public QueueProcessor(Queue<PacketHolder> queuedPackets, int actionsBeingPerformed, int maxActions)
            {
                _requestStop = false;

                _queuedPackets = queuedPackets;
                _actionsBeingPerformed = actionsBeingPerformed;
                _maxActions = maxActions;

                _currentlyWaiting = false;
                _waitingForOk = false;
                _paused = false;
            }

            public void DoWork()
            {
                while (!_requestStop)
                {
                    if (!_paused)
                    {
                        if (!_waitingForOk)
                        {

                            if ((_queuedPackets.Count > 0) || (_currentlyWaiting))
                            {
                                //_curretlyWaiting implies that _nextPacket is not null, and it is the wait packet
                                if (_currentlyWaiting)
                                {
                                    //We are waiting.  Let's check and see if we no longer need to wait.
                                    if (_actionsBeingPerformed == 0)
                                    {
                                        //Currently waiting and there are no pending actions.  Preceed to perform _nextPacket.
                                        ++_actionsBeingPerformed;

                                        DoAction(_nextAction.Clone());

                                        _currentlyWaiting = false;
                                    }
                                    else
                                    {
                                        //Currently waiting and there ARE pending actions.
                                        //Do nothing.
                                    }
                                }
                                else
                                {
                                    //Not currently waiting.  Let's dequeue the next packet
                                    _nextAction = _queuedPackets.Dequeue();

                                    if (_nextAction.WaitForPrev)
                                    {
                                        //This new action is a wait, and since we're not currently waiting...
                                        if (_actionsBeingPerformed == 0)
                                        {
                                            //New action is a wait, but there is nothing pending, so just do it.
                                            ++_actionsBeingPerformed;

                                            DoAction(_nextAction.Clone());
                                        }
                                        else
                                        {
                                            //New aciton is a wait, and we DO have pending actions.  Do nothing, but signal we are in a wait.
                                            _currentlyWaiting = true;
                                        }
                                    }
                                    else
                                    {
                                        //Not a wait action.
                                        ++_actionsBeingPerformed;
                                        DoAction(_nextAction.Clone());
                                    }
                                }
                            }
                        } 
                    }
                    

                    //wait some
                    Thread.Sleep(1);
                }
            }

            public void ActionHasCompleted()
            {
                --_actionsBeingPerformed;

                
            }

            public void Pause()
            {
                _paused = !_paused;
            }

            public void ReiceivedPacketOkay()
            {
                _waitingForOk = false;
            }

            public void SetWaiting()
            {
                _waitingForOk = true;
            }

            public void RequestStop()
            {
                _requestStop = true;
                Debug.Print("requetsed stop");
            }


        }

        public void Close()
        {
            _theCom.Close();
            _theProcessor.RequestStop();
        }

        public void TranslateX(bool printing, long position, bool waitForPrev)
        {
            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateX, waitForPrev);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = position;
            theTranslation.Printing = printing;
            theHolder.PacketData = theTranslation;

            EnqueueAction(theHolder);
        }

        public void TranslateY(long position, bool waitForPrev)
        {
            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.TranslateY, waitForPrev);
            Packets.Translate theTranslation = new Packets.Translate();
            theTranslation.Position = position;
            theTranslation.Printing = false;
            theHolder.PacketData = theTranslation;

            EnqueueAction(theHolder);
        }

        public void SendAPrintLine(long beginAt, bool direction, List<ushort> theArrays)
        {
            List<PacketHolder> packets = _theCom.GetLineDataPackets(beginAt, direction, theArrays);

            foreach (PacketHolder aPacket in packets)
            {
                EnqueueAction(aPacket);
            }
        }


    }
}
