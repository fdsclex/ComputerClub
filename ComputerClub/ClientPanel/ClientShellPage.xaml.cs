using System;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.ClientPanel
{
    public partial class ClientShellPage : Page
    {
        public ClientShellPage()
        {
            InitializeComponent();
        }

        private void OpenCabinet_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ClientCabinetPage());
        }

        private void CheckBalance_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Клиент не авторизован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                    if (client != null)
                    {
                        MessageBox.Show($"Ваш текущий баланс: {client.Balance:N0} ₽\n\nБонусный баланс: 0 ₽",
                                        "Баланс", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Клиент не найден в базе.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки баланса:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}