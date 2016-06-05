using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LyncHLib
{
    public enum OCStatus
    {
        Unknown,
        Running,
        NotRunning
    }

    class LyncDaemon
    {
        private int sleepTime = 1000;
        private OCStatus ocState = OCStatus.Unknown;
        private System.Timers.Timer timer = null;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void ProcessOfflineHeadle(object sender, OCStatus state);
        public delegate void ProcessOnlineHeadle(object sender, OCStatus state);

        public event ProcessOfflineHeadle OnCommunicatorNotRuning;
        public event ProcessOnlineHeadle OnCommunicatorRuning;

        public OCStatus ProcState
        {
            get
            {
                return this.ocState;
            }
        }

        public static bool CheckLyncUpAndRunning()
        {
            try
            {
                int _lyncUpAndRunning = 0;
                _lyncUpAndRunning = Convert.ToInt32(Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey("Software")
                    .OpenSubKey("IM Providers")
                    .OpenSubKey("Lync")
                    .GetValue("UpAndRunning", 1));

                return _lyncUpAndRunning == 2;
            }
            catch (Exception exp)
            {
                logger.Error(exp);
                return false;
            }
        }

        private bool ProcessMethod()
        {
            try
            {
                bool isLyncAlive = CheckLyncUpAndRunning();

                if (isLyncAlive)
                {
                    Process[] processes = Process.GetProcessesByName("lync");

                    if (processes != null && processes.Length > 0)
                    {
                        Process lyncProcess = processes[0];
                        lyncProcess.EnableRaisingEvents = true;
                        lyncProcess.Exited += lyncProcess_Exited;
                        timer.Stop();

                        this.ocState = OCStatus.Running;

                        if (OnCommunicatorRuning != null)
                        {
                            this.OnCommunicatorRuning(this, OCStatus.Running);
                        }
                    }
                }
                else
                {
                    this.ocState = OCStatus.NotRunning;

                    if (OnCommunicatorNotRuning != null)
                    {
                        this.OnCommunicatorNotRuning(this, OCStatus.NotRunning);
                    }
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception);
                return false;
            }

            return true;
        }

        private void lyncProcess_Exited(object sender, EventArgs e)
        {
            timer.Start();
            ProcessMethod();
        }

        public void StartMonitor()
        {
            if (timer == null)
            {
                timer = new System.Timers.Timer(sleepTime);
            }

            timer.Elapsed += timer_Elapsed;
            timer.Start();
        }

        public void StopMonitor()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ProcessMethod();
        }
    }
}
