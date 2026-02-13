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
                MessageBox.Show("Введите корректный номер устройства");
                return;
            }

            using (var ctx = new Entities())
            {
                var device = ctx.Devices.FirstOrDefault(d => d.DeviceID == deviceId);
                if (device != null)
                {
                    AppConfig.IsOnSite = true;
                    AppConfig.DeviceNumber = deviceId;
                    AppConfig.DeviceName = device.Name ?? "Устройство";  // имя из БД
                    AppConfig.DeviceType = device.Type;  // сохраняем тип 'PC' или 'Console'

                    NavigationService.Navigate(new ClientLoginPage());
                }
                else
                {
                    MessageBox.Show($"Устройство №{deviceId} не найдено");
                }
            }
        }
    }
}