using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace LanChat.Client.Services
{
    public sealed class CameraCaptureService : IDisposable
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private int _openVersion;

        public event Action<byte[]>? FrameReady;
        public event Action<string>? StatusChanged;

        public bool IsRunning => _captureTask != null && !_captureTask.IsCompleted;

        public static IReadOnlyList<MediaDeviceInfo> GetAvailableCameras()
        {
            return DirectShowCameraEnumerator.GetVideoInputDevices();
        }

        public static IReadOnlyList<MediaDeviceInfo> GetCameraCandidates()
        {
            return GetAvailableCameras();
        }

        public async Task<bool> StartAsync(int cameraIndex = 0, int width = 320, int height = 240, int fps = 10)
        {
            if (IsRunning) return true;

            VideoCapture? capture = null;
            Stop();
            int version = Interlocked.Increment(ref _openVersion);
            try
            {
                StatusChanged?.Invoke("Opening camera...");
                capture = await Task.Run(() => OpenCapture(cameraIndex));

                if (version != Volatile.Read(ref _openVersion))
                {
                    capture.Dispose();
                    return false;
                }

                if (!capture.IsOpened())
                {
                    StatusChanged?.Invoke("Camera unavailable; using fallback video.");
                    capture.Dispose();
                    return false;
                }

                capture.FrameWidth = width;
                capture.FrameHeight = height;
                capture.Fps = fps;

                _capture = capture;
                capture = null;
                _cts = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(width, height, fps, _cts.Token));
                StatusChanged?.Invoke("Camera started.");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Camera error: {ex.Message}");
                capture?.Dispose();
                return false;
            }
        }

        public void Stop()
        {
            Interlocked.Increment(ref _openVersion);
            _cts?.Cancel();
            _captureTask = null;
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
            _cts?.Dispose();
            _cts = null;
        }

        private async Task CaptureLoop(int width, int height, int fps, CancellationToken token)
        {
            int delayMs = Math.Max(30, 1000 / Math.Max(1, fps));
            using var frame = new Mat();
            using var resized = new Mat();

            while (!token.IsCancellationRequested && _capture != null)
            {
                try
                {
                    if (!_capture.Read(frame) || frame.Empty())
                    {
                        await Task.Delay(delayMs, token);
                        continue;
                    }

                    Cv2.Resize(frame, resized, new Size(width, height));
                    Cv2.ImEncode(".jpg", resized, out var jpegBytes, new[]
                    {
                        new ImageEncodingParam(ImwriteFlags.JpegQuality, 45)
                    });

                    FrameReady?.Invoke(jpegBytes);
                    await Task.Delay(delayMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Camera frame error: {ex.Message}");
                    await Task.Delay(delayMs);
                }
            }
        }

        private static VideoCapture OpenCapture(int cameraIndex)
        {
            var capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (capture.IsOpened())
            {
                return capture;
            }

            capture.Dispose();
            return new VideoCapture(cameraIndex);
        }

        public void Dispose() => Stop();
    }
}
