using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ComputerClub.ClientPanel
{
    public partial class ClientCabinetPage : Page
    {
        public ClientCabinetPage()
        {
            InitializeComponent();
            Loaded += ClientCabinetPage_Loaded;
        }

        private void ClientCabinetPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientInfo();
            SwitchTab("Shop");
            rbShop.IsChecked = true;
        }

        private void LoadClientInfo()
        {
            if (!AppConfig.CurrentClientId.HasValue) return;

            using (var ctx = new Entities())
            {
                var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                if (client != null)
                {
                    tbWelcome.Text = client.FullName;
                    tbBalance.Text = $"{client.Balance:N0} ₽";

                    tbStatus.Text = AppConfig.IsOnSite
                        ? $"На месте • №{AppConfig.DeviceNumber} • {AppConfig.DeviceName}"
                        : "Онлайн";
                }
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                SwitchTab(rb.Tag.ToString());
            }
        }

        private void SwitchTab(string tabName)
        {
            switch (tabName)
            {
                case "Shop":
                    CabinetFrame.Navigate(new CabinetShopPage());
                    break;
                case "Notifications":
                    CabinetFrame.Navigate(new CabinetNotificationsPage());
                    break;
                case "Loyalty":
                    CabinetFrame.Navigate(new CabinetLoyaltyPage());
                    break;
                case "Profile":
                    CabinetFrame.Navigate(new CabinetProfilePage());
                    break;
            }
        }

        private void SwitchChatTariff_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (btn.Tag.ToString() == "Chat")
                {
                    ChatPanel.Visibility = Visibility.Visible;
                    TariffPanel.Visibility = Visibility.Collapsed;
                    btnChatTab.Background = new SolidColorBrush(Colors.White);
                    btnTariffTab.Background = new SolidColorBrush(Colors.Transparent);
                }
                else // Tariff
                {
                    ChatPanel.Visibility = Visibility.Collapsed;
                    TariffPanel.Visibility = Visibility.Visible;
                    btnTariffTab.Background = new SolidColorBrush(Colors.White);
                    btnChatTab.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        private void BackToShell_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ClientShellPage());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AppConfig.Reset();
                NavigationService?.Navigate(new Navigation.RoleSelectionPage());
            }
        }
    }
}