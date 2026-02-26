using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ComputerClub.Navigation
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

        // Для восстановления пароля
        private string resetCode = "";
        private DateTime codeSentTime;
        private const int CODE_VALID_MINUTES = 5;

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
                MessageBox.Show("Заполните телефон и пароль", "Ошибка ввода");
                return;
            }

            // Проверка, что устройство выбрано (для режима "на месте")
            if (AppConfig.IsOnSite && !AppConfig.DeviceNumber.HasValue)
            {
                MessageBox.Show("Устройство не выбрано. Вернитесь назад и выберите ПК/консоль.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? currentDeviceId = AppConfig.DeviceNumber;

            using (var ctx = new Entities())
            {
                // 1. Проверяем активную сессию на этом устройстве (только если режим "на месте")
                if (currentDeviceId.HasValue)
                {
                    var activeSession = ctx.Sessions
                        .FirstOrDefault(s => s.DeviceID == currentDeviceId.Value &&
                                             s.EndTime == null &&
                                             s.Status == "Active");

                    if (activeSession != null)
                    {
                        // Есть активная сессия — проверяем, наш ли это клиент
                        var clientTemp = ctx.Clients.FirstOrDefault(c => c.Phone == phone);

                        if (clientTemp == null || clientTemp.ClientID != activeSession.ClientID)
                        {
                            MessageBox.Show(
                                $"Устройство №{currentDeviceId} уже занято другим пользователем.\n" +
                                "Сейчас на нём идёт активная сессия.\n\n" +
                                "Выберите другое свободное устройство или дождитесь завершения сессии.",
                                "Устройство занято",
                                MessageBoxButton.OK,
                                MessageBoxImage.Stop
                            );
                            return;
                        }
                        // Если это наш клиент → пускаем, продолжаем сессию
                    }
                }

                // 2. Проверка логина
                var client = ctx.Clients.FirstOrDefault(c => c.Phone == phone);

                if (client != null && PasswordHelper.VerifyPassword(password, client.Password))
                {
                    AppConfig.CurrentClientId = client.ClientID;
                    failedAttemptsCount = 0;
                    captchaPanel.Visibility = Visibility.Collapsed;
                    loginPanel.Visibility = Visibility.Visible;
                    ((MainWindow)Application.Current.MainWindow).ExitCaptchaMode();

                    // Решаем, куда перейти после логина
                    if (AppConfig.NavigateToBookingAfterLogin)
                    {
                        AppConfig.NavigateToBookingAfterLogin = false;
                        NavigationService.Navigate(new BookingPage());
                    }
                    else
                    {
                        NavigationService.Navigate(new ComputerClub.ClientPanel.ClientShellPage());
                    }
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

        // ------------------ Восстановление пароля ------------------

        private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            loginPanel.Visibility = Visibility.Collapsed;
            resetPanel.Visibility = Visibility.Visible;
            tbResetPhone.Text = tbPhone.Text.Trim();
            tbResetPhone.Focus();
        }

        private void SendResetCode_Click(object sender, RoutedEventArgs e)
        {
            string phone = tbResetPhone.Text.Trim();

            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 12)
            {
                MessageBox.Show("Введите корректный номер телефона", "Ошибка");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.FirstOrDefault(c => c.Phone == phone);
                    if (client == null)
                    {
                        MessageBox.Show("Пользователь с таким номером не найден", "Не найдено");
                        return;
                    }

                    Random rnd = new Random();
                    resetCode = rnd.Next(100000, 999999).ToString();
                    codeSentTime = DateTime.Now;

                    // Тестовый вывод кода
                    MessageBox.Show($"Ваш код подтверждения (тестовый режим): {resetCode}\nВ реальном проекте придёт SMS.",
                                    "Код отправлен");

                    resetCodePanel.Visibility = Visibility.Visible;
                    tbResetCode.Focus();

                    tbResetTimer.Text = $"Код действителен {CODE_VALID_MINUTES} минут";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string codeInput = tbResetCode.Text.Trim();
            string newPassword = pbNewPassword.Password;

            if (codeInput.Length != 6 || !int.TryParse(codeInput, out _))
            {
                MessageBox.Show("Введите 6-значный код", "Ошибка");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                MessageBox.Show("Новый пароль должен быть не короче 6 символов", "Ошибка");
                return;
            }

            if (codeInput != resetCode)
            {
                MessageBox.Show("Неверный код подтверждения", "Ошибка");
                return;
            }

            if ((DateTime.Now - codeSentTime).TotalMinutes > CODE_VALID_MINUTES)
            {
                MessageBox.Show($"Код устарел (действует {CODE_VALID_MINUTES} минут)", "Код истёк");
                return;
            }

            try
            {
                using (var ctx = new Entities())
                {
                    var client = ctx.Clients.FirstOrDefault(c => c.Phone == tbResetPhone.Text.Trim());
                    if (client == null)
                    {
                        MessageBox.Show("Клиент не найден", "Ошибка");
                        return;
                    }

                    client.Password = PasswordHelper.HashPassword(newPassword);
                    ctx.SaveChanges();

                    MessageBox.Show("Пароль успешно изменён!\nТеперь можете войти с новым паролем.", "Успех");

                    // Возврат к форме логина
                    resetPanel.Visibility = Visibility.Collapsed;
                    loginPanel.Visibility = Visibility.Visible;
                    tbPhone.Text = tbResetPhone.Text;
                    pbPassword.Password = "";
                    tbPasswordVisible.Text = "";
                    tbPhone.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка изменения пароля:\n{ex.Message}", "Ошибка");
            }
        }

        // ------------------ Капча (без изменений) ------------------

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

        // Переключение видимости пароля
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

        // Форматирование телефона
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
    }
}