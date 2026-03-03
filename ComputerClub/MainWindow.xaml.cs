using ComputerClub.AdminPanel;
using ComputerClub.ClientPanel;
using ComputerClub.Navigation;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace ComputerClub
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();

            this.WindowState = WindowState.Maximized;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Start();

            MainFrame.Navigated += MainFrame_Navigated;
            MainFrame.Navigate(new ComputerClub.Navigation.RoleSelectionPage());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            tbTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var noBackPages = new[]
            {
        typeof(AdminDashboardPage),
        typeof(ClientsPage),
        typeof(DevicesManagementPage),
        typeof(MenuManagementPage),
        typeof(ReportsPage),
        typeof(SessionsPage),
        typeof(ClubMapPage),
        typeof(ClientShellPage),
        typeof(ClientCabinetPage),
        typeof(BookingPage),
        typeof(RoleSelectionPage),

            };

            bool isProtectedPage = e.Content != null &&
                                  noBackPages.Contains(e.Content.GetType());

            btnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            btnBack.IsEnabled = !isProtectedPage;

            UpdatePcNumberVisibility();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (!btnBack.IsEnabled) return;
            if (MainFrame.CanGoBack)
                MainFrame.GoBack();
        }

        private void Language_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Смена языка пока не реализована");
        }

        private void UpdatePcNumberVisibility()
        {
            bool showPc = AppConfig.IsOnSite && AppConfig.DeviceNumber.HasValue &&
                          (MainFrame.Content is ComputerClub.Navigation.ClientLoginPage ||
                           MainFrame.Content is ComputerClub.Navigation.ClientRegistrationPage ||
                           MainFrame.Content is ComputerClub.ClientPanel.ClientShellPage ||
                           MainFrame.Content is ComputerClub.ClientPanel.ClientCabinetPage);

            tbPcNumber.Visibility = showPc ? Visibility.Visible : Visibility.Collapsed;

            if (showPc)
            {
                tbPcNumber.Text = $"{AppConfig.DeviceNumber} {AppConfig.DeviceName}";
            }
        }

        public void EnterCaptchaMode()
        {
            btnBack.Visibility = Visibility.Collapsed;
            headerBorder.Visibility = Visibility.Collapsed;
            footerBorder.Visibility = Visibility.Collapsed;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
        }

        public void ExitCaptchaMode()
        {
            btnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            headerBorder.Visibility = Visibility.Visible;
            footerBorder.Visibility = Visibility.Visible;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }
        private void RulesLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string rulesText =
                "ПРАВИЛА ПОСЕЩЕНИЯ КОМПЬЮТЕРНОГО КЛУБА\n\n" +

                "1. Общие положения\n" +
                "1.1. Настоящие Правила обязательны для исполнения всеми посетителями клуба.\n" +
                "1.2. Посетитель, заходя в клуб, автоматически соглашается с данными Правилами.\n" +
                "1.3. Администрация вправе отказать в посещении без объяснения причин.\n\n" +

                "2. Вход и регистрация\n" +
                "2.1. Минимальный возраст для посещения без сопровождения взрослых — 14 лет.\n" +
                "2.2. Посетители младше 14 лет допускаются только в сопровождении родителей/законных представителей.\n" +
                "2.3. При первом посещении необходимо зарегистрироваться (предъявить документ, удостоверяющий личность).\n\n" +

                "3. Оплата и тарифы\n" +
                "3.1. Оплата производится почасово или пакетами по действующим тарифам.\n" +
                "3.2. Время округляется в большую сторону до 15 минут.\n" +
                "3.3. При недостатке средств на балансе сессия автоматически завершается.\n\n" +

                "4. Поведение в клубе\n" +
                "4.1. Запрещено: громко разговаривать, использовать ненормативную лексику, курить (в т.ч. вейпы), употреблять алкоголь и наркотики.\n" +
                "4.2. Запрещено приносить и употреблять еду и напитки, купленные вне клуба (кроме случаев, разрешённых администрацией).\n" +
                "4.3. Запрещено спать на игровых местах.\n" +
                "4.4. Запрещено занимать более одного места без согласования.\n\n" +

                "5. Техника безопасности и ответственность\n" +
                "5.1. Посетитель несёт полную материальную ответственность за порчу оборудования.\n" +
                "5.2. Запрещено устанавливать любое ПО без разрешения администратора.\n" +
                "5.3. Запрещено проводить любые виды съёмок без письменного разрешения администрации.\n\n" +

                "6. Администрация имеет право\n" +
                "6.1. Проводить проверку личных вещей при подозрении на нарушение.\n" +
                "6.2. Вызвать сотрудников правоохранительных органов при необходимости.\n" +
                "6.3. В одностороннем порядке расторгнуть договор оказания услуг и удалить из клуба.\n\n" +

                "Приятной игры! Администрация клуба";

            var window = new Window
            {
                Title = "Правила посещения",
                Width = 720,
                Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 26)),
                WindowStyle = WindowStyle.SingleBorderWindow
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };

            var textBlock = new TextBlock
            {
                Text = rulesText,
                Foreground = Brushes.White,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24
            };

            scroll.Content = textBlock;
            window.Content = scroll;
            window.ShowDialog();
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            UpdateKeyboardLayoutDisplay();

            // Подписка на изменение (если пользователь сам переключит Win+Пробел)
            InputLanguageManager.Current.InputLanguageChanged += (s, args) =>
            {
                Dispatcher.Invoke(UpdateKeyboardLayoutDisplay);
            };
        }

        private void UpdateKeyboardLayoutDisplay()
        {
            var current = InputLanguageManager.Current.CurrentInputLanguage;
            string code = current?.TwoLetterISOLanguageName.ToUpper() ?? "??";

            string display;
            switch (code)
            {
                case "RU":
                    display = "RU";
                    break;
                case "EN":
                    display = "EN";
                    break;
                case "UK":
                    display = "UA";
                    break;
                default:
                    display = code;
                    break;
            }

            tbKeyboardLayout.Text = display;
        }
    }
}