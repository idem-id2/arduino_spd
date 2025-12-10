using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Высокоуровневый фасад над <see cref="HexEditorControl"/>. Предоставляет единое,
    /// детально документированное API для сторонних разработчиков, которым не нужен доступ
    /// к внутреннему устройству редактора. Позволяет размещать контрол в любом WPF‑окне
    /// и управлять им только через этот адаптер.
    /// </summary>
    /// <example>
    /// Простейший сценарий интеграции в XAML/Code-behind:
    /// <code language="xaml">
    /// &lt;Window ... xmlns:hex="clr-namespace:HexEditor.HexEdit"&gt;
    ///     &lt;hex:HexEditorControl x:Name="HexEditorView" /&gt;
    /// &lt;/Window&gt;
    /// </code>
    /// <code language="csharp">
    /// public partial class MainWindow : Window
    /// {
    ///     private HexEditorFacade _editor;
    ///
    ///     public MainWindow()
    ///     {
    ///         InitializeComponent();
    ///         _editor = new HexEditorFacade(HexEditorView);
    ///         _editor.FileLoaded += (s, e) =&gt; MessageBox.Show("Файл открыт");
    ///     }
    ///
    ///     private async void OnOpenClick(object sender, RoutedEventArgs e)
    ///     {
    ///         await _editor.LoadFileAsync("test.bin");
    ///         _editor.EnsureRangeVisible(0, 0x40);
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class HexEditorFacade
    {
        private readonly HexEditorControl _control;

        /// <summary>
        /// Создаёт фасад поверх уже существующего экземпляра <see cref="HexEditorControl"/>.
        /// Контрол можно объявить в XAML или создать вручную и передать сюда.
        /// </summary>
        /// <param name="control">Экземпляр редактора, с которым будет работать фасад.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="control"/> равен null.</exception>
        public HexEditorFacade(HexEditorControl control)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
        }

        /// <summary>
        /// Возвращает исходный WPF‑контрол, который можно разместить в визуальном дереве.
        /// Полезно, когда нужно добавить редактор в XAML, но управлять им через фасад.
        /// </summary>
        public HexEditorControl View => _control;

        #region Status / Capability Properties

        /// <summary>Редактор загружает файл в данный момент.</summary>
        public bool IsLoading => _control.IsLoading;

        /// <summary>Есть ли команды в стеке отмены.</summary>
        public bool CanUndo => _control.CanUndo;

        /// <summary>Есть ли команды в стеке повтора.</summary>
        public bool CanRedo => _control.CanRedo;

        /// <summary>Можно ли сейчас вызвать копирование (есть каретка/выделение).</summary>
        public bool CanCopy => _control.CanCopy;

        /// <summary>Доступна ли вставка (в буфере обмена есть байты).</summary>
        public bool CanPaste => _control.CanPaste;

        /// <summary>Длина документа в байтах.</summary>
        public long DocumentLength => _control.DocumentLength;

        /// <summary>Количество несохранённых изменений.</summary>
        public int ModifiedBytesCount => _control.ModifiedBytesCount;

        /// <summary>Количество одиночных и диапазонных закладок.</summary>
        public (int Single, int Range) BookmarkCounts =>
            (_control.BookmarksCount, _control.GetBookmarkRanges().Count);

        /// <summary>Метаданные текущего выделения.</summary>
        public (long Start, long End, long Length) Selection => _control.GetSelection();

        /// <summary>Позиция каретки.</summary>
        public long CaretOffset => _control.GetCaretPosition();

        #endregion

        #region Events

        /// <summary>Событие прогресса загрузки файла.</summary>
        public event EventHandler<HexFileLoadProgressEventArgs>? FileLoadProgress
        {
            add => _control.FileLoadProgress += value;
            remove => _control.FileLoadProgress -= value;
        }

        /// <summary>Событие завершения загрузки.</summary>
        public event EventHandler? FileLoaded
        {
            add => _control.FileLoadCompleted += value;
            remove => _control.FileLoadCompleted -= value;
        }

        /// <summary>Событие ошибки при загрузке файла.</summary>
        public event EventHandler<string>? FileLoadError
        {
            add => _control.FileLoadError += value;
            remove => _control.FileLoadError -= value;
        }

        /// <summary>Событие изменения выделения.</summary>
        public event EventHandler? SelectionChanged
        {
            add => _control.SelectionChanged += value;
            remove => _control.SelectionChanged -= value;
        }

        /// <summary>Событие перемещения каретки.</summary>
        public event EventHandler? CaretMoved
        {
            add => _control.CaretPositionChanged += value;
            remove => _control.CaretPositionChanged -= value;
        }

        /// <summary>Событие любых изменений данных (вставка, удаление, замена).</summary>
        public event EventHandler? DataModified
        {
            add => _control.DataModified += value;
            remove => _control.DataModified -= value;
        }

        #endregion

        #region Loading / Persistence

        /// <summary>Загружает данные из массива байтов, полностью заменяя текущий буфер.</summary>
        /// <example>
        /// <code language="csharp">
        /// byte[] bytes = File.ReadAllBytes("firmware.bin");
        /// var facade = new HexEditorFacade(HexEditorView);
        /// facade.LoadData(bytes);
        /// facade.SetBytesPerLine(32);
        /// </code>
        /// </example>
        public void LoadData(byte[] data) => _control.LoadData(data);

        /// <summary>Асинхронно открывает файл по пути.</summary>
        /// <example>
        /// <code language="csharp">
        /// await _facade.LoadFileAsync("dump.bin");
        /// _facade.AddBookmarkRange(0x100, 0x40, Colors.Red);
        /// </code>
        /// </example>
        public Task LoadFileAsync(string filePath) => _control.LoadFileAsync(filePath);

        /// <summary>Сохраняет текущий документ на диск.</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.SaveToFile("edited.bin");
        /// _facade.ResetModified();
        /// </code>
        /// </example>
        public void SaveToFile(string filePath) => _control.SaveToFile(filePath);

        /// <summary>Возвращает содержимое буфера (включая несохранённые изменения).</summary>
        /// <example>
        /// <code language="csharp">
        /// byte[] snapshot = _facade.GetData();
        /// File.WriteAllBytes("backup.bin", snapshot);
        /// </code>
        /// </example>
        public byte[] GetData() => _control.GetData();

        /// <summary>Очищает редактор — данных больше нет.</summary>
        public void Clear() => _control.Clear();

        /// <summary>Отменяет текущую операцию загрузки.</summary>
        public void CancelLoad() => _control.CancelFileLoad();

        #endregion

        #region Editing Commands

        /// <summary>Команды отмены/повтора/копирования/вставки/заливки.</summary>
        public void Undo() => _control.Undo();
        public void Redo() => _control.Redo();
        public void Copy() => _control.Copy();
        public void Paste() => _control.Paste();
        public void FillSelection(byte fillValue) => _control.FillSelection(fillValue);

        /// <summary>Заменяет байты по смещению на новые (длина совпадает).</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.ReplaceData(0x200, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        /// </code>
        /// </example>
        public void ReplaceData(long offset, byte[] newData) => _control.ReplaceData(offset, newData);

        /// <summary>Вставляет данные в указанное смещение.</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.InsertData(0x500, Enumerable.Repeat((byte)0x00, 16).ToArray());
        /// </code>
        /// </example>
        public void InsertData(long offset, byte[] data) => _control.InsertData(offset, data);

        /// <summary>Удаляет диапазон байт.</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.DeleteData(0x800, 0x40);
        /// </code>
        /// </example>
        public void DeleteData(long offset, long length) => _control.DeleteData(offset, length);

        /// <summary>Ищет шаблон, начиная с заданного смещения. Возвращает -1, если не найден.</summary>
        /// <example>
        /// <code language="csharp">
        /// long position = _facade.FindData(new byte[] { 0x01, 0x02, 0x03 });
        /// if (position &gt;= 0) _facade.SetCaret(position);
        /// </code>
        /// </example>
        public long FindData(byte[] pattern, long startOffset = 0) => _control.FindData(pattern, startOffset);

        #endregion

        #region Selection / Caret / Navigation

        /// <summary>Устанавливает выделение (события вызываются автоматически).</summary>
        public void SetSelection(long start, long end) => _control.SetSelection(start, end);

        /// <summary>Снимает выделение, не двигая каретку.</summary>
        public void ClearSelection() => _control.ClearSelection();

        /// <summary>Перемещает каретку к смещению и делает его видимым.</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.SetCaret(0x1234);
        /// _facade.EnsureRangeVisible(0x1200, 0x1300);
        /// </code>
        /// </example>
        public void SetCaret(long offset) => _control.SetCaretPosition(offset);

        /// <summary>Логический скролл к смещению (верхняя строка).</summary>
        public void ScrollToOffset(long offset) => _control.ScrollToOffset(offset);

        /// <summary>Гарантирует видимость байта.</summary>
        public void EnsureVisible(long offset) => _control.EnsureVisible(offset);

        /// <summary>Гарантирует видимость диапазона (прокрутка при необходимости).</summary>
        /// <example>
        /// <code language="csharp">
        /// _facade.EnsureRangeVisible(0x149, 0x15D);
        /// _facade.AddBookmarkRange(0x149, 0x15D - 0x149 + 1, Colors.Crimson);
        /// </code>
        /// </example>
        public void EnsureRangeVisible(long start, long end) => _control.EnsureRangeVisible(start, end);

        /// <summary>Выделяет весь документ.</summary>
        public void SelectAll() => _control.SelectAll();

        #endregion

        #region View / Formatting

        /// <summary>Меняет размер шрифта (Device Independent Pixels).</summary>
        public void SetFontSize(double size) => _control.SetFontSize(size);

        /// <summary>Настраивает количество байт в строке (1..32).</summary>
        public void SetBytesPerLine(int count) => _control.SetBytesPerLine(count);

        /// <summary>Возвращает логический диапазон, отображаемый сейчас.</summary>
        public (long Start, long End) GetVisibleRange() => _control.GetVisibleRange();

        /// <summary>Возвращает полностью видимый (без «обрезанных» строк) диапазон.</summary>
        public (long Start, long End) GetFullyVisibleRange() => _control.GetFullyVisibleRange();

        /// <summary>Принудительно обновляет DPI‑зависимые ресурсы (например, при смене монитора).</summary>
        public void ForceDpiRefresh() => _control.ForceDpiRefresh();

        #endregion

        #region Bookmarks

        /// <summary>Добавляет/удаляет одиночную закладку указанного цвета.</summary>
        public void AddBookmark(long offset, Color color) => _control.AddBookmark(offset, color);
        public void RemoveBookmark(long offset, Color color) => _control.RemoveBookmark(offset, color);

        /// <summary>Возвращает словарь одиночных закладок (offset -> color).</summary>
        public IReadOnlyDictionary<long, Color> GetBookmarks() => _control.GetBookmarks();

        /// <summary>Удаляет все закладки внутри диапазона.</summary>
        public void RemoveBookmarksInRange(long start, long end) => _control.RemoveBookmarksInRange(start, end);

        /// <summary>Снимает все одиночные закладки.</summary>
        public void ClearBookmarks() => _control.RemoveAllBookmarks();

        /// <summary>Есть ли закладка в точке.</summary>
        public bool HasBookmark(long offset) => _control.HasBookmark(offset);

        /// <summary>Добавляет/удаляет диапазонную (рамочную) закладку.</summary>
        public void AddBookmarkRange(long offset, long length, Color color) =>
            _control.AddBookmarkRange(offset, length, color);
        public void RemoveBookmarkRange(long offset, Color color) => _control.RemoveBookmarkRange(offset, color);

        /// <summary>Возвращает словарь диапазонных закладок.</summary>
        public IReadOnlyDictionary<long, (Color color, long length)> GetBookmarkRanges() =>
            _control.GetBookmarkRanges();

        /// <summary>Удаляет все диапазонные закладки.</summary>
        public void ClearBookmarkRanges() => _control.RemoveAllBookmarkRanges();

        /// <summary>Проверяет, начинается ли диапазонная закладка по смещению.</summary>
        public bool HasBookmarkRange(long offset) => _control.HasBookmarkRange(offset);

        #endregion

        #region Metrics / Information

        /// <summary>Возвращает агрегированную информацию о документе.</summary>
        public (long Length, int ModifiedBytes, int Bookmarks, int BookmarkRanges) GetDocumentInfo() =>
            _control.GetDocumentInfo();

        /// <summary>
        /// Возвращает диапазон изменённых байтов в формате [start, end). Возвращает false,
        /// если изменений нет.
        /// </summary>
        public bool TryGetModifiedRange(out long startOffset, out long endOffset) =>
            _control.TryGetModifiedRange(out startOffset, out endOffset);

        /// <summary>Сбрасывает состояние «изменён/не сохранён» (актуально после сохранения).</summary>
        public void ResetModified() => _control.ResetModified();

        /// <summary>Считывает произвольный диапазон байт.</summary>
        public byte[] ReadBytes(long offset, long length) => _control.ReadBytes(offset, length);

        /// <summary>Синхронизирует ScrollViewer с внутренним состоянием скролла.</summary>
        public void ForceScrollSynchronization() => _control.ForceScrollSynchronization();

        #endregion
    }
}

