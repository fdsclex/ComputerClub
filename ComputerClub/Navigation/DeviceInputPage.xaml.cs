using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.Navigation
{
    public partial class DeviceInputPage : Page
    {
        public DeviceInputPage()
        {
            InitializeComponent();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(tbDeviceNumber.Text.Trim(), out int deviceId) || deviceId <= 0)
            {
                MessageBox.Show("Введите корректный номер устройства (положительное число)",
                    "Неверный ввод", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var ctx = new Entities())
            {
                var device = ctx.Devices.FirstOrDefault(d => d.DeviceID == deviceId);
                if (device == null)
                {
                    MessageBox.Show($"Устройство №{deviceId} не найдено в системе.",
                        "Устройство не найдено", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (device.Status != "Available")
                {
                    string statusDisplay = string.IsNullOrWhiteSpace(device.Status) ? "не указан" : device.Status;
                    MessageBox.Show(
                        $"Устройство №{deviceId} ({device.Name ?? device.Type ?? "без названия"}) " +
                        $"в настоящее время недоступно.\n\n" +
                        $"Текущий статус: **{statusDisplay}**\n\n" +
                        "Пожалуйста, выберите другое свободное устройство.",
                        "Устройство занято / недоступно",
                        MessageBoxButton.OK, MessageBoxImage.Warning
                    );
                    tbDeviceNumber.Focus();
                    tbDeviceNumber.SelectAll();
                    return;
                }

                // Устройство выбрано и доступно → сохраняем
                AppConfig.IsOnSite = true;
                AppConfig.DeviceNumber = deviceId;
                AppConfig.DeviceName = device.Name ?? device.Type ?? "Устройство";
                AppConfig.DeviceType = device.Type;
                AppConfig.TariffID = device.TariffID;

                // Решаем, куда переходить
                if (AppConfig.IsDeviceSwitchInProgress)
                {
                    // Это была пересадка → возвращаемся в кабинет
                    AppConfig.IsDeviceSwitchInProgress = false; // сбрасываем флаг
                    NavigationService?.Navigate(new ClientPanel.ClientCabinetPage());
                }
                else
                {
                    // Обычный вход → идём на страницу логина
                    NavigationService?.Navigate(new ClientLoginPage());
                }
            }
        }
    }
}