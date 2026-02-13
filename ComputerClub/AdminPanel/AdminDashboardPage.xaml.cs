using System;
using System.Windows;
using System.Windows.Controls;

namespace ComputerClub.AdminPanel
{
    public partial class AdminDashboardPage : Page
    {
        public AdminDashboardPage()
        {
            InitializeComponent();
            // При загрузке открываем первую страницу (Карта клуба)
            NavigateToPage("ClubMapPage");
            rbMap.IsChecked = true;
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                string pageName = rb.Tag.ToString();
                NavigateToPage(pageName);

                // Можно добавить визуальное выделение, но т.к. RadioButton — оно уже есть
            }
        }

        private void NavigateToPage(string pageName)
        {
            try
            {
                switch (pageName)
                {
                    case "ClubMapPage":
                        MainFrame.Navigate(new ClubMapPage());
                        break;
                    case "DevicesManagementPage":
                        MainFrame.Navigate(new DevicesManagementPage()); // создай позже
                        break;
                    case "SessionsPage":
                        MainFrame.Navigate(new SessionsPage()); // создай позже
                        break;
                    case "MenuManagementPage":
                        MainFrame.Navigate(new MenuManagementPage());
                        break;
                    case "ClientsPage":
                        MainFrame.Navigate(new ClientsPage());
                        break;
                    case "ReportsPage":
                        MainFrame.Navigate(new ReportsPage());
                        break;
                    default:
                        MessageBox.Show("Страница в разработке");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки страницы: {ex.Message}");
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из аккаунта?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                NavigationService?.Navigate(new ComputerClub.Navigation.RoleSelectionPage());
            }
        }
    }
}