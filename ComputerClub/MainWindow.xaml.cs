using ComputerClub.AdminPanel;
using ComputerClub.ClientPanel;
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

            // Полноэкранный режим (киоск-режим)
            this.WindowState = WindowState.Maximized;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            // this.Topmost = true;   // раскомментируй, если нужно всегда сверху (полный киоск)

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
            btnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            UpdatePcNumberVisibility();
            if (e.Content is AdminDashboardPage ||
            e.Content is ClientsPage ||
            e.Content is DevicesManagementPage ||
            e.Content is MenuManagementPage ||
            e.Content is ReportsPage ||
            e.Content is SessionsPage ||
            e.Content is ClubMapPage ||
            e.Content is ClientDashboardPage)
            {
                btnBack.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnBack.Visibility = Visibility.Visible;
            }
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
                          (MainFrame.Content is ComputerClub.Navigation.ClientLoginPage ||
                           MainFrame.Content is ComputerClub.Navigation.ClientRegistrationPage ||
                           MainFrame.Content is ComputerClub.ClientPanel.ClientDashboardPage);

            tbPcNumber.Visibility = showPc ? Visibility.Visible : Visibility.Collapsed;

            if (showPc)
            {
                tbPcNumber.Text = $"{AppConfig.DeviceNumber} {AppConfig.DeviceName}";
            }
        }

        // Режим капчи (оставлен без изменений)
        public void EnterCaptchaMode()
        {
            btnBack.Visibility = Visibility.Collapsed;
            headerBorder.Visibility = Visibility.Collapsed;
            footerBorder.Visibility = Visibility.Collapsed;
            // В режиме капчи уже нет рамки, но на всякий случай
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
        }

        public void ExitCaptchaMode()
        {
            btnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            headerBorder.Visibility = Visibility.Visible;
            footerBorder.Visibility = Visibility.Visible;
            // В обычном режиме тоже без рамки, поэтому не возвращаем SingleBorderWindow
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
        }

        // Блокировка Alt+F4
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        // Опционально: блокировка закрытия окна через крестик (если вдруг появится)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // e.Cancel = true;   // раскомментируй, если хочешь полностью запретить закрытие
            base.OnClosing(e);
        }
    }
}