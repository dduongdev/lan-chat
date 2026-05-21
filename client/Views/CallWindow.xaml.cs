using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LanChat.Client.Services;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Views
{
    public partial class CallWindow : Window
    {
        private readonly DispatcherTimer _frameTimer;
        private readonly CameraCaptureService _camera = new();
        private readonly AudioCallService _audio = new();
        private bool _isClosingFromRemote;
        private bool _cameraEnabled = true;
        private bool _microphoneEnabled;
        private bool _usingRealCamera;
        private bool _isLoadingDevices;
        private int _selectedCameraIndex;
        private int _selectedMicrophoneIndex;
        private int _frameCounter;
        private int _cameraStartVersion;
        private int _localFrameRenderPending;
        private int _remoteFrameRenderPending;
        private int _videoSendPending;
        private bool _isOpeningCamera;

        public CallWindow(string title)
        {
            InitializeComponent();
            TitleText.Text = title;

            CallService.Instance.CallStatusChanged += OnCallStatusChanged;
            CallService.Instance.RemoteVideoFrameReceived += OnRemoteVideoFrameReceived;
            CallService.Instance.RemoteAudioPacketReceived += OnRemoteAudioPacketReceived;
            CallService.Instance.CallEnded += OnCallEnded;
            _camera.FrameReady += OnLocalCameraFrameReady;
            _camera.StatusChanged += OnLocalMediaStatusChanged;
            _audio.AudioCaptured += OnLocalAudioCaptured;
            _audio.StatusChanged += OnLocalMediaStatusChanged;

            _frameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(85)
            };
            _frameTimer.Tick += (_, _) =>
            {
                if (!_cameraEnabled || _usingRealCamera || _isOpeningCamera) return;
                var jpeg = CreateGeneratedFrameJpeg();
                LocalImage.Source = DecodeJpeg(jpeg);
                QueueLocalVideoSend(jpeg);
            };
            _frameTimer.Start();
            Loaded += async (_, _) =>
            {
                LoadMediaDevices();
                _audio.StartPlayback();
                await StartCameraAsync();
                await CallService.Instance.SetMediaStateAsync(_cameraEnabled, _microphoneEnabled);
            };
        }

        private void OnCallStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(() => StatusText.Text = status);
        }

        private void OnRemoteVideoFrameReceived(ushort participantId, byte[] jpegBytes)
        {
            if (Interlocked.Exchange(ref _remoteFrameRenderPending, 1) == 1) return;

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    RemoteImage.Source = DecodeJpeg(jpegBytes);
                    RemotePlaceholder.Visibility = Visibility.Collapsed;
                    StatusText.Text = $"Receiving video from participant {participantId}";
                }
                finally
                {
                    Interlocked.Exchange(ref _remoteFrameRenderPending, 0);
                }
            });
        }

        private void OnRemoteAudioPacketReceived(ushort participantId, byte[] pcmBytes)
        {
            _audio.PlayRemoteAudio(pcmBytes);
        }

        private void OnLocalCameraFrameReady(byte[] jpegBytes)
        {
            QueueLocalVideoSend(jpegBytes);
            if (Interlocked.Exchange(ref _localFrameRenderPending, 1) == 1) return;

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (_cameraEnabled)
                    {
                        LocalImage.Source = DecodeJpeg(jpegBytes);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _localFrameRenderPending, 0);
                }
            });
        }

        private void OnLocalAudioCaptured(byte[] pcmBytes)
        {
            _ = CallService.Instance.SendAudioPacketAsync(pcmBytes);
        }

        private void QueueLocalVideoSend(byte[] jpegBytes)
        {
            if (Interlocked.Exchange(ref _videoSendPending, 1) == 1) return;
            _ = SendLocalVideoFrameAsync(jpegBytes);
        }

        private async System.Threading.Tasks.Task SendLocalVideoFrameAsync(byte[] jpegBytes)
        {
            try
            {
                await CallService.Instance.SendVideoFrameAsync(jpegBytes);
            }
            finally
            {
                Interlocked.Exchange(ref _videoSendPending, 0);
            }
        }

        private void OnLocalMediaStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(() => StatusText.Text = status);
        }

        private void OnCallEnded(CallEndedPayload payload)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _isClosingFromRemote = true;
                StatusText.Text = $"Call ended: {payload.Reason}";
                Close();
            });
        }

        private async void EndButton_Click(object sender, RoutedEventArgs e)
        {
            await CallService.Instance.EndCallAsync();
            Close();
        }

        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {
            _cameraEnabled = !_cameraEnabled;
            if (_cameraEnabled)
            {
                await StartCameraAsync();
            }
            else
            {
                _cameraStartVersion++;
                _isOpeningCamera = false;
                _camera.Stop();
                _usingRealCamera = false;
                LocalImage.Source = null;
                StatusText.Text = "Camera off.";
            }

            UpdateMediaButtons();
            await CallService.Instance.SetMediaStateAsync(_cameraEnabled, _microphoneEnabled);
        }

        private async void MicButton_Click(object sender, RoutedEventArgs e)
        {
            _microphoneEnabled = !_microphoneEnabled;
            if (_microphoneEnabled)
            {
                _audio.StartMicrophone(_selectedMicrophoneIndex);
            }
            else
            {
                _audio.StopMicrophone();
                StatusText.Text = "Microphone off.";
            }

            UpdateMediaButtons();
            await CallService.Instance.SetMediaStateAsync(_cameraEnabled, _microphoneEnabled);
        }

        protected override async void OnClosed(EventArgs e)
        {
            _frameTimer.Stop();
            _cameraStartVersion++;
            CallService.Instance.CallStatusChanged -= OnCallStatusChanged;
            CallService.Instance.RemoteVideoFrameReceived -= OnRemoteVideoFrameReceived;
            CallService.Instance.RemoteAudioPacketReceived -= OnRemoteAudioPacketReceived;
            CallService.Instance.CallEnded -= OnCallEnded;
            _camera.FrameReady -= OnLocalCameraFrameReady;
            _camera.StatusChanged -= OnLocalMediaStatusChanged;
            _audio.AudioCaptured -= OnLocalAudioCaptured;
            _audio.StatusChanged -= OnLocalMediaStatusChanged;
            _camera.Dispose();
            _audio.Dispose();

            if (!_isClosingFromRemote && CallService.Instance.HasActiveCall)
            {
                await CallService.Instance.EndCallAsync();
            }

            base.OnClosed(e);
        }

        private void LoadMediaDevices()
        {
            _isLoadingDevices = true;
            try
            {
                var cameras = CameraCaptureService.GetCameraCandidates();
                CameraDeviceBox.ItemsSource = cameras;
                if (cameras.Count > 0)
                {
                    int defaultCameraListIndex = GetDefaultCameraListIndex(cameras);
                    CameraDeviceBox.SelectedIndex = defaultCameraListIndex;
                    _selectedCameraIndex = cameras[defaultCameraListIndex].DeviceIndex;
                    CameraDeviceBox.IsEnabled = true;
                    LocalVideoLabel.Text = "Local camera source";
                }
                else
                {
                    CameraDeviceBox.ItemsSource = new[]
                    {
                        new MediaDeviceInfo { DeviceIndex = 0, DisplayName = "No camera - test video" }
                    };
                    CameraDeviceBox.SelectedIndex = 0;
                    CameraDeviceBox.IsEnabled = false;
                    LocalVideoLabel.Text = "Local test video";
                }

                var microphones = AudioCallService.GetAvailableMicrophones();
                MicrophoneDeviceBox.ItemsSource = microphones;
                if (microphones.Count > 0)
                {
                    MicrophoneDeviceBox.SelectedIndex = 0;
                    _selectedMicrophoneIndex = microphones[0].DeviceIndex;
                    MicrophoneDeviceBox.IsEnabled = true;
                    MicButton.IsEnabled = true;
                }
                else
                {
                    MicrophoneDeviceBox.ItemsSource = new[]
                    {
                        new MediaDeviceInfo { DeviceIndex = 0, DisplayName = "No microphone" }
                    };
                    MicrophoneDeviceBox.SelectedIndex = 0;
                    MicrophoneDeviceBox.IsEnabled = false;
                    MicButton.IsEnabled = false;
                    _microphoneEnabled = false;
                }
            }
            finally
            {
                _isLoadingDevices = false;
            }
        }

        private async System.Threading.Tasks.Task StartCameraAsync()
        {
            if (!_cameraEnabled) return;

            int startVersion = ++_cameraStartVersion;
            _camera.Stop();
            _usingRealCamera = false;
            _isOpeningCamera = true;
            string cameraName = GetSelectedCameraName();
            StatusText.Text = $"Opening {cameraName}...";
            UpdateMediaButtons();

            try
            {
                bool started = await _camera.StartAsync(_selectedCameraIndex);
                if (startVersion != _cameraStartVersion) return;

                _usingRealCamera = started;
                if (!_usingRealCamera)
                {
                    StatusText.Text = "Camera unavailable; fallback test video is active.";
                    LocalVideoLabel.Text = "Local test video";
                }
                else
                {
                    LocalVideoLabel.Text = cameraName;
                }
            }
            finally
            {
                if (startVersion == _cameraStartVersion)
                {
                    _isOpeningCamera = false;
                }
            }

            UpdateMediaButtons();
        }

        private async void CameraDeviceBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingDevices) return;
            if (CameraDeviceBox.SelectedItem is not MediaDeviceInfo device) return;

            _selectedCameraIndex = device.DeviceIndex;
            if (_cameraEnabled)
            {
                await StartCameraAsync();
                await CallService.Instance.SetMediaStateAsync(_cameraEnabled, _microphoneEnabled);
            }
        }

        private async void MicrophoneDeviceBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingDevices) return;
            if (MicrophoneDeviceBox.SelectedItem is not MediaDeviceInfo device) return;

            _selectedMicrophoneIndex = device.DeviceIndex;
            if (_microphoneEnabled)
            {
                _audio.StopMicrophone();
                _audio.StartMicrophone(_selectedMicrophoneIndex);
                await CallService.Instance.SetMediaStateAsync(_cameraEnabled, _microphoneEnabled);
            }
        }

        private void UpdateMediaButtons()
        {
            CameraButton.Content = _cameraEnabled ? (_isOpeningCamera ? "Opening" : (_usingRealCamera ? "Camera On" : "Test Video")) : "Camera Off";
            CameraButton.Background = _cameraEnabled
                ? new SolidColorBrush(Color.FromRgb(36, 72, 63))
                : new SolidColorBrush(Color.FromRgb(61, 61, 61));

            MicButton.Content = _microphoneEnabled ? "Mic On" : "Mic Off";
            MicButton.Background = _microphoneEnabled
                ? new SolidColorBrush(Color.FromRgb(36, 72, 63))
                : new SolidColorBrush(Color.FromRgb(61, 61, 61));
        }

        private static int GetDefaultCameraListIndex(System.Collections.Generic.IReadOnlyList<MediaDeviceInfo> cameras)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                if (cameras[i].DeviceIndex == 1)
                {
                    return i;
                }
            }

            return 0;
        }

        private string GetSelectedCameraName()
        {
            return CameraDeviceBox.SelectedItem is MediaDeviceInfo device && !string.IsNullOrWhiteSpace(device.DisplayName)
                ? device.DisplayName
                : $"Camera {_selectedCameraIndex}";
        }

        private byte[] CreateGeneratedFrameJpeg()
        {
            const int width = 320;
            const int height = 240;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var hue = (byte)((_frameCounter * 7) % 180 + 40);
                var bg = new LinearGradientBrush(
                    Color.FromRgb(18, 31, 28),
                    Color.FromRgb(hue, 92, 82),
                    35);

                dc.DrawRectangle(bg, null, new Rect(0, 0, width, height));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), null,
                    new Point(64 + (_frameCounter % 160), 74), 34, 34);

                var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var name = ChatService.Instance.CurrentUsername ?? "LanChat";
                var text = new FormattedText(
                    $"{name}\n{DateTime.Now:HH:mm:ss}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    24,
                    Brushes.White,
                    dpi);

                dc.DrawText(text, new Point(24, 145));
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = 50 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            _frameCounter++;
            return stream.ToArray();
        }

        private static BitmapImage DecodeJpeg(byte[] jpegBytes)
        {
            var image = new BitmapImage();
            using var stream = new MemoryStream(jpegBytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
