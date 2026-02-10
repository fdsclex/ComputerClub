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
                    // Активные сессии
                    var active = ctx.Sessions
                        .Where(s => s.EndTime == null && s.Status == "Active")
                        .Select(s => new
                        {
                            s.SessionID,
                            ClientFullName = s.Clients.FullName,
                            DeviceName = s.Devices.Name,
                            s.StartTime,
                            Duration = DateTime.Now - s.StartTime,
                            TariffName = s.Devices.Tariffs.Name,
                            Cost = (decimal)(DateTime.Now - s.StartTime).TotalHours * s.Devices.Tariffs.PricePerHour
                        })
                        .ToList();

                    dgActiveSessions.ItemsSource = active;

                    // Бронирования
                    var reservations = ctx.Reservations
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

                    // История сессий (последние 50, можно добавить фильтр по дате позже)
                    var history = ctx.Sessions
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
                            Cost = (decimal)(s.EndTime - s.StartTime).Value.TotalHours * s.Devices.Tariffs.PricePerHour,
                            s.Status
                        })
                        .ToList();

                    dgHistory.ItemsSource = history;

                    tbInfo.Text = $"Активных сессий: {active.Count} | Броней: {reservations.Count} | В истории: {history.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}");
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void FinishSession_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgActiveSessions.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (MessageBox.Show($"Завершить сессию #{sel.SessionID} ({sel.ClientFullName})?",
                                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var session = ctx.Sessions.Find(sel.SessionID);
                        if (session != null)
                        {
                            session.EndTime = DateTime.Now;
                            ctx.SaveChanges(); // триггер сам спишет
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
        }

        private void CancelReservation_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgReservations.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (MessageBox.Show($"Отменить бронь #{sel.ReservationID} ({sel.ClientFullName})?",
                                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var res = ctx.Reservations.Find(sel.ReservationID);
                        if (res != null)
                        {
                            res.Status = "Cancelled";
                            ctx.SaveChanges(); // триггер вернёт предоплату
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
}