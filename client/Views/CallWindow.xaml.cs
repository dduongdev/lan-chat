using System;
using System.Globalization;
using System.IO;
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
        private bool _isClosingFromRemote;
        private int _frameCounter;

        public CallWindow(string title)
        {
            InitializeComponent();
            TitleText.Text = title;

            CallService.Instance.CallStatusChanged += OnCallStatusChanged;
            CallService.Instance.RemoteVideoFrameReceived += OnRemoteVideoFrameReceived;
            CallService.Instance.CallEnded += OnCallEnded;

            _frameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(85)
            };
            _frameTimer.Tick += async (_, _) =>
            {
                var jpeg = CreateGeneratedFrameJpeg();
                LocalImage.Source = DecodeJpeg(jpeg);
                await CallService.Instance.SendVideoFrameAsync(jpeg);
            };
            _frameTimer.Start();
        }

        private void OnCallStatusChanged(string status)
        {
            Dispatcher.Invoke(() => StatusText.Text = status);
        }

        private void OnRemoteVideoFrameReceived(ushort participantId, byte[] jpegBytes)
        {
            Dispatcher.Invoke(() =>
            {
                RemoteImage.Source = DecodeJpeg(jpegBytes);
                RemotePlaceholder.Visibility = Visibility.Collapsed;
                StatusText.Text = $"Receiving video from participant {participantId}";
            });
        }

        private void OnCallEnded(CallEndedPayload payload)
        {
            Dispatcher.Invoke(() =>
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

        protected override async void OnClosed(EventArgs e)
        {
            _frameTimer.Stop();
            CallService.Instance.CallStatusChanged -= OnCallStatusChanged;
            CallService.Instance.RemoteVideoFrameReceived -= OnRemoteVideoFrameReceived;
            CallService.Instance.CallEnded -= OnCallEnded;

            if (!_isClosingFromRemote && CallService.Instance.HasActiveCall)
            {
                await CallService.Instance.EndCallAsync();
            }

            base.OnClosed(e);
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
