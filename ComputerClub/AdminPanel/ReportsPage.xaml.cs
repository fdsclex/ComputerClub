using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (!IsLoaded) return;                    // ещё не загружено → выходим

            // Проверяем ключевые элементы
            if (tbRevenue == null || canvasChart == null || tbNoData == null)
            {
                // Можно даже логировать или показать сообщение разработчику
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

                    // Общая выручка
                    decimal revenue = ctx.Orders
                        .Where(o => o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed")
                        .Sum(o => (decimal?)o.TotalAmount) ?? 0m;

                    tbRevenue.Text = revenue.ToString("N0") + " ₽";

                    // Данные по дням
                    var dailyDataTemp = ctx.Orders
    .Where(o => o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed")
    .AsEnumerable()                             // ← !!! переносим обработку в память
    .GroupBy(o => o.OrderDate.Value.Date)       // теперь .Date работает в LINQ to Objects
    .Select(g => new
    {
        Date = g.Key,
        Sum = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m
    })
    .OrderBy(x => x.Date)
    .ToList();

                    dailyDataCache = dailyDataTemp
                        .Select(d => new DailyRevenue { Date = d.Date, Sum = d.Sum })
                        .ToList();

                    DrawChart(dailyDataCache);

                    // Топ-5 товаров
                    var topItems = (from oi in ctx.OrderItems
                                    join o in ctx.Orders on oi.OrderID equals o.OrderID
                                    where o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed"
                                    group oi by oi.MenuItemID into g
                                    join m in ctx.MenuItems on g.Key equals m.MenuItemID
                                    orderby g.Sum(oi => (decimal?)oi.Subtotal) descending
                                    select new
                                    {
                                        Name = m.Name,
                                        Quantity = g.Sum(oi => (int?)oi.Quantity) ?? 0,     // если Quantity тоже может быть null
                                        Total = g.Sum(oi => (decimal?)oi.Subtotal) ?? 0m
                                    })
                                    .Take(5)
                                    .ToList();

                    dgTopItems.ItemsSource = topItems;

                    // Топ-5 клиентов — аналогично
                    var topClients = (from o in ctx.Orders
                                      where o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed"
                                      group o by o.ClientID into g
                                      join c in ctx.Clients on g.Key equals c.ClientID
                                      orderby g.Sum(o => (decimal?)o.TotalAmount) descending
                                      select new
                                      {
                                          FullName = c.FullName,
                                          Total = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m
                                      })
                                      .Take(5)
                                      .ToList();

                    dgTopClients.ItemsSource = topClients;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления отчётов:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (start == DateTime.MinValue) return;

                using (var ctx = new Entities())
                {
                    var ordersQuery = from o in ctx.Orders
                                      where o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed"
                                      select new
                                      {
                                          o.OrderID,
                                          OrderDate = o.OrderDate.Value,
                                          ClientName = o.Clients != null ? o.Clients.FullName : "Гость",
                                          Amount = o.TotalAmount
                                      };

                    var orders = ordersQuery.OrderBy(o => o.OrderDate).ToList();

                    decimal total = 0m;
                    foreach (var ord in orders)
                    {
                        total += ord.Amount;
                    }

                    List<string> lines = new List<string>();
                    lines.Add("Отчёт по заказам за период");
                    lines.Add("Период: " + start.ToString("dd.MM.yyyy") + " – " + end.AddDays(-1).ToString("dd.MM.yyyy"));
                    lines.Add("Общая выручка: " + total.ToString("N0") + " ₽");
                    lines.Add("");
                    lines.Add("ID;Дата;Клиент;Сумма");

                    foreach (var o in orders)
                    {
                        lines.Add(o.OrderID.ToString() + ";" +
                                  o.OrderDate.ToString("dd.MM.yyyy HH:mm") + ";" +
                                  o.ClientName + ";" +
                                  o.Amount.ToString("N0"));
                    }

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = "Отчет_КомпьютерныйКлуб_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".csv";
                    string fullPath = System.IO.Path.Combine(desktopPath, fileName);

                    File.WriteAllLines(fullPath, lines, System.Text.Encoding.UTF8);

                    MessageBox.Show("Отчёт сохранён на рабочий стол:\n" + fullPath, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка экспорта:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}