using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CharacterApp
{
    public partial class IconRatingControl : UserControl
    {
        public ObservableCollection<int> IconIndices { get; } = new ObservableCollection<int>();

        public IconRatingControl()
        {
            InitializeComponent();
            SetIconIndices();
        }

        private void SetIconIndices()
        {
            IconIndices.Clear();
            for (int i = 1; i <= MaxValue; i++)
            {
                IconIndices.Add(i);
            }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(IconRatingControl), new PropertyMetadata(0));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(int), typeof(IconRatingControl), new PropertyMetadata(5, OnMaxValueChanged));

        public int MaxValue
        {
            get => (int)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        private static void OnMaxValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IconRatingControl control)
            {
                control.SetIconIndices();
            }
        }

        public static readonly DependencyProperty ActiveIconProperty =
            DependencyProperty.Register("ActiveIcon", typeof(string), typeof(IconRatingControl), new PropertyMetadata("Images/icon_active.png"));

        public string ActiveIcon
        {
            get => (string)GetValue(ActiveIconProperty);
            set => SetValue(ActiveIconProperty, value);
        }

        public static readonly DependencyProperty InactiveIconProperty =
            DependencyProperty.Register("InactiveIcon", typeof(string), typeof(IconRatingControl), new PropertyMetadata("Images/icon_inactive.png"));

        public string InactiveIcon
        {
            get => (string)GetValue(InactiveIconProperty);
            set => SetValue(InactiveIconProperty, value);
        }

        private void Image_MouseDown_Handler(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Tag != null && int.TryParse(img.Tag.ToString(), out int clicked))
            {
                Value = (Value == clicked ? 0 : clicked);
            }
        }
    }
}
