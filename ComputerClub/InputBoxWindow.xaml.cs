using System;
using System.Windows;
using System.Windows.Input;

namespace ComputerClub
{
    public partial class InputBoxWindow : Window
    {
        public decimal Result { get; private set; }

        public InputBoxWindow(string title, string defaultValue = "100", string currentBalanceText = "")
        {
            InitializeComponent();
            tbTitle.Text = title;
            tbAmount.Text = defaultValue;
            tbCurrentBalance.Text = currentBalanceText;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(tbAmount.Text, out decimal value) && value > 0)
            {
                Result = value;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Введите положительное число.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void tbAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, 0) && e.Text != "," && e.Text != ".")
                e.Handled = true;
        }
    }
}