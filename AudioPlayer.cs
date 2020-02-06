using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Threading;

namespace ChargeAudio
{
    class AudioPlayer
    {
        public string AudioFile { get; private set; }
        public bool MuteOnPlay { get; private set; }
        public float TargetVolume { get; private set; }

        WasapiOut _AudioOut = null;
        AudioFileReader _AudioReader = null;
        volatile bool _AudioPlaying = false;

        // previous volume state if MuteOnPlay is true
        int _Pid;
        bool _MuteCarriedOut;
        float _LastMasterVolume;
        bool _LastMasterMuteStatus;

        public AudioPlayer(string audioFile, float? playingVolume)
        {
            _Pid = Process.GetCurrentProcess().Id;
            AudioFile = audioFile;
            if (playingVolume != null)
            {
                MuteOnPlay = true;
                TargetVolume = (float)playingVolume;
            }
            else
                MuteOnPlay = false;
        }

        public void PlayAudio()
        {
            if (_AudioPlaying)
                return;
            try
            {
                //MuteAudio();

                _AudioPlaying = true;
                _AudioOut = new WasapiOut();
                _AudioOut.PlaybackStopped += OutputDevice_PlaybackStopped;

                _AudioReader = new AudioFileReader(AudioFile);
                _AudioOut.Init(_AudioReader);
                _AudioOut.Play();
            } 
            catch (Exception ex)
            {
                _AudioPlaying = false;
            }
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            //UnmuteAudio();
            _AudioPlaying = false;
            _AudioOut.PlaybackStopped -= OutputDevice_PlaybackStopped;
        }

        private void MuteAudio()
        {
            try
            {
                var deviceEnumerator = new MMDeviceEnumerator();
                var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sess = device.AudioSessionManager.Sessions;
                _LastMasterVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                _LastMasterMuteStatus = device.AudioEndpointVolume.Mute;
                // we don't need to mute if the volume is loud enough already
                if (_LastMasterVolume > TargetVolume && !_LastMasterMuteStatus)
                    return;

                _MuteCarriedOut = true;

                // mute all sessions
                for (int i = 0; i < sess.Count; ++i)
                    if (sess[i].GetProcessID != _Pid)
                        sess[i].SimpleAudioVolume.Mute = true;

                Thread.Sleep(200);
                device.AudioEndpointVolume.Mute = false;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = TargetVolume;
            } 
            catch (Exception)
            {
                _MuteCarriedOut = false;
            }
        }

        private void UnmuteAudio()
        {
            if (_MuteCarriedOut)
            {
                _MuteCarriedOut = false;
                try
                {
                    var deviceEnumerator = new MMDeviceEnumerator();
                    var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sess = device.AudioSessionManager.Sessions;

                    // restore master volume and mute status
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = _LastMasterVolume;
                    device.AudioEndpointVolume.Mute = _LastMasterMuteStatus;

                    // unmute all sessions
                    for (int i = 0; i < sess.Count; ++i)
                        if (sess[i].GetProcessID != _Pid)
                            sess[i].SimpleAudioVolume.Mute = false;
                }
                catch (Exception) { }
            }
        }
    }
}
