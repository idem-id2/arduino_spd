using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Централизованная палитра цветов закладок для hex-редактора.
    /// Все цвета закладок определяются в одном месте для избежания дублирования.
    /// 
    /// 10 стандартных контрастных цветов из System.Windows.Media.Colors.
    /// Подобраны с учетом прозрачности 0x50 (31%) на белом фоне для максимальной различимости.
    /// 
    /// Соответствие клавишам:
    /// - Ctrl+1..0 / Ctrl+Alt+1..0 = установка закладок (одиночных/рамочных)
    /// - NumPad 1..0 = альтернативная поддержка
    /// </summary>
    internal static class BookmarkColorPalette
    {
        #region Color Constants
        
        /// <summary>🔴 Red - Ctrl+1 / Ctrl+Alt+1</summary>
        public static Color Red => Colors.Red;
        
        /// <summary>🟠 Orange - Ctrl+2 / Ctrl+Alt+2</summary>
        public static Color Orange => Colors.Orange;
        
        /// <summary>🟡 Yellow - Ctrl+3 / Ctrl+Alt+3</summary>
        public static Color Yellow => Colors.Yellow;
        
        /// <summary>🟢 Green - Ctrl+4 / Ctrl+Alt+4</summary>
        public static Color Green => Colors.Green;
        
        /// <summary>🔵 RoyalBlue - Ctrl+5 / Ctrl+Alt+5</summary>
        public static Color RoyalBlue => Colors.RoyalBlue;
        
        /// <summary>🔷 Blue - Ctrl+6 / Ctrl+Alt+6</summary>
        public static Color Blue => Colors.Blue;
        
        /// <summary>🟣 Purple - Ctrl+7 / Ctrl+Alt+7</summary>
        public static Color Purple => Colors.Purple;
        
        /// <summary>🩷 DeepPink - Ctrl+8 / Ctrl+Alt+8</summary>
        public static Color DeepPink => Colors.DeepPink;
        
        /// <summary>🟤 Brown - Ctrl+9 / Ctrl+Alt+9</summary>
        public static Color Brown => Colors.Brown;
        
        /// <summary>⚫ Black - Ctrl+0 / Ctrl+Alt+0</summary>
        public static Color Black => Colors.Black;
        
        #endregion

        #region Color Array (for index access)
        
        /// <summary>
        /// Массив всех цветов закладок в порядке их соответствия клавишам 1..0.
        /// Индекс 0 = Red (Ctrl+1), индекс 9 = Black (Ctrl+0).
        /// </summary>
        public static readonly Color[] ColorArray = new[]
        {
            Colors.Red,        // [0] - Ctrl+1
            Colors.Orange,     // [1] - Ctrl+2
            Colors.Yellow,     // [2] - Ctrl+3
            Colors.Green,      // [3] - Ctrl+4
            Colors.RoyalBlue,  // [4] - Ctrl+5
            Colors.Blue,       // [5] - Ctrl+6
            Colors.Purple,     // [6] - Ctrl+7
            Colors.DeepPink,   // [7] - Ctrl+8
            Colors.Brown,      // [8] - Ctrl+9
            Colors.Black       // [9] - Ctrl+0
        };
        
        #endregion

        #region Key to Color Mapping
        
        /// <summary>
        /// Словарь соответствия клавиш клавиатуры цветам закладок.
        /// Поддерживает как основные цифровые клавиши (D1..D0), так и NumPad (NumPad1..NumPad0).
        /// </summary>
        public static readonly Dictionary<Key, Color> KeyToColorMap = new()
        {
            // Основные цифровые клавиши
            [Key.D1] = Colors.Red,
            [Key.D2] = Colors.Orange,
            [Key.D3] = Colors.Yellow,
            [Key.D4] = Colors.Green,
            [Key.D5] = Colors.RoyalBlue,
            [Key.D6] = Colors.Blue,
            [Key.D7] = Colors.Purple,
            [Key.D8] = Colors.DeepPink,
            [Key.D9] = Colors.Brown,
            [Key.D0] = Colors.Black,
            
            // NumPad поддержка (те же цвета)
            [Key.NumPad1] = Colors.Red,
            [Key.NumPad2] = Colors.Orange,
            [Key.NumPad3] = Colors.Yellow,
            [Key.NumPad4] = Colors.Green,
            [Key.NumPad5] = Colors.RoyalBlue,
            [Key.NumPad6] = Colors.Blue,
            [Key.NumPad7] = Colors.Purple,
            [Key.NumPad8] = Colors.DeepPink,
            [Key.NumPad9] = Colors.Brown,
            [Key.NumPad0] = Colors.Black
        };
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Получает цвет закладки по индексу (0-9).
        /// </summary>
        /// <param name="index">Индекс цвета (0 = Red/Ctrl+1, 9 = Black/Ctrl+0)</param>
        /// <returns>Цвет закладки или Black по умолчанию при невалидном индексе</returns>
        public static Color GetColorByIndex(int index)
        {
            if (index >= 0 && index < ColorArray.Length)
                return ColorArray[index];
            return Black; // Fallback
        }
        
        /// <summary>
        /// Получает цвет закладки по клавише.
        /// </summary>
        /// <param name="key">Клавиша (D1..D0 или NumPad1..NumPad0)</param>
        /// <param name="color">Найденный цвет, если клавиша соответствует цвету</param>
        /// <returns>True, если клавиша соответствует цвету закладки</returns>
        public static bool TryGetColorByKey(Key key, out Color color)
        {
            return KeyToColorMap.TryGetValue(key, out color);
        }
        
        #endregion
    }

    /// <summary>
    /// Обработчик пользовательского ввода для hex-редактора.
    /// 
    /// АРХИТЕКТУРНЫЕ ПРИНЦИПЫ:
    /// - Separation of Concerns: обрабатывает только ввод, не управляет скроллом и рендерингом
    /// - Event-driven: поднимает события для координации с другими компонентами
    /// - Stateful: хранит состояние каретки, выделения, режима редактирования
    /// 
    /// ОТВЕТСТВЕННОСТИ:
    /// - Обработка клавиатуры (hex/ascii ввод, навигация)
    /// - Обработка мыши (клики, перетаскивание для выделения)
    /// - Управление выделением
    /// - Управление позицией каретки и nibble
    /// - Копирование/вставка/заполнение
    /// 
    /// НЕ ОТВЕЧАЕТ ЗА:
    /// - Скроллинг (управляется через события CaretMoved)
    /// - Рендеринг (использует RenderState для изоляции)
    /// - Координатные преобразования (делегированы вызывающему коду)
    /// </summary>
    internal class HexInputHandler
    {
        public enum HexCaretDirection { Left, Right, Up, Down, PageUp, PageDown }
        public enum HexNibblePosition { High, Low }

        private readonly HexDocument _document;

        // Состояние выделения
        private long _selectionStart, _selectionEnd;
        private bool _isSelecting, _selectionStartedInHexArea = true;

        private long _caretOffset;
        private HexNibblePosition _currentNibblePosition = HexNibblePosition.High;
        private bool _lastClickWasInHexArea = true;
        private bool _isLoadingFile = false;
        private CancellationTokenSource? _fileLoadCancellation;
        private byte[]? _clipboardData;

        // Используем централизованную палитру цветов закладок
        // Все цвета определяются в BookmarkColorPalette для избежания дублирования

        // Свойства выделения
        public long SelectionStart => Math.Min(_selectionStart, _selectionEnd);
        public long SelectionEnd => Math.Max(_selectionStart, _selectionEnd);
        public long SelectionLength => SelectionEnd - SelectionStart + 1;
        public bool HasSelection => _selectionStart != _selectionEnd;
        public bool IsSelecting => _isSelecting;
        public bool SelectionStartedInHexArea => _selectionStartedInHexArea;

        // Основные свойства
        public long CaretOffset => _caretOffset;
        public bool LastClickWasInHexArea => _lastClickWasInHexArea;
        public HexNibblePosition CurrentNibblePosition => _currentNibblePosition;
        public bool IsLoading => _isLoadingFile;
        public bool CanCopy => HasSelection || _document.Length > 0;
        public bool CanPaste => (_clipboardData is { Length: > 0 }) || HexClipboard.TryPeekBytes(out _);

        // События
        public event Action? SelectionChanged;
        public event Action? CaretMoved;
        public event Action? NibblePositionChanged;
        public event EventHandler<HexFileLoadProgressEventArgs>? FileLoadProgress;
        public event EventHandler? FileLoadCompleted;
        public event EventHandler<string>? FileLoadError;

        /// <summary>
        /// Обработчик ввода для hex-редактора.
        /// Обрабатывает ввод и поднимает события. Скролл управляется извне.
        /// </summary>
        public HexInputHandler(HexDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _document.UndoRedoExecuted += OnUndoRedoExecuted;
        }

        private void OnUndoRedoExecuted(object? sender, HexUndoRedoEventArgs e)
            => MoveCaretTo(e.TargetOffset, false);

        #region Управление выделением
        public void StartSelection(long offset)
        {
            _selectionStart = _selectionEnd = offset;
            _isSelecting = true;
            SelectionChanged?.Invoke();
        }

        public void UpdateSelection(long offset)
        {
            if (!_isSelecting) return;
            _selectionEnd = offset;
            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            _selectionStart = _selectionEnd;
            _isSelecting = false;
            SelectionChanged?.Invoke();
        }

        public bool Contains(long offset) => offset >= SelectionStart && offset <= SelectionEnd;

        public IEnumerable<long> GetSelectedOffsets()
        {
            for (long offset = SelectionStart; offset <= SelectionEnd; offset++)
                yield return offset;
        }
        #endregion

        #region Оптимизированная обработка ввода
        /// <summary>
        /// Обрабатывает нажатие клавиши.
        /// </summary>
        /// <param name="key">Нажатая клавиша</param>
        /// <param name="isHexView">Режим HEX-ввода</param>
        /// <param name="bytesPerLine">Количество байт на строку (для навигации)</param>
        /// <param name="visibleLines">Количество видимых строк (для PageUp/Down)</param>
        public void HandleKeyPress(Key key, bool isHexView, int bytesPerLine = 16, int visibleLines = 10)
        {
            bool isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool isControlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isAltPressed = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

            // Обработка закладок:
            // Ctrl+1..0 = одиночные закладки (точки)
            // Ctrl+Alt+1..0 = рамочные закладки (те же цвета)
            if (isControlPressed && BookmarkColorPalette.TryGetColorByKey(key, out var color))
            {
                bool isRange = isAltPressed; // Alt = рамка, без Alt = точка
                HandleBookmarkKey(color, isRange);
                return;
            }

            // Упрощенная логика ввода
            if (isControlPressed)
            {
                // Ctrl+Delete = заполнение 0xFF (аналог Delete для 0x00)
                if (key == Key.Delete)
                {
                    HandleFillCommand(0xFF);
                    return;
                }
                HandleControlNavigation(key);
            }
            else if (isShiftPressed && IsNavigationKey(key))
            {
                // Shift + навигационные клавиши = выделение
                HandleSelectionNavigation(key, bytesPerLine, visibleLines);
            }
            else if (isHexView && IsHexDigit(key))
            {
                // HEX режим - только hex цифры
                HandleHexDigit(key, bytesPerLine, visibleLines);
            }
            else if (!isHexView && IsTypableAsciiKey(key))
            {
                // ASCII режим - любые печатные символы (включая Shift+буквы для заглавных и Shift+цифры для спецсимволов)
                HandleAsciiInput(key, bytesPerLine, visibleLines);
            }
            else
            {
                // Обычный Delete (без Ctrl) обрабатывается здесь и делает 0x00
                HandleCaretNavigation(key, bytesPerLine, visibleLines);
            }
        }

        /// <summary>
        /// Проверяет, является ли клавиша навигационной (стрелки, Home, End, PageUp/Down)
        /// </summary>
        private bool IsNavigationKey(Key key) =>
            key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down ||
            key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown;

        private bool IsHexDigit(Key key) =>
            (key >= Key.D0 && key <= Key.D9) ||
            (key >= Key.NumPad0 && key <= Key.NumPad9) ||
            (key >= Key.A && key <= Key.F);

        private bool IsTypableAsciiKey(Key key) =>
            (key >= Key.A && key <= Key.Z) ||
            (key >= Key.D0 && key <= Key.D9) ||
            (key >= Key.NumPad0 && key <= Key.NumPad9) ||
            key == Key.Space ||
            (key >= Key.Oem1 && key <= Key.Oem102) ||
            key == Key.OemComma || key == Key.OemPeriod || key == Key.OemMinus || key == Key.OemPlus;

        private void HandleHexDigit(Key key, int bytesPerLine, int visibleLines)
        {
            byte digit = KeyToHexDigit(key);
            if (digit > 15) return;

            byte newByte;

            if (_currentNibblePosition == HexNibblePosition.High)
            {
                // БАГ #8: Режим OVERWRITE - при начале ввода игнорируем старое значение (0x00)
                // Это позволяет просто начать печатать новое значение
                newByte = (byte)(digit << 4); // Старый low nibble = 0
                _currentNibblePosition = HexNibblePosition.Low;
                
                // Записываем байт на текущей позиции
                _document.WriteByte(_caretOffset, newByte);
            }
            else
            {
                // Low nibble - сохраняем уже введенный high nibble
                byte currentByte = _document.ReadByte(_caretOffset);
                newByte = (byte)((currentByte & 0xF0) | digit);
                
                // Записываем байт на текущей позиции ДО перемещения каретки
                _document.WriteByte(_caretOffset, newByte);
                
                _currentNibblePosition = HexNibblePosition.High;
                MoveCaret(HexCaretDirection.Right, false, bytesPerLine, visibleLines);
            }

            NibblePositionChanged?.Invoke();
        }

        private byte KeyToHexDigit(Key key) => key switch
        {
            >= Key.D0 and <= Key.D9 => (byte)(key - Key.D0),
            >= Key.NumPad0 and <= Key.NumPad9 => (byte)(key - Key.NumPad0),
            >= Key.A and <= Key.F => (byte)(10 + (key - Key.A)),
            _ => 16
        };

        private void HandleAsciiInput(Key key, int bytesPerLine, int visibleLines)
        {
            char inputChar = KeyToChar(key);
            if (inputChar == '\0') return;

            _document.WriteByte(_caretOffset, (byte)inputChar);
            MoveCaret(HexCaretDirection.Right, false, bytesPerLine, visibleLines);
            NibblePositionChanged?.Invoke();
        }

        private char KeyToChar(Key key)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            return key switch
            {
                >= Key.A and <= Key.Z => (char)((key - Key.A) + (shift ? 'A' : 'a')),
                >= Key.D0 and <= Key.D9 => shift ? KeyToCharWithShift(key) : (char)('0' + (key - Key.D0)),
                >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
                Key.Space => ' ',
                Key.OemComma => shift ? '<' : ',',
                Key.OemPeriod => shift ? '>' : '.',
                Key.OemMinus => shift ? '_' : '-',
                Key.OemPlus => shift ? '+' : '=',
                Key.Oem1 => shift ? ':' : ';',
                Key.Oem2 => shift ? '?' : '/',
                Key.Oem3 => shift ? '~' : '`',
                Key.Oem4 => shift ? '{' : '[',
                Key.Oem5 => shift ? '|' : '\\',
                Key.Oem6 => shift ? '}' : ']',
                Key.Oem7 => shift ? '"' : '\'',
                _ => '\0'
            };
        }

        private char KeyToCharWithShift(Key key) => key switch
        {
            Key.D1 => '!',
            Key.D2 => '@',
            Key.D3 => '#',
            Key.D4 => '$',
            Key.D5 => '%',
            Key.D6 => '^',
            Key.D7 => '&',
            Key.D8 => '*',
            Key.D9 => '(',
            Key.D0 => ')',
            _ => (char)('0' + (key - Key.D0))
        };
        #endregion

        #region Оптимизированная навигация
        private void HandleCaretNavigation(Key key, int bytesPerLine, int visibleLines)
        {
            switch (key)
            {
                case Key.Left:
                    if (_lastClickWasInHexArea && _currentNibblePosition == HexNibblePosition.Low)
                    {
                        _currentNibblePosition = HexNibblePosition.High;
                        NibblePositionChanged?.Invoke();
                    }
                    else
                        MoveCaret(HexCaretDirection.Left, false, bytesPerLine, visibleLines);
                    break;
                case Key.Right:
                    if (_lastClickWasInHexArea && _currentNibblePosition == HexNibblePosition.High)
                    {
                        _currentNibblePosition = HexNibblePosition.Low;
                        NibblePositionChanged?.Invoke();
                    }
                    else
                        MoveCaret(HexCaretDirection.Right, false, bytesPerLine, visibleLines);
                    break;
                case Key.Up: MoveCaret(HexCaretDirection.Up, false, bytesPerLine, visibleLines); break;
                case Key.Down: MoveCaret(HexCaretDirection.Down, false, bytesPerLine, visibleLines); break;
                case Key.PageUp: MoveCaret(HexCaretDirection.PageUp, false, bytesPerLine, visibleLines); break;
                case Key.PageDown: MoveCaret(HexCaretDirection.PageDown, false, bytesPerLine, visibleLines); break;
                case Key.Home: MoveCaretTo(0, false); break;
                case Key.End: MoveCaretTo(_document.Length - 1, false); break;
                case Key.Tab: ToggleEditingArea(); break;
                case Key.Back: HandleBackspace(); break;
                case Key.Delete: HandleDelete(); break;
                case Key.Escape: ClearSelection(); SelectionChanged?.Invoke(); break;
            }
        }

        private void HandleControlNavigation(Key key)
        {
            switch (key)
            {
                case Key.Z: _document.Undo(); break;
                case Key.Y: _document.Redo(); break;
                case Key.C: Copy(); break;
                case Key.V: Paste(); break;
                case Key.A: SelectAll(); break;
            }
        }

        private void HandleSelectionNavigation(Key key, int bytesPerLine, int visibleLines)
        {
            var direction = key switch
            {
                Key.Left => HexCaretDirection.Left,
                Key.Right => HexCaretDirection.Right,
                Key.Up => HexCaretDirection.Up,
                Key.Down => HexCaretDirection.Down,
                Key.PageUp => HexCaretDirection.PageUp,
                Key.PageDown => HexCaretDirection.PageDown,
                Key.Home => HexCaretDirection.Left, // Will be handled by MoveCaretTo
                Key.End => HexCaretDirection.Right, // Will be handled by MoveCaretTo
                _ => (HexCaretDirection?)null
            };

            if (direction.HasValue)
                MoveCaret(direction.Value, true, bytesPerLine, visibleLines);
            else if (key == Key.Home)
                MoveCaretTo(0, true);
            else if (key == Key.End)
                MoveCaretTo(_document.Length - 1, true);
        }

        /// <summary>
        /// Перемещает каретку в указанном направлении.
        /// </summary>
        /// <param name="direction">Направление движения</param>
        /// <param name="extendSelection">Расширять ли выделение</param>
        /// <param name="bytesPerLine">Количество байт на строку (для Up/Down/PageUp/PageDown)</param>
        /// <param name="visibleLines">Количество видимых строк (для PageUp/PageDown)</param>
        public void MoveCaret(HexCaretDirection direction, bool extendSelection, int bytesPerLine = 16, int visibleLines = 10)
        {
            int pageBytes = Math.Max(bytesPerLine, bytesPerLine * visibleLines);

            long newOffset = direction switch
            {
                HexCaretDirection.Left => Math.Max(0, _caretOffset - 1),
                HexCaretDirection.Right => Math.Min(_document.Length - 1, _caretOffset + 1),
                HexCaretDirection.Up => Math.Max(0, _caretOffset - bytesPerLine),
                HexCaretDirection.Down => Math.Min(_document.Length - 1, _caretOffset + bytesPerLine),
                HexCaretDirection.PageUp => Math.Max(0, _caretOffset - pageBytes),
                HexCaretDirection.PageDown => Math.Min(_document.Length - 1, _caretOffset + pageBytes),
                _ => _caretOffset
            };

            MoveCaretTo(newOffset, extendSelection);
        }

        /// <summary>
        /// Перемещает каретку на указанное смещение и поднимает событие CaretMoved.
        /// </summary>
        public void MoveCaretTo(long offset, bool extendSelection)
        {
            if (_document == null) return;

            offset = Math.Clamp(offset, 0, _document.Length - 1);

            if (extendSelection)
            {
                if (!_isSelecting)
                {
                    _selectionStartedInHexArea = _lastClickWasInHexArea;
                    StartSelection(_caretOffset);
                }
                UpdateSelection(offset);
            }
            else
                ClearSelection();

            _caretOffset = offset;
            ResetNibblePosition();

            // Поднимаем событие - скролл управляется извне (HexEditorControl)
            CaretMoved?.Invoke();
        }

        public void SelectAll()
        {
            if (_document.Length == 0) return;

            _selectionStartedInHexArea = true;
            StartSelection(0);
            UpdateSelection(_document.Length - 1);
            _caretOffset = _document.Length - 1;
            ResetNibblePosition();

            // Поднимаем событие - скролл управляется извне (HexEditorControl)
            CaretMoved?.Invoke();
        }
        #endregion

        #region Оптимизированная работа с данными
        public void Copy()
        {
            _clipboardData = HasSelection
                ? _document.CopyData(SelectionStart, SelectionLength)
                : new[] { _document.ReadByte(_caretOffset) };

            HexClipboard.TrySetBytes(_clipboardData);
        }

        public void Paste()
        {
            byte[]? dataToPaste = null;

            if (HexClipboard.TryGetBytes(out var clipboardBytes))
            {
                dataToPaste = clipboardBytes;
                _clipboardData = clipboardBytes;
            }
            else if (_clipboardData is { Length: > 0 })
            {
                dataToPaste = _clipboardData;
            }

            if (dataToPaste is not { Length: > 0 })
                return;

            long pasteOffset = HasSelection ? SelectionStart : _caretOffset;
            long maxLength = Math.Min(dataToPaste.Length, _document.Length - pasteOffset);

            if (maxLength > 0)
            {
                _document.PasteData(pasteOffset, dataToPaste);
                MoveCaretTo(Math.Min(pasteOffset + maxLength, _document.Length - 1), false);
            }
        }

        public void FillSelection(byte fillValue)
        {
            if (!HasSelection || SelectionLength <= 0) return;

            var changes = new List<(long offset, byte oldValue, byte newValue)>();

            for (long offset = SelectionStart; offset <= SelectionEnd; offset++)
            {
                if (offset >= _document.Length) break;
                byte oldValue = _document.ReadByte(offset);
                if (oldValue != fillValue)
                    changes.Add((offset, oldValue, fillValue));
            }

            if (changes.Count == 0) return;

            // Пакетное выполнение
            var command = new UniversalEditCommand(_document, changes);
            command.Execute();
            _document.AddToUndoStack(command);

            // Каретка остается на месте
        }
        #endregion

        #region Закладки
        private void HandleBookmarkKey(Color color, bool isRangeBookmark)
        {
            if ((!isRangeBookmark && _document.Bookmarks.Count >= HexDocument.MAX_SINGLE_BOOKMARKS - 100) ||
                (isRangeBookmark && _document.BookmarkRanges.Count >= HexDocument.MAX_RANGE_BOOKMARKS - 10))
            {
                Debug.WriteLine("Bookmark limit reached");
                return;
            }

            if (HasSelection)
            {
                if (isRangeBookmark && SelectionLength > 50000)
                {
                    Debug.WriteLine($"Range too large for bookmark: {SelectionLength} bytes");
                    return;
                }

                if (isRangeBookmark)
                    _document.ToggleBookmarkRange(SelectionStart, SelectionLength, color);
                else
                    _document.ToggleBookmarkOnSelection(SelectionStart, SelectionEnd, color);

                // Каретка остается на месте при установке закладки на выделение
            }
            else
            {
                if (isRangeBookmark)
                    _document.ToggleBookmarkRange(_caretOffset, 1, color);
                else
                    _document.ToggleBookmark(_caretOffset, color);

                // Для одиночной закладки перемещаемся на следующий байт
                if (_caretOffset < _document.Length - 1)
                    MoveCaretTo(_caretOffset + 1, false);
            }

            ResetNibblePosition();
            CaretMoved?.Invoke();
        }
        #endregion

        #region Удаление и очистка
        private void HandleBackspace() => HandleDeleteInternal(true);
        private void HandleDelete() => HandleDeleteInternal(false);

        private void HandleDeleteInternal(bool isBackspace)
        {
            if (HasSelection)
            {
                DeleteSelection();
                // Каретка остается на месте при удалении выделения
            }
            else
            {
                long targetOffset = isBackspace ? Math.Max(0, _caretOffset - 1) : _caretOffset;

                if (targetOffset >= 0 && targetOffset < _document.Length)
                {
                    if (isBackspace)
                    {
                        // Backspace: обнуляем предыдущий байт и перемещаемся назад
                        long prevOffset = Math.Max(0, _caretOffset - 1);
                        _document.WriteByte(prevOffset, 0x00);
                        MoveCaret(HexCaretDirection.Left, false);
                    }
                    else
                    {
                        // Delete: обнуляем текущий байт, каретка остается на месте
                        _document.WriteByte(_caretOffset, 0x00);
                        MoveCaret(HexCaretDirection.Right, false);
                    }
                }
                ResetNibblePosition();
            }
        }

        private void DeleteSelection()
        {
            var changes = GetSelectedOffsets()
                .Where(offset => _document.ReadByte(offset) != 0x00)
                .Select(offset => (offset, _document.ReadByte(offset), (byte)0x00))
                .ToList();

            if (changes.Count > 0)
            {
                var command = new UniversalEditCommand(_document, changes);
                command.Execute(); // ИСПРАВЛЕНИЕ: выполняем команду перед добавлением в stack
                _document.AddToUndoStack(command);
            }

            ResetNibblePosition();
            ClearSelection();
            CaretMoved?.Invoke();
        }

        private void HandleFillCommand(byte value)
        {
            if (HasSelection)
            {
                var changes = GetSelectedOffsets()
                    .Select(offset =>
                    {
                        byte current = _document.ReadByte(offset);
                        return (offset, current, value);
                    })
                    .Where(tuple => tuple.current != value)
                    .Select(tuple => (tuple.offset, tuple.current, tuple.value))
                    .ToList();

                if (changes.Count > 0)
                {
                    var command = new UniversalEditCommand(_document, changes);
                    command.Execute();
                    _document.AddToUndoStack(command);
                }

                ResetNibblePosition();
                ClearSelection();
                CaretMoved?.Invoke();
            }
            else if (_caretOffset >= 0 && _caretOffset < _document.Length)
            {
                _document.WriteByte(_caretOffset, value);
                ResetNibblePosition();
                MoveCaret(HexCaretDirection.Right, false);
            }
        }
        #endregion

        #region Вспомогательные методы
        private void ResetNibblePosition()
        {
            _currentNibblePosition = HexNibblePosition.High;
            NibblePositionChanged?.Invoke();
        }

        private void ToggleEditingArea()
        {
            _lastClickWasInHexArea = !_lastClickWasInHexArea;
            ResetNibblePosition();
        }
        #endregion

        #region Мышь
        public void HandleMouseClick(long offset, bool extendSelection, bool isHexArea)
        {
            _lastClickWasInHexArea = isHexArea;

            if (!extendSelection)
            {
                _selectionStartedInHexArea = isHexArea;
                StartSelection(offset);
            }
            else
            {
                if (!_isSelecting)
                {
                    _selectionStartedInHexArea = _lastClickWasInHexArea;
                    StartSelection(_caretOffset);
                }
                UpdateSelection(offset);
            }

            _caretOffset = offset;
            ResetNibblePosition();

            // Поднимаем событие - скролл управляется извне (HexEditorControl)
            // Поднимаем события - скролл управляется извне (HexEditorControl)
            CaretMoved?.Invoke();
            SelectionChanged?.Invoke();
        }

        public void HandleMouseDrag(long offset)
        {
            if (!_isSelecting)
            {
                _selectionStartedInHexArea = _lastClickWasInHexArea;
                StartSelection(_caretOffset);
            }
            UpdateSelection(offset);
            _caretOffset = offset;

            // Гарантируем видимость каретки при перетаскивании - прокрутка выполняется внешним контролом
            // Поднимаем события - скролл управляется извне (HexEditorControl)
            CaretMoved?.Invoke();
            SelectionChanged?.Invoke();
        }
        #endregion

        #region Файлы
        public async Task OpenFileAsync(string filePath)
        {
            try
            {
                _fileLoadCancellation?.Cancel();
                _fileLoadCancellation = new CancellationTokenSource();
                _isLoadingFile = true;

                var progress = new Progress<HexFileLoadProgressEventArgs>(p =>
                    FileLoadProgress?.Invoke(this, p));

                await _document.LoadFromFileAsync(filePath, progress, _fileLoadCancellation.Token);

                _caretOffset = 0;
                ClearSelection();
                ResetNibblePosition();
                FileLoadCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FileLoadError?.Invoke(this, ex.Message);
            }
            finally
            {
                _isLoadingFile = false;
            }
        }
        #endregion

        #region Clipboard helpers
        private static class HexClipboard
        {
            private static readonly char[] IgnoredSeparators = { ' ', '\t', '\r', '\n', ',', ';' };

            public static bool TrySetBytes(byte[]? data)
            {
                if (data is not { Length: > 0 })
                    return false;

                try
                {
                    var hexTokens = new string[data.Length];
                    for (int i = 0; i < data.Length; i++)
                        hexTokens[i] = data[i].ToString("X2", CultureInfo.InvariantCulture);

                    Clipboard.SetText(string.Join(" ", hexTokens));
                    return true;
                }
                catch (ExternalException ex)
                {
                    Debug.WriteLine($"Clipboard SetText failed: {ex.Message}");
                    return false;
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"Clipboard SetText argument error: {ex.Message}");
                    return false;
                }
            }

            public static bool TryGetBytes(out byte[] bytes)
            {
                if (!TryGetClipboardText(out var text))
                {
                    bytes = Array.Empty<byte>();
                    return false;
                }

                return TryParseHexBytes(text, out bytes);
            }

            public static bool TryPeekBytes(out byte[] bytes)
            {
                if (!TryGetClipboardText(out var text))
                {
                    bytes = Array.Empty<byte>();
                    return false;
                }

                return TryValidateHexString(text, out bytes);
            }

            private static bool TryGetClipboardText(out string text)
            {
                text = string.Empty;
                try
                {
                    if (!Clipboard.ContainsText())
                        return false;

                    text = Clipboard.GetText();
                    return !string.IsNullOrWhiteSpace(text);
                }
                catch (ExternalException ex)
                {
                    Debug.WriteLine($"Clipboard access failed: {ex.Message}");
                    return false;
                }
            }

            private static bool TryParseHexBytes(string text, out byte[] bytes)
                => TryValidateHexString(text, out bytes);

            private static bool TryValidateHexString(string text, out byte[] bytes)
            {
                bytes = Array.Empty<byte>();
                if (string.IsNullOrWhiteSpace(text))
                    return false;

                Span<char> sanitized = text.Length <= 1024
                    ? stackalloc char[text.Length]
                    : new char[text.Length];
                int length = 0;
                foreach (char c in text)
                {
                    if (IgnoredSeparators.Contains(c))
                        continue;
                    sanitized[length++] = c;
                }

                if (length == 0 || (length & 1) == 1)
                    return false;

                var result = new byte[length / 2];
                for (int i = 0; i < length; i += 2)
                {
                    char high = sanitized[i];
                    char low = sanitized[i + 1];
                    if (!IsHexChar(high) || !IsHexChar(low))
                        return false;

                    result[i / 2] = (byte)((HexValue(high) << 4) | HexValue(low));
                }

                bytes = result;
                return bytes.Length > 0;
            }

            private static bool IsHexChar(char c)
                => (c >= '0' && c <= '9') ||
                   (c >= 'A' && c <= 'F') ||
                   (c >= 'a' && c <= 'f');

            private static int HexValue(char c) => c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'F' => c - 'A' + 10,
                >= 'a' and <= 'f' => c - 'a' + 10,
                _ => 0
            };
        }
        #endregion
    }
}