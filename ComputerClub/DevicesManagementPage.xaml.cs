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

        private dynamic Selected => dgDevices.SelectedItem;

        private void dgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbSelectedInfo.Text = Selected != null
                ? $"Выбрано устройство #{Selected.DeviceID} ({Selected.Name}) — {Selected.Status}"
                : "Выберите устройство для редактирования или удаления...";
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
            if (Selected == null) return;

            var window = new DeviceEditWindow(isNew: false, deviceId: Selected.DeviceID);
            if (window.ShowDialog() == true)
            {
                LoadDevices();
            }
        }

        private void DeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;

            if (MessageBox.Show($"Удалить устройство {Selected.Name} (ID {Selected.DeviceID})?\nЭто действие нельзя отменить.",
                                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var dev = ctx.Devices.Find(Selected.DeviceID);
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
}