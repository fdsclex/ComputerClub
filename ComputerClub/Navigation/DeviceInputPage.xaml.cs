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

                // Разрешены только эти три статуса
                if (device.Status != "Available" &&
                    device.Status != "Reserved" &&
                    device.Status != "InUse")
                {
                    string statusText;

                    if (device.Status == "Maintenance")
                    {
                        statusText = "находится в техническом обслуживании";
                    }
                    else if (device.Status == "Offline")
                    {
                        statusText = "отключено или не работает";
                    }
                    else
                    {
                        statusText = $"имеет статус '{device.Status ?? "не указан"}'";
                    }

                    MessageBox.Show(
                        $"Устройство №{deviceId} ({device.Name ?? device.Type ?? "без названия"}) " +
                        $"в настоящее время недоступно.\n\n" +
                        $"Причина: устройство {statusText}.\n\n" +
                        "Вход возможен только на свободные, зарезервированные или занятые устройства.",
                        "Устройство недоступно",
                        MessageBoxButton.OK, MessageBoxImage.Warning
                    );

                    tbDeviceNumber.Focus();
                    tbDeviceNumber.SelectAll();
                    return;
                }

                // Устройство выбрано и разрешено → сохраняем
                AppConfig.IsOnSite = true;
                AppConfig.DeviceNumber = deviceId;
                AppConfig.DeviceName = device.Name ?? device.Type ?? "Устройство";
                AppConfig.DeviceType = device.Type;
                AppConfig.TariffID = device.TariffID;

                if (AppConfig.IsDeviceSwitchInProgress)
                {
                    AppConfig.IsDeviceSwitchInProgress = false;
                    NavigationService?.Navigate(new ClientPanel.ClientCabinetPage());
                }
                else
                {
                    NavigationService?.Navigate(new ClientLoginPage());
                }
            }
        }
    }
}