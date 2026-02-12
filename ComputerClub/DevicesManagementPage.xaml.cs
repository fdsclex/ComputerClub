using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class DevicesManagementPage : Page
    {
        public DevicesManagementPage()
        {
            InitializeComponent();
            LoadDevices();
            dgDevices.SelectionChanged += dgDevices_SelectionChanged;
        }

        private void LoadDevices()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var devicesList = ctx.Devices
                        .Include("Tariffs")
                        .Select(d => new
                        {
                            d.DeviceID,
                            d.Name,
                            d.Type,
                            d.Status,
                            d.Specs,
                            d.Location,
                            TariffName = d.Tariffs != null ? d.Tariffs.Name : "Не назначен",
                            PricePerHour = d.Tariffs != null ? d.Tariffs.PricePerHour : 0m
                        })
                        .ToList();

                    dgDevices.ItemsSource = devicesList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки устройств:\n{ex.Message}");
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDevices();
        }

        private void dgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = dgDevices.SelectedItem;
            if (item != null)
            {
                var deviceId = (int)item.GetType().GetProperty("DeviceID").GetValue(item);
                var name = (string)item.GetType().GetProperty("Name").GetValue(item);
                var status = (string)item.GetType().GetProperty("Status").GetValue(item);

                tbSelectedInfo.Text = $"Выбрано устройство #{deviceId} ({name}) — {status}";
            }
            else
            {
                tbSelectedInfo.Text = "Выберите устройство для редактирования или удаления...";
            }
        }

        private void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            var window = new DeviceEditWindow(isNew: true);
            if (window.ShowDialog() == true)
            {
                LoadDevices();
            }
        }

        private void EditDevice_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            var deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);

            var window = new DeviceEditWindow(isNew: false, deviceId: deviceId);
            if (window.ShowDialog() == true)
            {
                LoadDevices();
            }
        }

        private void DeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            var deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);
            var name = (string)selected.GetType().GetProperty("Name").GetValue(selected);

            if (MessageBox.Show($"Удалить устройство {name} (ID {deviceId})?\nЭто действие нельзя отменить.",
                                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var dev = ctx.Devices.Find(deviceId);
                    if (dev != null)
                    {
                        ctx.Devices.Remove(dev);
                        ctx.SaveChanges();
                        MessageBox.Show("Устройство удалено.");
                        LoadDevices();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}");
            }
        }
    }
}