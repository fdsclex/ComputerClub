using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
{
    public partial class ClientsPage : Page
    {
        private List<object> _allClients = new List<object>(); // для поиска

        public ClientsPage()
        {
            InitializeComponent();
            LoadClients();
        }

        private void LoadClients(string searchPhone = null)
        {
            try
            {
                using (var ctx = new Entities())
                {
                    var query = ctx.Clients.AsQueryable();

                    if (!string.IsNullOrWhiteSpace(searchPhone))
                    {
                        query = query.Where(c => c.Phone.Contains(searchPhone));
                    }

                    var clients = query
                        .Select(c => new
                        {
                            c.ClientID,
                            c.FullName,
                            c.Phone,
                            c.Email,
                            c.Balance,
                            c.RegistrationDate
                        })
                        .OrderBy(c => c.FullName)
                        .ToList<object>();

                    dgClients.ItemsSource = clients;
                    _allClients = clients; // сохраняем для поиска

                    tbInfo.Text = string.IsNullOrWhiteSpace(searchPhone)
                        ? $"Всего клиентов: {clients.Count}"
                        : $"Найдено по телефону: {clients.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки клиентов:\n{ex.Message}");
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            tbSearchPhone.Text = "";
            LoadClients();
        }

        private void tbSearchPhone_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = tbSearchPhone.Text.Trim();
            LoadClients(search);
        }

        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgClients.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            if (MessageBox.Show($"Удалить клиента {sel.FullName} (ID {sel.ClientID})?\nЭто действие нельзя отменить.",
                                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var ctx = new Entities())
                    {
                        var client = ctx.Clients.Find(sel.ClientID);
                        if (client != null)
                        {
                            ctx.Clients.Remove(client);
                            ctx.SaveChanges();
                            MessageBox.Show("Клиент удалён.");
                            LoadClients(tbSearchPhone.Text.Trim());
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления:\n{ex.Message}");
                }
            }
        }

        private void ReplenishBalance_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgClients.SelectedItem;
            if (selected == null) return;

            dynamic sel = selected;

            string title = $"Пополнение баланса для {sel.FullName}";
            string currentBalanceText = $"Текущий баланс: {sel.Balance:N0} ₽";

            var inputWindow = new InputBoxWindow(title, "100", currentBalanceText);
            if (inputWindow.ShowDialog() != true) return;

            decimal amount = inputWindow.Result;
            if (amount <= 0)
            {
                MessageBox.Show("Сумма должна быть больше 0.", "Ошибка");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.Find(sel.ClientID);
                    if (client == null)
                    {
                        MessageBox.Show("Клиент не найден в базе.", "Ошибка");
                        return;
                    }

                    // Пополняем баланс
                    client.Balance += amount;

                    // Создаём запись в транзакциях
                    ctx.Transactions.Add(new Transactions
                    {
                        ClientID = client.ClientID,
                        Amount = amount,
                        Type = "Deposit",
                        TransactionDate = DateTime.Now,
                    });

                    ctx.Notifications.Add(new Notifications
                    {
                        ClientID = client.ClientID,
                        Title = "Баланс пополнен",
                        Message = $"Ваш баланс пополнен на {amount:N0} ₽ администратором.\n" +
                                  $"Текущий баланс: {client.Balance:N0} ₽",
                        Type = "BalanceReplenished",   // или "AdminDeposit", "BalanceChange"
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    ctx.SaveChanges();

                    MessageBox.Show(
                        $"Баланс пополнен на {amount:N0} ₽.\n" +
                        $"Новый баланс: {client.Balance:N0} ₽.\n" +
                        "Клиент получил уведомление.",
                        "Успех");

                    LoadClients(tbSearchPhone.Text.Trim());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка пополнения:\n{ex.Message}", "Ошибка");
            }
        }
    }
}