using System.Windows;
using System.Windows.Input;

namespace ManagementApp.Views
{
    public partial class PasswordDialog : Window
    {
        private readonly string _expectedPassword;

        /// <summary>
        /// Creates the login dialog. Pass the expected password so the dialog can
        /// validate it internally — only closing when it is correct or cancelled.
        /// </summary>
        public PasswordDialog(string expectedPassword)
        {
            InitializeComponent();
            _expectedPassword = expectedPassword;
            AdminPasswordBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (AdminPasswordBox.Password == _expectedPassword)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                // Show inline error, stay open
                ErrorText.Visibility = Visibility.Visible;
                AdminPasswordBox.Clear();
                AdminPasswordBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AdminPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                OK_Click(sender, e);
        }

        /// <summary>Allow the user to drag the borderless window.</summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
