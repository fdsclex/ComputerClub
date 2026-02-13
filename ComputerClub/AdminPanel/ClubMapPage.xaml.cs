using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
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
                            s.StartTime,
                            s.IsFrozen,
                            s.FreezeStartTime,
                            s.MaxFreezeMinutes
                        })
                        .ToDictionary(
                            s => s.DeviceID,
                            s => new { s.ClientFullName, s.StartTime, s.IsFrozen, s.FreezeStartTime, s.MaxFreezeMinutes });

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
                        bool isFrozen = false;
                        string effectiveStatus = dev.Status;

                        if (isInUse)
                        {
                            effectiveStatus = "InUse";
                            currentClient = session.ClientFullName ?? "—";

                            if (session.IsFrozen)
                            {
                                isFrozen = true;
                                effectiveStatus = "Frozen";

                                if (session.FreezeStartTime.HasValue)
                                {
                                    var frozenFor = DateTime.Now - session.FreezeStartTime.Value;
                                    duration = $"Заморожено {frozenFor:mm\\:ss}";

                                    // Проверяем, не истекло ли время заморозки
                                    int maxMin = session.MaxFreezeMinutes ?? 60;
                                    if (frozenFor.TotalMinutes > maxMin)
                                    {
                                        duration += " (истекло время)";
                                        // Можно здесь автоматически разморозить или завершить — по желанию
                                    }
                                }
                            }
                            else
                            {
                                var ts = DateTime.Now - session.StartTime;
                                duration = FormatDuration(ts);
                            }
                        }
                        else if (isReserved && dev.Status == "Available")
                        {
                            effectiveStatus = "Reserved";
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
                            CanFinish = isInUse && !isFrozen,
                            CanFreeze = isInUse && !isFrozen,
                            IsFrozen = isFrozen
                        });
                    }

                    dgDevices.ItemsSource = viewModels;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}", "Ошибка");
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

            bool canFinish = (bool)selected.GetType().GetProperty("CanFinish").GetValue(selected);
            if (!canFinish) return;

            int deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);
            string name = (string)selected.GetType().GetProperty("Name").GetValue(selected);

            if (MessageBox.Show($"Завершить сессию на {name}?\nСредства будут списаны автоматически.",
                                "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var session = ctx.Sessions
                        .FirstOrDefault(s => s.DeviceID == deviceId && s.EndTime == null && s.Status == "Active");

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
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;
                
            bool canFreeze = (bool)selected.GetType().GetProperty("CanFreeze").GetValue(selected);
            if (!canFreeze)
            {
                MessageBox.Show("Устройство не в активной сессии или уже заморожено.");
                return;
            }

            int deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);
            string name = (string)selected.GetType().GetProperty("Name").GetValue(selected);

            var result = MessageBox.Show(
                $"Заморозить сессию на устройстве {name}?\n" +
                "Время сессии будет приостановлено (максимум 60 минут).",
                "Заморозка сессии",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var ctx = new Entities())
                {
                    var session = ctx.Sessions
                        .FirstOrDefault(s => s.DeviceID == deviceId && s.EndTime == null && s.Status == "Active");

                    if (session == null)
                    {
                        MessageBox.Show("Активная сессия не найдена.");
                        return;
                    }

                    if (session.IsFrozen == true)
                    {
                        MessageBox.Show("Сессия уже заморожена.");
                        return;
                    }

                    session.IsFrozen = true;
                    session.FreezeStartTime = DateTime.Now;
                    // session.MaxFreezeMinutes = 60;  // если хочешь хранить лимит в БД

                    ctx.SaveChanges();

                    MessageBox.Show(
                        "Сессия заморожена.\n" +
                        "Клиент может вернуться в течение 60 минут и продолжить без потери времени.",
                        "Успех");

                    LoadDevices();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка заморозки:\n{ex.Message}", "Ошибка");
            }
        }

        private void UnfreezeSession_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            bool isFrozen = (bool)selected.GetType().GetProperty("IsFrozen").GetValue(selected);
            if (!isFrozen)
            {
                MessageBox.Show("Сессия не заморожена.");
                return;
            }

            int deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);
            string name = (string)selected.GetType().GetProperty("Name").GetValue(selected);

            var result = MessageBox.Show(
                $"Разморозить сессию на устройстве {name}?\n" +
                "Время сессии возобновится с текущего момента.",
                "Разморозка сессии",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var ctx = new Entities())
                {
                    var session = ctx.Sessions
                        .FirstOrDefault(s => s.DeviceID == deviceId && s.EndTime == null && s.Status == "Active");

                    if (session == null || session.IsFrozen != true)
                    {
                        MessageBox.Show("Замороженная сессия не найдена.");
                        return;
                    }

                    session.IsFrozen = false;
                    session.FreezeStartTime = null;
                    // Здесь можно скорректировать StartTime, если хочешь учесть замороженное время
                    // Например: session.StartTime = session.StartTime.Add(session.FreezeDuration или что-то подобное)

                    ctx.SaveChanges();

                    MessageBox.Show("Сессия разморожена. Время возобновлено.", "Успех");

                    LoadDevices();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка разморозки:\n{ex.Message}", "Ошибка");
            }
        }

        private void SetMaintenance_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgDevices.SelectedItem;
            if (selected == null) return;

            string currentStatus = (string)selected.GetType().GetProperty("Status").GetValue(selected);
            string newStatus = currentStatus == "Maintenance" ? "Available" : "Maintenance";

            if (MessageBox.Show($"Перевести устройство в статус '{newStatus}'?", "Подтверждение",
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            int deviceId = (int)selected.GetType().GetProperty("DeviceID").GetValue(selected);

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