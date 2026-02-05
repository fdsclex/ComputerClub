using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ComputerClub
{
    public partial class ClientRegistrationPage : Page
    {
        private bool isMainPasswordVisible = false;   // по умолчанию СКРЫТО
        private bool isConfirmPasswordVisible = false;

        public ClientRegistrationPage()
        {
            InitializeComponent();
            UpdatePasswordVisibility();  // сразу скрываем пароли
        }

        private void UpdatePasswordVisibility()
        {
            tbPassword.Visibility = isMainPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
            pbPassword.Visibility = isMainPasswordVisible ? Visibility.Collapsed : Visibility.Visible;

            tbConfirmPassword.Visibility = isConfirmPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
            pbConfirmPassword.Visibility = isConfirmPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string fullName = tbFullName.Text.Trim();
            string phone = tbPhone.Text.Trim();
            string email = tbEmail.Text.Trim();
            string password = isMainPasswordVisible ? tbPassword.Text : pbPassword.Password;
            string confirm = isConfirmPasswordVisible ? tbConfirmPassword.Text : pbConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirm))
            {
                MessageBox.Show("Заполните обязательные поля: ФИО, телефон, пароль");
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("Пароли не совпадают");
                return;
            }

            if (phone.Length < 10 || !phone.StartsWith("+7"))
            {
                MessageBox.Show("Введите корректный номер телефона в формате +7...");
                return;
            }

            string gender = rbMale.IsChecked == true ? "M" :
                            rbFemale.IsChecked == true ? "F" : "Other";

            try
            {
                using (var ctx = new Entities())
                {
                    if (ctx.Clients.Any(c => c.Phone == phone))
                    {
                        MessageBox.Show("Этот номер телефона уже зарегистрирован");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(email) && ctx.Clients.Any(c => c.Email == email))
                    {
                        MessageBox.Show("Этот email уже зарегистрирован");
                        return;
                    }

                    string hashedPassword = PasswordHelper.HashPassword(password);

                    var newClient = new Clients
                    {
                        FullName = fullName,
                        Phone = phone,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        Password = hashedPassword,
                        Gender = gender,
                        RegistrationDate = DateTime.Now,
                        Balance = 0
                    };

                    ctx.Clients.Add(newClient);
                    ctx.SaveChanges();

                    MessageBox.Show("Регистрация успешна! Теперь можно войти.");
                    NavigationService.Navigate(new ClientLoginPage());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при регистрации:\n" + ex.Message);
            }
        }

        private void LoginLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new ClientLoginPage());
        }
        private void Phone_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Сохраняем позицию курсора ДО изменения
                int caretIndex = textBox.CaretIndex;

                // Только цифры
                string raw = new string(textBox.Text.Where(char.IsDigit).ToArray());

                // Убираем ведущую 7/8, если ввели
                if (raw.StartsWith("7") || raw.StartsWith("8"))
                    raw = raw.Substring(1);

                // Ограничиваем 10 цифр
                if (raw.Length > 10)
                    raw = raw.Substring(0, 10);

                // Форматируем
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

                // Применяем новый текст
                textBox.Text = formatted;

                // Восстанавливаем курсор
                // Если ввели первую цифру — сдвигаем на длину "+7 ("
                if (caretIndex == 3 && raw.Length == 1) // после "+7" ввели первую цифру
                {
                    caretIndex = 6; // после "+7 ("
                }
                else
                {
                    // Обычная логика
                    int added = formatted.Length - (textBox.Text.Length - caretIndex);
                    caretIndex += added;
                }

                caretIndex = Math.Max(3, Math.Min(caretIndex, formatted.Length));
                textBox.CaretIndex = caretIndex;
                textBox.SelectionLength = 0;
            }
        }
        // Маска телефона (оставил твою последнюю версию)
        private void Phone_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && !char.IsDigit(e.Text[0]))
                e.Handled = true;
        }

        

        // Переключение ТОЛЬКО первого пароля
        private void TogglePassword1_Click(object sender, RoutedEventArgs e)
        {
            isMainPasswordVisible = !isMainPasswordVisible;
            UpdatePasswordVisibility();

            if (isMainPasswordVisible)
            {
                tbPassword.Text = pbPassword.Password;
                tbPassword.Focus();
                tbPassword.CaretIndex = tbPassword.Text.Length;
            }
            else
            {
                pbPassword.Password = tbPassword.Text;
                pbPassword.Focus();
            }
        }

        // Переключение ТОЛЬКО второго пароля
        private void TogglePassword2_Click(object sender, RoutedEventArgs e)
        {
            isConfirmPasswordVisible = !isConfirmPasswordVisible;
            UpdatePasswordVisibility();

            if (isConfirmPasswordVisible)
            {
                tbConfirmPassword.Text = pbConfirmPassword.Password;
                tbConfirmPassword.Focus();
                tbConfirmPassword.CaretIndex = tbConfirmPassword.Text.Length;
            }
            else
            {
                pbConfirmPassword.Password = tbConfirmPassword.Text;
                pbConfirmPassword.Focus();
            }
        }
    }
}