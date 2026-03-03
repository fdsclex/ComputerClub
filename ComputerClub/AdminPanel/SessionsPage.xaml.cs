using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity; // для Include, если используешь EF6

namespace ComputerClub.AdminPanel
{
    public partial class SessionsPage : Page
    {
        private class ReservationRow
        {
            public int ReservationID { get; set; }
            public string ClientFullName { get; set; }
            public string DeviceName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public string Status { get; set; }
        }
        public SessionsPage()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    // 1. Активные сессии
                    var activeRaw = ctx.Sessions
                        .Include(s => s.Clients)
                        .Include(s => s.Devices)
                        .Include(s => s.Devices.Tariffs)
                        .Where(s => s.EndTime == null && s.Status == "Active")
                        .Select(s => new
                        {
                            s.SessionID,
                            ClientFullName = s.Clients.FullName,
                            DeviceName = s.Devices.Name,
                            s.StartTime,
                            TariffName = s.Devices.Tariffs.Name,
                            PricePerHour = s.Devices.Tariffs.PricePerHour
                        })
                        .ToList();

                    var active = activeRaw.Select(s => new
                    {
                        s.SessionID,
                        s.ClientFullName,
                        s.DeviceName,
                        s.StartTime,
                        DurationFormatted = FormatDuration(DateTime.Now - s.StartTime),
                        s.TariffName,
                        Cost = (decimal)(DateTime.Now - s.StartTime).TotalHours * s.PricePerHour
                    }).ToList();

                    dgActiveSessions.ItemsSource = active;

                    // 2. Бронирования (только активные)
                    var reservations = ctx.Reservations
                        .Include(r => r.Clients)
                        .Include(r => r.Devices)
                        .Where(r => r.Status == "Pending" || r.Status == "Confirmed")
                        .Select(r => new ReservationRow
                        {
                            ReservationID = r.ReservationID,
                            ClientFullName = r.Clients.FullName,
                            DeviceName = r.Devices.Name,
                            StartTime = r.StartTime,
                            EndTime = r.EndTime,
                            Status = r.Status
                        })
                        .ToList();

                    dgReservations.ItemsSource = reservations;

                    // 3. История сессий (последние 50)
                    var historyRaw = ctx.Sessions
                        .Include(s => s.Clients)
                        .Include(s => s.Devices)
                        .Include(s => s.Devices.Tariffs)
                        .Where(s => s.EndTime != null)
                        .OrderByDescending(s => s.EndTime)
                        .Take(50)
                        .Select(s => new
                        {
                            s.SessionID,
                            ClientFullName = s.Clients.FullName,
                            DeviceName = s.Devices.Name,
                            s.StartTime,
                            s.EndTime,
                            PricePerHour = s.Devices.Tariffs.PricePerHour,
                            s.Status
                        })
                        .ToList();

                    var history = historyRaw.Select(s => new
                    {
                        s.SessionID,
                        s.ClientFullName,
                        s.DeviceName,
                        s.StartTime,
                        s.EndTime,
                        DurationFormatted = FormatDuration(s.EndTime.Value - s.StartTime),
                        Cost = (decimal)(s.EndTime.Value - s.StartTime).TotalHours * s.PricePerHour,
                        s.Status
                    }).ToList();

                    dgHistory.ItemsSource = history;

                    tbInfo.Text = $"Активно: {active.Count} | Брони: {reservations.Count} | История: {history.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных:\n{ex.Message}\n\nПодробности:\n{ex.InnerException?.Message ?? "Нет"}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            LoadData();
        }

