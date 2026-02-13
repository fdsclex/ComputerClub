using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.ClientPanel
{
    public partial class ClientDashboardPage : Page
    {
        public ClientDashboardPage()
        {
            InitializeComponent();

            if (AppConfig.CurrentClientId.HasValue)
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                    if (client != null)
                    {
                        tbWelcomeMessage.Text = $"Здравствуйте, {client.FullName}!\nБаланс: {client.Balance:N2} руб.";
                    }
                }
            }

            // Показываем кнопки в зависимости от режима
            if (AppConfig.IsOnSite)
            {
                tbWelcomeMessage.Text += $"\n\nВы за устройством №{AppConfig.DeviceNumber} {AppConfig.DeviceName}";
                if (AppConfig.DeviceType == "PC")
                {
                    btnStartSession.Visibility = Visibility.Visible;
                }
                else if (AppConfig.DeviceType == "Console")
                {
                    btnStartConsole.Visibility = Visibility.Visible;
                    tbWelcomeMessage.Text += "\nЭто консоль — выберите игру для приставки.";
                }
            }
            else
            {
                btnBookPc.Visibility = Visibility.Visible;
            }
        }

        private void StartSession_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Сессия на ПК начата. Логика: INSERT INTO Sessions, обновление Status устройства.");
            // TODO: реальная логика начала сессии
        }

        private void StartConsole_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Режим консоли: показ списка игр для консоли из БД (DeviceGames).");
            // TODO: перейти на страницу выбора игр для консоли
        }

        private void BookDevice_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Форма бронирования устройства: выбор Device, времени, INSERT INTO Reservations.");
            // TODO: перейти на страницу брони
        }
    }
}