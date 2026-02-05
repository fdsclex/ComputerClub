using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class ClientModeSelectionPage : Page
    {
        public ClientModeSelectionPage()
        {
            InitializeComponent();
        }

        private void RemoteButton_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.IsOnSite = false;
            AppConfig.DeviceNumber = null;
            NavigationService.Navigate(new ClientLoginPage());
        }

        private void OnSiteButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DeviceInputPage());
        }
    }
}