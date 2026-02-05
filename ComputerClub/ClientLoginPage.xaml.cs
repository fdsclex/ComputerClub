using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ComputerClub
{
    public partial class ClientLoginPage : Page
    {
        // DependencyProperty для триггера в стиле глазика
        public static readonly DependencyProperty IsPasswordVisibleProperty =
            DependencyProperty.Register(nameof(IsPasswordVisible), typeof(bool), typeof(ClientLoginPage), new PropertyMetadata(false));

        public bool IsPasswordVisible
        {
            get => (bool)GetValue(IsPasswordVisibleProperty);
            set => SetValue(IsPasswordVisibleProperty, value);
        }

        private int failedAttemptsCount = 0;
        private string currentCaptcha = "";

        public ClientLoginPage()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string phone = tbPhone.Text.Trim();
            string password = IsPasswordVisible ? tbPasswordVisible.Text : pbPassword.Password;

            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Заполните телефон и пароль");
                return;
            }

            using (var ctx = new Entities())
            {
                var client = ctx.Clients.FirstOrDefault(c => c.Phone == phone);
                if (client != null && PasswordHelper.VerifyPassword(password, client.Password))
                {
                    AppConfig.CurrentClientId = client.ClientID;
                    failedAttemptsCount = 0;
                    captchaPanel.Visibility = Visibility.Collapsed;
                    loginPanel.Visibility = Visibility.Visible;
                    ((MainWindow)Application.Current.MainWindow).ExitCaptchaMode();
                    NavigationService.Navigate(new ClientDashboardPage());
                }
                else
                {
                    failedAttemptsCount++;

                    if (failedAttemptsCount >= 3)
                    {
                        GenerateCaptcha();
                        captchaPanel.Visibility = Visibility.Visible;
                        loginPanel.Visibility = Visibility.Collapsed;
                        ((MainWindow)Application.Current.MainWindow).EnterCaptchaMode();
                        tbCaptchaInput.Focus();
                    }
                    else
                    {
                        int remaining = 3 - failedAttemptsCount;
                        MessageBox.Show($"Неверный телефон или пароль. Осталось попыток: {remaining}");
                    }
                }
            }
        }

        private void GenerateCaptcha()
        {
            captchaCanvas.Children.Clear();

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random rnd = new Random();
            int length = rnd.Next(5, 7);

            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[rnd.Next(chars.Length)];
            }

            currentCaptcha = new string(result);

            TextBlock tbCaptcha = new TextBlock
            {
                Text = currentCaptcha,
                FontSize = rnd.Next(36, 48),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(150, 255), (byte)rnd.Next(150, 255), (byte)rnd.Next(150, 255))),
                RenderTransform = new RotateTransform(rnd.Next(-20, 20))
            };

            Canvas.SetLeft(tbCaptcha, rnd.Next(20, 100));
            Canvas.SetTop(tbCaptcha, rnd.Next(20, 40));
            captchaCanvas.Children.Add(tbCaptcha);

            // Шум: линии
            for (int i = 0; i < 10; i++)
            {
                Line line = new Line
                {
                    X1 = rnd.Next(0, 300),
                    Y1 = rnd.Next(0, 80),
                    X2 = rnd.Next(0, 300),
                    Y2 = rnd.Next(0, 80),
                    Stroke = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255))),
                    StrokeThickness = rnd.Next(1, 4)
                };
                captchaCanvas.Children.Add(line);
            }

            // Шум: точки
            for (int i = 0; i < 80; i++)
            {
                Ellipse dot = new Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = Brushes.White,
                    Opacity = rnd.NextDouble() * 0.6 + 0.2
                };
                Canvas.SetLeft(dot, rnd.Next(0, 300));
                Canvas.SetTop(dot, rnd.Next(0, 80));
                captchaCanvas.Children.Add(dot);
            }

            tbCaptchaInput.Clear();
            tbCaptchaInput.Focus();
        }

        private void ConfirmCaptcha_Click(object sender, RoutedEventArgs e)
        {
            string input = tbCaptchaInput.Text.Trim().ToUpper();

            if (input == currentCaptcha)
            {
                failedAttemptsCount = 0;
                captchaPanel.Visibility = Visibility.Collapsed;
                loginPanel.Visibility = Visibility.Visible;
                ((MainWindow)Application.Current.MainWindow).ExitCaptchaMode();
                MessageBox.Show("Капча пройдена. Попробуйте войти снова.");
            }
            else
            {
                MessageBox.Show("Неверно. Попробуйте снова.");
                GenerateCaptcha();
            }
        }

        private void RefreshCaptcha_Click(object sender, RoutedEventArgs e)
        {
            GenerateCaptcha();
        }

        private void TbCaptchaInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmCaptcha_Click(sender, e);
                e.Handled = true;
            }
        }

        // Переключение видимости пароля (без изменения Content — стиль сам сделает)
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
        }

        // Остальные методы остаются без изменений
        private void Phone_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && !char.IsDigit(e.Text[0]))
                e.Handled = true;
        }

        private void Phone_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                int caretIndex = textBox.CaretIndex;
                string raw = new string(textBox.Text.Where(char.IsDigit).ToArray());

                if (raw.StartsWith("7") || raw.StartsWith("8"))
                    raw = raw.Substring(1);

                if (raw.Length > 10)
                    raw = raw.Substring(0, 10);

                string formatted = "+7";
                if (raw.Length > 0)
                    formatted += " (" + raw.Substring(0, Math.Min(3, raw.Length));
                if (raw.Length > 3)
                    formatted += ") " + raw.Substring(3, Math.Min(3, raw.Length - 3));
                if (raw.Length > 6)
                    formatted += "-" + raw.Substring(6, Math.Min(2, raw.Length - 6));
                if (raw.Length > 8)
                    formatted += "-" + raw.Substring(8, Math.Min(2, raw.Length - 8));

                if (textBox.Text == formatted)
                    return;

                textBox.Text = formatted;

                if (caretIndex <= 3 && raw.Length > 0)
                    caretIndex = formatted.IndexOf('(') + 1 + Math.Min(1, raw.Length);
                else
                {
                    int delta = formatted.Length - textBox.Text.Length + caretIndex;
                    caretIndex += delta;
                }

                caretIndex = Math.Max(3, Math.Min(caretIndex, formatted.Length));
                textBox.CaretIndex = caretIndex;
                textBox.SelectionLength = 0;
            }
        }

        private void Register_Click(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new ClientRegistrationPage());
        }

        private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Восстановление пароля пока не реализовано");
        }
    }
}