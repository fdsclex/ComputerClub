using System;
using System.Data.Entity.Validation;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ComputerClub.Navigation
{
    public partial class BookingPage : Page
    {
        private int? _selectedDeviceId = null;

        private class DeviceViewModel
        {
            public int DeviceID { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Specs { get; set; }
            public string TariffText { get; set; }
            public bool IsAvailable { get; set; }
            public string StatusDisplay { get; set; }
            public string StatusColor { get; set; }
            public bool IsSelected { get; set; }
        }

        public BookingPage()
        {
            InitializeComponent();

            // Начальные значения времени
            dpDate.SelectedDate = DateTime.Today;
            var nowPlus30 = DateTime.Now.AddMinutes(30);
            tbHour.Text = nowPlus30.Hour.ToString("D2");
            tbMinute.Text = nowPlus30.Minute.ToString("D2");

            dpDate.SelectedDateChanged += DpDate_SelectedDateChanged;
            tbHour.TextChanged += TimeField_TextChanged;
            tbMinute.TextChanged += TimeField_TextChanged;
            cbDuration.SelectionChanged += CbDuration_SelectionChanged;

            Loaded += BookingPage_Loaded;
        }

        private void BookingPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDevices();
        }

        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConfirmButton();
        }

        private void TimeField_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConfirmButton();
        }

        private void CbDuration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConfirmButton();
        }

        private void LoadDevices()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var now = DateTime.Now;
                    var checkTo = now.AddHours(24);

                    var devices = ctx.Devices
                        .Where(d => d.Status == "Available" || d.Status == "Reserved")
                        .Select(d => new
                        {
                            d.DeviceID,
                            d.Name,
                            d.Type,
                            d.Specs,
                            TariffName = d.Tariffs.Name,
                            TariffPrice = d.Tariffs.PricePerHour,
                            HasReservation = ctx.Reservations.Any(r =>
                                r.DeviceID == d.DeviceID &&
                                (r.Status == "Pending" || r.Status == "Confirmed") &&
                                r.StartTime <= checkTo &&
                                r.EndTime > now),
                            HasSession = ctx.Sessions.Any(s =>
                                s.DeviceID == d.DeviceID &&
                                s.EndTime == null &&
                                s.Status == "Active"),
                            Status = d.Status
                        })
                        .OrderBy(d => d.Name)
                        .ToList();

                    var vmList = devices.Select(d => new DeviceViewModel
                    {
                        DeviceID = d.DeviceID,
                        Name = d.Name,
                        Type = d.Type,
                        Specs = d.Specs,
                        TariffText = d.TariffName != null
                            ? $"{d.TariffName} — {d.TariffPrice:N0} ₽/ч"
                            : "Тариф не указан",
                        IsAvailable = !d.HasSession && !d.HasReservation && d.Status != "Reserved",
                        StatusDisplay = d.HasSession ? "Занято (сессия активна)" :
                                        d.HasReservation ? "Забронировано" :
                                        d.Status == "Reserved" ? "Зарезервировано" :
                                        "Доступно",
                        StatusColor = d.HasSession ? "#EF5350" :
                                      d.HasReservation ? "#FFB74D" :
                                      d.Status == "Reserved" ? "#64B5F6" :
                                      "#66BB6A",  // зелёный для свободных
                        IsSelected = d.DeviceID == _selectedDeviceId.GetValueOrDefault(-1)
                    }).ToList();

                    icDevices.ItemsSource = vmList;

                    tbNoDevices.Visibility = vmList.Any(x => x.IsAvailable)
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                    UpdateConfirmButton();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки устройств:\n{ex.Message}", "Ошибка");
                tbNoDevices.Visibility = Visibility.Visible;
            }
        }

        private void DeviceCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border &&
                border.DataContext is DeviceViewModel vm &&
                vm.IsAvailable)
            {
                _selectedDeviceId = vm.DeviceID;
                LoadDevices();  // перерисовка для обновления "ВЫБРАНО" и подсветки
            }
        }

        private bool TryParseStartTime(out DateTime start, out DateTime end)
        {
            start = default(DateTime);
            end = default(DateTime);

            if (!dpDate.SelectedDate.HasValue) return false;

            int h, m;
            if (!int.TryParse(tbHour.Text, out h) || h < 0 || h > 23) return false;
            if (!int.TryParse(tbMinute.Text, out m) || m < 0 || m > 59) return false;

            m = (m / 30) * 30; // шаг 30 минут

            start = dpDate.SelectedDate.Value.Date.AddHours(h).AddMinutes(m);

            if (start < DateTime.Now.AddMinutes(2)) return false;

            if (cbDuration.SelectedIndex < 0) return false;
            int hours = cbDuration.SelectedIndex + 1;

            end = start.AddHours(hours);
            return true;
        }

        private void UpdateConfirmButton()
        {
            bool canBook = _selectedDeviceId.HasValue && TryParseStartTime(out _, out _);
            btnConfirm.IsEnabled = canBook;
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Необходимо войти в аккаунт", "Авторизация");
                NavigationService?.Navigate(new ClientLoginPage());
                return;
            }

            if (!TryParseStartTime(out DateTime start, out DateTime end))
            {
                MessageBox.Show("Проверьте корректность даты и времени", "Ошибка");
                return;
            }

            int devId = _selectedDeviceId.Value;
            int clientId = AppConfig.CurrentClientId.Value;

            try
            {
                using (var ctx = new Entities())
                {
                    var device = ctx.Devices
                        .Include("Tariffs")
                        .FirstOrDefault(d => d.DeviceID == devId);

                    if (device == null || device.Tariffs == null)
                    {
                        MessageBox.Show("Устройство или тариф не найден", "Ошибка");
                        return;
                    }

                    bool overlap = ctx.Reservations.Any(r =>
                        r.DeviceID == devId &&
                        r.Status != "Cancelled" &&
                        r.StartTime < end &&
                        r.EndTime > start);

                    if (!overlap)
                    {
                        overlap = ctx.Sessions.Any(s =>
                            s.DeviceID == devId &&
                            s.StartTime < end &&
                            (s.EndTime == null || s.EndTime > start));
                    }

                    if (overlap)
                    {
                        MessageBox.Show("Выбранное время уже занято", "Конфликт времени");
                        return;
                    }

                    int hours = cbDuration.SelectedIndex + 1;
                    decimal prepayment = device.Tariffs.PricePerHour * hours * 0.5m;

                    var client = ctx.Clients.Find(clientId);
                    if (client == null)
                    {
                        MessageBox.Show("Клиент не найден", "Ошибка");
                        return;
                    }

                    if (client.Balance < prepayment)
                    {
                        MessageBox.Show($"Недостаточно средств.\nТребуется: {prepayment:N2} ₽\nНа балансе: {client.Balance:N2} ₽",
                                        "Недостаточно средств", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    client.Balance -= prepayment;

                    var reservation = new Reservations
                    {
                        ClientID = clientId,
                        DeviceID = devId,
                        StartTime = start,
                        EndTime = end,
                        Status = "Pending",
                        IsPaid = true,
                        PrepaymentAmount = prepayment
                    };

                    ctx.Reservations.Add(reservation);
                    ctx.SaveChanges();

                    ctx.Transactions.Add(new Transactions
                    {
                        ClientID = clientId,
                        ReservationID = reservation.ReservationID,
                        Amount = -prepayment,
                        Type = "ResPrepay",                      // ← исправлено здесь
                        TransactionDate = DateTime.Now       // ← закомментируйте или удалите, если поля нет
                    });

                    device.Status = "Reserved";

                    ctx.SaveChanges();

                    CreateNotification(clientId,
                        "Бронь создана",
                        $"Устройство: {device.Name} ({device.Type})\n" +
                        $"Время: {start:dd.MM.yyyy HH:mm} – {end:dd.MM.yyyy HH:mm}\n" +
                        $"Списана предоплата: {prepayment:N2} ₽",
                        "ReservationCreated");

                    MessageBox.Show($"Бронь успешно создана!\n" +
                                    $"Списана предоплата: {prepayment:N2} ₽\n", "Успех");

                    _selectedDeviceId = null;
                    LoadDevices();
                }
            }
            catch (DbEntityValidationException dbEx)
            {
                var errorMsg = new System.Text.StringBuilder("Ошибка валидации:\n");
                foreach (var eve in dbEx.EntityValidationErrors)
                {
                    errorMsg.AppendLine($"Объект типа {eve.Entry.Entity.GetType().Name} в состоянии {eve.Entry.State}:");
                    foreach (var ve in eve.ValidationErrors)
                    {
                        errorMsg.AppendLine($" → {ve.PropertyName}: {ve.ErrorMessage}");
                    }
                }
                MessageBox.Show(errorMsg.ToString(), "Детали ошибки валидации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при бронировании:\n{ex.Message}\n\n" +
                                $"Inner: {ex.InnerException?.Message ?? "нет"}", "Ошибка");
            }
        }

        private void CreateNotification(int clientId, string title, string message, string type)
        {
            try
            {
                using (var ctx = new Entities())
                {
                    ctx.Notifications.Add(new Notifications
                    {
                        ClientID = clientId,
                        Title = title,
                        Message = message,
                        Type = type,
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                    ctx.SaveChanges();
                }
            }
            catch { /* игнорируем ошибки уведомлений */ }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDevices();
        }
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AppConfig.Reset();
                NavigationService?.Navigate(new Navigation.RoleSelectionPage());
            }
        }
    }
}