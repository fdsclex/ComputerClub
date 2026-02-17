using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.ClientPanel
{
    public partial class CabinetShopPage : Page
    {
        public CabinetShopPage()
        {
            InitializeComponent();
            Loaded += CabinetShopPage_Loaded;
        }

        private void CabinetShopPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMenuItems();
        }

        private void LoadMenuItems()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var items = ctx.MenuItems
                        .Where(m => m.Available == true) // ← исправлено: bool? → явное сравнение
                        .Select(m => new
                        {
                            m.MenuItemID,
                            m.Name,
                            m.Price
                        })
                        .ToList();

                    icItems.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров:\n{ex.Message}", "Ошибка");
            }
        }

        private void BuyItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null)
                return;

            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Клиент не авторизован.", "Ошибка");
                return;
            }

            int menuItemId;
            try
            {
                menuItemId = Convert.ToInt32(btn.Tag);
            }
            catch
            {
                MessageBox.Show("Ошибка идентификатора товара.", "Ошибка");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                    if (client == null)
                    {
                        MessageBox.Show("Клиент не найден.", "Ошибка");
                        return;
                    }

                    var item = ctx.MenuItems.Find(menuItemId);
                    if (item == null || item.Available != true)
                    {
                        MessageBox.Show("Товар не найден или недоступен.", "Ошибка");
                        return;
                    }

                    // Проверка баланса
                    if (client.Balance < item.Price)
                    {
                        MessageBox.Show($"Недостаточно средств.\nТребуется: {item.Price:N0} ₽\nНа балансе: {client.Balance:N0} ₽",
                                        "Недостаточно средств", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Ищем или создаём активный заказ со статусом Pending
                    var activeOrder = ctx.Orders
                        .FirstOrDefault(o => o.ClientID == client.ClientID && o.Status == "Pending");

                    if (activeOrder == null)
                    {
                        activeOrder = new Orders
                        {
                            ClientID = client.ClientID,
                            OrderDate = DateTime.Now,
                            Status = "Pending",
                            TotalAmount = 0m
                        };
                        ctx.Orders.Add(activeOrder);
                        ctx.SaveChanges(); // получаем OrderID
                    }

                    // Добавляем позицию в заказ
                    var orderItem = new OrderItems
                    {
                        OrderID = activeOrder.OrderID,
                        MenuItemID = item.MenuItemID,
                        Quantity = 1,
                        Subtotal = item.Price
                    };
                    ctx.OrderItems.Add(orderItem);

                    // Триггер TRG_OrderItem_UpdateTotal сам обновит TotalAmount в Orders
                    ctx.SaveChanges();

                    // Успех
                    MessageBox.Show($"Добавлено в заказ: {item.Name} за {item.Price:N0} ₽\n\nСумма заказа: {activeOrder.TotalAmount:N0} ₽",
                                    "Товар добавлен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка покупки:\n{ex.Message}", "Ошибка");
            }
        }
    }
}