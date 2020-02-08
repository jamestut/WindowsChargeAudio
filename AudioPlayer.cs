using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Threading;
using System.IO.Pipes;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ChargeAudio
{
    class AudioPlayer
    {
        public string AudioFile { get; private set; }
        public bool AdjustVolumeOnPlay { get; private set; }
        public float TargetVolume { get; private set; }

        WasapiOut _AudioOut = null;
        AudioFileReader _AudioReader = null;
        volatile bool _AudioPlaying = false;

        // previous volume state if MuteOnPlay is true
        int _Pid;
        bool _AdjustAudioCarriedOut = false;
        float _LastMasterVolume;
        bool _LastMasterMuteStatus;

        // muter sessions
        Dictionary<uint, Tuple<PipeStream,PipeStream>> _MuterSessions = new Dictionary<uint, Tuple<PipeStream, PipeStream>>();

        public AudioPlayer(string audioFile, float? playingVolume)
        {
            _Pid = Process.GetCurrentProcess().Id;
            AudioFile = audioFile;
            if (playingVolume != null)
            {
                AdjustVolumeOnPlay = true;
                TargetVolume = (float)playingVolume;
            }
            else
                AdjustVolumeOnPlay = false;

            // start session muter
            uint activeSess = SpawnInSession.GetCurrentUserSessionId();
            if (activeSess != SpawnInSession.INVALID_SESSION_ID)
                StartSessionMuter(activeSess);
        }

        public void PlayAudio()
        {
            if (_AudioPlaying)
                return;
            try
            {
                if(AdjustVolumeOnPlay)
                    AdjustAudioVolume();

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

        public void StartSessionMuter(uint sessId)
        {
            // test if we already have the session data
            if(_MuterSessions.ContainsKey(sessId))
            {
                var pipes = _MuterSessions[sessId];
                // ensure the pipe is working
                try
                {
                    pipes.Item1.WriteByte((byte)SessionMuter.Commands.PING);
                    pipes.Item2.ReadByte();
                    // do nothing if success
                    return;
                }
                catch(Exception)
                {
                    // very likely broken pipe. erase this pipe and create new child.
                    CloseSessionMuter(sessId);
                }
            }

            // create new anon pipes
            AnonymousPipeServerStream pipeServerOut = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            AnonymousPipeServerStream pipeServerIn = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            // open process
            bool success = false;
            try
            {
                success = SpawnInSession.StartProcessInSession(Assembly.GetExecutingAssembly().Location, sessId, 
                    $"ProcessName.exe startsessionmuter {Process.GetCurrentProcess().Id} {pipeServerOut.GetClientHandleAsString()} {pipeServerIn.GetClientHandleAsString()}");
            }
            catch (Exception) { success = false; }
            
            pipeServerIn.DisposeLocalCopyOfClientHandle();
            pipeServerOut.DisposeLocalCopyOfClientHandle();
            
            if (success)
            {
                _MuterSessions[sessId] = new Tuple<PipeStream, PipeStream>(pipeServerOut, pipeServerIn);
            }
            else
            {
                pipeServerOut.Dispose();
                pipeServerIn.Dispose();
            }
        }

        public void CloseSessionMuter(uint sessId)
        {
            if(_MuterSessions.ContainsKey(sessId))
            {
                var pipes = _MuterSessions[sessId];
                try
                {
                    pipes.Item1.WriteByte((byte)SessionMuter.Commands.EXIT);
                    pipes.Item1.Dispose();
                    pipes.Item2.Dispose();
                }
                catch(Exception) { }
                _MuterSessions.Remove(sessId);
            }
        }

        public void CleanSessionMuters()
        {
            List<uint> sessIds = new List<uint>(_MuterSessions.Count);
            foreach(var kvp in _MuterSessions)
                sessIds.Add(kvp.Key);
            foreach (var k in sessIds)
                CloseSessionMuter(k);
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            RestoreAudioVolume();
            _AudioPlaying = false;
            _AudioOut.PlaybackStopped -= OutputDevice_PlaybackStopped;
        }

        private void AdjustAudioVolume()
        {
            if (_AdjustAudioCarriedOut)
                return;

            try
            {
                var deviceEnumerator = new MMDeviceEnumerator();
                var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                
                _LastMasterVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                _LastMasterMuteStatus = device.AudioEndpointVolume.Mute;

                // we don't need to mute if the volume is loud enough already
                if (_LastMasterVolume > TargetVolume && !_LastMasterMuteStatus)
                    return;

                _AdjustAudioCarriedOut = true;

                SendMuteCommandToSessions(true);

                Thread.Sleep(200);
                device.AudioEndpointVolume.Mute = false;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = TargetVolume;
            } 
            catch (Exception)
            {
                _AdjustAudioCarriedOut = false;
            }
        }

        private void RestoreAudioVolume()
        {
            if (_AdjustAudioCarriedOut)
            {
                try
                {
                    var deviceEnumerator = new MMDeviceEnumerator();
                    var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sess = device.AudioSessionManager.Sessions;

                    // restore master volume and mute status
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = _LastMasterVolume;
                    device.AudioEndpointVolume.Mute = _LastMasterMuteStatus;

                    Thread.Sleep(200);
                    SendMuteCommandToSessions(false);
                }
                catch (Exception) { }

                _AdjustAudioCarriedOut = false;
            }
        }

        private void SendMuteCommandToSessions(bool mute)
        {
            SessionMuter.Commands cmd = mute ? SessionMuter.Commands.MUTE : SessionMuter.Commands.UNMUTE;

            // ask to unmute all sessions
            List<uint> destroyMuterList = new List<uint>();
            foreach (var kvp in _MuterSessions)
            {
                try
                {
                    kvp.Value.Item1.WriteByte((byte)cmd);
                    // wait for client to reply.
                    kvp.Value.Item2.ReadByte();
                }
                catch (Exception)
                {
                    destroyMuterList.Add(kvp.Key);
                }
            }
            // remove non working sessions
            foreach (uint k in destroyMuterList)
                CloseSessionMuter(k);
        }
    }
}
