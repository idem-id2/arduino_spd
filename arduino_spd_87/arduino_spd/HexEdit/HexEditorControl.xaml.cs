using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HexEditor.HexEdit
{
    public partial class HexEditorControl : UserControl
    {
        #region Constants
        private const double MENU_UPDATE_THROTTLE_MS = 50;
        private const int DEFAULT_FONT_SIZE = 12;
        private const int DEFAULT_BYTES_PER_LINE = 16;
        private const int MOUSE_WHEEL_LINES = 3;

        // Используем централизованную палитру цветов закладок
        // Все цвета определяются в BookmarkColorPalette для избежания дублирования
        // Используем локальные псевдонимы для удобства в контекстном меню
        private static Color RedColor => BookmarkColorPalette.Red;
        private static Color OrangeColor => BookmarkColorPalette.Orange;
        private static Color YellowColor => BookmarkColorPalette.Yellow;
        private static Color GreenColor => BookmarkColorPalette.Green;
        private static Color BlueColor => BookmarkColorPalette.RoyalBlue;
        private static Color DarkBlueColor => BookmarkColorPalette.Blue;
        private static Color MagentaColor => BookmarkColorPalette.Purple;
        private static Color CyanColor => BookmarkColorPalette.DeepPink;
        private static Color BrownColor => BookmarkColorPalette.Brown;
        private static Color DarkGrayColor => BookmarkColorPalette.Black;
        #endregion

        #region Fields
        private readonly HexDocument _document;
        private readonly HexRenderer _renderer;
        private readonly HexInputHandler _inputHandler;
        private readonly HexViewMetrics _metrics;
        private readonly HexEditorPanel _editorPanel;
        private readonly HexScrollState _scrollState;
        private readonly object _renderLock = new object();
        private bool _isDisposed;

        private FontFamily? _currentFontFamily;
        private CancellationTokenSource? _fileLoadCancellation;

        // State tracking
        private bool _isMouseDragging;
        private long _lastMouseOffset = -1;

        // Performance metrics
        private long _totalRenders;
        private double _averageRenderTimeMs;

        // Throttling
        private DateTime _lastMenuUpdateTime = DateTime.MinValue;
        #endregion

        #region Properties and Events
        public bool IsLoading => _inputHandler?.IsLoading ?? false;
        public bool CanUndo => _document?.CanUndo ?? false;
        public bool CanRedo => _document?.CanRedo ?? false;
        public bool CanCopy => _inputHandler?.CanCopy ?? false;
        public bool CanPaste => _inputHandler?.CanPaste ?? false;
        public bool HasSelection => _inputHandler?.HasSelection ?? false;
        public long SelectionStart => _inputHandler?.SelectionStart ?? 0;
        public long SelectionLength => _inputHandler?.SelectionLength ?? 0;
        public int ModifiedBytesCount => _document?.ModifiedBytes.Count ?? 0;
        public long DocumentLength => _document?.Length ?? 0;
        public int BookmarksCount => _document?.Bookmarks.Count ?? 0;
        public bool CanSave => _document?.ModifiedBytes.Count > 0;
        public bool IsModified => _document?.ModifiedBytes.Count > 0;
        public int UndoStackSize => _document?.UndoStackCount ?? 0;
        public int RedoStackSize => _document?.RedoStackCount ?? 0;

        public long ScrollOffset => _scrollState?.ScrollOffset ?? 0;

        public event EventHandler<HexFileLoadProgressEventArgs>? FileLoadProgress;
        public event EventHandler? FileLoadCompleted;
        public event EventHandler<string>? FileLoadError;
        public event EventHandler? SelectionChanged;
        public event EventHandler? CaretPositionChanged;
        public event EventHandler? DataModified;
        #endregion

        #region Constructor
        public HexEditorControl()
        {
            InitializeComponent();
            ConfigureForCrispRendering();

            _document = new HexDocument();

            // Инициализация чистого состояния скролла
            _scrollState = new HexScrollState();
            _scrollState.SetDocumentLength(0);

            var initialDpi = VisualTreeHelper.GetDpi(this);
            _metrics = new HexViewMetrics(
                LoadCustomFont(),
                DEFAULT_FONT_SIZE,
                (float)initialDpi.PixelsPerDip,
                DEFAULT_BYTES_PER_LINE);

            // Создаем InputHandler - он не знает о скролле, только обрабатывает ввод
            _inputHandler = new HexInputHandler(_document);

            // Передаем состояние скролла в HexRenderer
            _renderer = new HexRenderer(_document, _metrics, _scrollState);

            // Настраиваем панель
            _editorPanel = PART_HexPanel;
            _editorPanel.ScrollState = _scrollState;

            SubscribeToEvents();
            SetupInputHandling();

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            Unloaded += OnUnloaded;

            Focusable = true;
            Background = Brushes.Transparent;

#if DEBUG
            // Загружаем тестовые данные только в режиме отладки
            Dispatcher.BeginInvoke(new Action(LoadTestData), DispatcherPriority.Loaded);
#endif
        }

        private void SubscribeToEvents()
        {
            _document.DataChanged += OnDataChanged;
            _document.UndoRedoExecuted += OnUndoRedoExecuted;
            _document.BookmarksChanged += OnBookmarksChanged;

            _inputHandler.SelectionChanged += OnSelectionChanged;
            _inputHandler.CaretMoved += OnCaretMoved;
            _inputHandler.NibblePositionChanged += OnNibblePositionChanged;
            
            // Обрабатываем события каретки для управления скроллом
            _inputHandler.FileLoadProgress += OnFileLoadProgress;
            _inputHandler.FileLoadCompleted += OnFileLoadCompleted;
            _inputHandler.FileLoadError += OnFileLoadError;

            // Подписываемся на StateChanged ТОЛЬКО для перерисовки (не для управления скроллом)
            // Управление скроллом делает HexEditorPanel через IScrollInfo
            _scrollState.StateChanged += OnScrollStateChanged;
        }


        private void SetupInputHandling()
        {
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            PreviewMouseMove += OnPreviewMouseMove;
            // Обрабатываем MouseWheel для поддержки выделения при скролле
            PreviewMouseWheel += OnPreviewMouseWheel;
            KeyDown += OnKeyDown;
        }

        /// <summary>
        /// Обработчик изменения скролла. Помечает контрол для перерисовки.
        /// Управление скроллом делегировано HexEditorPanel через IScrollInfo.
        /// </summary>
        private void OnScrollStateChanged(object? sender, EventArgs e)
        {
            InvalidateRender();
            _renderer?.SetNeedsRender(true);
        }
        #endregion

        #region Scroll Management
        
        /// <summary>
        /// Обеспечивает видимость каретки. Синхронизирует скроллбар только при изменении позиции.
        /// </summary>
        private void EnsureCaretVisible()
        {
            if (_document?.Length == 0) return;

            long caretOffset = _inputHandler.CaretOffset;
            long oldScrollOffset = _scrollState.ScrollOffset;
            
            _scrollState.EnsureOffsetVisible(caretOffset);
            
            if (_scrollState.ScrollOffset != oldScrollOffset)
            {
                SyncScrollbarAndInvalidate();
            }
            else
            {
                InvalidateRender();
            }
        }

        /// <summary>
        /// Синхронизирует скроллбар и запускает перерисовку.
        /// </summary>
        private void SyncScrollbarAndInvalidate()
        {
            _editorPanel?.ForceScrollUpdate();
            InvalidateRender();
        }

        /// <summary>
        /// Помечает контрол для перерисовки и немедленно инвалидирует визуал.
        /// </summary>
        private void InvalidateRender()
        {
            InvalidateVisual();
        }

        /// <summary>
        /// Обновляет метрики панели и принудительно синхронизирует layout.
        /// </summary>
        private void UpdatePanelLayout()
        {
            _editorPanel?.UpdateMetrics(_metrics, _metrics.TotalWidth);
        }

        /// <summary>
        /// Обновляет layout панели на основе текущего состояния.
        /// </summary>
        private void RefreshLayoutFromState()
        {
            UpdatePanelLayout();
        }

        /// <summary>
        /// Принудительно синхронизирует скролл и layout.
        /// </summary>
        public void ForceScrollSynchronization()
        {
            RefreshLayoutFromState();
            InvalidateMeasure();
            InvalidateVisual();
        }

        #endregion

        #region Public API - Управление выделением
        public void SetSelection(long start, long end)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "Start offset cannot be negative");
            if (end < start) throw new ArgumentException("End offset cannot be less than start offset", nameof(end));
            if (_document == null || end >= _document.Length) return;

            _inputHandler.MoveCaretTo(start, false);
            _inputHandler.HandleMouseDrag(end);
            InvalidateRender();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSelection()
        {
            _inputHandler.ClearSelection();
            InvalidateRender();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public (long Start, long End, long Length) GetSelection()
        {
            return (_inputHandler.SelectionStart, _inputHandler.SelectionEnd, _inputHandler.SelectionLength);
        }
        #endregion

        #region Public API - Управление кареткой и навигацией
        public void SetCaretPosition(long offset)
        {
            if (_document == null || offset < 0 || offset >= _document.Length) return;

            _inputHandler.MoveCaretTo(offset, false);
            EnsureCaretVisible();
        }

        public long GetCaretPosition() => _inputHandler.CaretOffset;

        public void ScrollToOffset(long offset)
        {
            _scrollState.ScrollToOffset(offset);
            SyncScrollbarAndInvalidate();
        }

        public void EnsureVisible(long offset)
        {
            SetCaretPosition(offset);
            EnsureCaretVisible();
        }

        public void EnsureRangeVisible(long start, long end)
        {
            if (_document == null || _document.Length == 0)
                return;

            if (start > end)
                (start, end) = (end, start);

            start = Math.Max(0, start);
            end = Math.Min(end, _document.Length - 1);

            long oldOffset = _scrollState.ScrollOffset;
            _scrollState.EnsureOffsetVisible(start);
            _scrollState.EnsureOffsetVisible(end);

            if (_scrollState.ScrollOffset != oldOffset)
            {
                SyncScrollbarAndInvalidate();
            }
            else
            {
                InvalidateRender();
            }
        }
        #endregion

        #region Public API - Работа с данными
        public void InsertData(long offset, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
            if (_document == null || data.Length == 0) return;
            
            _document.PasteData(offset, data);
            InvalidateRender();
        }

        public void DeleteData(long offset, long length)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive");
            if (_document == null) return;

            _document.BeginBatch();
            try
            {
                for (long i = 0; i < length; i++)
                {
                    long currentOffset = offset + i;
                    if (currentOffset < _document.Length)
                        _document.WriteByte(currentOffset, 0x00);
                }
            }
            finally
            {
                _document.EndBatch();
            }
            InvalidateRender();
        }

        public long FindData(byte[] pattern, long startOffset)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            if (pattern.Length == 0) throw new ArgumentException("Pattern cannot be empty", nameof(pattern));
            if (startOffset < 0) throw new ArgumentOutOfRangeException(nameof(startOffset), "Start offset cannot be negative");
            if (_document == null) return -1;

            for (long i = startOffset; i <= _document.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (_document.ReadByte(i + j) != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        public void ReplaceData(long offset, byte[] newData)
        {
            if (newData == null) throw new ArgumentNullException(nameof(newData));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
            
            _document.PasteData(offset, newData);
            InvalidateRender();
        }

        public byte[] ReadBytes(long offset, long length)
        {
            if (_document == null || offset < 0 || length <= 0)
                return Array.Empty<byte>();

            return _document.CopyData(offset, length);
        }

        /// <summary>
        /// Начинает пакетное редактирование. Все изменения до EndBatch() будут объединены в одну команду undo.
        /// </summary>
        public void BeginBatch()
        {
            _document?.BeginBatch();
        }

        /// <summary>
        /// Завершает пакетное редактирование. Все изменения с BeginBatch() будут объединены в одну команду undo.
        /// </summary>
        public void EndBatch()
        {
            _document?.EndBatch();
            InvalidateRender();
        }

        /// <summary>
        /// Быстрая очистка диапазонов байтов (обнуление). Использует оптимизированный метод, который читает данные блоками.
        /// </summary>
        public void ClearByteRanges(List<(long offset, int length)> ranges)
        {
            if (ranges == null || ranges.Count == 0 || _document == null) return;

            // Используем специальный метод для быстрой очистки - читает данные блоками вместо байт за байтом
            _document.ClearByteRangesFast(ranges);
            InvalidateRender();
        }
        #endregion

        #region Public API - Управление закладками
        public void AddBookmark(long offset, Color color)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
            if (_document != null && offset < _document.Length)
            {
                _document.ToggleBookmark(offset, color);
                // InvalidateRender вызывается автоматически через BookmarksChanged
            }
        }

        public void RemoveBookmark(long offset, Color color)
        {
            if (_document != null && _document.Bookmarks.ContainsKey(offset) && _document.Bookmarks[offset] == color)
            {
                _document.ToggleBookmark(offset, color);
                // InvalidateRender вызывается автоматически через BookmarksChanged
            }
        }

        /// <summary>
        /// Массовое добавление закладок с одним обновлением рендера для производительности.
        /// </summary>
        public void AddBookmarks(IEnumerable<long> offsets, Color color)
        {
            if (offsets == null || _document == null) return;

            bool anyAdded = false;
            // Подавляем события при массовых операциях для производительности
            foreach (long offset in offsets)
            {
                if (offset >= 0 && offset < _document.Length)
                {
                    _document.ToggleBookmark(offset, color, suppressEvent: true);
                    anyAdded = true;
                }
            }

            // Вызываем событие один раз после всех операций
            if (anyAdded)
            {
                _document.NotifyBookmarksChanged();
            }
        }

        /// <summary>
        /// Массовое удаление закладок с одним обновлением рендера для производительности.
        /// </summary>
        public void RemoveBookmarks(IEnumerable<long> offsets, Color color)
        {
            if (offsets == null || _document == null) return;

            bool anyRemoved = false;
            // Подавляем события при массовых операциях для производительности
            foreach (long offset in offsets)
            {
                if (_document.Bookmarks.ContainsKey(offset) && _document.Bookmarks[offset] == color)
                {
                    _document.ToggleBookmark(offset, color, suppressEvent: true);
                    anyRemoved = true;
                }
            }

            // Вызываем событие один раз после всех операций
            if (anyRemoved)
            {
                _document.NotifyBookmarksChanged();
            }
        }

        public void RemoveAllBookmarks()
        {
            _document?.RemoveAllBookmarks();
            // InvalidateRender вызывается автоматически через BookmarksChanged
        }

        public IReadOnlyDictionary<long, Color> GetBookmarks() => _document?.Bookmarks ?? new Dictionary<long, Color>();

        public void RemoveBookmarksInRange(long start, long end)
        {
            if (_document == null || start < 0 || end < start) return;

            var toRemove = new List<long>();
            foreach (var bookmark in _document.Bookmarks)
            {
                if (bookmark.Key >= start && bookmark.Key <= end)
                    toRemove.Add(bookmark.Key);
            }

            foreach (var offset in toRemove)
                _document.ToggleBookmark(offset, _document.Bookmarks[offset]);

            // InvalidateRender вызывается автоматически через BookmarksChanged
        }

        public bool HasBookmark(long offset) => _document?.Bookmarks.ContainsKey(offset) ?? false;
        #endregion

        #region Public API - Управление закладками-рамками
        public void AddBookmarkRange(long offset, long length, Color color)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive");
            if (_document != null && offset + length <= _document.Length)
            {
                _document.ToggleBookmarkRange(offset, length, color);
                // InvalidateRender вызывается автоматически через BookmarksChanged
            }
        }

        public void RemoveBookmarkRange(long offset, Color color)
        {
            if (_document != null && _document.BookmarkRanges.ContainsKey(offset) && _document.BookmarkRanges[offset].color == color)
            {
                _document.ToggleBookmarkRange(offset, 0, color);
                // InvalidateRender вызывается автоматически через BookmarksChanged
            }
        }

        public void RemoveAllBookmarkRanges()
        {
            _document?.RemoveAllBookmarkRanges();
            // InvalidateRender вызывается автоматически через BookmarksChanged
        }

        /// <summary>
        /// Массовое добавление закладок-диапазонов с одним обновлением рендера для производительности.
        /// </summary>
        public void AddBookmarkRanges(IEnumerable<(long offset, long length)> ranges, Color color)
        {
            if (ranges == null || _document == null) return;

            bool anyAdded = false;
            // Подавляем события при массовых операциях для производительности
            foreach (var (offset, length) in ranges)
            {
                if (offset >= 0 && length > 0 && offset + length <= _document.Length)
                {
                    _document.ToggleBookmarkRange(offset, length, color, suppressEvent: true);
                    anyAdded = true;
                }
            }

            // Вызываем событие один раз после всех операций
            if (anyAdded)
            {
                _document.NotifyBookmarksChanged();
            }
        }

        /// <summary>
        /// Массовое удаление закладок-диапазонов с одним обновлением рендера для производительности.
        /// </summary>
        public void RemoveBookmarkRanges(IEnumerable<long> offsets, Color color)
        {
            if (offsets == null || _document == null) return;

            bool anyRemoved = false;
            // Подавляем события при массовых операциях для производительности
            foreach (var offset in offsets)
            {
                if (_document.BookmarkRanges.ContainsKey(offset) && _document.BookmarkRanges[offset].color == color)
                {
                    _document.ToggleBookmarkRange(offset, 0, color, suppressEvent: true);
                    anyRemoved = true;
                }
            }

            // Вызываем событие один раз после всех операций
            if (anyRemoved)
            {
                _document.NotifyBookmarksChanged();
            }
        }

        public IReadOnlyDictionary<long, (Color color, long length)> GetBookmarkRanges() =>
            _document?.BookmarkRanges ?? new Dictionary<long, (Color, long)>();

        public bool HasBookmarkRange(long offset) => _document?.BookmarkRanges.ContainsKey(offset) ?? false;
        #endregion

        #region Public API - Состояние и настройки
        public (long Length, int ModifiedBytes, int Bookmarks, int BookmarkRanges) GetDocumentInfo()
        {
            return (_document?.Length ?? 0, ModifiedBytesCount, BookmarksCount, _document?.BookmarkRanges.Count ?? 0);
        }

        /// <summary>
        /// Возвращает диапазон изменённых байтов. Диапазон задаётся в виде [start, end),
        /// где <paramref name="startOffset" /> — минимальный смещённый байт, а
        /// <paramref name="endOffset" /> — смещение, следующее за последним изменением.
        /// Возвращает false, если документ не содержит модификаций.
        /// </summary>
        public bool TryGetModifiedRange(out long startOffset, out long endOffset)
        {
            startOffset = 0;
            endOffset = 0;

            if (_document == null || _document.ModifiedBytes.Count == 0)
                return false;

            long min = long.MaxValue;
            long max = long.MinValue;

            foreach (var offset in _document.ModifiedBytes.Keys)
            {
                if (offset < min)
                    min = offset;

                if (offset > max)
                    max = offset;
            }

            if (min == long.MaxValue || max == long.MinValue)
                return false;

            startOffset = min;
            endOffset = max + 1; // endExclusive
            return true;
        }

        public void ResetModified()
        {
            _document?.SaveToFile(Path.GetTempFileName());
            InvalidateRender();
        }
        #endregion

        #region Public API - Визуальные настройки
        public void SetFontSize(double size)
        {
            if (size <= 0 || size > 100) return; // Валидация размера шрифта
            
            _metrics.UpdateFont(_metrics.Typeface, size);
            _renderer.ClearCache();
            InvalidateRender();
            RefreshLayoutFromState();
        }

        public void SetBytesPerLine(int count)
        {
            if (count < 1 || count > 32) return;

            _scrollState.SetBytesPerLine(count);
            _metrics.SetBytesPerLine(_scrollState.BytesPerLine);
            _renderer.ClearCache();
            InvalidateRender();
            RefreshLayoutFromState();
        }

        public (long Start, long End) GetVisibleRange() => _scrollState.GetVisibleRange();

        public (long Start, long End) GetFullyVisibleRange() => _scrollState.GetFullyVisibleRange();
        #endregion

        #region Public API - Управление истории
        public void ClearHistory() => _document?.ClearUndoRedoHistory();
        #endregion

        #region Основные публичные методы
        public void LoadData(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            _document.SetData(data);
            // Длина документа обновится автоматически через OnDataChanged
            InvalidateRender();
            _renderer?.SetNeedsRender(true);
            RefreshLayoutFromState();
            UpdateAllMenuStates();
        }

        public async Task LoadFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) 
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            try
            {
                _fileLoadCancellation = new CancellationTokenSource();
                FileLoadProgress?.Invoke(this, new HexFileLoadProgressEventArgs(0, 100));
                await Task.Run(() => _document.LoadFile(filePath));

                // Длина документа обновится автоматически через OnDataChanged
                InvalidateRender();
                _renderer?.SetNeedsRender(true);
                RefreshLayoutFromState();

                UpdateAllMenuStates();
                FileLoadCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                FileLoadError?.Invoke(this, ex.Message);
            }
            finally
            {
                _fileLoadCancellation = null;
            }
        }

        public void SaveToFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            try
            {
                _document.SaveToFile(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save error: {ex.Message}");
                throw; // Пробрасываем исключение для обработки вызывающим кодом
            }
        }

        public byte[] GetData() => _document?.GetData() ?? Array.Empty<byte>();

        public void Clear()
        {
            _document.SetData(Array.Empty<byte>());
            // Длина документа обновится автоматически через OnDataChanged
            InvalidateRender();
            RefreshLayoutFromState();
            UpdateAllMenuStates();
        }

        public void Undo() => _document.Undo();
        public void Redo() => _document.Redo();
        public void Copy() => _inputHandler.Copy();
        public void Paste() => _inputHandler.Paste();
        public void FillSelection(byte fillValue)
        {
            _inputHandler.FillSelection(fillValue);
            InvalidateRender();
            UpdateAllMenuStates();
        }
        public void SelectAll() => _inputHandler.SelectAll();
        
        /// <summary>
        /// Создаёт snapshot состояния для рендеринга.
        /// Изолирует рендерер от прямой зависимости на HexInputHandler.
        /// </summary>
        private RenderState CreateRenderState()
        {
            return new RenderState
            {
                CaretOffset = _inputHandler.CaretOffset,
                SelectionStart = _inputHandler.SelectionStart,
                SelectionEnd = _inputHandler.SelectionEnd,
                HasSelection = _inputHandler.HasSelection,
                LastClickWasInHexArea = _inputHandler.LastClickWasInHexArea,
                SelectionStartedInHexArea = _inputHandler.SelectionStartedInHexArea,
                CurrentNibblePosition = _inputHandler.CurrentNibblePosition
            };
        }
        #endregion

        #region DPI and Rendering Configuration
        public void ForceDpiRefresh()
        {
            try
            {
                var currentDpi = VisualTreeHelper.GetDpi(this);
                var pixelsPerDip = (float)currentDpi.PixelsPerDip;

                Debug.WriteLine($"Force refreshing DPI: {pixelsPerDip}");

                _metrics.UpdateDpi(pixelsPerDip);
                _renderer.UpdateDpi(pixelsPerDip);

                InvalidateRender();
                RefreshLayoutFromState();
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ForceDpiRefresh: {ex.Message}");
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            Debug.WriteLine($"DPI changed: {oldDpi.PixelsPerDip} -> {newDpi.PixelsPerDip}");

            var pixelsPerDip = (float)newDpi.PixelsPerDip;
            _metrics.UpdateDpi(pixelsPerDip);
            _renderer.UpdateDpi(pixelsPerDip);

            // Перезапускаем весь layout цикл с новым DPI через BeginInvoke
            // Это гарантирует правильный порядок обновления всех компонентов
            Dispatcher.BeginInvoke(() =>
            {
                InvalidateRender();
                RefreshLayoutFromState();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void ConfigureForCrispRendering()
        {
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);

            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
        }
        #endregion

        #region Font Loading
        private Typeface LoadCustomFont()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string ttfPath = Path.Combine(exeDirectory, "JetBrainsMonoNL-Regular.ttf");

                if (File.Exists(ttfPath))
                {
                    var fontUri = new Uri(ttfPath, UriKind.Absolute);
                    _currentFontFamily = new FontFamily(fontUri, "./#JetBrains Mono NL");
                    Debug.WriteLine("Loaded JetBrainsMonoNL-Regular.ttf for crisp rendering");

                    Dispatcher.BeginInvoke(new Action(UpdateStatusFont), DispatcherPriority.Loaded);
                    return new Typeface(_currentFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                }

                Debug.WriteLine("JetBrainsMonoNL-Regular.ttf not found, using Consolas fallback");
                _currentFontFamily = new FontFamily("Consolas");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load custom font: {ex.Message}, using Consolas fallback");
                _currentFontFamily = new FontFamily("Consolas");
            }

            Dispatcher.BeginInvoke(new Action(UpdateStatusFont), DispatcherPriority.Loaded);
            return new Typeface(_currentFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        }

        private void UpdateStatusFont()
        {
            if (AddressStatusText != null && _currentFontFamily != null)
                AddressStatusText.FontFamily = _currentFontFamily;
        }
        #endregion

        #region Event Handlers
        private void OnFileLoadProgress(object? sender, HexFileLoadProgressEventArgs e) =>
            Dispatcher.BeginInvoke(() => FileLoadProgress?.Invoke(this, e));

        private void OnFileLoadCompleted(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _scrollState.ScrollToOffset(0);
                    _editorPanel?.ForceScrollUpdate();
                    RefreshLayoutFromState();
                    InvalidateMeasure();
                    InvalidateVisual();
                    FileLoadCompleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnFileLoadCompleted: {ex.Message}");
                    FileLoadCompleted?.Invoke(this, EventArgs.Empty);
                }
            }), DispatcherPriority.Render);
        }

        private void OnFileLoadError(object? sender, string error) =>
            Dispatcher.BeginInvoke(() => FileLoadError?.Invoke(this, error));

        private void OnUndoRedoExecuted(object? sender, HexUndoRedoEventArgs e)
        {
            UpdateAllMenuStates();
            DataModified?.Invoke(this, EventArgs.Empty);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.HeightChanged || e.WidthChanged)
            {
                _metrics.UpdateMetrics();
                InvalidateRender();
                _renderer?.SetNeedsRender(true);
                RefreshLayoutFromState();
                
                if (e.HeightChanged)
                {
                    _editorPanel?.ForceScrollUpdate();
                }
            }
        }

        private void OnVisualStateChanged()
        {
            InvalidateRender();
            _renderer?.SetNeedsRender(true);
            UpdateAllMenuStates();
        }

        private void OnDataChanged(object? sender, HexDataChangedEventArgs e)
        {
            OnVisualStateChanged();
            DataModified?.Invoke(this, EventArgs.Empty);
            _scrollState.SetDocumentLength(_document.Length);
            
            if (!e.IsIncremental)
            {
                InvalidateMeasure();
            }
        }
        
        /// <summary>
        /// Обработчик изменения закладок - обновляет только визуализацию без полной перерисовки
        /// Использует BeginInvoke для отложенного обновления, чтобы не блокировать UI поток
        /// </summary>
        private void OnBookmarksChanged(object? sender, EventArgs e)
        {
            // Обновляем только визуализацию закладок, без вызова DataModified и других тяжелых операций
            // Используем BeginInvoke для отложенного обновления (низкий приоритет)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InvalidateRender();
                _renderer?.SetNeedsRender(true);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnSelectionChanged() => OnVisualStateChanged();
        private void OnNibblePositionChanged() => OnVisualStateChanged();
        private void OnCaretMoved()
        {
            EnsureCaretVisible();
            OnVisualStateChanged();
            CaretPositionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Context Menu for Status Text
        private void AddressStatusText_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            UpdateStatusContextMenuItems();

            if (StatusContextMenu != null)
            {
                StatusContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void UpdateStatusContextMenuItems()
        {
            if (StatusContextMenu == null) return;

            foreach (var item in StatusContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    switch (menuItem.Header?.ToString())
                    {
                        case "Copy HEX Offset":
                        case "Copy DEC Offset":
                        case "Copy Current Byte":
                            menuItem.IsEnabled = DocumentLength > 0;
                            break;
                        case "Copy Selection Info":
                            menuItem.IsEnabled = HasSelection;
                            break;
                        case "Copy Modified Bytes Count":
                            menuItem.IsEnabled = ModifiedBytesCount > 0;
                            break;
                        case "Copy Bookmarks Info":
                            menuItem.IsEnabled = BookmarksCount > 0 || GetBookmarkRanges().Count > 0;
                            break;
                    }
                }
            }
        }

        private void CopyFullStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(AddressStatusText.Text))
                {
                    Clipboard.SetText(AddressStatusText.Text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying full status: {ex.Message}");
            }
        }

        private void CopyHexOffset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                long caretPosition = GetCaretPosition();
                string hexOffset = $"0x{caretPosition:X8}";
                Clipboard.SetText(hexOffset);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying HEX offset: {ex.Message}");
            }
        }

        private void CopyDecOffset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                long caretPosition = GetCaretPosition();
                Clipboard.SetText(caretPosition.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying DEC offset: {ex.Message}");
            }
        }

        private void CopyCurrentByte_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                long caretPosition = GetCaretPosition();
                if (caretPosition >= 0 && caretPosition < DocumentLength)
                {
                    byte currentByte = _document.ReadByte(caretPosition);
                    string byteInfo = $"0x{currentByte:X2}";
                    Clipboard.SetText(byteInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying current byte: {ex.Message}");
            }
        }

        private void CopySelectionInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (HasSelection)
                {
                    var selection = GetSelection();
                    string selectionInfo = $"Start: 0x{selection.Start:X8}, End: 0x{selection.End:X8}, Length: {selection.Length} bytes";
                    Clipboard.SetText(selectionInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying selection info: {ex.Message}");
            }
        }

        private void CopyModifiedCount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int modifiedCount = ModifiedBytesCount;
                Clipboard.SetText(modifiedCount.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying modified count: {ex.Message}");
            }
        }

        private void CopyBookmarksInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int singleBookmarks = BookmarksCount;
                int rangeBookmarks = GetBookmarkRanges().Count;
                string bookmarksInfo = $"Single: {singleBookmarks}, Range: {rangeBookmarks}";
                Clipboard.SetText(bookmarksInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying bookmarks info: {ex.Message}");
            }
        }
        #endregion

        #region Context Menu Handlers
        private void UpdateContextMenu()
        {
            if (EditorContextMenu == null) return;

            foreach (var item in EditorContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    string header = menuItem.Header?.ToString() ?? "";
                    menuItem.IsEnabled = header switch
                    {
                        "Copy" => CanCopy,
                        "Paste" => CanPaste,
                        "Fill Selection" => HasSelection,
                        "Add Bookmark" or "Add Range Bookmark" or "Remove Bookmarks" => _document.Length > 0,
                        _ => menuItem.IsEnabled
                    };
                }
            }
        }

        private void UpdateAllMenuStates()
        {
            var now = DateTime.Now;
            if ((now - _lastMenuUpdateTime).TotalMilliseconds >= MENU_UPDATE_THROTTLE_MS)
            {
                _lastMenuUpdateTime = now;
                UpdateAddressStatus();
                UpdateContextMenu();
            }
        }

        private void ContextMenuCopy_Click(object sender, RoutedEventArgs e) => Copy();
        private void ContextMenuPaste_Click(object sender, RoutedEventArgs e) => Paste();
        private void ContextMenuSelectAll_Click(object sender, RoutedEventArgs e) => SelectAll();

        private void FillWith00_Click(object sender, RoutedEventArgs e) => FillSelection(0x00);
        private void FillWithFF_Click(object sender, RoutedEventArgs e) => FillSelection(0xFF);

        private void AddBookmarkInternal(Color color, bool isRange)
        {
            if (_inputHandler.HasSelection)
            {
                long start = _inputHandler.SelectionStart;
                long end = _inputHandler.SelectionEnd;

                if (isRange)
                    _document.ToggleBookmarkRange(start, end - start + 1, color);
                else
                    _document.ToggleBookmarkOnSelection(start, end, color);
            }
            else
            {
                if (isRange)
                    _document.ToggleBookmarkRange(_inputHandler.CaretOffset, 1, color);
                else
                    _document.ToggleBookmark(_inputHandler.CaretOffset, color);
            }
            InvalidateRender();
        }

        // Одиночные закладки (Ctrl+1..0)
        private void AddRedBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(RedColor, false);
        private void AddOrangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(OrangeColor, false);
        private void AddYellowBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(YellowColor, false);
        private void AddGreenBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(GreenColor, false);
        private void AddBlueBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(BlueColor, false);
        private void AddDarkBlueBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(DarkBlueColor, false);
        private void AddMagentaBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(MagentaColor, false);
        private void AddCyanBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(CyanColor, false);
        private void AddBrownBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(BrownColor, false);
        private void AddDarkGrayBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(DarkGrayColor, false);

        // Рамочные закладки (Ctrl+Alt+1..0)
        private void AddRedRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(RedColor, true);
        private void AddOrangeRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(OrangeColor, true);
        private void AddYellowRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(YellowColor, true);
        private void AddGreenRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(GreenColor, true);
        private void AddBlueRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(BlueColor, true);
        private void AddDarkBlueRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(DarkBlueColor, true);
        private void AddMagentaRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(MagentaColor, true);
        private void AddCyanRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(CyanColor, true);
        private void AddBrownRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(BrownColor, true);
        private void AddDarkGrayRangeBookmark_Click(object sender, RoutedEventArgs e) => AddBookmarkInternal(DarkGrayColor, true);

        // Удаление закладок по цвету
        private void RemoveRedBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(RedColor);
        private void RemoveOrangeBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(OrangeColor);
        private void RemoveYellowBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(YellowColor);
        private void RemoveGreenBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(GreenColor);
        private void RemoveBlueBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(BlueColor);
        private void RemoveDarkBlueBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(DarkBlueColor);
        private void RemoveMagentaBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(MagentaColor);
        private void RemoveCyanBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(CyanColor);
        private void RemoveBrownBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(BrownColor);
        private void RemoveDarkGrayBookmark_Click(object sender, RoutedEventArgs e) => _document.RemoveBookmarksByColor(DarkGrayColor);

        private void RemoveAllBookmarks_Click(object sender, RoutedEventArgs e)
        {
            _document.RemoveAllBookmarks();
            InvalidateRender();
        }

        private void RemoveAllRangeBookmarks_Click(object sender, RoutedEventArgs e)
        {
            _document.RemoveAllBookmarkRanges();
            InvalidateRender();
        }
        #endregion

        #region Mouse Input Handling
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // ПРОВЕРКА: разрешаем выделение только в зонах HEX и ASCII
                var position = e.GetPosition(this);

                // Если клик вне зон данных - не обрабатываем как выделение
                if (!IsInDataArea(position))
                {
                    // Но даем фокус контролу
                    Focus();
                    // НЕ помечаем как Handled - даём событиям пройти дальше
                    return;
                }

                Focus();
                CaptureMouse();

                long offset = GetOffsetFromPosition(position);
                bool extendSelection = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool isHexArea = IsInHexArea(position);

                _inputHandler.HandleMouseClick(offset, extendSelection, isHexArea);

                _isMouseDragging = true;
                _lastMouseOffset = offset;
                InvalidateRender();
                UpdateAllMenuStates();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MouseDown error: {ex.Message}");
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_isMouseDragging)
                {
                    var position = e.GetPosition(this);
                    long offset = GetOffsetFromPosition(position);
                    _inputHandler.HandleMouseDrag(offset);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MouseUp error: {ex.Message}");
            }
            finally
            {
                if (_isMouseDragging)
                {
                    ReleaseMouseCapture();
                    _isMouseDragging = false;
                    _lastMouseOffset = -1;
                    InvalidateRender();
                    UpdateAllMenuStates();
                }
                // НЕ помечаем как Handled - даём событиям пройти дальше
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_isMouseDragging && e.LeftButton == MouseButtonState.Pressed)
                {
                    var position = e.GetPosition(this);

                    // ПРОВЕРКА: продолжаем перетаскивание только в зонах HEX/ASCII
                    bool isHexArea = position.X >= _metrics.HexSectionStart && position.X < _metrics.AsciiSectionStart;
                    bool isAsciiArea = position.X >= _metrics.AsciiSectionStart &&
                                      position.X < _metrics.AsciiSectionStart + (_metrics.BytesPerLine * _metrics.AsciiCellWidth);

                    if (!isHexArea && !isAsciiArea)
                    {
                        // Если мышь вышла за пределы зон данных, останавливаем перетаскивание
                        ReleaseMouseCapture();
                        _isMouseDragging = false;
                        _lastMouseOffset = -1;
                        // НЕ помечаем как Handled - даём событиям пройти дальше
                        return;
                    }

                    long offset = GetOffsetFromPosition(position);

                    if (offset != _lastMouseOffset)
                    {
                        _inputHandler.HandleMouseDrag(offset);
                        _lastMouseOffset = offset;
                        InvalidateRender();
                        UpdateAllMenuStates();
                    }

                    AutoScrollDuringDrag(position);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MouseMove error: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает прокрутку колесом мыши с поддержкой выделения.
        /// При drag обрабатываем напрямую, иначе делегируем ScrollViewer.
        /// </summary>
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (_isMouseDragging && e.LeftButton == MouseButtonState.Pressed)
                {
                    if (e.Delta > 0)
                        _scrollState.ScrollLines(-MOUSE_WHEEL_LINES);
                    else
                        _scrollState.ScrollLines(MOUSE_WHEEL_LINES);
                    
                    _editorPanel?.ForceScrollUpdate();
                    
                    var position = e.GetPosition(this);
                    long offset = GetOffsetFromPosition(position);
                    _inputHandler.HandleMouseDrag(offset);
                    _lastMouseOffset = offset;
                    InvalidateRender();
                    UpdateAllMenuStates();
                    
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MouseWheel error: {ex.Message}");
            }
        }

        /// <summary>
        /// Автоматический скролл при перетаскивании мыши у границ viewport.
        /// </summary>
        private void AutoScrollDuringDrag(Point position)
        {
            if (_editorPanel == null || _metrics == null || _document?.Length == 0)
                return;
            
            double headerHeight = _metrics.LineHeight;
            double lineHeight = _metrics.LineHeight;
            if (lineHeight <= 0)
                return;
            
            double totalViewportHeight = _editorPanel.ActualHeight;
            if (totalViewportHeight <= 0)
                totalViewportHeight = ActualHeight;
            
            double dataAreaTop = headerHeight;
            double dataAreaBottom = totalViewportHeight;
            double threshold = lineHeight * 0.5;
            
            bool atTopBoundary = position.Y >= dataAreaTop && position.Y <= dataAreaTop + threshold;
            bool atBottomBoundary = position.Y >= dataAreaBottom - threshold && position.Y <= dataAreaBottom;
            
            if (atTopBoundary && _scrollState.ScrollOffset > 0)
            {
                _scrollState.ScrollLines(-1);
                SyncScrollbarAndInvalidate();
            }
            else if (atBottomBoundary && _scrollState.ScrollOffset < _scrollState.GetMaxScrollOffset())
            {
                _scrollState.ScrollLines(1);
                SyncScrollbarAndInvalidate();
            }
        }
        #endregion

        #region Keyboard Input
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                bool isHexArea = _inputHandler.LastClickWasInHexArea;
                
                // Передаём геометрию для навигации
                _inputHandler.HandleKeyPress(e.Key, isHexArea, 
                    _scrollState.BytesPerLine, 
                    _scrollState.VisibleLines);

                InvalidateRender();
                UpdateAllMenuStates();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KeyDown error: {ex.Message}");
            }
        }
        #endregion

        #region Data Management
        private async void LoadTestData()
        {
            try
            {
                await Task.Run(() =>
                {
                    byte[] testData = new byte[2048];
                    for (int i = 0; i < testData.Length; i++)
                        testData[i] = (byte)(i % 256);
                    return testData;
                }).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                        LoadData(task.Result);
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadTestData error: {ex.Message}");
            }
        }

        private void UpdateAddressStatus()
        {
            if (AddressStatusText == null) return;

            long caretOffset = _inputHandler.CaretOffset;
            int modifiedBytesCount = ModifiedBytesCount;
            int bookmarkCount = BookmarksCount;
            int bookmarkRangeCount = GetBookmarkRanges().Count;
            string warningMessage = string.Empty;

            string status;
            if (_inputHandler.HasSelection)
            {
                long start = _inputHandler.SelectionStart;
                long end = _inputHandler.SelectionEnd;
                long length = _inputHandler.SelectionLength;

                status = $"SELECTION | HEX: 0x{start:X8}-0x{end:X8} | DEC: {start}-{end} | Length: {length} bytes";

                if (length > 5000)
                    warningMessage = $"Выделение слишком большое ({length} байт)";
            }
            else
            {
                byte currentByte = _document.ReadByte(caretOffset);
                char asciiChar = (currentByte >= 32 && currentByte <= 126) ? (char)currentByte : '.';

                status = $"OFFSET | HEX: 0x{caretOffset:X8} | DEC: {caretOffset} | " +
                         $"BYTE: 0x{currentByte:X2} ('{asciiChar}')";
            }

            if (_inputHandler.IsLoading && string.IsNullOrEmpty(warningMessage))
                warningMessage = "Загрузка данных...";

            AddressStatusText.Text = status;
            UpdateSecondaryStatus(warningMessage, modifiedBytesCount, bookmarkCount, bookmarkRangeCount);
        }

        private void UpdateSecondaryStatus(string warning, int modifiedCount, int bookmarkCount, int bookmarkRangeCount)
        {
            if (WarningStatusText != null)
            {
                bool hasWarning = !string.IsNullOrWhiteSpace(warning);
                WarningStatusText.Text = hasWarning ? warning : "Предупреждений нет";
                WarningStatusText.Foreground = hasWarning ? Brushes.OrangeRed : Brushes.Gray;
            }

            if (ModifiedStatusText != null)
            {
                ModifiedStatusText.Text = $"Изменено: {modifiedCount}";
                ModifiedStatusText.Foreground = modifiedCount > 0 ? Brushes.DarkRed : Brushes.Gray;
            }

            if (EditorBookmarkStatusText != null)
            {
                EditorBookmarkStatusText.Text = $"Закладки: {bookmarkCount} / {bookmarkRangeCount}";
            }
        }
        #endregion

        #region Coordinate Conversion
        /// <summary>
        /// Преобразует экранные координаты в offset документа с учётом зон HEX/ASCII и скролла.
        /// </summary>
        private long GetOffsetFromPosition(Point position)
        {
            if (_document?.Length == 0) return 0;

            bool isHexArea = position.X >= _metrics.HexSectionStart && position.X < _metrics.AsciiSectionStart;
            bool isAsciiArea = position.X >= _metrics.AsciiSectionStart &&
                              position.X < _metrics.AsciiSectionStart + (_metrics.BytesPerLine * _metrics.AsciiCellWidth);

            if (!isHexArea && !isAsciiArea)
            {
                return _inputHandler.CaretOffset;
            }

            int bytesPerLine = _metrics.BytesPerLine;
            double lineHeight = _metrics.LineHeight;

            double contentY = Math.Max(0, position.Y - _metrics.LineHeight);
            int line = Math.Max(0, (int)(contentY / lineHeight));

            long offsetInLine = CalculateOffsetInLine(position);
            offsetInLine = Math.Clamp(offsetInLine, 0, bytesPerLine - 1);

            long lineStartOffset = _scrollState.ScrollOffset + (line * bytesPerLine);
            long offset = lineStartOffset + offsetInLine;
            if (_document == null || _document.Length == 0)
                return 0;

            long maxOffset = Math.Max(0, _document.Length - 1);
            return Math.Clamp(offset, 0, maxOffset);
        }

        /// <summary>
        /// Вычисляет offset внутри строки на основе X-координаты.
        /// Определяет область (HEX/ASCII) и вычисляет индекс байта в строке.
        /// </summary>
        private long CalculateOffsetInLine(Point position)
        {
            bool isHexArea = IsInHexArea(position);
            bool isAsciiArea = IsInAsciiArea(position);

            if (isHexArea)
            {
                double relativeX = position.X - _metrics.HexSectionStart;
                return (long)(relativeX / _metrics.HexCellWidth);
            }
            else if (isAsciiArea)
            {
                double relativeX = position.X - _metrics.AsciiSectionStart;
                return (long)(relativeX / _metrics.AsciiCellWidth);
            }

            return 0;
        }

        /// <summary>
        /// Проверяет, находится ли точка в зоне HEX.
        /// </summary>
        private bool IsInHexArea(Point position) =>
            position.X >= _metrics.HexSectionStart && position.X < _metrics.AsciiSectionStart;

        /// <summary>
        /// Проверяет, находится ли точка в зоне ASCII.
        /// </summary>
        private bool IsInAsciiArea(Point position) =>
            position.X >= _metrics.AsciiSectionStart &&
            position.X < _metrics.AsciiSectionStart + (_metrics.BytesPerLine * _metrics.AsciiCellWidth);

        /// <summary>
        /// Проверяет, находится ли точка в области данных (HEX или ASCII).
        /// </summary>
        private bool IsInDataArea(Point position) =>
            IsInHexArea(position) || IsInAsciiArea(position);
        #endregion

        #region Rendering
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InvalidateRender();
            _renderer?.SetNeedsRender(true);
            RefreshLayoutFromState();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                base.OnRender(drawingContext);

                if (_editorPanel == null)
                    return;

                var panelSize = _editorPanel.RenderSize;
                if (panelSize.Width <= 0 || panelSize.Height <= 0)
                    return;

                var panelOrigin = _editorPanel.TranslatePoint(new Point(0, 0), this);
                var panelRect = new Rect(panelOrigin, panelSize);

                drawingContext.PushClip(new RectangleGeometry(panelRect));
                drawingContext.PushTransform(new TranslateTransform(panelOrigin.X, panelOrigin.Y));

                try
                {
                    lock (_renderLock)
                    {
                        // Создаём snapshot состояния для рендеринга (изолируем рендерер от InputHandler)
                        var renderState = CreateRenderState();
                        // HexRenderer.Render() сам рисует белый фон
                        _renderer.Render(drawingContext, panelSize, renderState);
                    }
                }
                finally
                {
                    drawingContext.Pop(); // transform
                    drawingContext.Pop(); // clip
                }
            }
            catch (Exception ex)
            {
                double fallbackWidth = Math.Max(0, _editorPanel?.RenderSize.Width ?? ActualWidth);
                double fallbackHeight = Math.Max(0, _editorPanel?.RenderSize.Height ?? ActualHeight);

                if (fallbackWidth > 0 && fallbackHeight > 0)
                    drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, fallbackWidth, fallbackHeight));

                Debug.WriteLine($"OnRender error: {ex.Message}");
            }

            stopwatch.Stop();
            UpdateMetrics(stopwatch.ElapsedMilliseconds);
        }

        private void UpdateMetrics(long renderTimeMs)
        {
            _totalRenders++;
            _averageRenderTimeMs = (_averageRenderTimeMs * (_totalRenders - 1) + renderTimeMs) / _totalRenders;

            if (_totalRenders % 100 == 0)
                Debug.WriteLine($"Render Metrics: Last={renderTimeMs}ms, Avg={_averageRenderTimeMs:F2}ms");
        }
        #endregion

        #region File Operations
        public async Task OpenFileAsync(string filePath) => await _inputHandler.OpenFileAsync(filePath);
        public void OpenFile(string filePath) => _ = OpenFileAsync(filePath);
        public void SaveFile(string filePath) => _document.SaveToFile(filePath);
        public async Task SaveFileAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            try
            {
                await _document.SaveToFileAsync(filePath, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
                throw; // Пробрасываем исключение для обработки вызывающим кодом
            }
        }
        public void CancelFileLoad() => _fileLoadCancellation?.Cancel();
        #endregion

        #region Cleanup
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Не уничтожаем документ при временном удалении из визуального дерева (например, при переключении вкладок).
            // Очистка ресурсов выполняется вручную через DisposeResources при закрытии приложения.
        }

        public void DisposeResources()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_document != null)
            {
                _document.DataChanged -= OnDataChanged;
                _document.UndoRedoExecuted -= OnUndoRedoExecuted;
                _document.BookmarksChanged -= OnBookmarksChanged;
            }

            if (_inputHandler != null)
            {
                _inputHandler.SelectionChanged -= OnSelectionChanged;
                _inputHandler.CaretMoved -= OnCaretMoved;
                _inputHandler.NibblePositionChanged -= OnNibblePositionChanged;
                _inputHandler.FileLoadProgress -= OnFileLoadProgress;
                _inputHandler.FileLoadCompleted -= OnFileLoadCompleted;
                _inputHandler.FileLoadError -= OnFileLoadError;
            }

            if (_scrollState != null)
            {
                _scrollState.StateChanged -= OnScrollStateChanged;
            }

            _fileLoadCancellation?.Cancel();
            _fileLoadCancellation?.Dispose();

            _renderer?.Dispose();
            _document?.Dispose();
        }
        #endregion
    }
}