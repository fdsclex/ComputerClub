using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ComputerClub.ClientPanel
{
    public partial class ClientCabinetPage : Page
    {
        private int? _activeSessionId = null;

        public ClientCabinetPage()
        {
            InitializeComponent();
            Loaded += ClientCabinetPage_Loaded;
        }

        private void ClientCabinetPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientInfo();
            LoadActiveSessionIfExists();
            SwitchTab("Shop");
            rbShop.IsChecked = true;
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
            if (AppConfig.IsSessionActive)
            {
                btnSessionControl.Content = "Завершить сессию";
            }
            else
            {
                btnSessionControl.Content = "Начать сессию";

            }

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

        private void SwitchChatTariff_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (btn.Tag.ToString() == "Chat")
                {
                    ChatPanel.Visibility = Visibility.Visible;
                    TariffPanel.Visibility = Visibility.Collapsed;
                    btnChatTab.Background = new SolidColorBrush(Colors.White);
                    btnTariffTab.Background = new SolidColorBrush(Colors.Transparent);
                }
                else // Tariff
                {
                    ChatPanel.Visibility = Visibility.Collapsed;
                    TariffPanel.Visibility = Visibility.Visible;
                    btnTariffTab.Background = new SolidColorBrush(Colors.White);
                    btnChatTab.Background = new SolidColorBrush(Colors.Transparent);
                }
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
                // Завершаем сессию
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

                        // Запоминаем данные до сохранения (для уведомления)
                        int clientId = session.ClientID;
                        int deviceNumber = AppConfig.DeviceNumber ?? 0;

                        session.EndTime = DateTime.Now;
                        ctx.SaveChanges();

                        // Триггер уже должен был списать деньги и обновить статус
                        // Создаём уведомление о завершении
                        CreateSessionCompletedNotification(ctx, clientId, deviceNumber);

                        MessageBox.Show("Сессия завершена. Средства списаны.", "Успех");
                    }

                    AppConfig.IsSessionActive = false;
                    _activeSessionId = null;
                    UpdateSessionButtonState();
                    LoadClientInfo(); // обновляем баланс
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при завершении сессии:\n{ex.Message}", "Ошибка");
                }
            }
            else
            {
                // Начинаем сессию
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
                // Не показываем пользователю ошибку уведомления, чтобы не прерывать процесс
                Console.WriteLine($"Ошибка создания уведомления о завершении сессии: {ex.Message}");
            }
        }

        // Опционально: уведомление о начале сессии
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