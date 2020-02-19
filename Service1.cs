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
        private PowerLineStatus _CurrStatus;
        public static EventLog EventLogger { get; private set; }

        public Service1()
        {
            InitializeComponent();
            _CurrStatus = SystemInformation.PowerStatus.PowerLineStatus;

            EventLogger = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists(ServiceName))
            {
                System.Diagnostics.EventLog.CreateEventSource(ServiceName, EventLogger.Log);
            }
            EventLogger.Source = ServiceName;
        }

        protected override void OnStart(string[] args)
        {
            if (!LoadSettings())
            {
                EventLogger.WriteEntry("Failed to load settings. Ensure the registry settings are set correctly.", EventLogEntryType.Error);
                RaiseError(0x80004005);
            }
        }

        protected override void OnStop()
        {
            _Player.CleanSessionMuters();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if(powerStatus == PowerBroadcastStatus.PowerStatusChange)
            {
                if(SystemInformation.PowerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery &&
                    SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online &&
                    _CurrStatus != PowerLineStatus.Online)
                {
                    _Player.PlayAudio();
                }
                // update current status
                _CurrStatus = SystemInformation.PowerStatus.PowerLineStatus;
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
                case 129:
                    SpawnInSession.StartProcessInSession("notepad.exe", 1);
                    break;
            }
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            //EventLogger.WriteEntry($"Session change. Reason: {changeDescription.Reason}. Sess ID = {changeDescription.SessionId}.");
            switch(changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                case SessionChangeReason.RemoteConnect:
                case SessionChangeReason.ConsoleConnect:
                    _Player.StartSessionMuter((uint)changeDescription.SessionId);
                    break;
                case SessionChangeReason.SessionLogoff:
                    _Player.CloseSessionMuter((uint)changeDescription.SessionId);
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
