using System;
using System.Windows;
using System.Windows.Input;
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

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Start();

            MainFrame.Navigated += MainFrame_Navigated;

            MainFrame.Navigate(new RoleSelectionPage());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            tbTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            btnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            UpdatePcNumberVisibility();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
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
                          (MainFrame.Content is ClientLoginPage || MainFrame.Content is ClientRegistrationPage || MainFrame.Content is ClientDashboardPage);

            tbPcNumber.Visibility = showPc ? Visibility.Visible : Visibility.Collapsed;

            if (showPc)
            {
                tbPcNumber.Text = $"{AppConfig.DeviceNumber} {AppConfig.DeviceName}";
            }
        }

        // Публичные методы для режима капчи
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
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.ResizeMode = ResizeMode.NoResize;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}