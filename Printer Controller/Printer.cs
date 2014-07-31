using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;


namespace Printer_Controller
{
    /*
     * The top most class of the whole printer.
     * All GUI interaction and subsequent interal printer operations are implemented here.
     * */
    
    public class Printer
    {
        //int baudRate = 9600;
        QueueManager _theQueue;
        public AxesPositioning _thePositioning;
        private volatile PrinterSettings _settings;

        public delegate void PrinterEventHandler(PrinterEventDetails theEvent);
        public event PrinterEventHandler PrinterEvent;
        public class PrinterEventDetails
        {
            public string EventName;
            public string Description;

            public PrinterEventDetails(string eventName, string description)
            {
                EventName = eventName;
                Description = description;
            }
        }

        public delegate void PacketFlowIndicationEventHandler(PacketFlowInfo thePacketInfo);
        public event PacketFlowIndicationEventHandler PacketFlowInfo;

        public delegate void LongOperationEventHandler(int max, int cur);
        public event LongOperationEventHandler LongOperationProgress;

        public delegate void TimeEventHandler(string Time);
        public event TimeEventHandler TimeEtaReady;

        int _baudRate = 9600;

        private ETAEstimation _duringPrintEstimation;
        private int _curPrintLayers;
        private int _curPrintLayer;
        
        public Printer()
        {

            _theQueue = new QueueManager(8);
            _theQueue.CommEvent += new QueueManager.PrinterCommEventHandler(_theQueue_CommEvent);
            _theQueue.PacketFlowInfo += new QueueManager.PacketFlowIndicationEventHandler(_theQueue_PacketFlowInfo);

            _settings = new PrinterSettings(0.243);
            _settings.NeedToSendPacket += new PrinterSettings.NeedToSendPacketEventHandler(_settings_NeedToSendPacket);
            
            _thePositioning = new AxesPositioning(_settings);
            _thePositioning.NeedToSendPacket += new AxesPositioning.NeedToSendPacketEventHandler(_thePositioning_NeedToSendPacket);

            _duringPrintEstimation = new ETAEstimation("Current print ETA: ");
            _duringPrintEstimation.TimeReady += new ETAEstimation.TimeReadyEventHandler(_duringPrintEstimation_TimeReady);

        }

        void _duringPrintEstimation_TimeReady(string time)
        {
            TimeEtaReady(time);
        }

        public void _settings_NeedToSendPacket(PacketHolder thePacket)
        {
            _theQueue.EnqueueAction(thePacket);
        }

        private void _thePositioning_NeedToSendPacket(PacketHolder theAction)
        {
            _theQueue.EnqueueAction(theAction);
        }

        private void _theDensity_NeedToSendPacket(PacketHolder thePacket)
        {
            _theQueue.EnqueueAction(thePacket);
        }

        private void _theQueue_PacketFlowInfo(PacketFlowInfo thePacketInfo)
        {
            PacketFlowInfo(thePacketInfo);

            if (thePacketInfo.Packet.PacketType == Packets.PacketTypes.TranslateBed)
            {
                _curPrintLayer++;
                _duringPrintEstimation.SetValues(_curPrintLayers, _curPrintLayer);
                LongOperationProgress(_curPrintLayers, _curPrintLayer);
            }
        }

        private void _theQueue_CommEvent(PrinterComm.PrinterCommEventMessage theMessage)
        {
            PrinterEvent(new PrinterEventDetails(theMessage.Event, theMessage.Data));
        }
        
        public void ConnectToPrinter(string comPort, int baudRate)
        {
            _theQueue.StartCommunication(comPort, baudRate);
            _baudRate = baudRate;

        }

        public void FireOnce()
        {
            PacketHolder fireOnce = new PacketHolder(Packets.PacketTypes.FireNozzlesOnce, false);

            _theQueue.EnqueueAction(fireOnce);
        }

        public void StartFireContinuously()
        {
            PacketHolder continuously = new PacketHolder(Packets.PacketTypes.StartFireNozzlesContinuously, false);

            _theQueue.EnqueueAction(continuously);
        }

        public void StopFireContinuously()
        {
            PacketHolder continuously = new PacketHolder(Packets.PacketTypes.StopFireNozzlesContinuously, false);

            _theQueue.EnqueueAction(continuously);
        }

        public void Close()
        {
            _theQueue.Close();
        }

        public double GetXBindingAgentDensity(double xDistanceStepRatio, int stepsInbetweenXSprays)
        {
            _settings.setXDistanceStepRatio(xDistanceStepRatio);
            return _settings.getXBindingAgentDensity(stepsInbetweenXSprays);
        }

