using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.ClientPanel
{
    public partial class CabinetLoyaltyPage : Page
    {
        public CabinetLoyaltyPage()
        {
            InitializeComponent();
            Loaded += CabinetLoyaltyPage_Loaded;
        }

        private void CabinetLoyaltyPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTransactions();
        }

        private void LoadTransactions()
        {
            if (!AppConfig.CurrentClientId.HasValue)
            {
                MessageBox.Show("Клиент не авторизован.", "Ошибка");
                dgTransactions.ItemsSource = null;
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    int clientId = AppConfig.CurrentClientId.Value;

                    var transactions = ctx.Transactions
                        .Where(t => t.ClientID == clientId)
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            t.TransactionDate,
                            t.Type,
                            t.Amount
                        })
                        .ToList();

                    

                    dgTransactions.ItemsSource = transactions;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}\n\nДетали:\n{ex.InnerException?.Message ?? "нет"}", "Ошибка");
                dgTransactions.ItemsSource = null;
            }
        }
    }
}