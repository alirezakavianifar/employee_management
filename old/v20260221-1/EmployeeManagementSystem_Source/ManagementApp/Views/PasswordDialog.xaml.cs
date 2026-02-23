using System.Windows;
using System.Windows.Input;

namespace ManagementApp.Views
{
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; } = string.Empty;

        public PasswordDialog()
        {
            InitializeComponent();
            AdminPasswordBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Password = AdminPasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AdminPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
        }
    }
}
