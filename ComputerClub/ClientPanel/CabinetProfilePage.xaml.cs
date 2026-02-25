using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ComputerClub.ClientPanel
{
    public partial class CabinetProfilePage : Page
    {
        public CabinetProfilePage()
        {
            InitializeComponent();
            Loaded += CabinetProfilePage_Loaded;
        }

        private void CabinetProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProfile();
        }

        private void LoadProfile()
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
                    int clientId = AppConfig.CurrentClientId.Value;

                    var client = ctx.Clients.FirstOrDefault(c => c.ClientID == clientId);

                    if (client == null)
                    {
                        MessageBox.Show("Профиль клиента не найден.", "Ошибка");
                        return;
                    }

                    tbFullName.Text = client.FullName ?? "Не указано";
                    tbPhone.Text = client.Phone ?? "Не указано";
                    tbEmail.Text = client.Email ?? "Не указан";

                    // Обычный switch вместо switch-выражения (для C# 7.3)
                    switch (client.Gender)
                    {
                        case "M":
                            tbGender.Text = "Мужской";
                            break;
                        case "F":
                            tbGender.Text = "Женский";
                            break;
                        case "Other":
                            tbGender.Text = "Другой";
                            break;
                        default:
                            tbGender.Text = "Не указан";
                            break;
                    }
                    tbRegistrationDate.Text = client.RegistrationDate?.ToString("dd.MM.yyyy") ?? "Не указана";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки профиля:\n{ex.Message}", "Ошибка");
            }
        }
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

                if (caretIndex == 3 && raw.Length == 1)
                    caretIndex = 6;
                else
                {
                    int added = formatted.Length - textBox.Text.Length + caretIndex;
                    caretIndex += added;
                }

                caretIndex = Math.Max(3, Math.Min(caretIndex, formatted.Length));
                textBox.CaretIndex = caretIndex;
                textBox.SelectionLength = 0;
            }
        }
        private void EditProfile_Click(object sender, RoutedEventArgs e)
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
                    int clientId = AppConfig.CurrentClientId.Value;
                    var client = ctx.Clients.FirstOrDefault(c => c.ClientID == clientId);

                    if (client == null)
                    {
                        MessageBox.Show("Клиент не найден.", "Ошибка");
                        return;
                    }

                    // Окно редактирования — без верхней панели
                    var editWindow = new Window
                    {
                        Title = "Редактировать профиль",
                        Width = 450,
                        Height = 420,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        Background = new SolidColorBrush(Color.FromRgb(15, 15, 26)),
                        WindowStyle = WindowStyle.None,
                        ResizeMode = ResizeMode.NoResize
                    };

                    var editGrid = new Grid { Margin = new Thickness(30) };
                    editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // заголовок
                    editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // ФИО
                    editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Телефон
                    editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Email
                    editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кнопки

                    // Заголовок
                    var header = new TextBlock
                    {
                        Text = "Редактировать профиль",
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 0, 0, 20),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetRow(header, 0);
                    editGrid.Children.Add(header);

                    // ФИО
                    var lblName = new TextBlock { Text = "ФИО:", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 0, 0, 4) };
                    var tbEditName = new TextBox { Text = client.FullName ?? "", Margin = new Thickness(0, 0, 0, 12), Style = (Style)FindResource("DarkInput") };
                    Grid.SetRow(lblName, 1);
                    Grid.SetRow(tbEditName, 1);
                    editGrid.Children.Add(lblName);
                    editGrid.Children.Add(tbEditName);

                    // Телефон с маской
                    var lblPhone = new TextBlock { Text = "Телефон:", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 0, 0, 4) };
                    var tbEditPhone = new TextBox { Text = client.Phone ?? "", Margin = new Thickness(0, 0, 0, 12), Style = (Style)FindResource("DarkInput") };
                    tbEditPhone.PreviewTextInput += Phone_PreviewTextInput;
                    tbEditPhone.TextChanged += Phone_TextChanged;
                    tbEditPhone.GotFocus += (s, args) => tbEditPhone.SelectAll(); // выделяем весь текст при фокусе
                    Grid.SetRow(lblPhone, 2);
                    Grid.SetRow(tbEditPhone, 2);
                    editGrid.Children.Add(lblPhone);
                    editGrid.Children.Add(tbEditPhone);

                    // Email
                    var lblEmail = new TextBlock { Text = "Email:", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 0, 0, 4) };
                    var tbEditEmail = new TextBox { Text = client.Email ?? "", Margin = new Thickness(0, 0, 0, 20), Style = (Style)FindResource("DarkInput") };
                    Grid.SetRow(lblEmail, 3);
                    Grid.SetRow(tbEditEmail, 3);
                    editGrid.Children.Add(lblEmail);
                    editGrid.Children.Add(tbEditEmail);

                    // Кнопки
                    var btnSave = new Button
                    {
                        Content = "Сохранить",
                        Style = (Style)FindResource("FunButton"),
                        Width = 150,
                        Height = 45,
                        Margin = new Thickness(0, 0, 10, 0)
                    };

                    btnSave.Click += (s, args) =>
                    {
                        try
                        {
                            string newFullName = tbEditName.Text.Trim();
                            string newPhone = tbEditPhone.Text.Trim();
                            string newEmail = string.IsNullOrWhiteSpace(tbEditEmail.Text) ? null : tbEditEmail.Text.Trim();

                            // Проверка ФИО
                            if (string.IsNullOrWhiteSpace(newFullName))
                            {
                                MessageBox.Show("Укажите ФИО.", "Ошибка");
                                tbEditName.Focus();
                                return;
                            }

                            // Проверка телефона
                            string phoneDigits = new string(newPhone.Where(char.IsDigit).ToArray());
                            if (string.IsNullOrWhiteSpace(newPhone) || !newPhone.StartsWith("+7") || phoneDigits.Length != 11)
                            {
                                MessageBox.Show("Введите корректный номер телефона в формате +7 (XXX) XXX-XX-XX", "Ошибка");
                                tbEditPhone.Focus();
                                return;
                            }

                            // Проверка уникальности телефона (кроме текущего клиента)
                            if (ctx.Clients.Any(c => c.Phone == newPhone && c.ClientID != clientId))
                            {
                                MessageBox.Show("Этот номер телефона уже используется.", "Ошибка");
                                tbEditPhone.Focus();
                                return;
                            }

                            // Проверка email (если указан)
                            if (!string.IsNullOrWhiteSpace(newEmail))
                            {
                                if (!newEmail.Contains("@") || !newEmail.Contains("."))
                                {
                                    MessageBox.Show("Введите корректный email.", "Ошибка");
                                    tbEditEmail.Focus();
                                    return;
                                }

                                if (ctx.Clients.Any(c => c.Email == newEmail && c.ClientID != clientId))
                                {
                                    MessageBox.Show("Этот email уже используется.", "Ошибка");
                                    tbEditEmail.Focus();
                                    return;
                                }
                            }

                            // Сохраняем изменения
                            client.FullName = newFullName;
                            client.Phone = newPhone;
                            client.Email = newEmail;

                            ctx.SaveChanges();

                            MessageBox.Show("Профиль обновлён.", "Успех");
                            editWindow.Close();
                            LoadProfile(); // обновляем страницу
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка");
                        }
                    };

                    var btnCancel = new Button
                    {
                        Content = "Отмена",
                        Width = 150,
                        Height = 45,
                        Style = (Style)FindResource("FunButton")
                    };
                    btnCancel.Click += (s, args) => editWindow.Close();

                    var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    btnPanel.Children.Add(btnSave);
                    btnPanel.Children.Add(btnCancel);

                    Grid.SetRow(btnPanel, 4);
                    editGrid.Children.Add(btnPanel);

                    editWindow.Content = editGrid;
                    editWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия редактирования:\n{ex.Message}", "Ошибка");
            }
        }
    }
}