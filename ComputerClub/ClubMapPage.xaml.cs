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
            dgDevices.SelectionChanged += dgDevices_SelectionChanged;
        }

        private void LoadDevices()
        {
            try
            {
                using (var ctx = new Entities())
                {
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
                            duration = FormatDuration(ts);               // ← здесь используем красивый формат
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

        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalMinutes < 1) return "<1 мин";
            if (ts.TotalHours < 1) return $"{(int)ts.TotalMinutes} мин";
            if (ts.TotalDays < 1) return $"{(int)ts.TotalHours} ч {ts.Minutes:D2} мин";
            return $"{(int)ts.TotalDays} д {ts.Hours} ч {ts.Minutes:D2} мин";
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
                tbSelectedInfo.Text = "Выберите устройство для дополнительных действий...";
            }
        }

        private void FinishSession_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            var canFinishProp = selected.GetType().GetProperty("CanFinish");
            var canFinish = (bool)canFinishProp.GetValue(selected);
            if (!canFinish) return;

            var deviceIdProp = selected.GetType().GetProperty("DeviceID");
            var deviceId = (int)deviceIdProp.GetValue(selected);

            var nameProp = selected.GetType().GetProperty("Name");
            var name = (string)nameProp.GetValue(selected);

            if (MessageBox.Show($"Завершить сессию на {name}?\nСредства будут списаны автоматически.",
                                "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
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

        private void FreezeSession_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция заморозки сессии в разработке.");
        }

        private void SetMaintenance_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            var statusProp = selected.GetType().GetProperty("Status");
            var currentStatus = (string)statusProp.GetValue(selected);

            string newStatus = currentStatus == "Maintenance" ? "Available" : "Maintenance";

            if (MessageBox.Show($"Перевести устройство в статус '{newStatus}'?", "Подтверждение",
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            var deviceIdProp = selected.GetType().GetProperty("DeviceID");
            var deviceId = (int)deviceIdProp.GetValue(selected);

            try
            {
                using (var ctx = new Entities())
                {
                    var dev = ctx.Devices.Find(deviceId);
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