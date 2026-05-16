using System;
using System.Windows;
using LanChat.Client.Services;

namespace LanChat.Client.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ChatService.Instance.OnHandshakeCompleted += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "✅ Kết nối an toàn đã được thiết lập";
                    StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("SuccessBrush");
                    LoginButton.IsEnabled = true;
                    RegisterButton.IsEnabled = true;
                });
            };

            ChatService.Instance.OnLoginResponse += (success, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        StatusText.Text = $"❌ {message}";
                        StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
                        LoginButton.IsEnabled = true;
                        RegisterButton.IsEnabled = true;
                    }
                });
            };

            ChatService.Instance.OnRegisterResponse += (success, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        StatusText.Text = "✅ Đăng ký thành công! Hãy đăng nhập.";
                        StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("SuccessBrush");
                    }
                    else
                    {
                        StatusText.Text = $"❌ {message}";
                        StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
                    }
                    LoginButton.IsEnabled = true;
                    RegisterButton.IsEnabled = true;
                });
            };

            ChatService.Instance.OnDisconnected += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "❌ Đã mất kết nối đến server.";
                    StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
                    LoginButton.IsEnabled = false;
                    RegisterButton.IsEnabled = false;
                });
            };

            try
            {
                string serverIp = ServerIpBox.Text.Trim();
                if (string.IsNullOrEmpty(serverIp)) serverIp = "127.0.0.1";
                await ChatService.Instance.ConnectAsync(serverIp, 8080);
                StatusText.Text = "🔒 Đang thiết lập kênh bảo mật...";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Không thể kết nối: {ex.Message}";
                StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "⚠️ Vui lòng nhập đầy đủ thông tin";
                StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
                return;
            }

            LoginButton.IsEnabled = false;
            RegisterButton.IsEnabled = false;
            StatusText.Text = "Đang đăng nhập...";
            StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush");

            await ChatService.Instance.LoginAsync(username, password);
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "⚠️ Vui lòng nhập đầy đủ thông tin";
                StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("DangerBrush");
                return;
            }

            LoginButton.IsEnabled = false;
            RegisterButton.IsEnabled = false;
            StatusText.Text = "Đang đăng ký...";
            StatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush");

            await ChatService.Instance.RegisterAsync(username, password);
        }
    }
}