        public void SaveXRelatedValues(int stepsBetweenXDots, double xRatio, int xTransSpeed, int xTransAccel, int xPrintSpeed, int xPrintAccel)
        {
            _settings.setXStepsPerDot(stepsBetweenXDots);
            _settings.setXDistanceStepRatio(xRatio);

            Packets.XSpeedInfo theXSpeedInfo = new Packets.XSpeedInfo();
            theXSpeedInfo.xTransSpeed = _settings.xTransSpeed = xTransSpeed;
            theXSpeedInfo.xTransAccel = _settings.xTransAccel = xTransAccel;
            theXSpeedInfo.xPrintSpeed  = _settings.xPrintSpeed = xPrintSpeed;
            theXSpeedInfo.xPrintAccel = _settings.xPrintAccel = xPrintAccel;

            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetAllXSpeedInfo, false);
            h.PacketData = theXSpeedInfo;

            _theQueue.EnqueueAction(h);

            h = new PacketHolder(Packets.PacketTypes.SetXDotDensity, false);
            Packets.DensityInfo di = new Packets.DensityInfo();
            di.StepsBetween = stepsBetweenXDots;
            h.PacketData = di;

            _theQueue.EnqueueAction(h);
        }

        public void SetPlatformRelatedValues(double sourceDistanceStepRatio, double platformDistanceStepRatio, double printThickness, double powderSourceRatio, int platformSpeed, int platformAccel)
        {
            _settings.setPowderPlatformInfo(sourceDistanceStepRatio, platformDistanceStepRatio, printThickness, powderSourceRatio);
            _settings.platformsTransSpeed = platformSpeed;
            _settings.platformsTransAccel = platformAccel;
        }

        public void AskFreeRam()
        {
            _theQueue.EnqueueAction(new PacketHolder(Packets.PacketTypes.AskStackHeapDistance, false));

        }

        public void EmergencyAllStop()
        {
            _theQueue.sendAllStop();
        }

        public void SetInv(bool x, bool y, bool source, bool platform)
        {
            PacketHolder theHolder = new PacketHolder(Packets.PacketTypes.SetInversionOfAxes, false);
            Packets.AxisInversionInformation theInversion = new Packets.AxisInversionInformation();
            theInversion.XInverted = x;
            theInversion.YInverted = y;
            theInversion.PlatformInverted = platform;
            theInversion.SourceInverted = source;
            theHolder.PacketData = theInversion;

            _theQueue.EnqueueAction(theHolder);

            _settings.xInv = x;
            _settings.yInv = y;

        }

        public void requestTranslateX(long amount)
        {
            _thePositioning.translateX(amount);
        }

        public void reqeustTranslateY(long amount)
        {
            _thePositioning.translateY(amount);
        }

        public void requestTranslateSource(long amount)
        {
            _thePositioning.translateSource(amount);
        }

        public void requestTranslateBed(long amount)
        {
            _thePositioning.translateBed(amount);
        }

        public void setZeroPos()
        {
            _thePositioning.SetXYHome();
        }

        public void setXYMaxes()
        {
            _thePositioning.setXYMaxes();
        }

        public void setDist1()
        {
            _thePositioning.setDist1();
        }

        public void setDist2()
        {
            _thePositioning.setDist2();
        }

        public void SetYRelatedValues(ushort ySpeed, ushort yAccel, double distanceStepRatio)
        {
            _settings.yTransSpeed = (int)ySpeed;
            _settings.yTransAccel = (int)yAccel;

            _settings.setYDistanceStepRatio(distanceStepRatio);

            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetAllYSpeedInfo, false);
            Packets.SetSpeedInfo s = new Packets.SetSpeedInfo();
            s.Speed = ySpeed;
            s.Accel = yAccel;
            h.PacketData = s;

