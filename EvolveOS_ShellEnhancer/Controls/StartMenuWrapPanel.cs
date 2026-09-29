// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Numerics;

namespace EvolveOS_ShellEnhancer.Controls
{
    public sealed class StartMenuWrapPanel : Panel
    {
        private readonly Dictionary<UIElement, Point> _lastPos = new();
        public bool IsDragInProgress { get; set; } = false;

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            nameof(ItemWidth), typeof(double), typeof(StartMenuWrapPanel),
            new PropertyMetadata(88.0, OnLayoutPropertyChanged));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            nameof(ItemHeight), typeof(double), typeof(StartMenuWrapPanel),
            new PropertyMetadata(104.0, OnLayoutPropertyChanged));

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StartMenuWrapPanel panel)
            {
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
        }

        private readonly List<bool[]> _cellOccupancy = new List<bool[]>();
        private readonly Dictionary<int, int> _rowTypes = new Dictionary<int, int>();

        private void EnsureRowExists(int rowIndex, int totalColumns)
        {
            while (_cellOccupancy.Count <= rowIndex)
            {
                _cellOccupancy.Add(new bool[totalColumns]);
                _rowTypes[_cellOccupancy.Count - 1] = 0;
            }
        }

        private (int colSpan, int rowSpan) GetSpans(UIElement child)
        {
            AppItem? appItem = null;

            if (child is GridViewItem gvi)
            {
                appItem = gvi.Content as AppItem ?? gvi.DataContext as AppItem;
            }
            else if (child is FrameworkElement fe)
            {
                appItem = fe.DataContext as AppItem;
            }

            if (appItem != null && appItem.ExecutablePath == "PINNED_FOLDER" && appItem.FolderSize == 2)
            {
                return (2, 2);
            }

            return (1, 1);
        }

        private (int row, int col) FindNextAvailableCell(int colSpan, int rowSpan, int totalColumns)
        {
            int requiredType = rowSpan;

            for (int r = 0; ; r++)
            {
                EnsureRowExists(r + rowSpan - 1, totalColumns);

                bool rowsCompatible = true;
                for (int ar = r; ar < r + rowSpan; ar++)
                {
                    if (_rowTypes[ar] != 0 && _rowTypes[ar] != requiredType)
                    {
                        rowsCompatible = false;
                        break;
                    }
                }

                if (!rowsCompatible) continue;

                for (int c = 0; c <= totalColumns - colSpan; c++)
                {
                    bool isAreaFree = true;
                    for (int ar = r; ar < r + rowSpan; ar++)
                    {
                        for (int ac = c; ac < c + colSpan; ac++)
                        {
                            if (_cellOccupancy[ar][ac]) { isAreaFree = false; break; }
                        }
                        if (!isAreaFree) break;
                    }

                    if (isAreaFree) return (r, c);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _cellOccupancy.Clear();
            _rowTypes.Clear();

            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();
            if (visibleChildren.Count == 0) return new Size(0, 0);

            double safeWidth = availableSize.Width;
            if (double.IsNaN(safeWidth) || double.IsInfinity(safeWidth) || safeWidth > 15000 || safeWidth < 0)
            {
                safeWidth = 15000;
            }

            int totalColumns = 6;
            if (safeWidth > 0 && ItemWidth > 0)
            {
                totalColumns = (int)Math.Floor(safeWidth / ItemWidth);
                if (totalColumns < 2) totalColumns = 6;
            }

            double maxLayoutHeight = 0;

            foreach (var child in visibleChildren)
            {
                var spans = GetSpans(child);
                (int row, int col) = FindNextAvailableCell(spans.colSpan, spans.rowSpan, totalColumns);

                for (int r = row; r < row + spans.rowSpan; r++)
                {
                    EnsureRowExists(r, totalColumns);
                    _rowTypes[r] = spans.rowSpan;
                    for (int c = col; c < col + spans.colSpan; c++) _cellOccupancy[r][c] = true;
                }

                double w = ItemWidth * spans.colSpan;
                double h = ItemHeight * spans.rowSpan;

                child.Measure(new Size(Math.Max(0, w), Math.Max(0, h)));

                maxLayoutHeight = Math.Max(maxLayoutHeight, (row + spans.rowSpan) * ItemHeight);
            }

            double desiredWidth = totalColumns * ItemWidth;

            if (double.IsNaN(desiredWidth) || double.IsInfinity(desiredWidth) || desiredWidth < 0) desiredWidth = 528;
            if (double.IsNaN(maxLayoutHeight) || double.IsInfinity(maxLayoutHeight) || maxLayoutHeight < 0) maxLayoutHeight = 104;

            return new Size(desiredWidth, maxLayoutHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _cellOccupancy.Clear();
            _rowTypes.Clear();

            var visibleChildren = Children.Where(c => c.Visibility != Visibility.Collapsed).ToList();

            double safeWidth = finalSize.Width;
            double safeHeight = finalSize.Height;

            if (double.IsNaN(safeWidth) || double.IsInfinity(safeWidth) || safeWidth > 15000 || safeWidth < 0) safeWidth = 15000;
            if (double.IsNaN(safeHeight) || double.IsInfinity(safeHeight) || safeHeight < 0) safeHeight = 0;

            Size safeFinalSize = new Size(safeWidth, safeHeight);
            if (visibleChildren.Count == 0) return safeFinalSize;

            int totalColumns = 6;
            if (safeWidth > 0 && ItemWidth > 0)
            {
                totalColumns = (int)Math.Floor(safeWidth / ItemWidth);
                if (totalColumns < 2) totalColumns = 6;
            }

            double maxLayoutHeight = 0;

            foreach (var child in visibleChildren)
            {
                var spans = GetSpans(child);
                (int row, int col) = FindNextAvailableCell(spans.colSpan, spans.rowSpan, totalColumns);

                for (int r = row; r < row + spans.rowSpan; r++)
                {
                    EnsureRowExists(r, totalColumns);
                    _rowTypes[r] = spans.rowSpan;
                    for (int c = col; c < col + spans.colSpan; c++) _cellOccupancy[r][c] = true;
                }

                double targetX = col * ItemWidth;
                double targetY = row * ItemHeight;
                double w = ItemWidth * spans.colSpan;
                double h = ItemHeight * spans.rowSpan;

                child.Arrange(new Rect(Math.Max(0, targetX), Math.Max(0, targetY), Math.Max(0, w), Math.Max(0, h)));

                AnimateChild(child, new Point(Math.Max(0, targetX), Math.Max(0, targetY)));

                maxLayoutHeight = Math.Max(maxLayoutHeight, targetY + h);
            }

            double retW = double.IsNaN(finalSize.Width) || double.IsInfinity(finalSize.Width) ? (totalColumns * ItemWidth) : finalSize.Width;
            double retH = double.IsNaN(finalSize.Height) || double.IsInfinity(finalSize.Height) ? maxLayoutHeight : finalSize.Height;

            return new Size(Math.Max(0, retW), Math.Max(0, retH));
        }

        private void AnimateChild(UIElement child, Point newPos)
        {
            if (IsDragInProgress)
            {
                _lastPos[child] = newPos;
                return;
            }

            if (!_lastPos.ContainsKey(child))
            {
                _lastPos[child] = newPos;
                return;
            }

            var oldPos = _lastPos[child];

            if (Math.Abs(oldPos.X - newPos.X) > 0.5 || Math.Abs(oldPos.Y - newPos.Y) > 0.5)
            {
                _lastPos[child] = newPos;

                float deltaX = (float)(oldPos.X - newPos.X);
                float deltaY = (float)(oldPos.Y - newPos.Y);

                child.TranslationTransition = null;
                child.Translation = new Vector3(deltaX, deltaY, 0f);

                child.DispatcherQueue.TryEnqueue(() =>
                {
                    child.TranslationTransition = new Vector3Transition
                    {
                        Duration = TimeSpan.FromMilliseconds(400)
                    };
                    child.Translation = Vector3.Zero;
                });
            }
        }
    }
}