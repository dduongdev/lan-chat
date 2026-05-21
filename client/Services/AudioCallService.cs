using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace LanChat.Client.Services
{
    public sealed class AudioCallService : IDisposable
    {
        private readonly WaveFormat _waveFormat = new(16000, 16, 1);
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _playbackBuffer;

        public event Action<byte[]>? AudioCaptured;
        public event Action<string>? StatusChanged;

        public bool IsMicrophoneRunning => _waveIn != null;

        public static IReadOnlyList<MediaDeviceInfo> GetAvailableMicrophones()
        {
            var devices = new List<MediaDeviceInfo>();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                try
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    devices.Add(new MediaDeviceInfo
                    {
                        DeviceIndex = i,
                        DisplayName = string.IsNullOrWhiteSpace(caps.ProductName)
                            ? $"Microphone {i}"
                            : caps.ProductName
                    });
                }
                catch
                {
                    // Ignore broken/unsupported capture devices.
                }
            }

            return devices;
        }

        public void StartMicrophone(int deviceNumber = 0)
        {
            if (_waveIn != null) return;

            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = _waveFormat,
                    BufferMilliseconds = 20,
                    NumberOfBuffers = 3
                };
                _waveIn.DataAvailable += (_, e) =>
                {
                    var bytes = new byte[e.BytesRecorded];
                    Buffer.BlockCopy(e.Buffer, 0, bytes, 0, e.BytesRecorded);
                    AudioCaptured?.Invoke(bytes);
                };
                _waveIn.RecordingStopped += (_, e) =>
                {
                    if (e.Exception != null)
                    {
                        StatusChanged?.Invoke($"Microphone stopped: {e.Exception.Message}");
                    }
                };
                _waveIn.StartRecording();
                StatusChanged?.Invoke("Microphone started.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Microphone error: {ex.Message}");
                StopMicrophone();
            }
        }

        public void StopMicrophone()
        {
            if (_waveIn == null) return;
            try
            {
                _waveIn.StopRecording();
            }
            catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }

        public void StartPlayback()
        {
            if (_waveOut != null) return;

            try
            {
                _playbackBuffer = new BufferedWaveProvider(_waveFormat)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(500),
                    DiscardOnBufferOverflow = true
                };
                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 100
                };
                _waveOut.Init(_playbackBuffer);
                _waveOut.Play();
                StatusChanged?.Invoke("Audio playback started.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Audio playback error: {ex.Message}");
                StopPlayback();
            }
        }

        public void PlayRemoteAudio(byte[] pcmBytes)
        {
            if (_playbackBuffer == null || pcmBytes.Length == 0) return;
            _playbackBuffer.AddSamples(pcmBytes, 0, pcmBytes.Length);
        }

        public void StopPlayback()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _playbackBuffer = null;
        }

        public void Dispose()
        {
            StopMicrophone();
            StopPlayback();
        }
    }
}
