using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ComputerClub.ClientPanel
{
    public partial class ClientCabinetPage : Page
    {
        public ObservableCollection<SupportMessages> Messages { get; } = new ObservableCollection<SupportMessages>();
        private DispatcherTimer _chatTimer;
        private int? _activeSessionId;

        public ClientCabinetPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += ClientCabinetPage_Loaded;
        }

        private void ClientCabinetPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientInfo();
            LoadActiveSessionIfExists();
            LoadDeviceTariff();
            SwitchChatTariff_Click(btnTariffTab, new RoutedEventArgs());
            StartChatPolling();
        }

        private void StartChatPolling()
        {
            _chatTimer?.Stop();
            _chatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
            _chatTimer.Tick += async (s, ev) => await LoadAllMessagesAsync();
            _chatTimer.Start();
            _ = LoadAllMessagesAsync();
        }

        private async Task LoadAllMessagesAsync()
        {
            if (!AppConfig.CurrentClientId.HasValue) return;

            try
            {
                List<SupportMessages> allMsgs;
                bool hasUnreadFromAdmin = false;

                using (var ctx = new Entities())
                {
                    allMsgs = ctx.SupportMessages
                        .Where(m => m.ClientID == AppConfig.CurrentClientId.Value)
                        .OrderBy(m => m.SentAt)
                        .ToList();

                    // Обновляем LastReadTime, если чат открыт
                    if (ChatPanel.Visibility == Visibility.Visible)
                    {
                        var readStatus = ctx.ChatReadStatus
                            .FirstOrDefault(rs => rs.ClientID == AppConfig.CurrentClientId.Value
                                               && rs.EmployeeID == null);

                        if (readStatus == null)
                        {
                            readStatus = new ChatReadStatus
                            {
                                ClientID = AppConfig.CurrentClientId.Value,
                                EmployeeID = null,
                                LastReadTime = DateTime.UtcNow
                            };
                            ctx.ChatReadStatus.Add(readStatus);
                        }
                        else
                        {
                            readStatus.LastReadTime = DateTime.UtcNow;
                        }

                        await ctx.SaveChangesAsync();

                        // Помечаем сообщения от админа прочитанными (для галочек)
                        var unreadFromAdmin = allMsgs.Where(m => m.EmployeeID != null && !m.IsReadByClient).ToList();
                        if (unreadFromAdmin.Any())
                        {
                            foreach (var m in unreadFromAdmin) m.IsReadByClient = true;
                            await ctx.SaveChangesAsync();
                        }
                    }

                    // Считаем непрочитанные от админа
                    var lastRead = ctx.ChatReadStatus
                        .FirstOrDefault(rs => rs.ClientID == AppConfig.CurrentClientId.Value
                                           && rs.EmployeeID == null)?.LastReadTime
                        ?? DateTime.MinValue;

                    hasUnreadFromAdmin = allMsgs.Any(m => m.EmployeeID != null && m.SentAt > lastRead);
                }

                Dispatcher.Invoke(() =>
                {
                    Messages.Clear();
                    foreach (var msg in allMsgs)
                    {
                        msg.SentAt = DateTime.SpecifyKind(msg.SentAt, DateTimeKind.Utc).ToLocalTime();
                        Messages.Add(msg);
                    }

                    if (ChatPanel.Visibility == Visibility.Visible)
                        ChatScrollViewer?.ScrollToEnd();

                    // Обновляем badge
                    UnreadBadge.Visibility = hasUnreadFromAdmin ? Visibility.Visible : Visibility.Collapsed;
                    if (hasUnreadFromAdmin)
                    {
                        // Можно показывать цифру, если сообщений > 1
                        // UnreadBadgeText.Text = allMsgs.Count(m => m.EmployeeID != null && m.SentAt > lastRead).ToString();
                        UnreadBadgeText.Text = "!";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чата: {ex}");
            }
        }

        private async void SwitchChatTariff_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (tag == "Chat")
                {
                    ChatPanel.Visibility = Visibility.Visible;
                    TariffPanel.Visibility = Visibility.Collapsed;
                    btnChatTab.Background = new SolidColorBrush(Colors.White);
                    btnTariffTab.Background = new SolidColorBrush(Colors.Transparent);

                    await LoadAllMessagesAsync(); // обновляем LastReadTime и индикатор
                }
                else
                {
                    ChatPanel.Visibility = Visibility.Collapsed;
                    TariffPanel.Visibility = Visibility.Visible;
                    btnTariffTab.Background = new SolidColorBrush(Colors.White);
                    btnChatTab.Background = new SolidColorBrush(Colors.Transparent);

                    LoadDeviceTariff();
                }
            }
        }

        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentMessage();
        }

        private async void tbMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await SendCurrentMessage();
            }
        }

        private async Task SendCurrentMessage()
        {
            string text = tbMessageInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            tbMessageInput.Clear();

            try
            {
                using (var ctx = new Entities())
                {
                    var msg = new SupportMessages
                    {
                        ClientID = AppConfig.CurrentClientId.Value,
                        Content = text,
                        SentAt = DateTime.UtcNow,
                        IsReadByClient = true,
                        IsReadByEmployee = false
                    };

                    ctx.SupportMessages.Add(msg);
                    await ctx.SaveChangesAsync();

                    Dispatcher.Invoke(() =>
                    {
                        msg.SentAt = DateTime.SpecifyKind(msg.SentAt, DateTimeKind.Utc).ToLocalTime();
                        Messages.Add(msg);
                        if (ChatPanel.Visibility == Visibility.Visible)
                            ChatScrollViewer?.ScrollToEnd();
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось отправить.\n{ex.Message}");
            }
        }
        private void LoadDeviceTariff()
        {
            if (!AppConfig.IsOnSite || !AppConfig.DeviceNumber.HasValue)
            {
                tbNoTariff.Text = "Выберите устройство, чтобы увидеть тариф";
                tbNoTariff.Visibility = Visibility.Visible;
                TariffInfoPanel.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var device = ctx.Devices
                        .Include("Tariffs")
                        .FirstOrDefault(d => d.DeviceID == AppConfig.DeviceNumber.Value);

                    if (device != null && device.Tariffs != null)
                    {
                        tbTariffName.Text = device.Tariffs.Name ?? "Тариф не указан";
                        tbTariffDescription.Text = device.Tariffs.Description ?? "Нет описания";
                        tbTariffPrice.Text = $"Цена: {device.Tariffs.PricePerHour:N0} ₽/час";
                        tbNoTariff.Visibility = Visibility.Collapsed;
                        TariffInfoPanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        tbNoTariff.Text = "Тариф для этого устройства не найден";
                        tbNoTariff.Visibility = Visibility.Visible;
                        TariffInfoPanel.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                tbNoTariff.Text = "Ошибка загрузки тарифа устройства";
                tbNoTariff.Visibility = Visibility.Visible;
                TariffInfoPanel.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine("Ошибка тарифа: " + ex.Message);
            }
        }

        private void LoadClientInfo()
        {
            if (!AppConfig.CurrentClientId.HasValue) return;

            using (var ctx = new Entities())
            {
                var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                if (client != null)
                {
                    tbWelcome.Text = client.FullName;
                    tbBalance.Text = $"{client.Balance:N0} ₽";
                    tbStatus.Text = AppConfig.IsOnSite
                        ? $"На месте • №{AppConfig.DeviceNumber} • {AppConfig.DeviceName}"
                        : "Онлайн";
                }
            }
        }

        private void LoadActiveSessionIfExists()
        {
            if (!AppConfig.IsOnSite || !AppConfig.DeviceNumber.HasValue || !AppConfig.CurrentClientId.HasValue)
            {
                _activeSessionId = null;
                AppConfig.IsSessionActive = false;
                UpdateSessionButtonState();
                return;
            }

            using (var ctx = new Entities())
            {
                var activeSession = ctx.Sessions
                    .Where(s => s.ClientID == AppConfig.CurrentClientId.Value
                             && s.DeviceID == AppConfig.DeviceNumber.Value
                             && s.EndTime == null
                             && s.Status == "Active")
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefault();

                if (activeSession != null)
                {
                    _activeSessionId = activeSession.SessionID;
                    AppConfig.IsSessionActive = true;
                }
                else
                {
                    _activeSessionId = null;
                    AppConfig.IsSessionActive = false;
                }

                UpdateSessionButtonState();
            }
        }

        private void UpdateSessionButtonState()
        {
            btnSessionControl.Content = AppConfig.IsSessionActive ? "Завершить сессию" : "Начать сессию";
            btnSessionControl.IsEnabled = AppConfig.IsOnSite && AppConfig.DeviceNumber.HasValue;
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                SwitchTab(rb.Tag.ToString());
            }
        }

        private void SwitchTab(string tabName)
        {
            switch (tabName)
            {
                case "Shop":
                    CabinetFrame.Navigate(new CabinetShopPage());
                    break;
                case "Notifications":
                    CabinetFrame.Navigate(new CabinetNotificationsPage());
                    break;
                case "Loyalty":
                    CabinetFrame.Navigate(new CabinetLoyaltyPage());
                    break;
                case "Profile":
                    CabinetFrame.Navigate(new CabinetProfilePage());
                    break;
            }
        }

        private void BackToShell_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ClientShellPage());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AppConfig.Reset();
                NavigationService?.Navigate(new Navigation.RoleSelectionPage());
            }
        }

        private void SwitchDevice_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.IsDeviceSwitchInProgress = true;
            AppConfig.ResetDeviceOnly();
            NavigationService?.Navigate(new Navigation.DeviceInputPage());
        }

        private void SessionControl_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.IsOnSite || !AppConfig.DeviceNumber.HasValue)
            {
                MessageBox.Show("Сначала выберите устройство.", "Нет устройства",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AppConfig.IsSessionActive)
            {
                var result = MessageBox.Show(
                    "Завершить текущую сессию?\nСтоимость будет списана автоматически.",
                    "Завершение сессии",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    using (var ctx = new Entities())
                    {
                        var session = ctx.Sessions.Find(_activeSessionId);
                        if (session == null || session.EndTime != null)
                        {
                            MessageBox.Show("Сессия уже завершена или не найдена.", "Ошибка");
                            AppConfig.IsSessionActive = false;
                            _activeSessionId = null;
                            UpdateSessionButtonState();
                            return;
                        }

                        int clientId = session.ClientID;
                        int deviceNumber = AppConfig.DeviceNumber ?? 0;
                        session.EndTime = DateTime.Now;
                        ctx.SaveChanges();

                        CreateSessionCompletedNotification(ctx, clientId, deviceNumber);
                        MessageBox.Show("Сессия завершена. Средства списаны.", "Успех");
                    }

                    AppConfig.IsSessionActive = false;
                    _activeSessionId = null;
                    UpdateSessionButtonState();
                    LoadClientInfo();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при завершении сессии:\n{ex.Message}", "Ошибка");
                }
            }
            else
            {
                var result = MessageBox.Show(
                    $"Начать сессию на устройстве №{AppConfig.DeviceNumber} ({AppConfig.DeviceName})?",
                    "Начало сессии",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    using (var ctx = new Entities())
                    {
                        var newSession = new Sessions
                        {
                            ClientID = AppConfig.CurrentClientId.Value,
                            DeviceID = AppConfig.DeviceNumber.Value,
                            StartTime = DateTime.Now,
                            EndTime = null,
                            Status = "Active"
                        };

                        ctx.Sessions.Add(newSession);
                        ctx.SaveChanges();

                        _activeSessionId = newSession.SessionID;
                        AppConfig.IsSessionActive = true;

                        CreateSessionStartedNotification(ctx, AppConfig.CurrentClientId.Value);
                        MessageBox.Show("Сессия начата. Приятной игры!", "Успех");
                    }

                    UpdateSessionButtonState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось начать сессию:\n{ex.Message}", "Ошибка");
                }
            }
        }

        private void CreateSessionCompletedNotification(Entities ctx, int clientId, int deviceNumber)
        {
            try
            {
                ctx.Notifications.Add(new Notifications
                {
                    ClientID = clientId,
                    Title = "Сессия завершена",
                    Message = $"Ваша игровая сессия на устройстве №{deviceNumber} завершена.\n" +
                              $"Стоимость использования списана с баланса согласно тарифу устройства.",
                    Type = "SessionCompleted",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                ctx.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания уведомления о завершении сессии: {ex.Message}");
            }
        }

        private void CreateSessionStartedNotification(Entities ctx, int clientId)
        {
            try
            {
                ctx.Notifications.Add(new Notifications
                {
                    ClientID = clientId,
                    Title = "Сессия начата",
                    Message = $"Вы начали игровую сессию на устройстве №{AppConfig.DeviceNumber}.\n" +
                              $"Приятного времяпрепровождения!",
                    Type = "SessionStarted",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                ctx.SaveChanges();
            }
            catch { /* тихо */ }
        }
    }
}