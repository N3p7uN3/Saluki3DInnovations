using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Printer_Controller
{
    /*
     * This class auto generates estimated amounts of time that a long process may take.  It is very rough, but useful for potentially time consuming operations.
     * The creating object simiply sends a maximum value, reports the current value, and every 200ms, reports the current estimated time that this long operation
     * will complete.
     * */
    
    public class ETAEstimation
    {
        private int beginningTick;
        private Timer _timer;

        public delegate void TimeReadyEventHandler(String time);
        public event TimeReadyEventHandler TimeReady;

        public string _desc;

        private int _curTime, _max, _cur;

        public ETAEstimation(string LongProcessDescription)
        {
            beginningTick = -1;
            _timer = new Timer(200);
            _desc = LongProcessDescription;
        }

        public void Start()
        {
            _timer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string time;
            
            if (_cur > 0)
            {
                int timeRemaining = Convert.ToInt32((double)((((double)((double)_curTime * (double)_max)) / (double)_cur) - (double)(_curTime)));

                timeRemaining = timeRemaining / 1000;
                int hours = 0;
                int minutes = 0;

                while (timeRemaining >= 60 * 60)
                {
                    hours++;
                    timeRemaining -= 60 * 60;
                }

                while (timeRemaining >= 60)
                {
                    minutes++;
                    timeRemaining -= 60;
                }

                time =  hours.ToString("00") + ":" + minutes.ToString("00") + ":" + timeRemaining.ToString("00");
            }
            else
            {
                time = "n/a";
            }

            TimeReady(_desc + " eta: " + time);
        }

        public void SetValues(int max, int cur)
        {
            

            if (beginningTick == -1)
            {
                beginningTick = Environment.TickCount;

                

            }

            _max = max;
            _cur = cur;

            _curTime = Environment.TickCount - beginningTick;




        }
    }
}
