using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class ClubMapPage : Page
    {
        public ClubMapPage()
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
                    // Загружаем устройства + тарифы + активные сессии
                    var allDevices = ctx.Devices
                        .Include("Tariffs")
                        .ToList();

                    var activeSessions = ctx.Sessions
                        .Where(s => s.EndTime == null && s.Status == "Active")
                        .Select(s => new
                        {
                            s.DeviceID,
                            ClientFullName = s.Clients.FullName,
                            StartTime = s.StartTime
                        })
                        .ToDictionary(s => s.DeviceID, s => new { s.ClientFullName, s.StartTime });

                    var reservedDeviceIds = ctx.Reservations
                        .Where(r => r.Status == "Pending" || r.Status == "Confirmed")
                        .Select(r => r.DeviceID)
                        .ToHashSet();

                    var viewModels = new List<object>();

                    foreach (var dev in allDevices)
                    {
                        string currentClient = "—";
                        string duration = "—";
                        bool isInUse = activeSessions.TryGetValue(dev.DeviceID, out var session);
                        bool isReserved = reservedDeviceIds.Contains(dev.DeviceID);

                        string effectiveStatus = dev.Status;
                        if (isInUse) effectiveStatus = "InUse";
                        else if (isReserved && dev.Status == "Available") effectiveStatus = "Reserved";

                        if (isInUse)
                        {
                            var ts = DateTime.Now - session.StartTime;
                            duration = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}";
                            currentClient = session.ClientFullName;
                        }

                        viewModels.Add(new
                        {
                            DeviceID = dev.DeviceID,
                            Name = dev.Name,
                            Type = dev.Type,
                            Status = effectiveStatus,
                            TariffName = dev.Tariffs?.Name ?? "—",
                            CurrentClientFullName = currentClient,
                            SessionDuration = duration,
                            CanFinish = isInUse,
                            CanFreeze = isInUse
                        });
                    }

                    dgDevices.ItemsSource = viewModels;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}");
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDevices();
        }

        private dynamic SelectedDevice => dgDevices.SelectedItem;

        private void dgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedDevice != null)
            {
                tbSelectedInfo.Text = $"Выбрано устройство #{SelectedDevice.DeviceID} ({SelectedDevice.Name}) — {SelectedDevice.Status}";
            }
            else
            {
                tbSelectedInfo.Text = "Выберите устройство для дополнительных действий...";
            }
        }

        private void FinishSession_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (!sel.CanFinish) return;

            int deviceId = (int)sel.DeviceID;   // ← вытаскиваем значение заранее

            if (MessageBox.Show($"Завершить сессию на {sel.Name}?\nСредства будут списаны автоматически.",
                                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        // Теперь в лямбде только статический int — EF нормально переведёт в SQL
                        var session = ctx.Sessions
                            .FirstOrDefault(s => s.DeviceID == deviceId && s.EndTime == null);

                        if (session != null)
                        {
                            session.EndTime = DateTime.Now;
                            ctx.SaveChanges();
                            MessageBox.Show("Сессия завершена. Деньги списаны.");
                            LoadDevices();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка завершения сессии:\n{ex.Message}");
                }
            }
        }

        private void FreezeSession_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция заморозки сессии в разработке.");
        }

        private void SetMaintenance_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null) return;

            string newStatus = SelectedDevice.Status == "Maintenance" ? "Available" : "Maintenance";

            if (MessageBox.Show($"Перевести устройство в статус '{newStatus}'?", "Подтверждение",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var dev = ctx.Devices.Find(SelectedDevice.DeviceID);
                        if (dev != null)
                        {
                            dev.Status = newStatus;
                            ctx.SaveChanges();
                            LoadDevices();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка изменения статуса:\n{ex.Message}");
                }
            }
        }
    }
}