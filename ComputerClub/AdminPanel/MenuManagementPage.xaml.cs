using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Data.Entity;          

namespace ComputerClub.AdminPanel
{
    public partial class MenuManagementPage : Page
    {
        private List<object> _allMenuItems = new List<object>();

        public MenuManagementPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LoadData();
            };
        }

        private void LoadData()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var activeOrders = ctx.Orders
                        .Where(o => o.Status == "Pending" || o.Status == "Processing")  // ← только активные
                        .Select(o => new
                        {
                            o.OrderID,
                            ClientFullName = o.Clients.FullName,
                            o.OrderDate,
                            o.Status,
                            o.TotalAmount
                        })
                        .OrderByDescending(o => o.OrderDate)
                        .ToList();

                    dgActiveOrders.ItemsSource = activeOrders;

                    // Показываем/скрываем кнопку "Отдал заказ"
                    foreach (var row in dgActiveOrders.Items)
                    {
                        var rowContainer = dgActiveOrders.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
                        if (rowContainer == null) continue;

                        var statusProp = row.GetType().GetProperty("Status");
                        string status = statusProp?.GetValue(row)?.ToString();

                        var actionsPanel = FindVisualChild<StackPanel>(rowContainer, "actionsPanel");
                        if (actionsPanel == null) continue;

                        var btnMarkDelivered = actionsPanel.Children.OfType<Button>()
                            .FirstOrDefault(b => b.Content?.ToString() == "Отдал заказ");

                        if (btnMarkDelivered != null)
                        {
                            btnMarkDelivered.Visibility = status == "Processing"
                                ? Visibility.Visible
                                : Visibility.Collapsed;
                        }
                    }

                    var menuItems = ctx.MenuItems
                        .Select(m => new
                        {
                            m.MenuItemID,
                            m.Name,
                            m.Type,
                            m.Price,
                            m.Description,
                            m.Available
                        })
                        .OrderBy(m => m.Name)
                        .ToList<object>();

                    _allMenuItems = menuItems;
                    ApplyMenuFilters();

                    tbInfo.Text = $"Активных заказов: {activeOrders.Count} | Товаров в меню: {menuItems.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}\n\nДетали:\n{ex.InnerException?.Message ?? "Нет"}");
            }
        }

        // Вспомогательный метод для поиска элемента в визуальном дереве (если нужно искать по имени)
        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void ApplyMenuFilters()
        {
            if (dgMenuItems == null) return;

            string search = tbSearchName?.Text?.Trim().ToLower() ?? "";
            string type = (cbTypeFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

            var filtered = _allMenuItems.Where(m =>
            {
                dynamic item = m;
                bool nameMatch = string.IsNullOrWhiteSpace(search) || item.Name.ToLower().Contains(search);
                bool typeMatch = string.IsNullOrWhiteSpace(type) || item.Type == type;
                return nameMatch && typeMatch;
            }).ToList();

            dgMenuItems.ItemsSource = filtered;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            tbSearchName.Text = "";
            cbTypeFilter.SelectedIndex = 0;
            LoadData();
        }

        private void tbSearchName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyMenuFilters();
        }

        private void cbTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMenuFilters();
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
            catch (Exception ex)
            {
                // Можно логировать, но не показывать пользователю
                Console.WriteLine($"Ошибка создания уведомления: {ex.Message}");
            }
        }
        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (dgActiveOrders.SelectedItem == null)
            {
                MessageBox.Show("Выберите заказ для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic selected = dgActiveOrders.SelectedItem;
            int orderId = selected.OrderID;

            var confirm = MessageBox.Show(
                $"Удалить заказ №{orderId}?\n" +
                "Если заказ уже доставлен — деньги вернутся клиенту.\n" +
                "Действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using (var ctx = new Entities())
                {
                    var order = ctx.Orders
                        .Include(o => o.OrderItems)
                        .Include(o => o.Clients)   // если есть навигация, иначе ниже используем Find
                        .FirstOrDefault(o => o.OrderID == orderId);

                    if (order == null)
                    {
                        MessageBox.Show("Заказ не найден или уже удалён.", "Ошибка");
                        return;
                    }
                    CreateNotification(
                        order.ClientID,
                        "Заказ отменён",
                        $"Ваш заказ №{order.OrderID} отменён администратором.",
                        "OrderCancelled"
                    );
                    if (order.TotalAmount > 0 && order.Status != "Pending")
                    {
                        var client = order.Clients ?? ctx.Clients.Find(order.ClientID);
                        if (client != null)
                        {
                            client.Balance += order.TotalAmount;

                            ctx.Transactions.Add(new Transactions
                            {
                                ClientID = client.ClientID,
                                OrderID = order.OrderID,
                                Amount = order.TotalAmount,          // положительная сумма = возврат
                                Type = "Refund",
                                TransactionDate = DateTime.Now
                            });

                            MessageBox.Show(
                                $"Средства возвращены клиенту: {order.TotalAmount:N0} ₽",
                                "Возврат средств",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Клиент не найден — возврат не выполнен.", "Предупреждение");
                        }
                    }
                    if (order.OrderItems?.Any() == true)
                    {
                        ctx.OrderItems.RemoveRange(order.OrderItems);
                    }

                    var relatedTransactions = ctx.Transactions
                        .Where(t => t.OrderID == orderId)
                        .ToList();

                    if (relatedTransactions.Any())
                    {
                        ctx.Transactions.RemoveRange(relatedTransactions);
                    }

                    ctx.Orders.Remove(order);

                    ctx.SaveChanges();

                    var activeOrders = ctx.Orders
                        .Include(o => o.Clients)
                        .Where(o => o.Status == "Pending" || o.Status == "Processing")
                        .Select(o => new
                        {
                            o.OrderID,
                            ClientFullName = o.Clients != null ? o.Clients.FullName : "(клиент удалён)",
                            o.OrderDate,
                            o.Status,
                            o.TotalAmount
                        })
                        .OrderByDescending(o => o.OrderDate)
                        .ToList();

                    dgActiveOrders.ItemsSource = activeOrders;

                    MessageBox.Show($"Заказ №{orderId} удалён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                string msg = dbEx.Message;
                var inner = dbEx.InnerException;
                while (inner != null)
                {
                    msg += "\n→ " + inner.Message;
                    inner = inner.InnerException;
                }
                MessageBox.Show("Ошибка базы данных:\n" + msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
       
        private void MarkDelivered_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgActiveOrders?.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (sel.Status.ToString() != "Processing")
            {
                MessageBox.Show("Кнопка доступна только для заказов со статусом 'Processing'.", "Предупреждение");
                return;
            }

            if (MessageBox.Show($"Отметить заказ #{sel.OrderID} как доставленный (клиент: {sel.ClientFullName})?",
                                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var order = ctx.Orders.Find(sel.OrderID);
                        if (order != null && order.Status == "Processing")
                        {
                            order.Status = "Delivered";
                            CreateNotification(order.ClientID,
                                       "Заказ доставлен",
                                       $"Ваш заказ №{order.OrderID} успешно доставлен! Приятного аппетита!",
                                       "OrderDelivered");
                            ctx.SaveChanges();
                            MessageBox.Show("Заказ отмечен как доставленный.");
                            LoadData();
                        }
                        else
                        {
                            MessageBox.Show("Статус заказа изменился.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка:\n{ex.Message}");
                }
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgMenuItems?.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            var window = new MenuItemEditWindow(isNew: false, itemId: sel.MenuItemID);
            if (window.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgMenuItems?.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (MessageBox.Show($"Удалить товар '{sel.Name}' (ID {sel.MenuItemID})?\nЭто действие нельзя отменить.",
                                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var item = ctx.MenuItems.Find(sel.MenuItemID);
                        if (item != null)
                        {
                            ctx.MenuItems.Remove(item);
                            ctx.SaveChanges();
                            MessageBox.Show("Товар удалён.");
                            LoadData();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления:\n{ex.Message}");
                }
            }
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new MenuItemEditWindow(isNew: true);
            if (window.ShowDialog() == true)
            {
                LoadData();
            }
        }
    }
}