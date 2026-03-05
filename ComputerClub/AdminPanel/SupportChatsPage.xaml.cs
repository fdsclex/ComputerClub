using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ComputerClub.AdminPanel
{
    public class ClientChatItem
    {
        public Clients Client { get; set; }
        public string FullName => Client?.FullName ?? "";
        public int ClientID => Client?.ClientID ?? 0;
        public int UnreadCount { get; set; }
        public Visibility BadgeVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public partial class SupportChatsPage : Page
    {
        public ObservableCollection<SupportMessages> Messages { get; } = new ObservableCollection<SupportMessages>();
        public ObservableCollection<ClientChatItem> ChatClients { get; } = new ObservableCollection<ClientChatItem>();

        private DispatcherTimer _chatTimer;
        private DispatcherTimer _globalPollingTimer;
        private int? _selectedClientId = null;
        private const int AdminId = 4;
        private bool _isLoading;

        public SupportChatsPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += SupportChatsPage_Loaded;
        }

        private void SupportChatsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientsWithChats();
            StartGlobalPolling();
        }

        private void StartGlobalPolling()
        {
            _globalPollingTimer?.Stop();
            _globalPollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _globalPollingTimer.Tick += async (s, ev) => await Dispatcher.InvokeAsync(LoadClientsWithChats);
            _globalPollingTimer.Start();
        }

        private void LoadClientsWithChats()
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

                    var clients = ctx.Clients
                        .Where(c => clientIds.Contains(c.ClientID))
                        .ToList();

                    var items = new List<ClientChatItem>();

                    foreach (var client in clients)
                    {
                        var readStatus = ctx.ChatReadStatus
                            .FirstOrDefault(rs => rs.ClientID == client.ClientID && rs.EmployeeID == AdminId);

                        DateTime lastRead = readStatus?.LastReadTime ?? DateTime.MinValue;

                        int unread = ctx.SupportMessages
                            .Count(m => m.ClientID == client.ClientID
                                     && m.EmployeeID == null
                                     && m.SentAt > lastRead);

                        items.Add(new ClientChatItem
                        {
                            Client = client,
                            UnreadCount = unread
                        });
                    }

                    int? prevSelectedId = _selectedClientId ?? (lbClients.SelectedItem as ClientChatItem)?.ClientID;

                    ChatClients.Clear();
                    foreach (var item in items.OrderByDescending(x => x.UnreadCount).ThenBy(x => x.FullName))
                        ChatClients.Add(item);

                    lbClients.ItemsSource = ChatClients;

                    if (prevSelectedId.HasValue)
                    {
                        var toSelect = ChatClients.FirstOrDefault(x => x.ClientID == prevSelectedId.Value);
                        if (toSelect != null)
                            lbClients.SelectedItem = toSelect;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка чатов:\n" + ex.Message);
            }
        }

        private async void lbClients_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            _chatTimer?.Stop();

            if (lbClients.SelectedItem is ClientChatItem item && item != null)
            {
                _isLoading = true;
                _selectedClientId = item.ClientID;
                tbChatHeader.Text = $"Чат с {item.FullName}";
                ChatGrid.Visibility = Visibility.Visible;
                tbPlaceholder.Visibility = Visibility.Collapsed;

                // Обновляем время последнего прочтения в базе
                using (var ctx = new Entities())
                {
                    var readStatus = ctx.ChatReadStatus
                        .FirstOrDefault(rs => rs.ClientID == item.ClientID && rs.EmployeeID == AdminId);

                    if (readStatus == null)
                    {
                        readStatus = new ChatReadStatus
                        {
                            ClientID = item.ClientID,
                            EmployeeID = AdminId,
                            LastReadTime = DateTime.UtcNow
                        };
                        ctx.ChatReadStatus.Add(readStatus);
                    }
                    else
                    {
                        readStatus.LastReadTime = DateTime.UtcNow;
                    }

                    await ctx.SaveChangesAsync();
                }

                Messages.Clear();
                await LoadAllMessagesForClient();
                StartChatPollingForClient();

                _isLoading = false;
            }
            else
            {
                ChatGrid.Visibility = Visibility.Collapsed;
                tbPlaceholder.Visibility = Visibility.Visible;
                _selectedClientId = null;
            }
        }

        private void StartChatPollingForClient()
        {
            _chatTimer?.Stop();
            _chatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
            _chatTimer.Tick += async (s, ev) => await LoadAllMessagesForClient();
            _chatTimer.Start();
        }

        private async Task LoadAllMessagesForClient()
        {
            if (!_selectedClientId.HasValue) return;

            try
            {
                List<SupportMessages> allMsgs;
                bool shouldRefresh = false;

                using (var ctx = new Entities())
                {
                    allMsgs = ctx.SupportMessages
                        .Where(m => m.ClientID == _selectedClientId.Value)
                        .OrderBy(m => m.SentAt)
                        .ToList();

                    if (ChatGrid.Visibility == Visibility.Visible)
                    {
                        var unread = allMsgs.Where(m => m.EmployeeID == null && !m.IsReadByEmployee).ToList();
                        if (unread.Any())
                        {
                            foreach (var m in unread) m.IsReadByEmployee = true;
                            await ctx.SaveChangesAsync();
                            shouldRefresh = true;
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    Messages.Clear();
                    foreach (var msg in allMsgs)
                    {
                        msg.SentAt = DateTime.SpecifyKind(msg.SentAt, DateTimeKind.Utc).ToLocalTime();
                        Messages.Add(msg);
                    }
                    ChatScrollViewer?.ScrollToEnd();

                    if (shouldRefresh)
                        LoadClientsWithChats();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки чата: " + ex.Message);
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
            if (!_selectedClientId.HasValue) return;
            string text = tbMessageInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            tbMessageInput.Clear();

            try
            {
                using (var ctx = new Entities())
                {
                    var msg = new SupportMessages
                    {
                        EmployeeID = AdminId,
                        ClientID = _selectedClientId.Value,
                        Content = text,
                        SentAt = DateTime.UtcNow,
                        IsReadByEmployee = true,
                        IsReadByClient = false
                    };

                    ctx.SupportMessages.Add(msg);
                    await ctx.SaveChangesAsync();

                    Dispatcher.Invoke(() =>
                    {
                        msg.SentAt = DateTime.SpecifyKind(msg.SentAt, DateTimeKind.Utc).ToLocalTime();
                        Messages.Add(msg);
                        ChatScrollViewer?.ScrollToEnd();
                        LoadClientsWithChats();
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось отправить: " + ex.Message);
            }
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedClientId.HasValue) return;
            var result = MessageBox.Show("Очистить весь чат с этим клиентом?\nСообщения будут удалены без возможности восстановления.",
                                         "Очистка чата", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var ctx = new Entities())
                {
                    var messagesToDelete = ctx.SupportMessages
                        .Where(m => m.ClientID == _selectedClientId.Value)
                        .ToList();

                    ctx.SupportMessages.RemoveRange(messagesToDelete);
                    ctx.SaveChanges();

                    Dispatcher.Invoke(() =>
                    {
                        Messages.Clear();
                        tbChatHeader.Text = tbChatHeader.Text + " (очищен)";
                        LoadClientsWithChats();
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка очистки чата: " + ex.Message);
            }
        }
    }
}