using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace Printer_Controller
{   
    /*
     * This class simply implements the GUI of the bot, instantiating and interacting with a Printer object.
     *
     * 
     * */
    public partial class Main : Form
    {
        Printer _printer;
        
        public Main()
        {
            InitializeComponent();

            _printer = new Printer();
            _printer.PrinterEvent += new Printer.PrinterEventHandler(_printer_PrinterEvent);
            _printer.PacketFlowInfo += new Printer.PacketFlowIndicationEventHandler(_printer_PacketFlowInfo);
            
        }

        private void _printer_PacketFlowInfo(PacketFlowInfo thePacketInfo)
        {

            if (thePacketInfo.Packet.PacketType == Packets.PacketTypes.StackHeapResponse)
            {
                PacketHolder theHolder = thePacketInfo.Packet;
                Packets.StringResponse theResponse = (Packets.StringResponse)theHolder.PacketData;
                MessageBox.Show(theResponse.TheString);
            }

            

        }

        private void addPacketText(string stringToAdd)
        {
            txtPacketFlow.Invoke((MethodInvoker)delegate
            {
                txtPacketFlow.Text = txtPacketFlow.Text + Environment.NewLine + stringToAdd;
                txtPacketFlow.Select(txtPacketFlow.Text.Length, 0);
            });
        }

        private void _printer_PrinterEvent(Printer.PrinterEventDetails theEvent)
        {
            if (theEvent.EventName == "connected")
            {
                lbConStatus.Invoke((MethodInvoker)delegate
                {
                    lbConStatus.Text = "Connected";
                    lbConStatus.ForeColor = Color.Green;
                });
            }
            else if (theEvent.EventName == "connectionFailed")
            {
                lbConStatus.Invoke((MethodInvoker)delegate
                {
                    lbConStatus.Text = theEvent.Description;
                    lbConStatus.ForeColor = Color.Red;
                });
            }
            else if (theEvent.EventName == "notConnected")
            {
                lbConStatus.Invoke((MethodInvoker)delegate
                {
                    lbConStatus.Text = theEvent.Description;
                    lbConStatus.ForeColor = Color.Red;
                });
            }
        }

        private void Main_Load(object sender, EventArgs e)
        {
            _printer.LongOperationProgress += new Printer.LongOperationEventHandler(_printer_GCodeProcessingProgress);
            _printer.TimeEtaReady += new Printer.TimeEventHandler(_printer_TimeEtaReady);

            SaveAllStuffs();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            _printer.ConnectToPrinter(txtComPort.Text, 57600);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            _printer.Close();
        }

        private void upDown_SelectedItemChanged(object sender, EventArgs e)
        {
            double percent = _printer.GetXBindingAgentDensity(Double.Parse(txtXDistStepRatio.Text), Int16.Parse(upDown.Text));
            string strPercent = ((double)(percent * (double)100)).ToString("000.00");
            strPercent += "%";

            lbXDensity.Text = strPercent;


        }

        private void btnSaveXAxis_Click(object sender, EventArgs e)
        {
            SaveXRelatedValues();
            Debug.Print("got past saving");
        }

        private void SaveXRelatedValues()
        {
            _printer.SaveXRelatedValues(Int16.Parse(upDown.Text), Double.Parse(txtXDistStepRatio.Text), int.Parse(txtXTransSpeed.Text), int.Parse(txtXTransAccel.Text), int.Parse(txtPrintSpeed.Text), int.Parse(txtPrintAccel.Text));
        }

        private void SaveYRelatedValues()
        {
            _printer.SetYRelatedValues(ushort.Parse(txtYSpeed.Text), ushort.Parse(txtYAccel.Text), double.Parse(txtYRatio.Text));
        }

        private void SavePlatformRelatedValues()
        {
            _printer.SetPlatformRelatedValues(Double.Parse(txtSourceRatio.Text),Double.Parse(txtPlatformRatio.Text), Double.Parse(txtPrintThickness.Text), Double.Parse(txtPowderRatio.Text), int.Parse(txtPlatformSpeed.Text), int.Parse(txtPlatformAccel.Text));
        }

        private void button9_Click(object sender, EventArgs e)
        {
            //int result = _printer.Test6(150.0);
            //Debug.Print("got past the calculation");
            //MessageBox.Show(result.ToString());
            //Debug.Print("got apst the message box");

            //GPolygon gp = new GPolygon(2.5, 0.5, 5.8, 5.3, 1.8);
            //bool result = gp.WithinPolygon(5, 1);

            //_printer.Test7();

            

        }

        private void btnSavePowderValues_Click(object sender, EventArgs e)
        {
            SavePlatformRelatedValues();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            _printer.AskFreeRam();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            //_printer.Test5();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _printer.EmergencyAllStop();
        }

        private void btnSaveAll_Click(object sender, EventArgs e)
        {

            

            SaveAllStuffs();

        }

        private void SaveAllStuffs()
        {
            btnTestPart.Enabled = true;
            btnOpenGcode.Enabled = true;
            btnOpenProcessedPart.Enabled = true;
            
            SaveXRelatedValues();
            SaveYRelatedValues();
            SavePlatformRelatedValues();
            SaveAxisInversions();
            SaveRollingMechanismRelatedValues();
        }

        private void chkInvertX_CheckedChanged(object sender, EventArgs e)
        {
            SaveAxisInversions();
        }

        private void SaveAxisInversions()
        {
            _printer.SetInv(chkInvertX.Checked, chkInvertY.Checked, chkInvertSource.Checked, chkInvertBed.Checked);
        }

        private void btnNegX_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateX(-long.Parse(txtStepAmount.Text));
        }

        private void btnPosX_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateX(long.Parse(txtStepAmount.Text));
        }

        private void btnPosY_Click(object sender, EventArgs e)
        {
            _printer.reqeustTranslateY(long.Parse(txtStepAmount.Text));
        }

        private void btnNegY_Click(object sender, EventArgs e)
        {
            _printer.reqeustTranslateY(-long.Parse(txtStepAmount.Text));
        }

        private void btnPosSource_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateSource(long.Parse(txtBedAmount.Text));
        }

        private void btnNegSource_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateSource(-long.Parse(txtBedAmount.Text));
        }

        private void btnPosBed_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateBed(long.Parse(txtBedAmount.Text));
        }

        private void btnNegBed_Click(object sender, EventArgs e)
        {
            _printer.requestTranslateBed(-long.Parse(txtBedAmount.Text));
        }

        private void chkInvertY_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void btnZero_Click(object sender, EventArgs e)
        {
            _printer.setZeroPos();
        }

        private void btnMax_Click(object sender, EventArgs e)
        {
            _printer.setXYMaxes();
        }

        private void btnDistBegin_Click(object sender, EventArgs e)
        {
            _printer.setDist1();
        }

        private void btnDistEnd_Click(object sender, EventArgs e)
        {
            _printer.setDist2();
        }

        private void btnSaveY_Click(object sender, EventArgs e)
        {
            
        }

        private void btnTestPart_Click(object sender, EventArgs e)
        {
            _printer.DoTestPart(false);
        }

        private void btnSaveRatios_Click(object sender, EventArgs e)
        {
            SaveXRelatedValues();
            SaveYRelatedValues();
            SavePlatformRelatedValues();
        }

        private void btnPlatformHome_Click(object sender, EventArgs e)
        {
            _printer.SetPlatformsZero();
        }

        private void SaveRollingMechanismRelatedValues()
        {
            _printer.SetRollingMechanismSpeed(Double.Parse(txtRollingBarRatio.Text), Double.Parse(txtPowderDistRatio.Text));
        }

        private void chkInvPlatforms_CheckedChanged(object sender, EventArgs e)
        {
            SaveAxisInversions();
        }

        private void btnOpenGcode_Click(object sender, EventArgs e)
        {

            
            

            _printer.ProcessGCodePart(Convert.ToInt32(coresToRun.Value), chkSaveToFile.Checked);
            
        }

        private void _printer_GCodeProcessingProgress(int max, int cur)
        {
            progressProcessing.Invoke((MethodInvoker)delegate
            {
                progressProcessing.Maximum = max;
                progressProcessing.Value = cur;
            });
        }

        private void _printer_TimeEtaReady(string Time)
        {
            gbProgress.Invoke((MethodInvoker)delegate
            {
                gbProgress.Text = Time;
            });
        }

        private void _printer_GCodeNewProcess()
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _printer.DoTestPart(true);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _printer.FireOnce();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
                _printer.StartFireContinuously();
            else
                _printer.StopFireContinuously();
        }

        private void chkInvertSource_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            _printer.Pause();
        }

        private void btnOpenProcessedPart_Click(object sender, EventArgs e)
        {
            _printer.LoadExistingPart();
        }



    }
}