            _theQueue.EnqueueAction(h);
        }

        public void DoTestPart(bool onlyOneLayer)
        {

            TestPart t = new TestPart((double)20, _settings, _thePositioning, onlyOneLayer, false);

            List<PacketHolder>print = t.GetTestPart();
            PrintTimeEstimator pte = new PrintTimeEstimator(print, _baudRate, _settings);
            double time = pte.GetPrintTime();
            time = (double)(time / 1000);
            int hours, mins, secs;
            hours =  mins = secs = 0;
            while (time >= 60 * 60)
            {
                time -= 60 * 60;
                hours++;
            }
            while (time >= 60)
            {
                time -= 60;
                mins++;
            }
            secs = Convert.ToInt32( time);

            DialogResult result = MessageBox.Show("The requested test part will take " + hours.ToString() + "h " + mins.ToString() + "m " + secs.ToString() + "s to print.  Continue?", "Test part", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                for (int i = 0; i < print.Count; i++)
                {
                    _theQueue.EnqueueAction(print[i]);
                }

            }
        }

        //public void SetPowderBedsInv(bool invert)
        //{
        //    PacketHolder h = new PacketHolder(Packets.PacketTypes.SetInversionOfPowderBeds, false);
        //    Packets.AxisInversionInformation i = new Packets.AxisInversionInformation();
        //    i.PlatformsInverted = invert;
        //    h.PacketData = i;

        //    _theQueue.EnqueueAction(h);

        //    _settings.bedsInv = invert;
        //}

        public void SetPlatformsZero()
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetPowderPlatformsHome, false);

            _theQueue.EnqueueAction(h);
        }

        public void SetXSpeedInfo(Packets.XSpeedInfo theSpeedInfo)
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetAllXSpeedInfo, false);
            h.PacketData = theSpeedInfo;

            _theQueue.EnqueueAction(h);
        }

        public void SetYSpeedInfo(Packets.SetSpeedInfo speedInfo)
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetAllYSpeedInfo, false);
            h.PacketData = speedInfo;

        }

        public void SetRollingMechanismSpeed(double RollingMechanismDistanceStepRatio, double RollingMechanismSpeedRatio)
        {
            _settings.RollingMechanismDistanceStepRatio = RollingMechanismDistanceStepRatio;
            _settings.RollingMechanismDistrSpeedRatio = RollingMechanismSpeedRatio;
            _settings.CalculateRollingMechanismSpeed();

            PacketHolder h = new PacketHolder(Packets.PacketTypes.SetRollingMechanismSpeeds, false);
            Packets.SetSpeedInfo s = new Packets.SetSpeedInfo();
            s.Speed = (ushort)_settings.rollingMechanismSpeed;
            s.Accel = (ushort)_settings.rollingMechanismAccel;
            h.PacketData = s;

            _theQueue.EnqueueAction(h);
        }

        public void ProcessGCodePart(int cores, bool save)
        {
            MultiThreadLayerProcessing mtlp = new MultiThreadLayerProcessing(cores, _settings.PrintThickness, _settings, save);

            mtlp.ProcessingCompleted += new MultiThreadLayerProcessing.ProcessingCompletedEventHandler(mtlp_ProcessingCompleted);
            mtlp.ProgressReport += new MultiThreadLayerProcessing.ProgressReportEventHandler(pcg_ProgressReport);
            mtlp.TimeEstimationReady += new MultiThreadLayerProcessing.TimeEstimationEventHandler(mtlp_TimeEstimationReady);

            mtlp.ReadFile();
            
            mtlp.Start();
        }

        private void mtlp_TimeEstimationReady(string time)
        {
            TimeEtaReady(time);
        }

        private void mtlp_ProcessingCompleted(PowderPart Part)
        {
            PrintCommandGeneration pcg = new PrintCommandGeneration(Part, _settings, _thePositioning);
            pcg.PrintCommandsReady += new PrintCommandGeneration.PrintCommandsReadyEventHandler(pcg_PrintCommandsReady);
            pcg.ProgressReport += new PrintCommandGeneration.ProgressReportEventHandler(pcg_ProgressReport);
            pcg.TimeEtaReady += new PrintCommandGeneration.TimeEtaEventHandler(mtlp_TimeEstimationReady);
            
            new Thread(pcg.DoWork).Start();

            _curPrintLayers = Part.Part.Count;

        }

        private void pcg_ProgressReport(int max, int curPos)
        {
            LongOperationProgress(max, curPos);
        }

        private void pcg_PrintCommandsReady(List<PacketHolder> Packets)
        {
            PrintTimeEstimator pte = new PrintTimeEstimator(Packets, _baudRate, _settings);
            double time = pte.GetPrintTime();
            time = (double)(time / 1000);
            int hours, mins, secs;
            hours = mins = secs = 0;
            while (time >= 60 * 60)
            {
                time -= 60 * 60;
                hours++;
            }
            while (time >= 60)
            {
                time -= 60;
                mins++;
            }
            secs = Convert.ToInt32(time);

            DialogResult result = MessageBox.Show("The requested test part will take " + hours.ToString() + "h " + mins.ToString() + "m " + secs.ToString() + "s to print.  Continue?", "Test part", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                
                _duringPrintEstimation.Start();
                _duringPrintEstimation.SetValues(_curPrintLayers, 0);
                _curPrintLayer = 0;

                for (int i = 0; i < Packets.Count; ++i)
                {
                    _theQueue.EnqueueAction(Packets[i]);
                }
            }
        }

        public void FireSingleArray()
        {
            PacketHolder h = new PacketHolder(Packets.PacketTypes.FireNozzlesOnce, false);

            _theQueue.EnqueueAction(h);

        }

        public void Pause()
        {
            _theQueue.Pause();
        }

        public void LoadExistingPart()
        {
            LoadExistingProcessedPart lepp = new LoadExistingProcessedPart(_settings);
            lepp.PowderPartCompleted += new LoadExistingProcessedPart.PowderPartCompletedEventHandler(lepp_PowderPartCompleted);
            lepp.ProgressReport += new LoadExistingProcessedPart.ProgressReportEventHandler(lepp_ProgressReport);
            lepp.Start();
        }

        private void lepp_ProgressReport(int max, int cur)
        {
            LongOperationProgress(max, cur);
        }

        private void lepp_PowderPartCompleted(PowderPart thePart)
        {
            mtlp_ProcessingCompleted(thePart);
        }




    }
}
