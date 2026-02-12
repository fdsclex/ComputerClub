using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class SessionsPage : Page
    {
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
                        .Include("Clients")
                        .Include("Devices")
                        .Include("Devices.Tariffs")
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

                    // 2. Бронирования
                    var reservations = ctx.Reservations
                        .Include("Clients")
                        .Include("Devices")
                        .Where(r => r.Status == "Pending" || r.Status == "Confirmed")
                        .Select(r => new
                        {
                            r.ReservationID,
                            ClientFullName = r.Clients.FullName,
                            DeviceName = r.Devices.Name,
                            r.StartTime,
                            r.EndTime,
                            r.Status
                        })
                        .ToList();

                    dgReservations.ItemsSource = reservations;

                    // 3. История сессий
                    var historyRaw = ctx.Sessions
                        .Include("Clients")
                        .Include("Devices")
                        .Include("Devices.Tariffs")
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
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}\n\nПодробности:\n{ex.InnerException?.Message ?? "Нет внутренней ошибки"}");
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
            dynamic sel = dgActiveSessions.SelectedItem;

            if (MessageBox.Show($"Завершить сессию #{sel.SessionID} клиента {sel.ClientFullName}?",
                                "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var session = ctx.Sessions.Find(sel.SessionID);
                    if (session != null)
                    {
                        session.EndTime = DateTime.Now;
                        ctx.SaveChanges();
                        MessageBox.Show("Сессия завершена.");
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}");
            }
        }

        private void CancelReservation_Click(object sender, RoutedEventArgs e)
        {
            if (dgReservations.SelectedItem == null) return;
            dynamic sel = dgReservations.SelectedItem;

            if (MessageBox.Show($"Отменить бронь #{sel.ReservationID} клиента {sel.ClientFullName}?",
                                "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var ctx = new Entities())
                {
                    var res = ctx.Reservations.Find(sel.ReservationID);
                    if (res != null)
                    {
                        res.Status = "Cancelled";
                        ctx.SaveChanges();
                        MessageBox.Show("Бронь отменена.");
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}");
            }
        }
    }
}