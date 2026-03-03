using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ComputerClub.AdminPanel
{
    public partial class ReportsPage : Page
    {
        // Простой класс для хранения данных графика
        private class DailyRevenue
        {
            public DateTime Date { get; set; }
            public decimal Sum { get; set; }
        }

        private List<DailyRevenue> dailyDataCache;

        public ReportsPage()
        {
            InitializeComponent();

            // Не вызываем RefreshReports здесь!

            // Подписываемся на Loaded один раз
            Loaded += ReportsPage_Loaded;
            dpFrom.SelectedDateChanged += DatePicker_SelectedDateChanged;
            dpTo.SelectedDateChanged += DatePicker_SelectedDateChanged;
        }

        private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Сначала устанавливаем индекс без вызова событий
            cbPeriod.SelectionChanged -= cbPeriod_SelectionChanged;  // отключаем временно
            cbPeriod.SelectedIndex = 0;
            cbPeriod.SelectionChanged += cbPeriod_SelectionChanged;  // возвращаем

            // Теперь безопасно обновляем
            RefreshReports();

            Loaded -= ReportsPage_Loaded;
        }

        private void ReportsPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (dailyDataCache != null)
            {
                DrawChart(dailyDataCache);
            }
        }

        private void cbPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpFrom == null || dpTo == null) return;
            if (cbPeriod.SelectedIndex < 0) return;

            bool isCustom = (cbPeriod.SelectedIndex == 4);

            dpFrom.IsEnabled = isCustom;
            dpTo.IsEnabled = isCustom;

            if (!isCustom)
            {
                dpFrom.SelectedDate = null;
                dpTo.SelectedDate = null;
            }

            // Обновляем отчёты сразу после смены режима
            RefreshReports();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbPeriod.SelectedIndex != 4) return;

            // Если хотя бы одна дата не выбрана — просто обновляем отчёты (без проверки порядка)
            if (!dpFrom.SelectedDate.HasValue || !dpTo.SelectedDate.HasValue)
            {
                RefreshReports();
                return;
            }

            // Теперь обе даты есть → можно безопасно брать .Value
            DateTime from = dpFrom.SelectedDate.Value.Date;
            DateTime to = dpTo.SelectedDate.Value.Date;

            if (from > to)
            {
                // Меняем местами
                dpFrom.SelectedDate = to;
                dpTo.SelectedDate = from;

                // Опционально: уведомление (можно закомментировать, если раздражает)
                MessageBox.Show(
                    "Дата начала была позже даты окончания — даты автоматически поменяны местами.",
                    "Порядок исправлен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            RefreshReports();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshReports();
        }

        private void RefreshReports()
        {
            if (!IsLoaded) return;
            if (tbRevenue == null || canvasChart == null || tbNoData == null)
            {
                System.Diagnostics.Debug.WriteLine("UI элементы ещё не инициализированы!");
                return;
            }
            try
            {
                using (var ctx = new Entities())
                {
                    var range = GetDateRange();
                    DateTime start = range.Item1;
                    DateTime end = range.Item2;
                    if (start == DateTime.MinValue) return;

                    // ─────────────────────────────────────────────────────────────
                    // Общая выручка за период
                    // ─────────────────────────────────────────────────────────────
                    decimal totalRevenue = ctx.Transactions
                        .Where(t => t.TransactionDate >= start
                                 && t.TransactionDate < end
                                 && t.Amount < 0
                                 && (t.Type == "Withdrawal" || t.Type == "FoodOrder" || t.Type == "SessionWithdrawal"))
                        .Sum(t => (decimal?)-t.Amount) ?? 0m;

                    tbRevenue.Text = totalRevenue.ToString("N0") + " ₽";

                    // ─────────────────────────────────────────────────────────────
                    // Данные по дням для графика
                    // ─────────────────────────────────────────────────────────────
                    var dailyDataTemp = ctx.Transactions
                        .Where(t => t.TransactionDate >= start
                                 && t.TransactionDate < end
                                 && t.Amount < 0
                                 && (t.Type == "Withdrawal" || t.Type == "FoodOrder" || t.Type == "SessionWithdrawal"))
                        .GroupBy(t => DbFunctions.TruncateTime(t.TransactionDate))
                        .Select(g => new
                        {
                            Date = g.Key ?? DateTime.MinValue,
                            Sum = g.Sum(t => (decimal?)-t.Amount) ?? 0m
                        })
                        .Where(x => x.Date != DateTime.MinValue)
                        .OrderBy(x => x.Date)
                        .ToList();

                    dailyDataCache = dailyDataTemp
                        .Select(d => new DailyRevenue
                        {
                            Date = d.Date,
                            Sum = d.Sum
                        })
                        .ToList();

                    DrawChart(dailyDataCache);

                    // ─────────────────────────────────────────────────────────────
                    // Топ-5 товаров — без .First() внутри Select
                    // ─────────────────────────────────────────────────────────────
                    var topItems = ctx.OrderItems
                        .Join(ctx.Orders, oi => oi.OrderID, o => o.OrderID, (oi, o) => new { oi, o })
                        .Where(x => x.o.OrderDate >= start && x.o.OrderDate < end && (x.o.Status == "Delivered" || x.o.Status == "Completed"))
                        .GroupBy(x => x.oi.MenuItemID)
                        .Select(g => new
                        {
                            MenuItemID = g.Key,
                            Quantity = g.Sum(x => x.oi.Quantity),
                            Total = g.Sum(x => (decimal?)x.oi.Subtotal) ?? 0m
                        })
                        .Join(ctx.MenuItems, g => g.MenuItemID, m => m.MenuItemID, (g, m) => new
                        {
                            Name = m.Name,
                            Quantity = g.Quantity,
                            Total = g.Total
                        })
                        .OrderByDescending(x => x.Total)
                        .Take(5)
                        .ToList();

                    dgTopItems.ItemsSource = topItems;

                    // ─────────────────────────────────────────────────────────────
                    // Топ-5 клиентов — тоже без .First()
                    // ─────────────────────────────────────────────────────────────
                    var topClients = ctx.Transactions
                        .Where(t => t.TransactionDate >= start
                                 && t.TransactionDate < end
                                 && t.Amount < 0
                                 && (t.Type == "Withdrawal" || t.Type == "FoodOrder" || t.Type == "SessionWithdrawal"))
                        .GroupBy(t => t.ClientID)
                        .Select(g => new
                        {
                            ClientID = g.Key,
                            TotalSpent = g.Sum(t => -t.Amount)
                        })
                        .Join(ctx.Clients, g => g.ClientID, c => c.ClientID, (g, c) => new
                        {
                            FullName = c.FullName ?? "Гость",
                            Total = g.TotalSpent
                        })
                        .OrderByDescending(x => x.Total)
                        .Take(5)
                        .ToList();

                    dgTopClients.ItemsSource = topClients;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления отчётов:\n" + ex.Message,
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void DrawChart(List<DailyRevenue> dailyData)
        {
            if (canvasChart == null) return;

            canvasChart.Children.Clear();

            if (dailyData == null || dailyData.Count == 0)
            {
                if (tbNoData != null) tbNoData.Visibility = Visibility.Visible;
                return;
            }

            if (tbNoData != null) tbNoData.Visibility = Visibility.Collapsed;

            double maxRevenue = 0;
            foreach (var day in dailyData)
            {
                double val = (double)day.Sum;
                if (val > maxRevenue) maxRevenue = val;
            }
            if (maxRevenue == 0) maxRevenue = 1;

            double canvasWidth = canvasChart.ActualWidth;
            double canvasHeight = canvasChart.ActualHeight - 80;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double barMaxHeight = canvasHeight * 0.92;
            double barWidth = Math.Max(24, canvasWidth / dailyData.Count * 0.65);
            double spacing = (canvasWidth - dailyData.Count * barWidth) / (dailyData.Count + 1);

            double currentX = spacing;

            foreach (var day in dailyData)
            {
                double barHeight = (double)day.Sum / maxRevenue * barMaxHeight;
                if (barHeight < 2) barHeight = 2;

                Rectangle bar = new Rectangle();
                bar.Width = barWidth;
                bar.Height = barHeight;
                bar.Fill = new SolidColorBrush(Color.FromArgb(220, 255, 234, 0));
                bar.RadiusX = 6;
                bar.RadiusY = 6;

                Canvas.SetLeft(bar, currentX);
                Canvas.SetBottom(bar, 60);

                bar.Height = 0;
                DoubleAnimation anim = new DoubleAnimation();
                anim.To = barHeight;
                anim.Duration = new Duration(TimeSpan.FromMilliseconds(800));
                QuadraticEase ease = new QuadraticEase();
                ease.EasingMode = EasingMode.EaseOut;
                anim.EasingFunction = ease;
                bar.BeginAnimation(Rectangle.HeightProperty, anim);

                canvasChart.Children.Add(bar);

                TextBlock sumLabel = new TextBlock();
                sumLabel.Text = day.Sum.ToString("N0") + " ₽";
                sumLabel.Foreground = Brushes.White;
                sumLabel.FontSize = 12;
                sumLabel.FontWeight = FontWeights.SemiBold;

                sumLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(sumLabel, currentX + (barWidth - sumLabel.DesiredSize.Width) / 2);
                Canvas.SetBottom(sumLabel, barHeight + 65);
                canvasChart.Children.Add(sumLabel);

                TextBlock dateLabel = new TextBlock();
                dateLabel.Text = day.Date.ToString("dd MMM");
                dateLabel.Foreground = Brushes.LightGray;
                dateLabel.FontSize = 11;

                dateLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(dateLabel, currentX + (barWidth - dateLabel.DesiredSize.Width) / 2);
                Canvas.SetBottom(dateLabel, 20);
                canvasChart.Children.Add(dateLabel);

                currentX += barWidth + spacing;
            }
        }

        private Tuple<DateTime, DateTime> GetDateRange()
        {
            if (cbPeriod == null) return new Tuple<DateTime, DateTime>(DateTime.Today, DateTime.Today.AddDays(1));
            DateTime today = DateTime.Today;
            int index = cbPeriod.SelectedIndex;

            // Пользовательский период — всегда приоритетно берём из датапикеров, если они заполнены
            if (index == 4)
            {
                DateTime from = today.AddDays(-7); // fallback, если ничего не выбрано
                DateTime to = today;

                if (dpFrom.SelectedDate.HasValue)
                    from = dpFrom.SelectedDate.Value.Date;

                if (dpTo.SelectedDate.HasValue)
                    to = dpTo.SelectedDate.Value.Date;

                // Защита от перевёрнутого периода
                if (from > to)
                {
                    (from, to) = (to, from); // меняем местами
                                             // Можно вывести предупреждение, но автокоррекция обычно удобнее
                }

                return new Tuple<DateTime, DateTime>(from, to.AddDays(1)); // до конца дня "по"
            }

            // Всё время
            if (index == 5)
            {
                return new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1), DateTime.Now.AddDays(1));
            }

            // Сегодня
            if (index == 0)
                return new Tuple<DateTime, DateTime>(today, today.AddDays(1));

            // Вчера
            if (index == 1)
                return new Tuple<DateTime, DateTime>(today.AddDays(-1), today);

            // Текущий месяц
            if (index == 2)
                return new Tuple<DateTime, DateTime>(
                    new DateTime(today.Year, today.Month, 1),
                    today.AddDays(1));

            // Прошлый месяц
            if (index == 3)
            {
                var firstOfCurrent = new DateTime(today.Year, today.Month, 1);
                return new Tuple<DateTime, DateTime>(
                    firstOfCurrent.AddMonths(-1),
                    firstOfCurrent);
            }

            if (index == 4)
            {
                DateTime from = today.AddDays(-7);
                DateTime to = today;

                if (dpFrom?.SelectedDate.HasValue == true)
                    from = dpFrom.SelectedDate.Value.Date;

                if (dpTo?.SelectedDate.HasValue == true)
                    to = dpTo.SelectedDate.Value.Date;

                if (from > to)
                    (from, to) = (to, from);

                return new Tuple<DateTime, DateTime>(from, to.AddDays(1));
            }
            // fallback — сегодня
            return new Tuple<DateTime, DateTime>(today, today.AddDays(1));
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var range = GetDateRange();
                DateTime start = range.Item1;
                DateTime end = range.Item2;

                string periodName;
                switch (cbPeriod.SelectedIndex)
                {
                    case 0: periodName = "Сегодня"; break;
                    case 1: periodName = "Вчера"; break;
                    case 2: periodName = $"Текущий месяц ({start:MMMM yyyy})"; break;
                    case 3: periodName = $"Прошлый месяц ({start:MMMM yyyy})"; break;
                    case 4: periodName = $"Пользовательский ({start:dd.MM.yyyy} – {end.AddDays(-1):dd.MM.yyyy})"; break;
                    default: periodName = "Всё время"; break;
                }

                using (var ctx = new Entities())
                {
                    // Общая выручка
                    decimal totalRevenue = ctx.Transactions
                        .Where(t => t.TransactionDate >= start && t.TransactionDate < end
                                 && t.Amount < 0
                                 && (t.Type == "Withdrawal" || t.Type == "FoodOrder" || t.Type == "SessionWithdrawal"))
                        .Sum(t => (decimal?)-t.Amount) ?? 0m;

                    // Топ-5 товаров
                    var topItems = ctx.OrderItems
                        .Join(ctx.Orders, oi => oi.OrderID, o => o.OrderID, (oi, o) => new { oi, o })
                        .Where(x => x.o.OrderDate >= start && x.o.OrderDate < end && (x.o.Status == "Delivered" || x.o.Status == "Completed"))
                        .GroupBy(x => x.oi.MenuItemID)
                        .Select(g => new
                        {
                            MenuItemID = g.Key,
                            Quantity = g.Sum(x => x.oi.Quantity),
                            Total = g.Sum(x => (decimal?)x.oi.Subtotal) ?? 0m
                        })
                        .Join(ctx.MenuItems, g => g.MenuItemID, m => m.MenuItemID, (g, m) => new
                        {
                            Name = m.Name ?? "Неизвестно",
                            Quantity = g.Quantity,
                            Total = g.Total
                        })
                        .OrderByDescending(x => x.Total)
                        .Take(5)
                        .ToList();

                    // Топ-5 клиентов
                    var topClients = ctx.Transactions
                        .Where(t => t.TransactionDate >= start && t.TransactionDate < end
                                 && t.Amount < 0
                                 && (t.Type == "Withdrawal" || t.Type == "FoodOrder" || t.Type == "SessionWithdrawal"))
                        .GroupBy(t => t.ClientID)
                        .Select(g => new
                        {
                            ClientID = g.Key,
                            TotalSpent = g.Sum(t => -t.Amount)
                        })
                        .Join(ctx.Clients, g => g.ClientID, c => c.ClientID, (g, c) => new
                        {
                            FullName = c.FullName ?? "Гость",
                            Total = g.TotalSpent
                        })
                        .OrderByDescending(x => x.Total)
                        .Take(5)
                        .ToList();

                    // Формируем CSV — аккуратный и читаемый в Excel
                    var sb = new StringBuilder();

                    // Заголовок отчёта
                    sb.AppendLine($"Отчёт: {periodName}");
                    sb.AppendLine($"Период: {start:dd.MM.yyyy} – {end.AddDays(-1):dd.MM.yyyy}");
                    sb.AppendLine($"Общая выручка: {totalRevenue:N0} ₽");
                    sb.AppendLine("");

                    // Топ-5 товаров
                    sb.AppendLine("Топ-5 товаров");
                    sb.AppendLine("Название;Количество;Сумма (₽)");
                    foreach (var item in topItems)
                    {
                        sb.AppendLine($"\"{item.Name.Replace("\"", "\"\"")}\";{item.Quantity};{item.Total:N0}");
                    }
                    sb.AppendLine("");

                    // Топ-5 клиентов
                    sb.AppendLine("Топ-5 клиентов");
                    sb.AppendLine("Клиент;Потрачено (₽)");
                    foreach (var client in topClients)
                    {
                        sb.AppendLine($"\"{client.FullName.Replace("\"", "\"\"")}\";{client.Total:N0}");
                    }

                    // Диалог сохранения
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"Отчёт_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv",
                        DefaultExt = ".csv",
                        Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        // UTF-8 + BOM — Excel откроет русские символы без кракозябр
                        File.WriteAllText(dialog.FileName, "\uFEFF" + sb.ToString(), Encoding.UTF8);
                        MessageBox.Show("Отчёт успешно сохранён!\n\nФайл готов к открытию в Excel.",
                                        "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}