        private void FinishSession_Click(object sender, RoutedEventArgs e)
        {
            if (dgActiveSessions.SelectedItem == null) return;

            var sel = (dynamic)dgActiveSessions.SelectedItem;

            if (MessageBox.Show($"Завершить сессию #{sel.SessionID} клиента {sel.ClientFullName}?",
                                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var session = ctx.Sessions.Find(sel.SessionID);
                    if (session != null)
                    {
                        session.EndTime = DateTime.Now;
                        // можно также обновить статус, если нужно
                        // session.Status = "Completed";
                        ctx.SaveChanges();
                        MessageBox.Show("Сессия успешно завершена.", "Успех");
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось завершить сессию:\n{ex.Message}", "Ошибка");
            }
        }

        private void CancelReservation_Click(object sender, RoutedEventArgs e)
        {
            if (dgReservations.SelectedItem == null)
            {
                MessageBox.Show("Выберите бронь для отмены.", "Предупреждение");
                return;
            }

            // Теперь безопасное приведение
            if (!(dgReservations.SelectedItem is ReservationRow sel))
            {
                MessageBox.Show("Выбранная строка имеет неверный тип данных.", "Ошибка");
                return;
            }

            var result = MessageBox.Show(
                $"Отменить бронь №{sel.ReservationID} клиента {sel.ClientFullName}?",
                "Подтверждение отмены",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var reservation = ctx.Reservations
                        .Include(r => r.Devices)
                        .FirstOrDefault(r => r.ReservationID == sel.ReservationID);

                    if (reservation == null)
                    {
                        MessageBox.Show("Бронь не найдена в базе данных.", "Ошибка");
                        return;
                    }

                    reservation.Status = "Cancelled";

                    bool hasActiveSession = ctx.Sessions
                        .Any(s => s.DeviceID == reservation.DeviceID &&
                                  s.EndTime == null &&
                                  s.Status == "Active");

                    if (!hasActiveSession)
                    {
                        var device = reservation.Devices;
                        if (device != null)
                        {
                            device.Status = "Available";
                        }
                    }

                    ctx.SaveChanges();

                    MessageBox.Show("Бронь успешно отменена.", "Успех");
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отмене брони:\n{ex.Message}", "Ошибка");
            }
        }
        private void ActivateReservation_Click(object sender, RoutedEventArgs e)
        {
            if (dgReservations.SelectedItem == null)
            {
                MessageBox.Show("Выберите бронь для активации.", "Предупреждение");
                return;
            }

            var sel = dgReservations.SelectedItem as ReservationRow;

            if (sel == null)
            {
                MessageBox.Show("Выбранная строка имеет неверный тип данных.", "Ошибка");
                return;
            }

            if (sel.Status != "Pending")
            {
                MessageBox.Show("Эту бронь уже нельзя активировать.", "Ошибка");
                return;
            }

            var result = MessageBox.Show(
                $"Начать сессию для клиента {sel.ClientFullName}?\nУстройство: {sel.DeviceName}",
                "Активация брони",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var reservation = ctx.Reservations
                        .Include(r => r.Devices)
                        .FirstOrDefault(r => r.ReservationID == sel.ReservationID);

                    if (reservation == null)
                    {
                        MessageBox.Show("Бронь не найдена.", "Ошибка");
                        return;
                    }

                    // Создаём активную сессию
                    var session = new Sessions
                    {
                        ClientID = reservation.ClientID,
                        DeviceID = reservation.DeviceID,
                        StartTime = DateTime.Now,
                        Status = "Active"
                        // Если нужно — добавь TariffID = reservation.Devices.TariffID
                    };

                    ctx.Sessions.Add(session);

                    // Обновляем статус брони
                    reservation.Status = "Active";

                    ctx.SaveChanges();

                    // Создаём уведомление администратору (аналогично клиентскому)
                    CreateAdminNotification(ctx, reservation.ClientID, sel.DeviceName, "SessionActivatedByAdmin");

                    MessageBox.Show("Сессия успешно начата!", "Успех");
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при активации:\n{ex.Message}", "Ошибка");
            }
        }

        // Новый метод для уведомления администратору (можно вызвать и в других местах)
        private void CreateAdminNotification(Entities ctx, int clientId, string deviceName, string type)
        {
            try
            {
                string message = $"Администратор начал сессию для клиента на устройстве {deviceName}.";

                ctx.Notifications.Add(new Notifications
                {
                    ClientID = clientId,  // или null, если уведомление только админу
                    Title = "Сессия активирована администратором",
                    Message = message,
                    Type = type,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                ctx.SaveChanges();
            }
            catch
            {
                // Тихо игнорируем, чтобы не ломать основной процесс
            }
        }
        private void FixStuckReservedDevices()
        {
            using (var ctx = new Entities())
            {
                var stuck = ctx.Devices
                    .Where(d => d.Status == "Reserved")
                    .Where(d => !ctx.Reservations.Any(r =>
                        r.DeviceID == d.DeviceID &&
                        r.Status == "Pending" || r.Status == "Confirmed"))
                    .ToList();

                foreach (var device in stuck)
                {
                    device.Status = "Available";
                }

                if (stuck.Any())
                {
                    ctx.SaveChanges();
                    MessageBox.Show($"Освобождено {stuck.Count} залипших устройств.", "Информация");
                }
            }
        }  
    }
}