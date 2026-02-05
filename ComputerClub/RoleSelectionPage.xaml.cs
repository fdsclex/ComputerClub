using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class RoleSelectionPage : Page
    {
        public RoleSelectionPage()
        {
            InitializeComponent();
        }

        private void AdminButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdminLoginPage());
        }

        private void ClientButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ClientModeSelectionPage());
        }
    }
}