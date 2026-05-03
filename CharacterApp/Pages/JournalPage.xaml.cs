// Pages/JournalPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CharacterApp.Pages
{
    public class JournalEntry
    {
        public string Id      { get; set; } = Guid.NewGuid().ToString();
        public string Title   { get; set; } = "Новая запись";
        public string Content { get; set; } = "";
        public string Tags    { get; set; } = "";
        public DateTime Date  { get; set; } = DateTime.Now;
        public string DateStr => Date.ToString("dd.MM.yyyy  HH:mm");
    }

    public partial class JournalPage : Page
    {
        private readonly ObservableCollection<JournalEntry> _entries = new();
        private JournalEntry? _current;
        private bool _suppressChange;

        public JournalPage()
        {
            InitializeComponent();
            LbEntries.ItemsSource = _entries;
        }

        // ── Public API — called by MainWindow on save/load ───────────────────
        public System.Collections.Generic.List<JournalEntry> GetEntries()
            => _entries.ToList();

        public void LoadEntries(System.Collections.Generic.List<JournalEntry> list)
        {
            _entries.Clear();
            foreach (var e in list.OrderByDescending(e => e.Date))
                _entries.Add(e);
        }

        // ── UI events ─────────────────────────────────────────────────────────
        private void NewEntry_Click(object sender, RoutedEventArgs e)
        {
            var entry = new JournalEntry();
            _entries.Insert(0, entry);
            LbEntries.SelectedItem = entry;
            TbTitle.Focus();
            TbTitle.SelectAll();
            MarkUnsaved();
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var r = MessageBox.Show($"Удалить запись «{_current.Title}»?", "Удаление",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            _entries.Remove(_current);
            _current = null;
            EmptyOverlay.Visibility = Visibility.Visible;
            MarkUnsaved();
        }

        private void LbEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _current = LbEntries.SelectedItem as JournalEntry;
            bool has = _current != null;
            TbTitle.IsEnabled   = has;
            TbContent.IsEnabled = has;
            TbTags.IsEnabled    = has;

            if (_current == null) return;
            _suppressChange = true;
            TbTitle.Text   = _current.Title;
            TbContent.Text = _current.Content;
            TbTags.Text    = _current.Tags;
            TbDate.Text    = _current.DateStr;
            EmptyOverlay.Visibility = Visibility.Collapsed;
            TbSaveStatus.Text = "✔ Сохранено";
            UpdateWordCount();
            _suppressChange = false;
        }

        private void TbTitle_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressChange || _current == null) return;
            _current.Title = TbTitle.Text;
            // Refresh list display
            var idx = _entries.IndexOf(_current);
            _entries[idx] = _current;   // trigger ObservableCollection update
            LbEntries.SelectedIndex = idx;
            TbSaveStatus.Text = "• Не сохранено";
            MarkUnsaved();
        }

        private void TbContent_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressChange || _current == null) return;
            _current.Content = TbContent.Text;
            UpdateWordCount();
            TbSaveStatus.Text = "• Не сохранено";
            MarkUnsaved();
        }

        private void TbTags_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressChange || _current == null) return;
            _current.Tags = TbTags.Text;
            MarkUnsaved();
        }

        private void UpdateWordCount()
        {
            var words = TbContent.Text.Split(
                new[] { ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            TbWordCount.Text = $"{words} слов";
        }

        private static void MarkUnsaved()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
    }
}
