// Controls/SearchFilterBar.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace CharacterApp.Controls
{
    public class SearchFilterBar : Border
    {
        // Ссылка на сам DataGrid больше не нужна: соответствие «колонка → свойство»
        // вычисляется один раз в Attach и живёт в _columnPaths
        private ICollectionView? _view;
        private readonly ComboBox _cbColumn = new();
        private readonly TextBox  _tbSearch = new();
        private ComboBox?         _cbFilter;
        private string            _searchText = "";
        private int               _filterIdx  = 0;
        private Func<object, string, bool>? _filterPredicate;
        private List<string>      _filterValues = new();

        public SearchFilterBar()
        {
            Margin          = new Thickness(0, 0, 0, 6);
            Padding         = new Thickness(8, 5, 8, 5);
            CornerRadius    = new CornerRadius(7);
            BorderThickness = new Thickness(1);
            SetResourceReference(BackgroundProperty,   "SurfaceBrush");
            SetResourceReference(BorderBrushProperty,  "BorderMedBrush");

            var row = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new TextBlock
            {
                Text = "🔍", FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            row.Children.Add(icon);

            _tbSearch.Width  = 190;
            _tbSearch.Height = 28;
            _tbSearch.Margin = new Thickness(0, 0, 8, 0);
            _tbSearch.VerticalContentAlignment = VerticalAlignment.Center;
            _tbSearch.SetResourceReference(TextBox.BackgroundProperty, "FieldBrush");
            _tbSearch.TextChanged += (_, _) => { _searchText = _tbSearch.Text; _view?.Refresh(); };
            row.Children.Add(_tbSearch);

            var lbl = new TextBlock
            {
                Text = "в:", VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            row.Children.Add(lbl);

            _cbColumn.Width  = 140;
            _cbColumn.Height = 28;
            _cbColumn.Margin = new Thickness(0, 0, 0, 0);
            _cbColumn.SelectionChanged += (_, _) => _view?.Refresh();
            row.Children.Add(_cbColumn);

            Child = row;
        }

        /// <summary>
        /// Задать текст поиска извне — этим пользуется общая строка поиска
        /// в боковом меню. Раньше страница в ответ на неё ставила собственный
        /// фильтр на ту же коллекцию и затирала фильтр этой панели.
        /// </summary>
        public void SetSearchText(string text)
        {
            if (_tbSearch.Text == text) return;
            _tbSearch.Text = text;   // TextChanged сам обновит представление
        }

        public void AddDropdownFilter(string label, IEnumerable<string> values,
                                      Func<object, string, bool> predicate)
        {
            if (Child is not StackPanel row) return;

            // Страницы зовут это из обработчика Loaded, а он срабатывает при
            // каждом показе страницы. Без проверки на каждый заход добавлялся
            // ещё один выпадающий список «Категория».
            if (_cbFilter != null) return;

            var sep = new TextBlock
            {
                Text = "|", Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };
            row.Children.Add(sep);

            var lbl = new TextBlock
            {
                Text = label, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            row.Children.Add(lbl);

            _cbFilter = new ComboBox { Width = 170, Height = 28 };
            _filterValues = values.ToList();
            foreach (var v in _filterValues) _cbFilter.Items.Add(v);
            _cbFilter.SelectedIndex = 0;
            _filterPredicate = predicate;
            _cbFilter.SelectionChanged += (_, _) =>
            {
                _filterIdx = _cbFilter.SelectedIndex;
                _view?.Refresh();
            };
            row.Children.Add(_cbFilter);
        }

        /// <summary>Заголовок колонки → имя свойства, по которому реально искать.</summary>
        private readonly Dictionary<string, string> _columnPaths = new();

        /// <summary>Строковые свойства элемента — по ним идёт поиск в режиме «Все колонки».</summary>
        private List<PropertyDescriptor> _searchableProps = new();

        public void Attach(DataGrid grid, IEnumerable source)
        {
            _view = CollectionViewSource.GetDefaultView(source);
            _view.Filter = FilterRow;

            var itemType = GetItemType(source);
            var props    = itemType != null
                ? TypeDescriptor.GetProperties(itemType).Cast<PropertyDescriptor>().ToList()
                : new List<PropertyDescriptor>();

            // Ищем только по тексту: bool-галочки и числовые индексы категорий
            // выдавали бы совпадения на "True" и на цифрах
            _searchableProps = props.Where(p => p.PropertyType == typeof(string)).ToList();

            _columnPaths.Clear();
            _cbColumn.Items.Clear();
            _cbColumn.Items.Add("Все колонки");

            foreach (DataGridColumn col in grid.Columns)
            {
                if (col.Header is not string h || string.IsNullOrWhiteSpace(h)) continue;

                var path = ResolvePath(col);
                // Колонку берём в список, только если за ней стоит текстовое
                // свойство. Так из списка сам собой выпадает столбец-галочка «●»,
                // искать по которому нечего.
                if (string.IsNullOrEmpty(path)) continue;
                if (!_searchableProps.Any(p => p.Name == path)) continue;

                _columnPaths[h] = path;
                _cbColumn.Items.Add(h);
            }

            _cbColumn.SelectedIndex = 0;
        }

        /// <summary>
        /// Определяет, какое свойство стоит за колонкой.
        ///
        /// Раньше для шаблонных колонок брался их заголовок — и поиск искал
        /// свойство с именем «Название», которого у модели нет. Совпадений
        /// не находилось никогда, поиск по конкретной колонке молча возвращал
        /// пустой список. Теперь основной источник — SortMemberPath.
        /// </summary>
        private static string ResolvePath(DataGridColumn col)
        {
            if (!string.IsNullOrWhiteSpace(col.SortMemberPath)) return col.SortMemberPath;

            if (col is DataGridBoundColumn bc &&
                bc.Binding is System.Windows.Data.Binding b &&
                !string.IsNullOrWhiteSpace(b.Path?.Path))
                return b.Path.Path;

            return col.Header?.ToString() ?? "";
        }

        /// <summary>
        /// Подключение для таблиц, у которых колонки не отображаются на свойства
        /// объекта — например у кастомных листов, где ячейки лежат в списке
        /// Cells по индексу. Колонки и способ достать из строки текст задаёт
        /// вызывающая сторона.
        /// </summary>
        /// <param name="textProvider">
        /// (строка, индекс колонки) → тексты для поиска. Индекс −1 означает
        /// «искать по всем колонкам».
        /// </param>
        public void AttachCustom(IEnumerable source, IEnumerable<string> columns,
                                 Func<object, int, IEnumerable<string>> textProvider)
        {
            _view = CollectionViewSource.GetDefaultView(source);
            _view.Filter = FilterRow;
            _textProvider = textProvider;

            _columnPaths.Clear();
            _searchableProps.Clear();

            _cbColumn.Items.Clear();
            _cbColumn.Items.Add("Все колонки");
            foreach (var c in columns)
                if (!string.IsNullOrWhiteSpace(c)) _cbColumn.Items.Add(c);
            _cbColumn.SelectedIndex = 0;
        }

        private Func<object, int, IEnumerable<string>>? _textProvider;

        /// <summary>Тип элемента коллекции — работает и для пустого списка.</summary>
        private static Type? GetItemType(IEnumerable source)
        {
            var t = source.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                     i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (t != null) return t.GetGenericArguments()[0];

            foreach (var first in source) return first?.GetType();
            return null;
        }

        private bool FilterRow(object item)
        {
            // Dropdown filter
            if (_filterPredicate != null && _cbFilter != null && _filterIdx > 0)
            {
                var val = _filterValues.ElementAtOrDefault(_filterIdx) ?? "";
                if (!_filterPredicate(item, val)) return false;
            }

            // Text search
            var q = _searchText.Trim();
            if (string.IsNullOrEmpty(q)) return true;

            return GetTexts(item)
                .Any(t => t.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IEnumerable<string> GetTexts(object item)
        {
            // «Все колонки» — первый пункт списка
            var selected = _cbColumn.SelectedIndex <= 0
                ? null
                : _cbColumn.SelectedItem as string;

            // Таблица с собственным поставщиком текста (кастомные листы)
            if (_textProvider != null)
            {
                foreach (var s in _textProvider(item, _cbColumn.SelectedIndex - 1))
                    if (!string.IsNullOrEmpty(s)) yield return s;
                yield break;
            }

            IEnumerable<PropertyDescriptor> targets;

            if (selected != null && _columnPaths.TryGetValue(selected, out var path))
                targets = _searchableProps.Where(p => p.Name == path);
            else
                targets = _searchableProps;

            foreach (var p in targets)
            {
                var v = p.GetValue(item);
                if (v is string s && s.Length > 0) yield return s;
            }
        }
    }
}
