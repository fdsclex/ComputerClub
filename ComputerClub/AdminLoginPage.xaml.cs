using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub
{
    public partial class AdminLoginPage : Page
    {
        // DependencyProperty для триггера в стиле глазика
        public static readonly DependencyProperty IsPasswordVisibleProperty =
            DependencyProperty.Register(nameof(IsPasswordVisible), typeof(bool), typeof(AdminLoginPage), new PropertyMetadata(false));

        public bool IsPasswordVisible
        {
            get => (bool)GetValue(IsPasswordVisibleProperty);
            set => SetValue(IsPasswordVisibleProperty, value);
        }

        public AdminLoginPage()
        {
            InitializeComponent();
        }

        private void TbAdminLogin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string text = tb.Text.Trim();
                int atIndex = text.IndexOf('@');
                if (atIndex >= 0)
                {
                    string newText = text.Substring(0, atIndex);
                    tb.Text = newText;
                    tb.CaretIndex = newText.Length;
                }
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = tbAdminLogin.Text.Trim();
            string fullEmail = login + "@club.ru";
            string password = IsPasswordVisible ? tbPasswordVisible.Text : pbPassword.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Заполните логин и пароль");
                return;
            }

            using (var ctx = new Entities())
            {
                var employee = ctx.Employees.FirstOrDefault(emp => emp.Email == fullEmail);
                if (employee != null && PasswordHelper.VerifyPassword(password, employee.Password))
                {
                    NavigationService.Navigate(new AdminDashboardPage());
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль");
                }
            }
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            IsPasswordVisible = !IsPasswordVisible;

            if (IsPasswordVisible)
            {
                tbPasswordVisible.Text = pbPassword.Password;
                pbPassword.Visibility = Visibility.Collapsed;
                tbPasswordVisible.Visibility = Visibility.Visible;
                tbPasswordVisible.Focus();
                tbPasswordVisible.CaretIndex = tbPasswordVisible.Text.Length;
            }
            else
            {
                pbPassword.Password = tbPasswordVisible.Text;
                tbPasswordVisible.Visibility = Visibility.Collapsed;
                pbPassword.Visibility = Visibility.Visible;
                pbPassword.Focus();
            }
            // НЕ меняем Content — стиль сам переключит иконку
        }
    }
}