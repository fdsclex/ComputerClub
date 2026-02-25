using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.ClientPanel
{
    public partial class CabinetNotificationsPage : Page
    {
        public CabinetNotificationsPage()
        {
            InitializeComponent();
            Loaded += CabinetNotificationsPage_Loaded;
        }

        private void CabinetNotificationsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNotifications();
        }

        private void LoadNotifications()
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Клиент не авторизован.", "Ошибка");
                dgNotifications.ItemsSource = null;
                tbInfo.Text = "Клиент не авторизован";
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    int clientId = AppConfig.CurrentClientId.Value;

                    var notifications = ctx.Notifications
                        .Where(n => n.ClientID == clientId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Select(n => new
                        {
                            n.NotificationID,
                            n.CreatedAt,
                            n.Type,
                            n.Message,
                            n.IsRead
                        })
                        .ToList();

                    dgNotifications.ItemsSource = notifications;

                    int unreadCount = notifications.Count(n => !n.IsRead);
                    tbInfo.Text = $"Всего уведомлений: {notifications.Count} (непрочитано: {unreadCount})";

                    // Автоматически отмечаем все как прочитанные при открытии страницы
                    //foreach (var n in ctx.Notifications.Where(n => n.ClientID == clientId && !n.IsRead))
                    //{
                    //    n.IsRead = true;
                    //}
                    //ctx.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки уведомлений:\n{ex.Message}", "Ошибка");
                tbInfo.Text = "Ошибка загрузки";
            }
        }

        private void MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.CurrentClientId.HasValue) return;

            try
            {
                using (var ctx = new Entities())
                {
                    int clientId = AppConfig.CurrentClientId.Value;

                    foreach (var n in ctx.Notifications.Where(n => n.ClientID == clientId && !n.IsRead))
                    {
                        n.IsRead = true;
                    }

                    ctx.SaveChanges();
                    MessageBox.Show("Все уведомления отмечены как прочитанные.", "Успех");
                    LoadNotifications();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}", "Ошибка");
            }
        }
    }
}