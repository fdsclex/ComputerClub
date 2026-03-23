using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace ComputerClub.AdminPanel
{
    public partial class AdminDashboardPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _totalUnreadChats;
        public int TotalUnreadChats
        {
            get => _totalUnreadChats;
            private set
            {
                if (_totalUnreadChats != value)
                {
                    _totalUnreadChats = value;
                    OnPropertyChanged(nameof(TotalUnreadChats));
                    OnPropertyChanged(nameof(ChatsBadgeVisibility));
                }
            }
        }

        public Visibility ChatsBadgeVisibility => TotalUnreadChats > 0 ? Visibility.Visible : Visibility.Collapsed;

        private DispatcherTimer _unreadTimer;
        private const int AdminId = 4;

        public AdminDashboardPage()
        {
            InitializeComponent();

            // При первой загрузке страницы
            NavigateToPage("ClubMapPage");
            rbMap.IsChecked = true;

            // Подписываемся на события жизненного цикла страницы
            Loaded += AdminDashboardPage_Loaded;
            Unloaded += AdminDashboardPage_Unloaded;
        }

        private void AdminDashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            StartUnreadPolling();
        }

        private void AdminDashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _unreadTimer?.Stop();
            _unreadTimer = null;
        }

        private void StartUnreadPolling()
        {
            _unreadTimer?.Stop();

            _unreadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(7)
            };

            _unreadTimer.Tick += (s, ev) => Dispatcher.Invoke(UpdateUnreadChatsCount);
            _unreadTimer.Start();

            // Немедленный подсчёт при показе страницы
            UpdateUnreadChatsCount();
        }

        private void UpdateUnreadChatsCount()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var clientIds = ctx.SupportMessages
                        .Where(m => m.ClientID != null)
                        .Select(m => m.ClientID.Value)
                        .Distinct()
                        .ToList();

                    int totalUnread = 0;

                    foreach (var clientId in clientIds)
                    {
                        var lastRead = ctx.ChatReadStatus
                            .Where(rs => rs.ClientID == clientId && rs.EmployeeID == AdminId)
                            .Select(rs => rs.LastReadTime)
                            .FirstOrDefault();
                        DateTime lastReadTime = ctx.ChatReadStatus
                            .Where(rs => rs.ClientID == clientId && rs.EmployeeID == AdminId)
                            .Select(rs => rs.LastReadTime)
                            .FirstOrDefault();   // если записи нет → вернёт DateTime.MinValue (default(DateTime))

                        int unreadCount = ctx.SupportMessages
                            .Count(m => m.ClientID == clientId
                                     && m.EmployeeID == null
                                     && m.SentAt > lastReadTime);

                        totalUnread += unreadCount;
                    }

                    TotalUnreadChats = totalUnread;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка подсчёта непрочитанных чатов: {ex.Message}");
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                string pageName = rb.Tag.ToString();
                NavigateToPage(pageName);
            }
        }

        private void NavigateToPage(string pageName)
        {
            try
            {
                switch (pageName)
                {
                    case "ClubMapPage": MainFrame.Navigate(new ClubMapPage()); break;
                    case "DevicesManagementPage": MainFrame.Navigate(new DevicesManagementPage()); break;
                    case "SessionsPage": MainFrame.Navigate(new SessionsPage()); break;
                    case "MenuManagementPage": MainFrame.Navigate(new MenuManagementPage()); break;
                    case "ClientsPage": MainFrame.Navigate(new ClientsPage()); break;
                    case "ReportsPage": MainFrame.Navigate(new ReportsPage()); break;
                    case "SupportChatsPage": MainFrame.Navigate(new SupportChatsPage()); break;
                    default:
                        MessageBox.Show("Страница в разработке");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки страницы: {ex.Message}");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из аккаунта?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                NavigationService?.Navigate(new ComputerClub.Navigation.RoleSelectionPage());
            }
        }
    }
}