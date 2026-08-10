// NotificationControl.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CharacterApp
{
    public enum NotificationType { Info, Success, Warning, Error }

    public partial class NotificationControl : UserControl
    {
        public NotificationControl(string message, NotificationType type = NotificationType.Info)
        {
            InitializeComponent();
            MessageBlock.Text = message;

            // Определяем цвет фона через тему если возможно, иначе fallback
            var isDark = IsDarkTheme();

            switch (type)
            {
                case NotificationType.Success:
                    IconBlock.Text   = "✔";
                    Root.Background  = isDark
                        ? new SolidColorBrush(Color.FromArgb(230, 28, 110, 28))
                        : new SolidColorBrush(Color.FromArgb(230, 34, 139, 34));
                    Root.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 60, 200, 60));
                    break;
                case NotificationType.Warning:
                    IconBlock.Text   = "⚠";
                    Root.Background  = isDark
                        ? new SolidColorBrush(Color.FromArgb(230, 140, 80, 0))
                        : new SolidColorBrush(Color.FromArgb(230, 180, 100, 0));
                    Root.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 165, 0));
                    break;
                case NotificationType.Error:
                    IconBlock.Text   = "✖";
                    Root.Background  = isDark
                        ? new SolidColorBrush(Color.FromArgb(230, 140, 20, 20))
                        : new SolidColorBrush(Color.FromArgb(230, 180, 30, 30));
                    Root.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 80, 80));
                    break;
                default:
                    IconBlock.Text   = "ℹ";
                    Root.Background  = isDark
                        ? new SolidColorBrush(Color.FromArgb(230, 30, 50, 160))
                        : new SolidColorBrush(Color.FromArgb(230, 50, 100, 200));
                    Root.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 100, 160, 255));
                    break;
            }
        }

        private static bool IsDarkTheme()
        {
            try
            {
                var res = Application.Current.Resources;
                if (res["WindowBackgroundBrush"] is LinearGradientBrush lgb &&
                    lgb.GradientStops.Count > 0)
                {
                    var c = lgb.GradientStops[0].Color;
                    // Яркость: если R+G+B < 200 — тёмная тема
                    return (c.R + c.G + c.B) < 200;
                }
            }
            catch { /* ресурс темы не найден — считаем тему тёмной, см. return ниже */ }
            return true;
        }

        public async Task ShowAsync(UIElementCollection host)
        {
            host.Add(this);

            // Fade + slide in
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
            Root.BeginAnimation(MarginProperty, new ThicknessAnimation
            {
                From     = new Thickness(80, 0, -80, 0),
                To       = new Thickness(0, 0, 4, 0),
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            await Task.Delay(3500);

            // Fade + slide out
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)));
            Root.BeginAnimation(MarginProperty, new ThicknessAnimation
            {
                From     = new Thickness(0, 0, 4, 0),
                To       = new Thickness(80, 0, -80, 0),
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });

            await Task.Delay(230);
            host.Remove(this);
        }
    }
}
