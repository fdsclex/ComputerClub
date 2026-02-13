using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
{
    public partial class MenuManagementPage : Page
    {
        private List<object> _allMenuItems = new List<object>();

        public MenuManagementPage()
        {
            InitializeComponent();

            // Загрузка данных после полной инициализации страницы
            Loaded += (s, e) =>
            {
                LoadData();
                Loaded -= MenuManagementPage_Loaded; // отписка
            };
        }

        private void MenuManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var activeOrders = ctx.Orders
                        .Where(o => o.OrderDate >= DateTime.Today && o.Status != "Completed" && o.Status != "Cancelled")
                        .Select(o => new
                        {
                            o.OrderID,
                            ClientFullName = o.Clients.FullName,
                            o.OrderDate,
                            o.Status,
                            o.TotalAmount
                        })
                        .ToList();

                    dgActiveOrders.ItemsSource = activeOrders;

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

                    tbInfo.Text = $"Активных заказов сегодня: {activeOrders.Count} | Товаров в меню: {menuItems.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}\n\nДетали:\n{ex.InnerException?.Message ?? "Нет"}");
            }
        }

        private void ApplyMenuFilters()
        {
            if (dgMenuItems == null) return; // защита

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

        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgActiveOrders?.SelectedItem;
            if (selected == null) return;
            dynamic sel = selected;
            if (MessageBox.Show($"Удалить заказ #{sel.OrderID} клиента {sel.ClientFullName}?",
                                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var order = ctx.Orders.Find(sel.OrderID);
                        if (order != null)
                        {
                            ctx.Orders.Remove(order);
                            ctx.SaveChanges();
                            MessageBox.Show("Заказ удалён.");
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