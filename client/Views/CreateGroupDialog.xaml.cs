using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LanChat.Client.Views
{
    public partial class CreateGroupDialog : Window
    {
        public string GroupName { get; private set; } = "";
        public List<string> SelectedMembers { get; private set; } = new List<string>();

        public CreateGroupDialog(List<string> availableUsers)
        {
            InitializeComponent();
            UsersListBox.ItemsSource = availableUsers;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            GroupName = GroupNameBox.Text.Trim();
            if (string.IsNullOrEmpty(GroupName))
            {
                MessageBox.Show("Vui lòng nhập tên nhóm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedMembers = UsersListBox.SelectedItems.Cast<string>().ToList();
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
