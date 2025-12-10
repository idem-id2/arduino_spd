using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Snapshot состояния для рендеринга. Изолирует рендерер от зависимости на HexInputHandler.
    /// </summary>
    internal struct RenderState
    {
        public long CaretOffset { get; init; }
        public long SelectionStart { get; init; }
        public long SelectionEnd { get; init; }
        public bool HasSelection { get; init; }
        public bool LastClickWasInHexArea { get; init; }
        public bool SelectionStartedInHexArea { get; init; }
        public HexInputHandler.HexNibblePosition CurrentNibblePosition { get; init; }

        public bool IsOffsetSelected(long offset) =>
            HasSelection && offset >= SelectionStart && offset <= SelectionEnd;
    }

    /// <summary>
    /// Простой LRU-кэш для глифов offset'ов
    /// </summary>
    internal class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _cache;
        private readonly LinkedList<(TKey key, TValue value)> _lruList;

        public LruCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
            _lruList = new LinkedList<(TKey, TValue)>();
        }

        public bool TryGetValue(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Перемещаем в начало списка (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.value;
                return true;
            }

            value = default;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Обновляем существующий
                _lruList.Remove(existingNode);
                existingNode.Value = (key, value);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Если кэш полон - удаляем самый старый элемент
            if (_cache.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cache.Remove(lastNode.Value.key);
                    _lruList.RemoveLast();
                }
            }

            // Добавляем новый элемент
            var newNode = _lruList.AddFirst((key, value));
            _cache[key] = newNode;
        }

        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }

        public int Count => _cache.Count;
    }

    internal class HexRenderer : IDisposable
    {
        private readonly HexDocument _document;
        private readonly HexViewMetrics _metrics;
        private readonly GlyphCacheManager _glyphCache;
        private readonly object _cacheLock = new object();

        // Кэши
        private Geometry? _cachedGridGeometry;
        private Size _lastGridSize;
        private readonly LruCache<long, GlyphRun?> _offsetGlyphCache;
        
        // Оптимизация рендеринга
        private Size _lastRenderSize = new Size(0, 0);
        private bool _needsRender = true;

        // Лимиты
        private const int MAX_GLYPH_CACHE_SIZE = 500; // Уменьшен размер кэша с LRU стратегией
        private const int MAX_RENDERED_BOOKMARKS = 5000;

        // Кисти для выделения
        private readonly Brush _hexDarkSelectionBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x33, 0x66, 0xCC));
        private readonly Brush _hexLightSelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x66, 0x99, 0xFF));
        private readonly Brush _asciiDarkSelectionBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x33, 0x66, 0xCC));
        private readonly Brush _asciiLightSelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x66, 0x99, 0xFF));
        private readonly Pen _gridPen = new Pen(Brushes.LightGray, 1.0);
        private readonly Pen _caretPen = new Pen(Brushes.Red, 1.5);
        private readonly Dictionary<Color, Brush> _bookmarkBrushes = new Dictionary<Color, Brush>();
        private readonly Dictionary<Color, Pen> _bookmarkRangePens = new Dictionary<Color, Pen>();

        private bool _isDisposed = false;

        // Поддержка HexScrollState
        private HexScrollState _scrollState;
        private readonly Encoding _ansiEncoding;
        private static bool _encodingProviderRegistered;

        public HexRenderer(HexDocument document, HexViewMetrics metrics, HexScrollState scrollState)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _scrollState = scrollState;
            _ansiEncoding = CreateAnsiEncoding();
            _glyphCache = new GlyphCacheManager(metrics.Typeface, metrics.FontSize, metrics.PixelsPerDip, metrics.CharAdvancePx, _ansiEncoding);
            _offsetGlyphCache = new LruCache<long, GlyphRun?>(MAX_GLYPH_CACHE_SIZE);

            _hexDarkSelectionBrush.Freeze();
            _hexLightSelectionBrush.Freeze();
            _asciiDarkSelectionBrush.Freeze();
            _asciiLightSelectionBrush.Freeze();
            _gridPen.Freeze();
            _caretPen.Freeze();
        }

        private static Encoding CreateAnsiEncoding()
        {
            EnsureEncodingProvider();

            try
            {
                int codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                return Encoding.GetEncoding(codePage);
            }
            catch (ArgumentException)
            {
                return Encoding.GetEncoding(1252);
            }
        }

        private static void EnsureEncodingProvider()
        {
            if (_encodingProviderRegistered)
                return;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encodingProviderRegistered = true;
        }

        public void UpdateDpi(float pixelsPerDip)
        {
            _glyphCache.UpdateDpi(pixelsPerDip);
            ClearCache();
            _needsRender = true; // Требуется перерисовка после изменения DPI
        }

        public void ClearCache()
        {
            _needsRender = true; // Требуется перерисовка после очистки кэша
            lock (_cacheLock)
            {
                _offsetGlyphCache.Clear();
                _cachedGridGeometry = null;
            }
        }
        
        /// <summary>
        /// Устанавливает флаг необходимости перерисовки.
        /// Вызывается из HexEditorControl при изменениях состояния.
        /// </summary>
        public void SetNeedsRender(bool needsRender = true)
        {
            _needsRender = needsRender;
        }

        /// <summary>
        /// Рендерит содержимое редактора используя snapshot состояния.
        /// </summary>
        public void Render(DrawingContext context, Size renderSize, RenderState renderState)
        {
            if (context == null || _isDisposed) return;

            try
            {
                // ВАЖНО: Всегда рендерим когда OnRender вызван, т.к. WPF может инвалидировать 
                // визуал по своим причинам (фокус, перекрытие окон и т.д.)
                // Оптимизация только для размера - если размер изменился, сбрасываем кэш
                bool sizeChanged = Math.Abs(renderSize.Width - _lastRenderSize.Width) > 0.1 || 
                                   Math.Abs(renderSize.Height - _lastRenderSize.Height) > 0.1;
                
                if (sizeChanged)
                {
                    _lastRenderSize = renderSize;
                    ClearCacheIfNeeded();
                }
                
                _needsRender = false; // Сбрасываем флаг после рендеринга

                // 1. Фон
                RenderBackground(context, renderSize);

                // 2. Сетка и заголовки
                RenderGrid(context, renderSize);
                RenderColumnHeaders(context);

                if (_scrollState == null)
                    throw new InvalidOperationException("HexRenderer requires HexScrollState to be set");

                long scrollOffset = _scrollState.ScrollOffset;

                // 3. Закладки (под выделением и данными)
                RenderBookmarks(context, renderSize, scrollOffset);
                RenderBookmarkRanges(context, renderSize, scrollOffset);

                // 4. Выделение (под данными)
                RenderSelection(context, renderSize, scrollOffset, renderState);

                // 5. Данные - с инвертированием текста
                RenderHexData(context, renderSize, scrollOffset, renderState);

                // 6. Каретка (поверх всего)
                RenderCaret(context, renderSize, scrollOffset, renderState);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Render error: {ex.Message}");
            }
        }

        private void ClearCacheIfNeeded()
        {
            // LRU-кэш автоматически управляет размером, дополнительная проверка не нужна
        }

        #region Основной рендеринг
        private void RenderBackground(DrawingContext context, Size renderSize)
        {
            context.DrawRectangle(Brushes.White, null, new Rect(renderSize));
        }

        private void RenderGrid(DrawingContext context, Size renderSize)
        {
            if (_cachedGridGeometry == null || _lastGridSize != renderSize)
            {
                _cachedGridGeometry = CreateGridGeometry(renderSize);
                _lastGridSize = renderSize;
            }
            context.DrawGeometry(null, _gridPen, _cachedGridGeometry);
        }

        private Geometry CreateGridGeometry(Size renderSize)
        {
            var geometryGroup = new GeometryGroup();
            double asciiSectionEnd = _metrics.AsciiSectionStart + (_metrics.BytesPerLine * _metrics.AsciiCellWidth);

            // Вертикальные линии
            foreach (double x in new[] { _metrics.FirstVerticalLinePosition, _metrics.HexSectionEnd })
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(x, _metrics.LineHeight),
                    new Point(x, renderSize.Height)));
            }

            // Горизонтальные линии
            for (double y = _metrics.LineHeight; y < renderSize.Height; y += _metrics.LineHeight)
            {
                geometryGroup.Children.Add(new LineGeometry(
                    new Point(0, y),
                    new Point(asciiSectionEnd, y)));
            }

            geometryGroup.Freeze();
            return geometryGroup;
        }

        private void RenderColumnHeaders(DrawingContext context)
        {
            double lineTop = 0;
            int bytesPerLine = _scrollState.BytesPerLine;

            // HEX заголовки
            for (int i = 0; i < bytesPerLine; i++)
            {
                var glyphRun = _glyphCache.GetHeaderGlyphRun($"{i:X2}");
                if (glyphRun != null)
                {
                    double headerX = _metrics.GetHexHeaderPosition(i);
                    double headerY = _metrics.GetBaselineY(lineTop);
                    context.PushTransform(new TranslateTransform(headerX, headerY));
                    context.DrawGlyphRun(Brushes.Blue, glyphRun);
                    context.Pop();
                }
            }

            // ASCII заголовки
            for (int i = 0; i < bytesPerLine; i++)
            {
                var glyphRun = _glyphCache.GetHeaderGlyphRun($"{i:X1}");
                if (glyphRun != null)
                {
                    double glyphRunWidth = GetGlyphRunWidth(glyphRun);
                    double headerX = _metrics.GetAsciiHeaderPosition(i, glyphRunWidth);
                    double headerY = _metrics.GetBaselineY(lineTop);
                    context.PushTransform(new TranslateTransform(headerX, headerY));
                    context.DrawGlyphRun(Brushes.Blue, glyphRun);
                    context.Pop();
                }
            }
        }
        #endregion

        #region Рендеринг данных с инвертированием текста
        private void RenderHexData(DrawingContext context, Size renderSize, long scrollOffset, RenderState renderState)
        {
            if (_document.Length == 0)
            {
                RenderEmptyState(context, renderSize);
                return;
            }

            int bytesPerLine = _scrollState.BytesPerLine;
            double lineHeight = _metrics.LineHeight;

            // Используем полностью видимый диапазон из HexScrollState
            var visibleRange = GetFullyVisibleRange();
            int startLine = (int)(visibleRange.Start / bytesPerLine);
            int endLine = (int)(visibleRange.End / bytesPerLine);

            for (int line = startLine; line <= endLine; line++)
            {
                long lineOffset = (long)line * bytesPerLine;
                if (lineOffset >= _document.Length) break;

                int visualLine = line - startLine;
                double lineTop = (visualLine + 1) * _metrics.LineHeight;
                double lineBottom = (visualLine + 2) * _metrics.LineHeight;

                if (lineBottom > renderSize.Height) break;

                RenderHexLine(context, lineOffset, visualLine, bytesPerLine, scrollOffset, renderState);
            }
        }

        private void RenderHexLine(DrawingContext context, long offset, int visualLine, int bytesPerLine, long scrollOffset, RenderState renderState)
        {
            double lineTop = (visualLine + 1) * _metrics.LineHeight;

            RenderOffset(context, offset, lineTop);

            // HEX байты
            double xHex = _metrics.HexSectionStart;
            for (int i = 0; i < bytesPerLine; i++)
            {
                long currentOffset = offset + i;
                if (currentOffset < _document.Length)
                {
                    byte b = _document.ReadByte(currentOffset);
                    RenderHexByte(context, b, currentOffset, xHex, lineTop, scrollOffset, renderState);
                }
                xHex += _metrics.HexCellWidth;
            }

            // ASCII байты
            double xAscii = _metrics.AsciiSectionStart;
            for (int i = 0; i < bytesPerLine; i++)
            {
                long currentOffset = offset + i;
                if (currentOffset < _document.Length)
                {
                    byte b = _document.ReadByte(currentOffset);
                    RenderAsciiByte(context, b, currentOffset, xAscii, lineTop, scrollOffset, renderState);
                }
                xAscii += _metrics.AsciiCellWidth;
            }
        }

        private void RenderOffset(DrawingContext context, long offset, double lineTop)
        {
            string offsetText = $"{offset:X8}";
            if (!_offsetGlyphCache.TryGetValue(offset, out GlyphRun? offsetGlyphRun))
            {
                offsetGlyphRun = _glyphCache.GetHeaderGlyphRun(offsetText);
                if (offsetGlyphRun != null) 
                    _offsetGlyphCache.Add(offset, offsetGlyphRun);
            }

            if (offsetGlyphRun != null)
            {
                double glyphRunWidth = GetGlyphRunWidth(offsetGlyphRun);
                double offsetSectionWidth = _metrics.FirstVerticalLinePosition - 5;
                double offsetX = (offsetSectionWidth - glyphRunWidth) / 2;
                double offsetY = _metrics.GetBaselineY(lineTop);

                context.PushTransform(new TranslateTransform(
                    _metrics.SnapPosition(offsetX),
                    _metrics.SnapPosition(offsetY)));
                context.DrawGlyphRun(Brushes.Blue, offsetGlyphRun);
                context.Pop();
            }
        }

        private void RenderHexByte(DrawingContext context, byte b, long offset, double x, double lineTop, long scrollOffset, RenderState renderState)
        {
            var glyphRun = _glyphCache.GetHexGlyphRun(b);
            if (glyphRun == null) return;

            bool isSelected = renderState.IsOffsetSelected(offset);
            bool shouldInvertInHex = isSelected && renderState.SelectionStartedInHexArea;

            Brush brush = shouldInvertInHex ? Brushes.White :
                (_document.ModifiedBytes.ContainsKey(offset) ? Brushes.Red : Brushes.Black);

            double hexX = x + _metrics.FirstNibblePosition;
            double hexY = _metrics.GetBaselineY(lineTop);

            context.PushTransform(new TranslateTransform(
                _metrics.SnapPosition(hexX),
                _metrics.SnapPosition(hexY)));
            context.DrawGlyphRun(brush, glyphRun);
            context.Pop();
        }

        private void RenderAsciiByte(DrawingContext context, byte b, long offset, double x, double lineTop, long scrollOffset, RenderState renderState)
        {
            var glyphRun = _glyphCache.GetAnsiGlyphRun(b);
            if (glyphRun == null) return;

            bool isSelected = renderState.IsOffsetSelected(offset);
            bool shouldInvertInAscii = isSelected && !renderState.SelectionStartedInHexArea;

            Brush brush = shouldInvertInAscii ? Brushes.White :
                (_document.ModifiedBytes.ContainsKey(offset) ? Brushes.Red : Brushes.Black);

            double glyphRunWidth = GetGlyphRunWidth(glyphRun);
            double centeredX = x + (_metrics.AsciiCellWidth - glyphRunWidth) / 2;
            double centeredY = _metrics.GetBaselineY(lineTop);

            context.PushTransform(new TranslateTransform(
                _metrics.SnapPosition(centeredX),
                _metrics.SnapPosition(centeredY)));
            context.DrawGlyphRun(brush, glyphRun);
            context.Pop();
        }
        #endregion

        #region Выделение и каретка
        private void RenderSelection(DrawingContext context, Size renderSize, long scrollOffset, RenderState renderState)
        {
            if (!renderState.HasSelection)
            {
                // Подсветка только каретки
                long caretOffset = renderState.CaretOffset;
                if (IsOffsetFullyVisible(caretOffset))
                {
                    var hexRect = _metrics.GetHexByteRect(caretOffset, scrollOffset);
                    var asciiRect = _metrics.GetAsciiByteRect(caretOffset, scrollOffset);

                    var caretHexBrush = renderState.LastClickWasInHexArea ? _hexDarkSelectionBrush : _hexLightSelectionBrush;
                    var caretAsciiBrush = renderState.LastClickWasInHexArea ? _asciiLightSelectionBrush : _asciiDarkSelectionBrush;

                    context.DrawRectangle(caretHexBrush, null, hexRect);
                    context.DrawRectangle(caretAsciiBrush, null, asciiRect);
                }
                return;
            }

            long startOffset = renderState.SelectionStart;
            long endOffset = renderState.SelectionEnd;

            // Используем ПОЛНОСТЬЮ видимый диапазон из HexScrollState
            var visibleRange = GetFullyVisibleRange();
            if (endOffset < visibleRange.Start || startOffset > visibleRange.End)
                return;

            // Определяем цвета для КАЖДОЙ области отдельно
            bool startedInHexArea = renderState.SelectionStartedInHexArea;
            var hexAreaBrush = startedInHexArea ? _hexDarkSelectionBrush : _hexLightSelectionBrush;
            var asciiAreaBrush = startedInHexArea ? _asciiLightSelectionBrush : _asciiDarkSelectionBrush;

            var rects = CalculateRangeRectangles(startOffset, endOffset, scrollOffset, renderSize);

            foreach (var rect in rects)
            {
                if (rect.Top < _metrics.LineHeight || rect.Bottom > renderSize.Height) continue;

                bool isHexArea = rect.Left >= _metrics.HexSectionStart && rect.Left < _metrics.AsciiSectionStart;
                var brush = isHexArea ? hexAreaBrush : asciiAreaBrush;

                context.DrawRectangle(brush, null, rect);
            }
        }

        private void RenderCaret(DrawingContext context, Size renderSize, long scrollOffset, RenderState renderState)
        {
            long caretOffset = renderState.CaretOffset;
            if (!IsOffsetFullyVisible(caretOffset)) return;

            var hexRect = _metrics.GetHexByteRect(caretOffset, scrollOffset);
            var asciiRect = _metrics.GetAsciiByteRect(caretOffset, scrollOffset);

            if (hexRect.Bottom > renderSize.Height || asciiRect.Bottom > renderSize.Height) return;

            if (renderState.LastClickWasInHexArea)
            {
                double nibblePosition = _metrics.GetCaretPosition(renderState.CurrentNibblePosition);
                double caretX = hexRect.Left + nibblePosition;

                context.DrawLine(_caretPen,
                    new Point(caretX, hexRect.Top + 1),
                    new Point(caretX, hexRect.Bottom - 1));
            }
            else
            {
                context.DrawLine(_caretPen,
                    new Point(asciiRect.Left + 1, asciiRect.Top + 1),
                    new Point(asciiRect.Left + 1, asciiRect.Bottom - 1));
            }
        }
        #endregion

        #region Закладки
        private void RenderBookmarks(DrawingContext context, Size renderSize, long scrollOffset)
        {
            if (_document.Bookmarks.Count == 0) return;

            var visibleRange = GetFullyVisibleRange();
            var bookmarksByColor = new Dictionary<Color, GeometryGroup>();
            int renderedCount = 0;

            // Быстрая фильтрация видимых закладок
            foreach (var bookmark in _document.Bookmarks)
            {
                if (bookmark.Key < visibleRange.Start || bookmark.Key > visibleRange.End)
                    continue;

                if (renderedCount >= MAX_RENDERED_BOOKMARKS) break;

                var hexRect = _metrics.GetHexByteRect(bookmark.Key, scrollOffset);

                if (hexRect.Bottom > renderSize.Height) continue;

                var color = bookmark.Value;
                if (!bookmarksByColor.TryGetValue(color, out GeometryGroup? geometryGroup))
                {
                    geometryGroup = new GeometryGroup();
                    bookmarksByColor[color] = geometryGroup;
                }

                var asciiRect = _metrics.GetAsciiByteRect(bookmark.Key, scrollOffset);

                geometryGroup.Children.Add(new RectangleGeometry(hexRect));
                geometryGroup.Children.Add(new RectangleGeometry(asciiRect));
                renderedCount++;
            }

            // Пакетный рендеринг по цветам
            foreach (var kvp in bookmarksByColor)
            {
                kvp.Value.Freeze();
                context.DrawGeometry(GetBookmarkBrush(kvp.Key), null, kvp.Value);
            }
        }

        private void RenderBookmarkRanges(DrawingContext context, Size renderSize, long scrollOffset)
        {
            if (_document.BookmarkRanges.Count == 0) return;

            var visibleRange = GetFullyVisibleRange();

            foreach (var range in _document.BookmarkRanges)
            {
                long startOffset = range.Key;
                long length = range.Value.length;
                Color color = range.Value.color;
                long endOffset = Math.Min(startOffset + length - 1, _document.Length - 1);

                // БЫСТРАЯ ПРОВЕРКА: если диапазон полностью вне видимой области - пропускаем
                if (endOffset < visibleRange.Start || startOffset > visibleRange.End)
                    continue;

                var pen = GetBookmarkRangePen(color);

                // Рисуем ступенчатый внешний контур без внутренних линий
                DrawSteppedRangeOutline(context, startOffset, endOffset, scrollOffset, renderSize, pen);
            }
        }

        /// <summary>
        /// Упрощенное рисование рамки выделения - отдельные прямоугольники для каждой строки
        /// </summary>
        private void DrawSteppedRangeOutline(DrawingContext context, long startOffset, long endOffset,
            long scrollOffset, Size renderSize, Pen pen)
        {
            int bytesPerLine = _scrollState.BytesPerLine;
            var visibleRange = GetFullyVisibleRange();

            // Только видимые строки
            long startLine = Math.Max(visibleRange.Start / bytesPerLine, startOffset / bytesPerLine);
            long endLine = Math.Min(visibleRange.End / bytesPerLine, endOffset / bytesPerLine);

            for (long line = startLine; line <= endLine; line++)
            {
                long lineStart = line * bytesPerLine;
                long lineRangeStart = Math.Max(lineStart, startOffset);
                long lineRangeEnd = Math.Min(lineStart + bytesPerLine - 1, endOffset);

                if (lineRangeStart > lineRangeEnd) continue;

                // Рисуем отдельную рамку для каждой строки
                DrawSingleLineRangeOutline(context, line, lineRangeStart, lineRangeEnd, scrollOffset, renderSize, pen);
            }
        }

        /// <summary>
        /// Рисует рамку выделения для одной строки
        /// </summary>
        private void DrawSingleLineRangeOutline(DrawingContext context, long line, long lineRangeStart, long lineRangeEnd,
            long scrollOffset, Size renderSize, Pen pen)
        {
            int bytesPerLine = _scrollState.BytesPerLine;
            long lineStart = line * bytesPerLine;

            long startInLine = lineRangeStart - lineStart;
            long endInLine = lineRangeEnd - lineStart;

            double visualLine = line - (scrollOffset / bytesPerLine);
            double lineTop = (visualLine + 1) * _metrics.LineHeight;
            double lineBottom = (visualLine + 2) * _metrics.LineHeight;

            if (lineBottom > renderSize.Height) return;

            double cellPadding = 1.2;

            // HEX область
            double hexLeft = _metrics.HexSectionStart + (startInLine * _metrics.HexCellWidth) + cellPadding;
            double hexRight = _metrics.HexSectionStart + ((endInLine + 1) * _metrics.HexCellWidth) - cellPadding;

            // ASCII область  
            double asciiLeft = _metrics.AsciiSectionStart + (startInLine * _metrics.AsciiCellWidth) + cellPadding;
            double asciiRight = _metrics.AsciiSectionStart + ((endInLine + 1) * _metrics.AsciiCellWidth) - cellPadding;

            // Рисуем два отдельных прямоугольника для HEX и ASCII
            DrawRectangleOutline(context, hexLeft, lineTop + cellPadding, hexRight, lineBottom - cellPadding, pen);
            DrawRectangleOutline(context, asciiLeft, lineTop + cellPadding, asciiRight, lineBottom - cellPadding, pen);
        }

        /// <summary>
        /// Рисует прямоугольник по координатам
        /// </summary>
        private void DrawRectangleOutline(DrawingContext context, double left, double top, double right, double bottom, Pen pen)
        {
            context.DrawLine(pen, new Point(left, top), new Point(right, top));
            context.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));
            context.DrawLine(pen, new Point(left, top), new Point(left, bottom));
            context.DrawLine(pen, new Point(right, top), new Point(right, bottom));
        }

        private Brush GetBookmarkBrush(Color color)
        {
            if (!_bookmarkBrushes.TryGetValue(color, out Brush? brush))
            {
                brush = new SolidColorBrush(Color.FromArgb(0x50, color.R, color.G, color.B));
                brush.Freeze();
                _bookmarkBrushes[color] = brush;
            }
            return brush;
        }

        private Pen GetBookmarkRangePen(Color color)
        {
            if (!_bookmarkRangePens.TryGetValue(color, out Pen? pen))
            {
                var brush = new SolidColorBrush(Color.FromArgb(0xFF, color.R, color.G, color.B));
                brush.Freeze();
                pen = new Pen(brush, 1.0) { DashStyle = DashStyles.Dash };
                pen.Freeze();
                _bookmarkRangePens[color] = pen;
            }
            return pen;
        }
        #endregion

        #region Вспомогательные методы
        private List<Rect> CalculateRangeRectangles(long startOffset, long endOffset, long scrollOffset, Size renderSize)
        {
            var rects = new List<Rect>();
            if (startOffset > endOffset) return rects;

            int bytesPerLine = _scrollState.BytesPerLine;
            long startLine = startOffset / bytesPerLine;
            long endLine = endOffset / bytesPerLine;

            var visibleRange = GetFullyVisibleRange();
            long visibleStartLine = visibleRange.Start / bytesPerLine;
            long visibleEndLine = visibleRange.End / bytesPerLine;

            long renderStartLine = Math.Max(startLine, visibleStartLine);
            long renderEndLine = Math.Min(endLine, visibleEndLine);

            if (renderStartLine > renderEndLine) return rects;

            for (long line = renderStartLine; line <= renderEndLine; line++)
            {
                long lineStartOffset = line * bytesPerLine;
                long lineEndOffset = Math.Min((line + 1) * bytesPerLine - 1, _document.Length - 1);

                if (endOffset < lineStartOffset || startOffset > lineEndOffset) continue;

                long lineRangeStart = Math.Max(lineStartOffset, startOffset);
                long lineRangeEnd = Math.Min(lineEndOffset, endOffset);

                long startInLine = lineRangeStart - lineStartOffset;
                long endInLine = lineRangeEnd - lineStartOffset;
                long countInLine = endInLine - startInLine + 1;

            long visualLine = line - (scrollOffset / bytesPerLine);
                double y = (visualLine + 1) * _metrics.LineHeight;
                double lineHeight = _metrics.LineHeight;

                if (y + lineHeight > renderSize.Height) continue;

                // HEX прямоугольник
                double hexX = _metrics.HexSectionStart + (startInLine * _metrics.HexCellWidth);
                double hexWidth = countInLine * _metrics.HexCellWidth;
                var hexRect = new Rect(hexX, y, hexWidth, lineHeight);

                // ASCII прямоугольник  
                double asciiX = _metrics.AsciiSectionStart + (startInLine * _metrics.AsciiCellWidth);
                double asciiWidth = countInLine * _metrics.AsciiCellWidth;
                var asciiRect = new Rect(asciiX, y, asciiWidth, lineHeight);

                if (hexRect.Top >= _metrics.LineHeight)
                {
                    rects.Add(hexRect);
                    rects.Add(asciiRect);
                }
            }

            return rects;
        }

        /// <summary>
        /// Проверка полной видимости смещения через HexScrollState.
        /// ТРЕБУЕТ: валидное состояние скролла (передаётся в конструкторе).
        /// </summary>
        private bool IsOffsetFullyVisible(long offset)
        {
            if (_scrollState == null)
                throw new InvalidOperationException("HexRenderer requires HexScrollState to be set");
            
            return _scrollState.IsOffsetFullyVisible(offset);
        }

        /// <summary>
        /// Получение полностью видимого диапазона через HexScrollState.
        /// ТРЕБУЕТ: валидное состояние скролла (передаётся в конструкторе).
        /// </summary>
        private (long Start, long End) GetFullyVisibleRange()
        {
            if (_scrollState == null)
                throw new InvalidOperationException("HexRenderer requires HexScrollState to be set");
            
            return _scrollState.GetFullyVisibleRange();
        }

        private void RenderEmptyState(DrawingContext context, Size renderSize)
        {
            var glyphRun = _glyphCache.GetHeaderGlyphRun("No data loaded");
            if (glyphRun != null)
            {
                double glyphRunWidth = GetGlyphRunWidth(glyphRun);
                double centerX = (renderSize.Width - glyphRunWidth) / 2;
                double centerY = renderSize.Height / 2 + _metrics.BaselineOffsetInLine;

                context.PushTransform(new TranslateTransform(
                    _metrics.SnapPosition(centerX),
                    _metrics.SnapPosition(centerY)));
                context.DrawGlyphRun(Brushes.Gray, glyphRun);
                context.Pop();
            }
        }

        private double GetGlyphRunWidth(GlyphRun glyphRun)
        {
            if (glyphRun?.AdvanceWidths == null) return 0;
            return glyphRun.AdvanceWidths.Sum();
        }
        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _glyphCache?.Dispose();
            ClearCache();
        }

        private class GlyphCacheManager : IDisposable
        {
            private readonly Typeface _typeface;
            private readonly double _fontSize;
            private float _pixelsPerDip;
            private readonly double _charAdvancePx;

            private readonly GlyphRun?[] _hexGlyphRuns = new GlyphRun[256];
            private readonly GlyphRun?[] _ansiGlyphRuns = new GlyphRun[256];
            private readonly GlyphRun?[] _headerGlyphRuns = new GlyphRun[32];
            private readonly char[] _ansiCharMap = new char[256];
            private readonly Encoding _ansiEncoding;

            public GlyphCacheManager(Typeface typeface, double fontSize, float pixelsPerDip, double charAdvancePx, Encoding ansiEncoding)
            {
                _typeface = typeface;
                _fontSize = fontSize;
                _pixelsPerDip = pixelsPerDip;
                _charAdvancePx = charAdvancePx;
                _ansiEncoding = ansiEncoding ?? Encoding.GetEncoding(1252);
                PrecacheGlyphs();
            }

            public void UpdateDpi(float pixelsPerDip)
            {
                if (Math.Abs(_pixelsPerDip - pixelsPerDip) < 0.001f) return;
                _pixelsPerDip = pixelsPerDip;
                ClearCache();
                PrecacheGlyphs();
            }

            private void PrecacheGlyphs()
            {
                BuildAnsiCharMap();

                // HEX глифы (00-FF)
                for (int i = 0; i < 256; i++)
                    _hexGlyphRuns[i] = CreateHexGlyphRun((byte)i);

                // ANSI глифы
                for (int i = 0; i < 256; i++)
                {
                    _ansiGlyphRuns[i] = CreateSingleCharGlyphRun(_ansiCharMap[i]);
                }

                // Заголовки
                for (int i = 0; i < 16; i++)
                {
                    _headerGlyphRuns[i] = CreateTextGlyphRun($"{i:X2}");
                    char asciiHeaderChar = i < 10 ? (char)('0' + i) : (char)('A' + (i - 10));
                    _headerGlyphRuns[i + 16] = CreateSingleCharGlyphRun(asciiHeaderChar);
                }
            }

            public GlyphRun? GetHexGlyphRun(byte b) => _hexGlyphRuns[b];

            public GlyphRun? GetAnsiGlyphRun(byte b) => _ansiGlyphRuns[b];

            private void BuildAnsiCharMap()
            {
                for (int i = 0; i < 256; i++)
                {
                    char fallback = '.';
                    try
                    {
                        var buffer = new[] { (byte)i };
                        string decoded = _ansiEncoding.GetString(buffer);
                        if (!string.IsNullOrEmpty(decoded))
                        {
                            char ch = decoded[0];
                            fallback = char.IsControl(ch) ? '.' : ch;
                        }
                    }
                    catch (DecoderFallbackException ex)
                    {
                        Debug.WriteLine($"ANSI decode failed for byte {i:X2}: {ex.Message}");
                    }

                    _ansiCharMap[i] = fallback;
                }
            }

            public GlyphRun? GetHeaderGlyphRun(string text)
            {
                if (text.Length == 2)
                {
                    if (text[0] >= '0' && text[0] <= '9' && text[1] >= '0' && text[1] <= '9')
                    {
                        int index = int.Parse(text);
                        if (index >= 0 && index < 16) return _headerGlyphRuns[index];
                    }
                    else if (text[0] >= '0' && text[0] <= '9' && text[1] >= 'A' && text[1] <= 'F')
                    {
                        int index = Convert.ToInt32(text, 16);
                        if (index >= 0 && index < 16) return _headerGlyphRuns[index];
                    }
                }
                else if (text.Length == 1)
                {
                    char c = text[0];
                    if (c >= '0' && c <= '9') return _headerGlyphRuns[16 + (c - '0')];
                    if (c >= 'A' && c <= 'F') return _headerGlyphRuns[16 + 10 + (c - 'A')];
                }

                return CreateTextGlyphRun(text);
            }

            private GlyphRun? CreateHexGlyphRun(byte b)
            {
                try
                {
                    return CreateTextGlyphRun($"{b:X2}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CreateHexGlyphRun error for byte '{b:X2}': {ex.Message}");
                    return null;
                }
            }

            private GlyphRun? CreateSingleCharGlyphRun(char c)
            {
                var glyphTypeface = GetGlyphTypeface();
                if (glyphTypeface == null) return null;

                try
                {
                    if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(c, out ushort glyphIndex))
                        return null;

                    double advance = glyphTypeface.AdvanceWidths[glyphIndex] * _fontSize;
                    return new GlyphRun(
                        glyphTypeface, 0, false, _fontSize, _pixelsPerDip,
                        new ushort[] { glyphIndex }, new Point(0, 0),
                        new double[] { advance }, null, null, null, null, null, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CreateSingleCharGlyphRun error for char '{c}': {ex.Message}");
                    return null;
                }
            }

            private GlyphRun? CreateTextGlyphRun(string text)
            {
                if (string.IsNullOrEmpty(text)) return null;
                var glyphTypeface = GetGlyphTypeface();
                if (glyphTypeface == null) return null;

                try
                {
                    ushort[] glyphIndices = new ushort[text.Length];
                    double[] advanceWidths = new double[text.Length];

                    for (int i = 0; i < text.Length; i++)
                    {
                        if (glyphTypeface.CharacterToGlyphMap.TryGetValue(text[i], out ushort glyphIndex))
                        {
                            glyphIndices[i] = glyphIndex;
                            advanceWidths[i] = glyphTypeface.AdvanceWidths[glyphIndex] * _fontSize;
                        }
                        else
                        {
                            glyphIndices[i] = 0;
                            advanceWidths[i] = _charAdvancePx;
                        }
                    }

                    return new GlyphRun(
                        glyphTypeface, 0, false, _fontSize, _pixelsPerDip,
                        glyphIndices, new Point(0, 0), advanceWidths,
                        null, null, null, null, null, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CreateTextGlyphRun error for text '{text}': {ex.Message}");
                    return null;
                }
            }

            private GlyphTypeface? GetGlyphTypeface()
            {
                return _typeface.TryGetGlyphTypeface(out GlyphTypeface? glyphTypeface) ? glyphTypeface : null;
            }

            private void ClearCache()
            {
                Array.Clear(_hexGlyphRuns, 0, _hexGlyphRuns.Length);
                Array.Clear(_ansiGlyphRuns, 0, _ansiGlyphRuns.Length);
                Array.Clear(_headerGlyphRuns, 0, _headerGlyphRuns.Length);
            }

            public void Dispose()
            {
                ClearCache();
            }
        }
    }
}