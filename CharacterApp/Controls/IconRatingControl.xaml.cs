using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CharacterApp.Controls
{
    public partial class IconRatingControl : UserControl
    {
        public ObservableCollection<int> IconIndices { get; } = new ObservableCollection<int>();

        public IconRatingControl()
        {
            InitializeComponent();
            SetIconIndices();
            ItemsHost.ItemsSource = IconIndices;
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(IconRatingControl), new PropertyMetadata(0));

        public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(int), typeof(IconRatingControl), new PropertyMetadata(5, OnMaxValueChanged));

        public int MaxValue { get => (int)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

        private static void OnMaxValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IconRatingControl c) c.SetIconIndices();
        }

        public static readonly DependencyProperty ActiveIconProperty =
            DependencyProperty.Register(nameof(ActiveIcon), typeof(string), typeof(IconRatingControl), new PropertyMetadata(string.Empty));

        public string ActiveIcon { get => (string)GetValue(ActiveIconProperty); set => SetValue(ActiveIconProperty, value); }

        public static readonly DependencyProperty InactiveIconProperty =
            DependencyProperty.Register(nameof(InactiveIcon), typeof(string), typeof(IconRatingControl), new PropertyMetadata(string.Empty));

        public string InactiveIcon { get => (string)GetValue(InactiveIconProperty); set => SetValue(InactiveIconProperty, value); }

        private void SetIconIndices()
        {
            IconIndices.Clear();
            for (int i = 1; i <= MaxValue; i++) IconIndices.Add(i);
        }

        // Обработчик клика по иконке — DataContext каждой иконки это её индекс (int)
        private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is int idx)
            {
                // Если кликнули по той же иконке — сбрасываем, иначе устанавливаем выбранное значение
                if (Value == idx)
                    Value = 0;
                else
                    Value = idx;

                e.Handled = true;
            }
        }

    }
}
