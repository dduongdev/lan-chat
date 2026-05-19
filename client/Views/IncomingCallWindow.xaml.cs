using System;
using System.Windows;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Views
{
    public partial class IncomingCallWindow : Window
    {
        public event Action<CallInviteIncomingPayload>? Accepted;
        public event Action<CallInviteIncomingPayload>? Rejected;

        private readonly CallInviteIncomingPayload _invite;

        public IncomingCallWindow(CallInviteIncomingPayload invite)
        {
            InitializeComponent();
            _invite = invite;
            ContentText.Text = $"{invite.Caller} đang gọi video. Bạn muốn nhận cuộc gọi?";
            Loaded += IncomingCallWindow_Loaded;
        }

        private void IncomingCallWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            Accepted?.Invoke(_invite);
            Close();
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            Rejected?.Invoke(_invite);
            Close();
        }
    }
}
