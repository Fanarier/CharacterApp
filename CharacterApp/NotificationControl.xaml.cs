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
            switch (type)
            {
                case NotificationType.Success:
                    IconBlock.Text = "✔";
                    Root.Background = new SolidColorBrush(Color.FromRgb(30, 120, 30));
                    break;
                case NotificationType.Warning:
                    IconBlock.Text = "⚠";
                    Root.Background = new SolidColorBrush(Color.FromRgb(180, 100, 0));
                    break;
                case NotificationType.Error:
                    IconBlock.Text = "✖";
                    Root.Background = new SolidColorBrush(Color.FromRgb(180, 0, 0));
                    break;
                default:
                    IconBlock.Text = "ℹ";
                    Root.Background = new SolidColorBrush(Color.FromRgb(50, 50, 200));
                    break;
            }
        }

        public async Task ShowAsync(UIElementCollection host)
        {
            host.Add(this);

            // Fade-in
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));

            // Slide-in только по X
            var slideIn = new ThicknessAnimation
            {
                From = new Thickness(350, 0, -350, 0),
                To = new Thickness(0, 0, 4, 0),
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            Root.BeginAnimation(MarginProperty, slideIn);

            await Task.Delay(4000);

            // Fade-out
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)));

            // Slide-out только по X
            var slideOut = new ThicknessAnimation
            {
                From = new Thickness(0, 0, 0, 0),
                To = new Thickness(350, 0, -350, 0),
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            Root.BeginAnimation(MarginProperty, slideOut);

            await Task.Delay(300);
            host.Remove(this);
        }

    }
}
