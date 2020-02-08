using System;
using System.IO.Pipes;
using NAudio.CoreAudioApi;

namespace ChargeAudio
{
    class SessionMuter
    {
        public enum Commands
        {
            NONE = 0,
            PING = 1,
            MUTE = 2,
            UNMUTE = 3,
            EXIT = 254,
            ACK = 255
        }

        private int _ParentPid;
        private PipeStream _PipeIn, _PipeOut;
        private bool _MuteCarriedOut;

        public SessionMuter(int parentPid, string pipeHandleIn, string pipeHandleOut)
        {
            _ParentPid = parentPid;
            _PipeIn = new AnonymousPipeClientStream(PipeDirection.In, pipeHandleIn);
            _PipeOut = new AnonymousPipeClientStream(PipeDirection.Out, pipeHandleOut);
        }

        ~SessionMuter()
        {
            _PipeIn.Dispose();
            _PipeOut.Dispose();
        }

        public void Run()
        {
            Commands serverCmd = Commands.NONE;
            while(serverCmd != Commands.EXIT)
            {
                serverCmd = (Commands)Enum.ToObject(typeof(Commands), _PipeIn.ReadByte());
                switch (serverCmd)
                {
                    case Commands.PING:
                        _PipeOut.WriteByte((byte)Commands.ACK);
                        break;
                    case Commands.MUTE:
                        MuteAudioSessions();
                        _PipeOut.WriteByte((byte)Commands.ACK);
                        break;
                    case Commands.UNMUTE:
                        UndoMuteAudioSessions();
                        _PipeOut.WriteByte((byte)Commands.ACK);
                        break;
                }
            }
        }

        private void MuteAudioSessions()
        {
            if (_MuteCarriedOut)
                return;
            try
            {
                var deviceEnumerator = new MMDeviceEnumerator();
                var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sess = device.AudioSessionManager.Sessions;

                // mute all sessions
                for (int i = 0; i < sess.Count; ++i)
                    if(sess[i].GetProcessID != _ParentPid)
                        sess[i].SimpleAudioVolume.Mute = true;

                _MuteCarriedOut = true;
            }
            catch (Exception)
            {
                _MuteCarriedOut = false;
            }
        }

        private void UndoMuteAudioSessions()
        {
            if(_MuteCarriedOut)
            {
                try
                {
                    var deviceEnumerator = new MMDeviceEnumerator();
                    var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sess = device.AudioSessionManager.Sessions;

                    for (int i = 0; i < sess.Count; ++i)
                        sess[i].SimpleAudioVolume.Mute = false;
                }
                catch (Exception) { }
                _MuteCarriedOut = false;
            }
        }
    }
}
