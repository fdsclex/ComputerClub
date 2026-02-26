using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.Navigation
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
            AppConfig.TariffID = null;

            // Говорим: после логина → сразу в бронь
            AppConfig.NavigateToBookingAfterLogin = true;

            NavigationService.Navigate(new ClientLoginPage());
        }

        private void OnSiteButton_Click(object sender, RoutedEventArgs e)
        {
            // Обычный путь: выбор устройства → логин → кабинет
            NavigationService.Navigate(new DeviceInputPage());
        }
    }
}