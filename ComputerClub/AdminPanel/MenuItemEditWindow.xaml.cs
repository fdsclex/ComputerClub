using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
{
    public partial class MenuItemEditWindow : Window
    {
        private readonly bool _isNew;
        private readonly int? _itemId;

        public MenuItemEditWindow(bool isNew, int? itemId = null)
        {
            InitializeComponent();
            _isNew = isNew;
            _itemId = itemId;

            if (!_isNew && _itemId.HasValue)
                LoadItem();
        }

        private void LoadItem()
        {
            using (var ctx = new Entities())
            {
                var item = ctx.MenuItems.Find(_itemId);
                if (item != null)
                {
                    tbName.Text = item.Name ?? "";
                    cbType.SelectedItem = cbType.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == item.Type);
                    sliderPrice.Value = (double)item.Price;
                    tbDescription.Text = item.Description ?? "";
                    cbAvailable.IsChecked = item.Available;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = tbName.Text.Trim();
            string type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Food";
            decimal price = (decimal)sliderPrice.Value;  // берём с ползунка
            string description = tbDescription.Text.Trim();
            bool available = cbAvailable.IsChecked ?? true;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Заполните название.");
                return;
            }

            if (price <= 0)
            {
                MessageBox.Show("Цена должна быть больше 0.");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    MenuItems item;
                    if (_isNew)
                    {
                        item = new MenuItems();
                        ctx.MenuItems.Add(item);
                    }
                    else
                    {
                        item = ctx.MenuItems.Find(_itemId);
                    }

                    item.Name = name;
                    item.Type = type;
                    item.Price = price;
                    item.Description = description;
                    item.Available = available;

                    ctx.SaveChanges();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n{ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}