using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LanChat.Client.Views
{
    public partial class AddMemberDialog : Window
    {
        public List<string> SelectedMembers { get; private set; } = new List<string>();

        public AddMemberDialog(List<string> availableUsers)
        {
            InitializeComponent();
            UsersListBox.ItemsSource = availableUsers;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMembers = UsersListBox.SelectedItems.Cast<string>().ToList();
            if (!SelectedMembers.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một người dùng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
