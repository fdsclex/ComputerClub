using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
{
    public partial class DeviceEditWindow : Window
    {
        private readonly bool _isNew;
        private readonly int? _deviceId;

        public DeviceEditWindow(bool isNew, int? deviceId = null)
        {
            InitializeComponent();
            _isNew = isNew;
            _deviceId = deviceId;

            LoadTariffs();
            if (!_isNew && _deviceId.HasValue)
                LoadDevice();
        }

        private void LoadTariffs()
        {
            using (var ctx = new Entities())
            {
                cbTariff.ItemsSource = ctx.Tariffs.ToList();
            }
        }

        private void LoadDevice()
        {
            using (var ctx = new Entities())
            {
                var dev = ctx.Devices.Find(_deviceId);
                if (dev != null)
                {
                    tbName.Text = dev.Name;
                    cbType.SelectedItem = cbType.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == dev.Type);
                    cbTariff.SelectedValue = dev.TariffID;
                    tbSpecs.Text = dev.Specs;
                    tbLocation.Text = dev.Location;
                    cbStatus.SelectedItem = cbStatus.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == dev.Status);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbName.Text) || cbTariff.SelectedValue == null)
            {
                MessageBox.Show("Заполните имя и выберите тариф.");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    Devices dev;
                    if (_isNew)
                    {
                        dev = new Devices();
                        ctx.Devices.Add(dev);
                    }
                    else
                    {
                        dev = ctx.Devices.Find(_deviceId);
                    }

                    dev.Name = tbName.Text.Trim();
                    dev.Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "PC";
                    dev.TariffID = (int)cbTariff.SelectedValue;
                    dev.Specs = tbSpecs.Text.Trim();
                    dev.Location = tbLocation.Text.Trim();
                    dev.Status = (cbStatus.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Available";

                    ctx.SaveChanges();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n{ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}