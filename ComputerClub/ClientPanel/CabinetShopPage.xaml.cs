using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Data.Entity;

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
            UpdateCartButton();
        }

        private void LoadMenuItems()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var items = ctx.MenuItems
                        .Where(m => m.Available == true)
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

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                var parent = btn.Parent as StackPanel;
                var tbQuantity = parent?.Children.OfType<TextBox>().FirstOrDefault();
                if (tbQuantity == null || !int.TryParse(tbQuantity.Text, out int quantity) || quantity < 1)
                {
                    MessageBox.Show("Укажите количество от 1", "Ошибка");
                    return;
                }

                int menuItemId = Convert.ToInt32(btn.Tag);

                try
                {
                    using (var ctx = new Entities())
                    {
                        var item = ctx.MenuItems.Find(menuItemId);
                        if (item == null || item.Available != true)
                        {
                            MessageBox.Show("Товар недоступен.", "Ошибка");
                            return;
                        }

                        if (!AppConfig.CurrentClientId.HasValue)
                        {
                            MessageBox.Show("Клиент не авторизован.", "Ошибка");
                            return;
                        }

                        var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                        if (client == null)
                        {
                            MessageBox.Show("Клиент не найден.", "Ошибка");
                            return;
                        }

                        var cartOrder = ctx.Orders
                            .FirstOrDefault(o => o.ClientID == client.ClientID && o.Status == "Pending");

                        if (cartOrder == null)
                        {
                            cartOrder = new Orders
                            {
                                ClientID = client.ClientID,
                                OrderDate = DateTime.Now,
                                Status = "Pending",
                                TotalAmount = 0m
                            };
                            ctx.Orders.Add(cartOrder);
                            ctx.SaveChanges();
                        }

                        var existingItem = ctx.OrderItems
                            .FirstOrDefault(oi => oi.OrderID == cartOrder.OrderID && oi.MenuItemID == item.MenuItemID);

                        if (existingItem != null)
                        {
                            existingItem.Quantity += quantity;
                            existingItem.Subtotal = existingItem.Quantity * item.Price;
                        }
                        else
                        {
                            ctx.OrderItems.Add(new OrderItems
                            {
                                OrderID = cartOrder.OrderID,
                                MenuItemID = item.MenuItemID,
                                Quantity = quantity,
                                Subtotal = quantity * item.Price
                            });
                        }

                        ctx.SaveChanges();
                        MessageBox.Show($"Добавлено в корзину: {item.Name} × {quantity}", "Успех");
                        UpdateCartButton();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка добавления:\n{ex.Message}", "Ошибка");
                }
            }
        }

        private void UpdateCartButton()
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                btnCart.Content = "Корзина";
                btnCart.IsEnabled = false;
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var cartOrder = ctx.Orders
                        .FirstOrDefault(o => o.ClientID == AppConfig.CurrentClientId.Value && o.Status == "Pending");

                    if (cartOrder == null || !ctx.OrderItems.Any(oi => oi.OrderID == cartOrder.OrderID))
                    {
                        btnCart.Content = "Корзина";
                        btnCart.IsEnabled = false;
                    }
                    else
                    {
                        int itemCount = ctx.OrderItems.Count(oi => oi.OrderID == cartOrder.OrderID);
                        btnCart.Content = $"Корзина ({itemCount}) — {cartOrder.TotalAmount:N0} ₽";
                        btnCart.IsEnabled = true;
                    }
                }
            }
            catch
            {
                btnCart.Content = "Корзина";
                btnCart.IsEnabled = false;
            }
        }

        private void ShowCart_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Клиент не авторизован.", "Ошибка");
                return;
            }
            try
            {
                using (var ctx = new Entities())
                {
                    var cartOrder = ctx.Orders
                        .FirstOrDefault(o => o.ClientID == AppConfig.CurrentClientId.Value && o.Status == "Pending");
                    if (cartOrder == null || !ctx.OrderItems.Any(oi => oi.OrderID == cartOrder.OrderID))
                    {
                        MessageBox.Show("Корзина пуста.", "Корзина");
                        return;
                    }
                    var window = new Window
                    {
                        WindowStyle = WindowStyle.None,
                        Width = 600,
                        Height = 500,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        Background = new SolidColorBrush(Color.FromRgb(15, 15, 26)),
                        ResizeMode = ResizeMode.NoResize
                    };
                    var grid = new Grid { Margin = new Thickness(20) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var title = new TextBlock
                    {
                        Text = "Корзина",
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 0, 0, 16)
                    };
                    Grid.SetRow(title, 0);
                    grid.Children.Add(title);
                    var list = new ListView
                    {
                        ItemsSource = ctx.OrderItems
                            .Include(oi => oi.MenuItems)
                            .Where(oi => oi.OrderID == cartOrder.OrderID)
                            .ToList(),
                        Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                        Foreground = new SolidColorBrush(Colors.White)
                    };
                    list.ItemTemplate = new DataTemplate
                    {
                        VisualTree = new FrameworkElementFactory(typeof(StackPanel))
                    };
                    var spFactory = list.ItemTemplate.VisualTree;
                    spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                    spFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 4, 0, 4));
                    var nameTb = new FrameworkElementFactory(typeof(TextBlock));
                    nameTb.SetValue(TextBlock.FontSizeProperty, 14.0);
                    nameTb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
                    nameTb.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
                    nameTb.SetValue(TextBlock.WidthProperty, 220.0);
                    nameTb.SetBinding(TextBlock.TextProperty, new Binding("MenuItems.Name"));
                    spFactory.AppendChild(nameTb);
                    var priceTb = new FrameworkElementFactory(typeof(TextBlock));
                    priceTb.SetValue(TextBlock.WidthProperty, 100.0);
                    priceTb.SetBinding(TextBlock.TextProperty, new Binding("Subtotal") { StringFormat = "{0:N0} ₽" });
                    spFactory.AppendChild(priceTb);
                    var qtyTb = new FrameworkElementFactory(typeof(TextBlock));
                    qtyTb.SetValue(TextBlock.WidthProperty, 60.0);
                    qtyTb.SetBinding(TextBlock.TextProperty, new Binding("Quantity"));
                    spFactory.AppendChild(qtyTb);
                    // totalText объявлен ДО лямбды удаления — порядок важен!
                    var totalText = new TextBlock
                    {
                        Text = $"Итого: {cartOrder.TotalAmount:N0} ₽",
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.Yellow),
                        Margin = new Thickness(0, 16, 0, 0)
                    };
                    // Кнопка удаления — здесь totalText ещё не объявлен, но мы его используем после
                    var removeBtn = new FrameworkElementFactory(typeof(Button));
                    removeBtn.SetValue(Button.ContentProperty, "Удалить");
                    removeBtn.SetValue(Button.WidthProperty, 80.0);
                    removeBtn.SetValue(Button.MarginProperty, new Thickness(10, 0, 0, 0));
                    removeBtn.SetValue(Button.StyleProperty, FindResource("FunButton"));
                    removeBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, args) =>
                    {
                        if (((FrameworkElement)s).DataContext is OrderItems item)
                        {
                            ctx.OrderItems.Remove(item);
                            ctx.SaveChanges();
                            list.ItemsSource = ctx.OrderItems
                                .Include(oi => oi.MenuItems)
                                .Where(oi => oi.OrderID == cartOrder.OrderID)
                                .ToList();
                            list.Items.Refresh();
                            UpdateCartButton();
                            totalText.Text = $"Итого: {cartOrder.TotalAmount:N0} ₽"; // ← работает, потому что totalText уже объявлен ниже
                        }
                    }));
                    spFactory.AppendChild(removeBtn);
                    Grid.SetRow(list, 1);
                    grid.Children.Add(list);
                    var bottom = new StackPanel { Orientation = Orientation.Horizontal };
                    var cancelBtn = new Button
                    {
                        Content = "Отмена",
                        Width = 150,
                        Height = 50,
                        Margin = new Thickness(0, 16, 16, 0),
                        Style = (Style)FindResource("FunButton")
                    };
                    cancelBtn.Click += (s, args) => window.Close();
                    var confirmBtn = new Button
                    {
                        Content = "Оформить заказ",
                        Style = (Style)FindResource("FunButton"),
                        Width = 200,
                        Height = 50,
                        Margin = new Thickness(0, 16, 0, 0)
                    };
                    confirmBtn.Click += (s, args) => ConfirmOrder(window, cartOrder.OrderID);
                    bottom.Children.Add(cancelBtn);
                    bottom.Children.Add(confirmBtn);
                    var bottomPanel = new StackPanel { Orientation = Orientation.Vertical };
                    bottomPanel.Children.Add(totalText);
                    bottomPanel.Children.Add(bottom);
                    Grid.SetRow(bottomPanel, 2);
                    grid.Children.Add(bottomPanel);
                    window.Content = grid;
                    window.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
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
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания уведомления: {ex.Message}");
            }
        }
        private void ConfirmOrder(Window window, int orderId)
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var order = ctx.Orders.Find(orderId);
                    if (order == null || order.Status != "Pending")
                    {
                        MessageBox.Show("Заказ не найден или уже оформлен.", "Ошибка");
                        return;
                    }

                    var client = ctx.Clients.Find(AppConfig.CurrentClientId.Value);
                    if (client == null) throw new Exception("Клиент не найден");

                    decimal orderAmount = order.TotalAmount; 
                    decimal clientBalance = client.Balance ?? 0m;

                    if (clientBalance < orderAmount)
                    {
                        decimal shortage = orderAmount - clientBalance;
                        throw new Exception($"Недостаточно средств.\n" +
                                            $"Требуется: {orderAmount:N0} ₽\n" +
                                            $"На балансе: {clientBalance:N0} ₽\n" +
                                            $"Не хватает: {shortage:N0} ₽");
                    }

                    client.Balance = clientBalance - orderAmount;

                    ctx.Transactions.Add(new Transactions
                    {
                        ClientID = client.ClientID,
                        OrderID = order.OrderID,
                        Amount = -orderAmount,
                        Type = "FoodOrder",
                        TransactionDate = DateTime.Now
                    });

                    order.Status = "Processing";
                    ctx.SaveChanges();
                    CreateNotification(client.ClientID,
                               "Заказ оплачен",
                               $"Ваш заказ №{order.OrderID} оплачен на сумму {orderAmount:N0} ₽. Ожидайте доставки!",
                               "OrderPaid");
                    MessageBox.Show($"Заказ №{order.OrderID} оплачен!\nСписано: {orderAmount:N0} ₽\nОжидайте доставки.", "Успех");
                    window.Close();
                    UpdateCartButton();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка оформления");
            }
        }
    }
}