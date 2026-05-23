using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    /// <summary>
    /// Полностью динамическая страница — строится по CustomSheet-описанию.
    /// Не использует XAML — создаётся программно.
    /// </summary>
    public class CustomSheetPage : Page, ISaveLoad, IPageSearchable
    {
        private readonly CustomSheet _sheet;
        private readonly DataGrid    _grid;
        private          TextBlock   _headerBlock = null!;
        public  CustomSheet          Sheet        => _sheet;
        public ObservableCollection<CustomRowEntry> Rows { get; } = new();

        public CustomSheetPage(CustomSheet sheet)
        {
            _sheet = sheet;
            Title  = sheet.Name;

            _grid = BuildGrid();
            _headerBlock = new TextBlock
            {
                Text       = sheet.Name,
                FontSize   = 18,
                FontWeight = FontWeights.Bold,
                Margin     = new Thickness(4, 0, 4, 12)
            };
            _headerBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            var addBtn = new Button { Content = "Добавить строку", Margin = new Thickness(6), Padding = new Thickness(12,7,12,7) };
            addBtn.Click += (_, _) => AddRow();
            var delBtn = new Button { Content = "Удалить строку", Margin = new Thickness(6), Padding = new Thickness(12,7,12,7) };
            delBtn.Click += (_, _) => DeleteRow();

            var btnPanel = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 8, 0, 0)
            };
            btnPanel.Children.Add(addBtn);
            btnPanel.Children.Add(delBtn);

            var root = new Grid();
            root.Margin = new Thickness(12);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(_headerBlock, 0);
            Grid.SetRow(_grid,        1);
            Grid.SetRow(btnPanel,     2);

            root.Children.Add(_headerBlock);
            root.Children.Add(_grid);
            root.Children.Add(btnPanel);

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = root
            };

            _grid.ItemsSource = Rows;
        }

        private DataGrid BuildGrid()
        {
            var dg = new DataGrid
            {
                AutoGenerateColumns  = false,
                CanUserAddRows       = false,
                CanUserResizeRows    = false,
                HeadersVisibility    = DataGridHeadersVisibility.Column,
                RowHeaderWidth       = 0,
                SelectionMode        = DataGridSelectionMode.Single,
                IsReadOnly           = false,
                Background           = System.Windows.Media.Brushes.Transparent,
                GridLinesVisibility  = DataGridGridLinesVisibility.Horizontal,
            };
            dg.SetResourceReference(StyleProperty, "SkillsGridStyle");
            dg.SetResourceReference(DataGrid.RowStyleProperty, "SkillsRowStyle");

            for (int i = 0; i < _sheet.Columns.Count; i++)
            {
                var col    = _sheet.Columns[i];
                var colIdx = i;
                DataGridTemplateColumn dgc;

                if (col.ColumnType == "toggle")
                {
                    dgc = BuildToggleColumn(col.Header, colIdx);
                }
                else
                {
                    dgc = BuildTextColumn(col.Header, colIdx, col.ColumnType == "number");
                }

                dgc.Width = colIdx == _sheet.Columns.Count - 1 && col.ColumnType != "toggle"
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(colIdx == 0 ? 160 : 200);

                if (col.ColumnType == "toggle") dgc.Width = new DataGridLength(80);

                dg.Columns.Add(dgc);
            }
            return dg;
        }

        private DataGridTemplateColumn BuildTextColumn(string header, int colIdx, bool numericOnly)
        {
            var cellTpl = new DataTemplate();
            var cellFac = new FrameworkElementFactory(typeof(TextBlock));
            cellFac.SetBinding(TextBlock.TextProperty, new Binding($"Cells[{colIdx}]"));
            cellFac.SetValue(TextBlock.PaddingProperty, new Thickness(8, 6, 8, 6));
            cellFac.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            cellFac.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            cellTpl.VisualTree = cellFac;

            var editTpl = new DataTemplate();
            var editFac = new FrameworkElementFactory(typeof(TextBox));
            editFac.SetBinding(TextBox.TextProperty, new Binding($"Cells[{colIdx}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            editFac.SetResourceReference(StyleProperty, "CellTextBox");
            editTpl.VisualTree = editFac;

            return new DataGridTemplateColumn
            {
                Header              = header,
                CellTemplate        = cellTpl,
                CellEditingTemplate = editTpl
            };
        }

        private DataGridTemplateColumn BuildToggleColumn(string header, int colIdx)
        {
            var cellTpl = new DataTemplate();
            var fac     = new FrameworkElementFactory(typeof(CheckBox));
            fac.SetBinding(CheckBox.IsCheckedProperty, new Binding($"BoolCells[{colIdx}]")
            {
                Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            fac.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            fac.SetValue(FrameworkElement.VerticalAlignmentProperty,   VerticalAlignment.Center);
            cellTpl.VisualTree = fac;

            return new DataGridTemplateColumn
            {
                Header       = header,
                CellTemplate = cellTpl,
                IsReadOnly   = false
            };
        }

        private void AddRow()
        {
            var row = new CustomRowEntry(_sheet.Columns.Count);
            Rows.Add(row);
            _grid.SelectedItem = row;
            _grid.ScrollIntoView(row);
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void DeleteRow()
        {
            if (_grid.SelectedItem is CustomRowEntry sel)
            {
                Rows.Remove(sel);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }

        // ── Переименование ───────────────────────────────────────────────────
        public void UpdateTitle(string newTitle)
        {
            _sheet.Name      = newTitle;
            Title            = newTitle;
            _headerBlock.Text = newTitle;
        }

        public void UpdateHeaders(System.Collections.Generic.List<string> newHeaders)
        {
            for (int i = 0; i < System.Math.Min(newHeaders.Count, _sheet.Columns.Count); i++)
            {
                _sheet.Columns[i].Header = newHeaders[i];
                if (i < _grid.Columns.Count)
                    _grid.Columns[i].Header = newHeaders[i];
            }
        }

        // ── ISaveLoad ────────────────────────────────────────────────────────
        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void FillCharacter(Character c)
        {
            _grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var existing = c.CustomSheets.FirstOrDefault(s => s.Name == _sheet.Name);
            if (existing == null)
            {
                existing = new CustomSheet { Name = _sheet.Name, Columns = _sheet.Columns };
                c.CustomSheets.Add(existing);
            }
            else
            {
                existing.Columns = _sheet.Columns;
            }

            existing.Rows.Clear();
            foreach (var r in Rows)
            {
                var sheetRow = new CustomSheetRow();
                for (int i = 0; i < _sheet.Columns.Count; i++)
                {
                    var col = _sheet.Columns[i];
                    sheetRow.Cells.Add(col.ColumnType == "toggle"
                        ? (r.BoolCells[i] ? "1" : "0")
                        : (i < r.Cells.Count ? r.Cells[i] : ""));
                }
                existing.Rows.Add(sheetRow);
            }
        }

        public void ApplyCharacter(Character c)
        {
            Rows.Clear();
            var sheet = c.CustomSheets.FirstOrDefault(s => s.Name == _sheet.Name);
            if (sheet == null) return;
            foreach (var sr in sheet.Rows)
            {
                var entry = new CustomRowEntry(_sheet.Columns.Count) { SuppressNotify = true };
                for (int i = 0; i < _sheet.Columns.Count && i < sr.Cells.Count; i++)
                {
                    var col = _sheet.Columns[i];
                    if (col.ColumnType == "toggle")
                        entry.BoolCells[i] = sr.Cells[i] == "1";
                    else if (i < entry.Cells.Count)
                        entry.Cells[i] = sr.Cells[i];
                }
                entry.SuppressNotify = false;
                Rows.Add(entry);
            }
        }


        public void FilterItems(string query)
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Rows);
            if (string.IsNullOrWhiteSpace(query))
            { view.Filter = null; return; }
            view.Filter = obj => obj is CustomRowEntry row &&
                row.Cells.Any(c => c?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
        }

        public void ResetAll() => Rows.Clear();
    }

    // ── ViewModel строки кастомного листа ────────────────────────────────────
    public class CustomRowEntry : INotifyPropertyChanged
    {
        /// <summary>Когда true — не вызывает MarkUnsaved при изменении ячеек (используется при загрузке).</summary>
        public bool SuppressNotify { get; set; }

        public ObservableCollection<string> Cells     { get; }
        public ObservableCollection<bool>   BoolCells { get; }

        public CustomRowEntry(int columnCount)
        {
            Cells     = new ObservableCollection<string>(Enumerable.Repeat("", columnCount));
            BoolCells = new ObservableCollection<bool>(Enumerable.Repeat(false, columnCount));

            Cells.CollectionChanged     += (_, _) => { if (!SuppressNotify) Mark(); };
            BoolCells.CollectionChanged += (_, _) => { if (!SuppressNotify) Mark(); };
        }

        private static void Mark() => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
