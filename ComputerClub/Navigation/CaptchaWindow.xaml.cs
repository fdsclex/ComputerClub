using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ComputerClub.Navigation
{
    public partial class CaptchaWindow : Window
    {
        private string captchaText;

        public CaptchaWindow()
        {
            InitializeComponent();
            GenerateCaptcha();
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

            captchaText = new string(result);

            TextBlock tbCaptcha = new TextBlock
            {
                Text = captchaText,
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
                    X1 = rnd.Next(0, 400),
                    Y1 = rnd.Next(0, 100),
                    X2 = rnd.Next(0, 400),
                    Y2 = rnd.Next(0, 100),
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
                Canvas.SetLeft(dot, rnd.Next(0, 400));
                Canvas.SetTop(dot, rnd.Next(0, 100));
                captchaCanvas.Children.Add(dot);
            }

            tbInput.Clear();
            tbInput.Focus();
        }

        private void ConfirmCaptcha_Click(object sender, RoutedEventArgs e)
        {
            string input = tbInput.Text.Trim().ToUpper();

            if (input == captchaText)
            {
                DialogResult = true;
                Close();
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

        private void TbInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmCaptcha_Click(sender, e);
                e.Handled = true;
            }
        }

        // Запрет закрытия крестиком (и Esc)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // нельзя закрыть крестиком
            base.OnClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true; // блокируем Esc
            }
            base.OnKeyDown(e);
        }
    }
}