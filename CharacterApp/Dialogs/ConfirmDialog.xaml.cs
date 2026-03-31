using System.Windows;
using System.Windows.Media;

namespace CharacterApp.Dialogs
{
    public enum ConfirmMode { YesNo, YesNoCancel }

    public partial class ConfirmDialog : Window
    {
        public enum ConfirmResult { Yes, No, Cancel }
        public ConfirmResult Result { get; private set; } = ConfirmResult.Cancel;

        public ConfirmDialog(string message, string title = "",
                             ConfirmMode mode = ConfirmMode.YesNo,
                             ConfirmDialogIcon icon = ConfirmDialogIcon.Question)
        {
            InitializeComponent();
            MessageText.Text = message;
            if (!string.IsNullOrEmpty(title)) Title = title;

            BtnCancel.Visibility = mode == ConfirmMode.YesNoCancel
                ? Visibility.Visible : Visibility.Collapsed;

            switch (icon)
            {
                case ConfirmDialogIcon.Warning:
                    IconText.Text = "!";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(200, 120, 0));
                    break;
                case ConfirmDialogIcon.Info:
                    IconText.Text = "i";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(50, 130, 200));
                    break;
                default:
                    IconText.Text = "?";
                    break;
            }

            // Перетаскивание окна
            MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        }

        private void BtnYes_Click   (object sender, RoutedEventArgs e) { Result = ConfirmResult.Yes;    Close(); }
        private void BtnNo_Click    (object sender, RoutedEventArgs e) { Result = ConfirmResult.No;     Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { Result = ConfirmResult.Cancel; Close(); }
    }

    public enum ConfirmDialogIcon { Question, Warning, Info }
}
