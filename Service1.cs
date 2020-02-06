using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ChargeAudio
{
    public partial class Service1 : ServiceBase
    {
        static AudioPlayer _Player;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (!LoadSettings())
                RaiseError(0x80004005);
        }

        protected override void OnStop()
        {
            
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if(powerStatus == PowerBroadcastStatus.PowerStatusChange)
            {
                if(SystemInformation.PowerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery &&
                    SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online)
                {
                    _Player.PlayAudio();
                }
            }
            return true;
        }

        protected override void OnCustomCommand(int command)
        {
            switch(command)
            {
                case 128:
                    _Player.PlayAudio();
                    break;
            }
        }

        private bool LoadSettings()
        {
            using (var regKey = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{ServiceName}\\Config"))
            {
                if (regKey == null)
                    return false;
                
                var audioFileName = (string)regKey.GetValue("AudioFile");
                if (audioFileName == null)
                    return false;
                
                float? targetVolumeLevel = null;
                try
                {
                    int volPercent = (int)regKey.GetValue("TargetVolume");
                    targetVolumeLevel = Math.Min(100.0f, (float)volPercent) / 100.0f;
                }
                catch (Exception) { }
                
                _Player = new AudioPlayer(audioFileName, targetVolumeLevel);
                return true;
            }
        }

        private void RaiseError(uint code)
        {
            ExitCode = unchecked((int)code);
            Stop();
        }
    }
}
