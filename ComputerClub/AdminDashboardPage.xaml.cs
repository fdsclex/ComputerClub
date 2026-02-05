using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class AdminDashboardPage : Page
    {
        public AdminDashboardPage()
        {
            InitializeComponent();
        }

        private void ManageDevices_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Переход к управлению устройствами (ПК, консоли, тарифы)");
            // NavigationService.Navigate(new DevicesManagementPage());
        }

        private void ViewSessions_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Просмотр активных сессий и броней");
        }

        private void ManageMenu_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Управление меню и заказами");
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из аккаунта администратора?", "Выход",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AppConfig.CurrentClientId = null; // если был клиент
                NavigationService.Navigate(new RoleSelectionPage());
            }
        }
    }
}