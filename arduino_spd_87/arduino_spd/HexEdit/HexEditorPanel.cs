using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Панель, реализующая IScrollInfo для интеграции с ScrollViewer.
    /// Единственная ответственность: конвертация между логическим скроллом (строки/байты) и пикселями.
    /// HexScrollState - единственный источник истины для логического скролла.
    /// </summary>
    internal class HexEditorPanel : Panel, IScrollInfo
    {
        private HexScrollState? _scrollState;
        private HexViewMetrics? _metrics;
        private double _totalWidth = 800;

        // IScrollInfo свойства
        private double _extentWidth;
        private double _extentHeight;
        private double _viewportWidth;
        private double _viewportHeight;
        private bool _canVerticallyScroll = true;
        private ScrollViewer? _scrollOwner;
        
        // Флаги и кэш
        private bool _isUpdatingFromScrollViewer = false;
        private long _lastNotifiedLine = -1;
        private HexScrollViewportSnapshot _viewportSnapshot = HexScrollViewportSnapshot.Empty;

        public HexEditorPanel()
        {
            Background = Brushes.Transparent;
            Focusable = true;
            
            // НЕ обрабатываем MouseWheel напрямую - ScrollViewer с CanContentScroll="True"
            // автоматически вызывает MouseWheelUp/Down через IScrollInfo
        }

        #region Public API for HexEditorControl

        public HexScrollState? ScrollState
        {
            get => _scrollState;
            set
            {
                if (_scrollState != null)
                {
                    _scrollState.StateChanged -= OnScrollStateChanged;
                }
                _scrollState = value;
                if (_scrollState != null)
                {
                    _scrollState.StateChanged += OnScrollStateChanged;
                }

                UpdateScrollInfoFromState(ActualHeight);
            }
        }

        /// <summary>
        /// Обновляет метрики панели. Использует HexViewMetrics как единственный источник истины.
        /// </summary>
        public void UpdateMetrics(HexViewMetrics metrics, double totalWidth)
        {
            _metrics = metrics;
            _totalWidth = totalWidth;
            _extentWidth = totalWidth;

            // Геометрию обновит NotifyScrollState при следующем layout pass
            // Принудительное обновление layout - следующий Measure/Arrange вызовет NotifyScrollState
            InvalidateMeasure();
            // НЕ вызываем InvalidateVisual() - данные рендерятся в HexEditorControl
            // НЕ вызываем UpdateScrollInfoFromState здесь - это сделает layout pass через NotifyScrollState
        }

        public void ForceScrollUpdate()
        {
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0;
            UpdateScrollInfoFromState(ActualHeight);
            _scrollOwner?.InvalidateScrollInfo();
        }
        #endregion

        #region Layout and Measurement

        protected override Size MeasureOverride(Size availableSize)
        {
            NotifyScrollState(availableSize.Height);

            _viewportWidth = double.IsInfinity(availableSize.Width) ? _totalWidth : availableSize.Width;

            // Возвращаем желаемый размер
            return new Size(_totalWidth, availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            NotifyScrollState(finalSize.Height);
            UpdateScrollInfoFromState(finalSize.Height);
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0; // Обновляем кэш
            _scrollOwner?.InvalidateScrollInfo();

            return finalSize;
        }

        private void OnScrollStateChanged(object? sender, EventArgs e)
        {
            // IScrollInfo обновляется только через методы интерфейса (LineDown, SetVerticalOffset и т.д.)
            // Этот обработчик используется только для уведомления других подписчиков
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Уведомляет ScrollState об изменении viewport.
        /// Вычисляет VisibleLines на основе viewportHeight и LineHeight (с учётом заголовка).
        /// </summary>
        private void NotifyScrollState(double viewportHeight)
        {
            if (!TryUpdateViewportSnapshot(viewportHeight))
                return;

            if (_scrollState == null)
                return;

            if (!_isUpdatingFromScrollViewer && _viewportSnapshot.IsValid && _viewportSnapshot.VisibleLinesInt != _scrollState.VisibleLines)
            {
                _scrollState.SetVisibleLines(_viewportSnapshot.VisibleLinesInt);
            }
        }

        /// <summary>
        /// Обновляет IScrollInfo свойства из HexScrollState.
        /// Всегда вычисляет значения из состояния, не хранит копии.
        /// </summary>
        private void UpdateScrollInfoFromState(double viewportHeight)
        {
            if (!TryUpdateViewportSnapshot(viewportHeight))
                return;

            _viewportWidth = ActualWidth > 0 ? ActualWidth : _viewportWidth;
            _extentWidth = _totalWidth;
        }

        public (long Start, long End) GetVisibleRange()
        {
            return _scrollState?.GetVisibleRange() ?? (0, 0);
        }

        public bool IsLineVisible(long lineIndex)
        {
            if (_scrollState == null) return false;

            var documentLength = _scrollState.DocumentLength;
            if (documentLength == 0) return false;

            // Используем BytesPerLine из HexScrollState (единственный источник истины)
            int bytesPerLine = _scrollState.BytesPerLine;
            var visibleRange = _scrollState.GetVisibleRange();
            long lineStart = lineIndex * bytesPerLine;
            long lineEnd = Math.Min(lineStart + bytesPerLine - 1, documentLength - 1);

            return visibleRange.End >= lineStart && visibleRange.Start <= lineEnd;
        }
        #endregion

        #region IScrollInfo Implementation

        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll
        {
            get => _canVerticallyScroll;
            set => _canVerticallyScroll = value;
        }

        public double ExtentWidth => _extentWidth;
        public double ExtentHeight => _extentHeight;
        public double ViewportWidth => _viewportWidth;
        public double ViewportHeight => _viewportHeight;
        public double HorizontalOffset => 0;
        
        /// <summary>
        /// VerticalOffset всегда вычисляется из HexScrollState (CurrentLine * LineHeight).
        /// Не хранится отдельно, чтобы избежать рассинхронизации.
        /// </summary>
        public double VerticalOffset
        {
            get
            {
                if (_scrollState == null || !_viewportSnapshot.IsValid)
                    return 0;

                return HexScrollGeometry.LineToPixels(_viewportSnapshot, _scrollState.CurrentLine);
            }
        }

        public ScrollViewer? ScrollOwner
        {
            get => _scrollOwner;
            set
            {
                _scrollOwner = value;
                // При установке ScrollOwner обновляем информацию о скролле
                if (_scrollOwner != null)
                {
                    UpdateScrollInfoFromState(ActualHeight);
                }
            }
        }

        public void LineUp()
        {
            _scrollState?.ScrollLines(-1);
            long currentLine = _scrollState?.CurrentLine ?? 0;
            if (currentLine != _lastNotifiedLine)
            {
                _lastNotifiedLine = currentLine;
                UpdateScrollInfoFromState(ActualHeight);
                _scrollOwner?.InvalidateScrollInfo();
            }
        }
        
        public void LineDown()
        {
            _scrollState?.ScrollLines(1);
            long currentLine = _scrollState?.CurrentLine ?? 0;
            if (currentLine != _lastNotifiedLine)
            {
                _lastNotifiedLine = currentLine;
                UpdateScrollInfoFromState(ActualHeight);
                _scrollOwner?.InvalidateScrollInfo();
            }
        }
        
        public void LineLeft() { }
        public void LineRight() { }

        public void PageUp()
        {
            _scrollState?.PageUp();
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0;
            UpdateScrollInfoFromState(ActualHeight);
            _scrollOwner?.InvalidateScrollInfo();
        }
        
        public void PageDown()
        {
            _scrollState?.PageDown();
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0;
            UpdateScrollInfoFromState(ActualHeight);
            _scrollOwner?.InvalidateScrollInfo();
        }
        
        public void PageLeft() { }
        public void PageRight() { }

        public void MouseWheelUp()
        {
            _scrollState?.ScrollLines(-3);
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0;
            UpdateScrollInfoFromState(ActualHeight);
            _scrollOwner?.InvalidateScrollInfo();
        }
        
        public void MouseWheelDown()
        {
            _scrollState?.ScrollLines(3);
            _lastNotifiedLine = _scrollState?.CurrentLine ?? 0;
            UpdateScrollInfoFromState(ActualHeight);
            _scrollOwner?.InvalidateScrollInfo();
        }
        
        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }

        public void SetHorizontalOffset(double offset) { }

        /// <summary>
        /// Обрабатывает изменение вертикального скролла от ScrollViewer.
        /// Конвертирует пиксели в строки и обновляет HexScrollState.
        /// </summary>
        public void SetVerticalOffset(double offset)
        {
            if (_scrollState == null || _metrics == null)
                return;

            if (!_viewportSnapshot.IsValid)
                TryUpdateViewportSnapshot(ActualHeight);

            if (!_viewportSnapshot.IsValid)
                return;

            offset = Math.Max(0, Math.Min(offset, _viewportSnapshot.MaxOffset));
            long targetLine = HexScrollGeometry.PixelsToLine(_scrollState, _viewportSnapshot, offset);

            // Всегда обновляем IScrollInfo для плавного автоповтора при удержании стрелок
            _isUpdatingFromScrollViewer = true;
            try
            {
                _scrollState.ScrollToLine(targetLine);
                _lastNotifiedLine = targetLine;
                UpdateScrollInfoFromState(ActualHeight);
                _scrollOwner?.InvalidateScrollInfo();
            }
            finally
            {
                _isUpdatingFromScrollViewer = false;
            }
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (visual == null || _metrics == null || _scrollState == null)
                return Rect.Empty;

            if (visual == this)
                return rectangle;

            try
            {
                GeneralTransform transform = visual.TransformToAncestor(this);
                Rect bounds = transform.TransformBounds(rectangle);
                double lineHeight = _metrics.LineHeight;
                if (lineHeight <= 0)
                    return bounds;

                double headerHeight = _metrics.LineHeight;
                // bounds.Top отсчитывается от начала панели (включая заголовок)
                // Вычитаем headerHeight, чтобы получить Y относительно начала данных
                // Теперь это соответствует VerticalOffset (без заголовка)
                double y = Math.Max(0, bounds.Top - headerHeight);
                long targetLine = (long)Math.Floor(y / lineHeight);
                _scrollState.ScrollToLine(targetLine);
                return bounds;
            }
            catch (InvalidOperationException)
            {
                return Rect.Empty;
            }
            catch (ArgumentException)
            {
                return Rect.Empty;
            }
        }
        #endregion

        private bool TryUpdateViewportSnapshot(double viewportHeight)
        {
            if (_scrollState == null || _metrics == null)
            {
                _viewportSnapshot = HexScrollViewportSnapshot.Empty;
                _viewportHeight = 0;
                _extentHeight = 0;
                return false;
            }

            double effectiveHeight = viewportHeight;
            if (double.IsNaN(effectiveHeight) || double.IsInfinity(effectiveHeight) || effectiveHeight <= 0)
                effectiveHeight = ActualHeight;

            if (double.IsNaN(effectiveHeight) || double.IsInfinity(effectiveHeight))
                effectiveHeight = 0;

            _viewportSnapshot = HexScrollGeometry.CalculateViewport(_scrollState, _metrics, effectiveHeight);
            _viewportHeight = _viewportSnapshot.ContentViewportHeight;
            _extentHeight = _viewportSnapshot.ContentHeight;

            return _viewportSnapshot.IsValid;
        }
    }

    internal static class HexScrollGeometry
    {
        public static HexScrollViewportSnapshot CalculateViewport(HexScrollState state, HexViewMetrics metrics, double totalViewportHeight)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            double lineHeight = metrics.LineHeight <= 0 ? 1 : metrics.LineHeight;
            double headerHeight = lineHeight;

            double viewportHeight = NormalizeDimension(totalViewportHeight);
            double contentViewportHeight = Math.Max(0, viewportHeight - headerHeight);

            double visibleLinesExact = lineHeight > 0 ? contentViewportHeight / lineHeight : 0;
            if (double.IsNaN(visibleLinesExact) || double.IsInfinity(visibleLinesExact))
            {
                visibleLinesExact = 0;
            }

            int visibleLinesInt = Math.Max(1, (int)Math.Floor(visibleLinesExact));
            double fractionalLines = Math.Max(0, visibleLinesExact - visibleLinesInt);
            double fractionalPixels = fractionalLines * lineHeight;

            long totalLines = state.TotalLines;
            double contentHeight = totalLines * lineHeight;
            double maxOffset = Math.Max(0, contentHeight - contentViewportHeight);

            double effectiveVisible = Math.Max(1.0, Math.Min(totalLines, visibleLinesExact));
            double maxLineDouble = Math.Max(0.0, totalLines - effectiveVisible);
            long maxVisibleLine = (long)Math.Ceiling(maxLineDouble);

            return new HexScrollViewportSnapshot(
                isValid: true,
                lineHeight: lineHeight,
                headerHeight: headerHeight,
                viewportHeight: viewportHeight,
                contentViewportHeight: contentViewportHeight,
                visibleLinesInt: visibleLinesInt,
                visibleLinesExact: visibleLinesExact,
                fractionalPixels: fractionalPixels,
                fractionalLines: fractionalLines,
                contentHeight: contentHeight,
                maxOffset: maxOffset,
                maxVisibleLine: maxVisibleLine
            );
        }

        public static long PixelsToLine(HexScrollState state, HexScrollViewportSnapshot viewport, double offset)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!viewport.IsValid || viewport.LineHeight <= 0)
                return 0;

            double clampedOffset = Math.Max(0, Math.Min(offset, viewport.MaxOffset));
            double adjustedOffset = clampedOffset + viewport.FractionalPixels;
            long targetLine = (long)Math.Floor(adjustedOffset / viewport.LineHeight);

            long maxLine = Math.Max(0, Math.Min(viewport.MaxVisibleLine, state.TotalLines == 0 ? 0 : state.TotalLines - 1));
            return Math.Max(0, Math.Min(targetLine, maxLine));
        }

        public static double LineToPixels(HexScrollViewportSnapshot viewport, long line)
        {
            if (!viewport.IsValid || viewport.LineHeight <= 0)
                return 0;

            double rawOffset = line * viewport.LineHeight - viewport.FractionalPixels;
            if (double.IsNaN(rawOffset) || double.IsInfinity(rawOffset))
                rawOffset = 0;

            return Math.Max(0, Math.Min(rawOffset, viewport.MaxOffset));
        }

        private static double NormalizeDimension(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            return Math.Max(0, value);
        }
    }

    internal readonly struct HexScrollViewportSnapshot
    {
        public static readonly HexScrollViewportSnapshot Empty = new HexScrollViewportSnapshot(false);

        private HexScrollViewportSnapshot(bool isValid)
        {
            IsValid = isValid;
            LineHeight = 0;
            HeaderHeight = 0;
            ViewportHeight = 0;
            ContentViewportHeight = 0;
            VisibleLinesInt = 0;
            VisibleLinesExact = 0;
            FractionalPixels = 0;
            FractionalLines = 0;
            ContentHeight = 0;
            MaxOffset = 0;
            MaxVisibleLine = 0;
        }

        public HexScrollViewportSnapshot(
            bool isValid,
            double lineHeight,
            double headerHeight,
            double viewportHeight,
            double contentViewportHeight,
            int visibleLinesInt,
            double visibleLinesExact,
            double fractionalPixels,
            double fractionalLines,
            double contentHeight,
            double maxOffset,
            long maxVisibleLine)
        {
            IsValid = isValid;
            LineHeight = lineHeight;
            HeaderHeight = headerHeight;
            ViewportHeight = viewportHeight;
            ContentViewportHeight = contentViewportHeight;
            VisibleLinesInt = visibleLinesInt;
            VisibleLinesExact = visibleLinesExact;
            FractionalPixels = fractionalPixels;
            FractionalLines = fractionalLines;
            ContentHeight = contentHeight;
            MaxOffset = maxOffset;
            MaxVisibleLine = maxVisibleLine;
        }

        public bool IsValid { get; }
        public double LineHeight { get; }
        public double HeaderHeight { get; }
        public double ViewportHeight { get; }
        public double ContentViewportHeight { get; }
        public int VisibleLinesInt { get; }
        public double VisibleLinesExact { get; }
        public double FractionalPixels { get; }
        public double FractionalLines { get; }
        public double ContentHeight { get; }
        public double MaxOffset { get; }
        public long MaxVisibleLine { get; }
    }

    public sealed class HexScrollState
    {
        private long _scrollOffset;
        private long _documentLength;
        private int _bytesPerLine = 16;
        private int _visibleLines = 1;

        public event EventHandler? StateChanged;

        public long ScrollOffset => _scrollOffset;
        public long DocumentLength => _documentLength;
        public int BytesPerLine => _bytesPerLine;
        public int VisibleLines => _visibleLines;
        public long CurrentLine => _bytesPerLine == 0 ? 0 : _scrollOffset / _bytesPerLine;
        public long TotalLines => _bytesPerLine == 0 ? 0 : (_documentLength + _bytesPerLine - 1) / _bytesPerLine;

        public void SetDocumentLength(long length)
        {
            length = Math.Max(0, length);
            if (_documentLength == length)
                return;

            _documentLength = length;
            ClampScrollOffset();
            RaiseChanged();
        }

        public void SetBytesPerLine(int bytesPerLine)
        {
            bytesPerLine = Math.Max(1, bytesPerLine);
            if (_bytesPerLine == bytesPerLine)
                return;

            _bytesPerLine = bytesPerLine;
            ClampScrollOffset();
            RaiseChanged();
        }

        public void SetVisibleLines(int visibleLines)
        {
            visibleLines = Math.Max(1, visibleLines);
            if (_visibleLines == visibleLines)
                return;

            _visibleLines = visibleLines;
            ClampScrollOffset();
            RaiseChanged();
        }

        public void ScrollToLine(long lineIndex)
        {
            if (_bytesPerLine == 0)
                return;

            long clampedLine = Math.Max(0, Math.Min(lineIndex, GetMaxVisibleLine()));
            long newOffset = clampedLine * _bytesPerLine;
            if (newOffset == _scrollOffset)
                return;

            _scrollOffset = newOffset;
            RaiseChanged();
        }

        public void ScrollToOffset(long byteOffset)
        {
            if (_bytesPerLine == 0)
                return;

            long alignedOffset = AlignToLine(byteOffset);
            long clampedOffset = Math.Max(0, Math.Min(alignedOffset, GetMaxScrollOffset()));

            if (clampedOffset == _scrollOffset)
                return;

            _scrollOffset = clampedOffset;
            RaiseChanged();
        }

        public void ScrollLines(int lineCount)
        {
            ScrollToLine(CurrentLine + lineCount);
        }

        public void PageUp() => ScrollLines(-_visibleLines);
        public void PageDown() => ScrollLines(_visibleLines);

        public void ScrollToBottom() => ScrollToOffset(GetMaxScrollOffset());

        public bool IsOffsetVisible(long offset)
        {
            if (_documentLength == 0)
                return false;

            var visibleRange = GetVisibleRange();
            return offset >= visibleRange.Start && offset <= visibleRange.End;
        }

        public bool IsOffsetFullyVisible(long offset)
        {
            if (_documentLength == 0)
                return false;

            var visibleRange = GetFullyVisibleRange();
            return offset >= visibleRange.Start && offset <= visibleRange.End;
        }

        public void EnsureOffsetVisible(long offset)
        {
            if (_documentLength == 0)
                return;

            if (IsOffsetFullyVisible(offset))
                return;

            if (offset < _scrollOffset)
            {
                ScrollToOffset(offset);
            }
            else
            {
                long visibleBytes = GetVisibleByteCount();
                long newOffset = offset - visibleBytes + _bytesPerLine;
                ScrollToOffset(newOffset);
            }
        }

        public (long Start, long End) GetVisibleRange()
        {
            if (_documentLength == 0)
                return (0, 0);

            long visibleBytes = GetVisibleByteCount();
            long start = _scrollOffset;
            long end = Math.Min(_documentLength - 1, _scrollOffset + visibleBytes - 1);
            return (start, end);
        }

        public (long Start, long End) GetFullyVisibleRange()
        {
            if (_documentLength == 0)
                return (0, 0);

            long fullyVisibleLines = Math.Max(1, Math.Min(VisibleLines, (int)TotalLines));
            long visibleBytes = fullyVisibleLines * _bytesPerLine;
            long start = _scrollOffset;
            long end = Math.Min(_documentLength - 1, _scrollOffset + visibleBytes - 1);
            return (start, end);
        }

        public long GetMaxScrollOffset()
        {
            if (_documentLength == 0 || _bytesPerLine == 0)
                return 0;

            long totalLines = TotalLines;
            long clampedVisible = Math.Max(1, Math.Min(totalLines, (long)_visibleLines));
            long maxLine = Math.Max(0, totalLines - clampedVisible);
            return maxLine * _bytesPerLine;
        }

        private long GetMaxVisibleLine()
        {
            long totalLines = TotalLines;
            long clampedVisible = Math.Max(1, Math.Min(totalLines, (long)_visibleLines));
            return Math.Max(0, totalLines - clampedVisible);
        }

        private long GetVisibleByteCount()
        {
            if (_bytesPerLine == 0)
                return 0;
            long visibleLines = Math.Max(1, _visibleLines);
            return visibleLines * _bytesPerLine;
        }

        private long AlignToLine(long offset)
        {
            if (_bytesPerLine == 0)
                return 0;

            long line = offset / _bytesPerLine;
            return line * _bytesPerLine;
        }

        private void ClampScrollOffset()
        {
            if (_documentLength == 0)
            {
                _scrollOffset = 0;
                return;
            }

            long maxOffset = GetMaxScrollOffset();
            _scrollOffset = Math.Max(0, Math.Min(AlignToLine(_scrollOffset), maxOffset));
        }

        private void RaiseChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
