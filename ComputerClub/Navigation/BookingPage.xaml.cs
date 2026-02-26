using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.Navigation
{
    public partial class BookingPage : Page
    {
        public BookingPage()
        {
            InitializeComponent();
            Loaded += BookingPage_Loaded;
        }

        private void BookingPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvailableDevices();
        }

        private void LoadAvailableDevices()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var now = DateTime.Now;
                    var checkUntil = now.AddHours(24); // проверяем брони в ближайшие 24 часа

                    var devices = ctx.Devices
                        .Where(d => d.Status == "Available" || d.Status == "Reserved")
                        .Select(d => new
                        {
                            d.DeviceID,
                            d.Name,
                            d.Type,
                            d.Specs,
                            d.Status,
                            TariffName = d.Tariffs.Name,
                            TariffPrice = d.Tariffs.PricePerHour,

                            HasActiveReservation = ctx.Reservations
                                .Any(r => r.DeviceID == d.DeviceID &&
                                          r.Status == "Pending" &&
                                          r.StartTime <= checkUntil &&
                                          r.EndTime > now),

                            HasActiveSession = ctx.Sessions
                                .Any(s => s.DeviceID == d.DeviceID &&
                                          s.EndTime == null &&
                                          s.Status == "Active")
                        })
                        .OrderBy(d => d.Name)
                        .ToList();

                    // Форматируем уже в памяти + добавляем все нужные свойства для XAML
                    var viewModels = devices.Select(d => new
                    {
                        d.DeviceID,
                        d.Name,
                        d.Type,
                        d.Specs,
                        TariffText = d.TariffName != null
                            ? $"{d.TariffName} — {d.TariffPrice:N0} ₽/час"
                            : "Тариф не указан",
                        IsAvailable = d.Status == "Available" &&
                                      !d.HasActiveReservation &&
                                      !d.HasActiveSession,
                        StatusDisplay = d.HasActiveSession ? "Занято (сессия)" :
                                        d.HasActiveReservation ? "Забронировано" :
                                        d.Status == "Reserved" ? "Зарезервировано" : "",
                        // Вместо конвертера — просто строка цвета для TextBlock
                        StatusColor = d.HasActiveSession ? "#EF5350" :           // красный
                                      d.HasActiveReservation ? "#FFB74D" :      // оранжевый
                                      d.Status == "Reserved" ? "#64B5F6" :      // синий
                                      "#AAAAAA"                                 // серый
                    }).ToList();

                    icDevices.ItemsSource = viewModels;

                    tbNoDevices.Visibility = viewModels.Any(d => d.IsAvailable)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки устройств:\n{ex.Message}", "Ошибка");
                tbNoDevices.Visibility = Visibility.Visible;
            }
        }

        private void BookDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int deviceId)
            {
                if (!AppConfig.CurrentClientId.HasValue)
                {
                    MessageBox.Show("Для бронирования необходимо войти в аккаунт.",
                                    "Требуется авторизация",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    NavigationService?.Navigate(new ClientLoginPage());
                    return;
                }

                try
                {
                    using (var ctx = new Entities())
                    {
                        var device = ctx.Devices.Find(deviceId);
                        if (device == null || device.Status != "Available")
                        {
                            MessageBox.Show("Устройство больше недоступно или занято.", "Ошибка");
                            LoadAvailableDevices();
                            return;
                        }

                        var start = DateTime.Now.AddMinutes(10);
                        var end = start.AddHours(2);

                        bool hasConflict = ctx.Reservations
                            .Any(r => r.DeviceID == deviceId &&
                                      r.Status != "Cancelled" &&
                                      ((r.StartTime < end && r.EndTime > start)));

                        if (hasConflict)
                        {
                            MessageBox.Show("На это время уже есть бронь или сессия.", "Конфликт времени");
                            return;
                        }

                        var reservation = new Reservations
                        {
                            ClientID = AppConfig.CurrentClientId.Value,
                            DeviceID = deviceId,
                            StartTime = start,
                            EndTime = end,
                            Status = "Pending",
                            IsPaid = false
                        };

                        ctx.Reservations.Add(reservation);
                        device.Status = "Reserved";

                        ctx.SaveChanges();

                        CreateNotification(AppConfig.CurrentClientId.Value,
                            "Бронь создана",
                            $"Вы забронировали {device.Name} ({device.Type})\n" +
                            $"с {start:dd.MM.yyyy HH:mm} до {end:dd.MM.yyyy HH:mm}",
                            "ReservationCreated");

                        MessageBox.Show("Бронь успешно создана!\nОжидайте подтверждения.", "Успех");

                        LoadAvailableDevices();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка бронирования:\n{ex.Message}", "Ошибка");
                }
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
            catch { /* тихо игнорируем */ }
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableDevices();
        }
    }
}