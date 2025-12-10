using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Diagnostics;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Интерфейс команды редактирования для undo/redo системы.
    /// Реализует Command Pattern для всех изменений документа.
    /// </summary>
    internal interface IHexEditCommand
    {
        /// <summary>Выполняет команду (применяет изменения).</summary>
        void Execute();
        
        /// <summary>Отменяет команду (откатывает изменения).</summary>
        void Undo();
        
        /// <summary>Возвращает целевое смещение команды (для перемещения каретки).</summary>
        long GetTargetOffset();
    }

    /// <summary>
    /// Универсальная команда редактирования. Может применять множественные изменения байтов как одну атомарную операцию.
    /// Используется для всех видов редактирования: ввода, вставки, заполнения.
    /// </summary>
    internal class UniversalEditCommand : IHexEditCommand
    {
        private readonly HexDocument _document;
        private readonly (long offset, byte oldValue, byte newValue)[] _changes;

        public UniversalEditCommand(HexDocument document, IEnumerable<(long offset, byte oldValue, byte newValue)> changes)
        {
            _document = document;
            _changes = changes.ToArray();
        }

        public void Execute() => ExecuteChanges(false);
        public void Undo() => ExecuteChanges(true);

        private void ExecuteChanges(bool isUndo)
        {
            _document.BeginBatch();
            foreach (var change in _changes)
            {
                byte value = isUndo ? change.oldValue : change.newValue;
                _document.WriteByteDirect(change.offset, value);
            }
            _document.EndBatch();
        }

        public long GetTargetOffset() => _changes.Length > 0 ? _changes[0].offset : 0;
    }

    /// <summary>
    /// Модель документа hex-редактора. Управляет данными, undo/redo, закладками.
    /// 
    /// АРХИТЕКТУРНЫЕ ПРИНЦИПЫ:
    /// - Single Responsibility: управление данными и историей изменений
    /// - Command Pattern: все изменения через IHexEditCommand для undo/redo
    /// - Batch editing: поддержка пакетного редактирования для производительности
    /// - Thread-safe: блокировки для файловых операций
    /// 
    /// ИСПОЛЬЗОВАНИЕ:
    /// - Для одиночных изменений: WriteByte() - автоматически создаёт команду undo
    /// - Для множественных изменений: BeginBatch() -> WriteByte() -> EndBatch()
    /// - Для вставки данных: PasteData() - автоматически создаёт команду undo
    /// </summary>
    internal class HexDocument : IDisposable
    {
        private FileStream? _fileStream;
        private string? _tempFilePath; // Для отслеживания временных файлов
        private readonly Dictionary<long, byte> _modifiedBytes = new();
        private readonly Dictionary<long, Color> _bookmarks = new();
        private readonly Dictionary<long, (Color color, long length)> _bookmarkRanges = new();
        private readonly object _fileLock = new object();

        // Буфер для оптимизации чтения
        private byte[]? _readBuffer;
        private long _bufferOffset = -1;
        private const int BUFFER_SIZE = 16 * 1024; // 16KB буфер

        // Константы ограничений (используются также в HexInputHandler)
        internal const int MAX_SINGLE_BOOKMARKS = 10000;
        internal const int MAX_RANGE_BOOKMARKS = 1000;
        private const int MAX_UNDO_REDO_STACK_SIZE = 50000;

        // Система отмены/повтора
        private Stack<IHexEditCommand> _undoStack = new();
        private readonly Stack<IHexEditCommand> _redoStack = new();
        private bool _isUndoRedoInProgress = false;

        // Пакетное редактирование
        private bool _isBatching = false;
        private readonly List<(long offset, byte value)> _batchChanges = new();
        private readonly Dictionary<long, byte> _batchOldValues = new();

        public long Length { get; private set; }
        public IReadOnlyDictionary<long, byte> ModifiedBytes => _modifiedBytes;
        public IReadOnlyDictionary<long, Color> Bookmarks => _bookmarks;
        public IReadOnlyDictionary<long, (Color color, long length)> BookmarkRanges => _bookmarkRanges;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoStackCount => _undoStack.Count;
        public int RedoStackCount => _redoStack.Count;

        public event EventHandler<HexDataChangedEventArgs>? DataChanged;
        public event EventHandler<HexUndoRedoEventArgs>? UndoRedoExecuted;
        
        // Отдельное событие для изменения закладок (не изменяет данные, только визуализацию)
        public event EventHandler? BookmarksChanged;
        
        /// <summary>
        /// Уведомляет об изменении закладок (для вызова извне при массовых операциях)
        /// </summary>
        public void NotifyBookmarksChanged()
        {
            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }

        public HexDocument() => Length = 0;

        public void SetData(byte[] data)
        {
            lock (_fileLock)
            {
                // Очищаем предыдущие ресурсы
                CleanupFileResources();

                if (data is { Length: > 0 })
                {
                    _tempFilePath = Path.GetTempFileName();
                    File.WriteAllBytes(_tempFilePath, data);
                    _fileStream = new FileStream(_tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    Length = data.Length;
                }
                else
                {
                    _fileStream = null;
                    _tempFilePath = null;
                    Length = 0;
                }
            }

            ClearDocumentState();
            DataChanged?.Invoke(this, new HexDataChangedEventArgs(0, Length, false));
        }

        public void LoadFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            lock (_fileLock)
            {
                // Очищаем предыдущие ресурсы (включая временные файлы)
                CleanupFileResources();
                
                _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                _tempFilePath = null; // Это не временный файл
                Length = new FileInfo(filePath).Length;
            }

            ClearDocumentState();
            DataChanged?.Invoke(this, new HexDataChangedEventArgs(0, Length, false));
        }

        public async Task LoadFromFileAsync(string filePath, IProgress<HexFileLoadProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            ClearDocumentState();

            var fileInfo = new FileInfo(filePath);
            Length = fileInfo.Length;

            lock (_fileLock)
            {
                // Очищаем предыдущие ресурсы (включая временные файлы)
                CleanupFileResources();
                
                _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                _tempFilePath = null; // Это не временный файл
            }

            progress?.Report(new HexFileLoadProgressEventArgs(Length, Length));
            DataChanged?.Invoke(this, new HexDataChangedEventArgs(0, Length, false));

            await Task.CompletedTask;
        }

        public byte ReadByte(long offset)
        {
            if (offset < 0 || offset >= Length) return 0;
            
            // Исправление race condition: проверяем _modifiedBytes ВНУТРИ lock для thread safety
            // Это гарантирует, что чтение синхронизировано с возможной записью из другого потока
            lock (_fileLock)
            {
                // Проверяем модифицированные байты в первую очередь (горячий путь)
                if (_modifiedBytes.TryGetValue(offset, out byte modifiedValue))
                    return modifiedValue;

                if (_fileStream == null) return 0;

                try
                {
                    // Проверяем, находится ли байт в буфере
                    if (_readBuffer != null && 
                        offset >= _bufferOffset && 
                        offset < _bufferOffset + _readBuffer.Length)
                    {
                        return _readBuffer[offset - _bufferOffset];
                    }

                    // Загружаем новый блок в буфер
                    _bufferOffset = offset;
                    int bufferSize = (int)Math.Min(BUFFER_SIZE, Length - offset);
                    _readBuffer = new byte[bufferSize];
                    
                    _fileStream.Seek(offset, SeekOrigin.Begin);
                    int bytesRead = _fileStream.Read(_readBuffer, 0, bufferSize);
                    
                    if (bytesRead > 0)
                        return _readBuffer[0];
                    
                    return 0;
                }
                catch
                {
                    // Сбрасываем буфер при ошибке
                    _readBuffer = null;
                    _bufferOffset = -1;
                    return 0;
                }
            }
        }

        internal void WriteByteDirect(long offset, byte value)
        {
            if (offset < 0 || offset >= Length) return;

            byte oldValue = ReadByte(offset);
            if (oldValue == value) return;

            if (value == ReadOriginalByte(offset))
                _modifiedBytes.Remove(offset);
            else
                _modifiedBytes[offset] = value;

            // Инвалидируем буфер чтения при записи
            InvalidateReadBuffer();

            DataChanged?.Invoke(this, new HexDataChangedEventArgs(offset, 1, true));
        }

        /// <summary>
        /// Инвалидирует буфер чтения. Должен вызываться при записи данных.
        /// </summary>
        private void InvalidateReadBuffer()
        {
            _readBuffer = null;
            _bufferOffset = -1;
        }

        private byte ReadOriginalByte(long offset)
        {
            lock (_fileLock)
            {
                if (_fileStream == null) return 0;

                try
                {
                    _fileStream.Seek(offset, SeekOrigin.Begin);
                    return (byte)_fileStream.ReadByte();
                }
                catch
                {
                    return 0;
                }
            }
        }

        public void WriteByte(long offset, byte value)
        {
            if (offset < 0 || offset >= Length || _isUndoRedoInProgress) return;

            byte oldValue = ReadByte(offset);
            if (oldValue == value) return;

            if (_isBatching)
            {
                // Сохраняем старое значение только при первом изменении этого offset в батче
                if (!_batchOldValues.ContainsKey(offset))
                {
                    _batchOldValues[offset] = oldValue;
                }
                _batchChanges.Add((offset, value));
                WriteByteDirect(offset, value);
            }
            else
            {
                var changes = new List<(long offset, byte oldValue, byte newValue)> { (offset, oldValue, value) };
                var command = new UniversalEditCommand(this, changes);
                command.Execute();
                AddToUndoStack(command);
            }
        }

        public byte[] CopyData(long offset, long length)
        {
            if (offset < 0 || offset >= Length || length <= 0)
                return Array.Empty<byte>();

            long endOffset = Math.Min(offset + length - 1, Length - 1);
            long actualLength = endOffset - offset + 1;
            var result = new byte[actualLength];

            // Последовательное чтение: Parallel.For неэффективен здесь, т.к. ReadByte()
            // использует lock (_fileLock), что приводит к блокировкам и overhead от создания задач
            // Для больших блоков лучше использовать прямое чтение через FileStream в будущем
            for (long i = 0; i < actualLength; i++)
                result[i] = ReadByte(offset + i);

            return result;
        }

        public void PasteData(long offset, byte[] data)
        {
            if (offset < 0 || offset >= Length || data == null || data.Length == 0) return;

            long maxLength = Math.Min(data.Length, Length - offset);
            if (maxLength <= 0) return;

            // Быстрая проверка на изменения
            bool hasChanges = false;
            for (long i = 0; i < maxLength; i++)
            {
                if (ReadByte(offset + i) != data[i])
                {
                    hasChanges = true;
                    break;
                }
            }
            if (!hasChanges) return;

            var changes = new List<(long offset, byte oldValue, byte newValue)>();
            ProcessPasteChunk(offset, data, 0, (int)maxLength, changes);

            if (changes.Count > 0 && !_isUndoRedoInProgress)
            {
                var command = new UniversalEditCommand(this, changes);
                AddToUndoStack(command);
                DataChanged?.Invoke(this, new HexDataChangedEventArgs(offset, maxLength, true));
            }
        }

        /// <summary>
        /// Быстрая очистка диапазонов байтов (обнуление). Читает данные напрямую из файла блоками для максимальной производительности.
        /// </summary>
        public void ClearByteRangesFast(List<(long offset, int length)> ranges)
        {
            if (ranges == null || ranges.Count == 0 || _isUndoRedoInProgress || _fileStream == null) return;

            var allChanges = new List<(long offset, byte oldValue, byte newValue)>();

            lock (_fileLock)
            {
                foreach (var (offset, length) in ranges)
                {
                    if (offset < 0 || length <= 0) continue;
                    
                    long maxLength = Math.Min(length, Length - offset);
                    if (maxLength <= 0) continue;

                    // Читаем данные напрямую из файла блоками - намного быстрее, чем ReadByte для каждого байта
                    byte[] buffer = new byte[maxLength];
                    try
                    {
                        _fileStream.Seek(offset, SeekOrigin.Begin);
                        int bytesRead = _fileStream.Read(buffer, 0, (int)maxLength);
                        
                        if (bytesRead > 0)
                        {
                            // Применяем модифицированные байты поверх прочитанных данных
                            for (int i = 0; i < bytesRead; i++)
                            {
                                long currentOffset = offset + i;
                                if (_modifiedBytes.TryGetValue(currentOffset, out byte modifiedValue))
                                {
                                    buffer[i] = modifiedValue;
                                }
                            }

                            // Записываем нули сразу после чтения (оптимизация памяти - не накапливаем offsets)
                            long lastWriteOffset = -1;
                            for (int i = 0; i < bytesRead; i++)
                            {
                                if (buffer[i] != 0x00)
                                {
                                    long currentOffset = offset + i;
                                    byte oldValue = buffer[i];
                                    allChanges.Add((currentOffset, oldValue, 0x00));
                                    
                                    // Обновляем _modifiedBytes напрямую
                                    _modifiedBytes[currentOffset] = 0x00;
                                    
                                    // Записываем сразу (оптимизация: минимизируем Seek операции)
                                    try
                                    {
                                        if (currentOffset != lastWriteOffset + 1)
                                        {
                                            _fileStream.Seek(currentOffset, SeekOrigin.Begin);
                                        }
                                        _fileStream.WriteByte(0x00);
                                        lastWriteOffset = currentOffset;
                                    }
                                    catch
                                    {
                                        // Игнорируем ошибки записи для отдельных байтов
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки чтения для отдельных диапазонов
                    }
                }

                _fileStream.Flush();
            }

            // Инвалидируем буфер чтения
            InvalidateReadBuffer();

            // Создаем команду undo/redo
            if (allChanges.Count > 0)
            {
                var command = new UniversalEditCommand(this, allChanges);
                AddToUndoStack(command);
                
                long minOffset = allChanges.Min(x => x.offset);
                long maxOffset = allChanges.Max(x => x.offset);
                DataChanged?.Invoke(this, new HexDataChangedEventArgs(minOffset, maxOffset - minOffset + 1, true));
            }
        }

        private void ProcessPasteChunk(long startOffset, byte[] data, long dataStart, int length,
            List<(long offset, byte oldValue, byte newValue)> changes)
        {
            for (int i = 0; i < length; i++)
            {
                long currentOffset = startOffset + i;
                byte oldValue = ReadByte(currentOffset);
                byte newValue = data[dataStart + i];

                if (oldValue != newValue)
                {
                    changes.Add((currentOffset, oldValue, newValue));
                    WriteByteDirect(currentOffset, newValue);
                }
            }
        }

        public void SaveToFile(string filePath)
        {
            // Используем GetAwaiter().GetResult() вместо Wait() для избежания deadlock
            // при вызове из UI thread с SynchronizationContext
            SaveToFileAsync(filePath).GetAwaiter().GetResult();
        }

        public async Task SaveToFileAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (Length == 0) return;

            using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            var buffer = new byte[64 * 1024];
            long totalBytes = Length;
            long processedBytes = 0;

            for (long offset = 0; offset < totalBytes; offset += buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesToProcess = (int)Math.Min(buffer.Length, totalBytes - offset);
                
                // Читаем блок данных
                for (int i = 0; i < bytesToProcess; i++)
                    buffer[i] = ReadByte(offset + i);

                // Асинхронная запись
                await outputStream.WriteAsync(buffer, 0, bytesToProcess, cancellationToken);
                
                processedBytes += bytesToProcess;
                progress?.Report((double)processedBytes / totalBytes);
            }

            await outputStream.FlushAsync(cancellationToken);

            // Очищаем модифицированные байты после успешного сохранения
            _modifiedBytes.Clear();
            InvalidateReadBuffer();
        }

        public byte[] GetData()
        {
            if (Length == 0) return Array.Empty<byte>();
            var result = new byte[Length];
            for (long i = 0; i < Length; i++)
                result[i] = ReadByte(i);
            return result;
        }

        // Упрощенные методы закладок
        public void ToggleBookmark(long offset, Color color, bool suppressEvent = false)
        {
            if (_bookmarks.Count >= MAX_SINGLE_BOOKMARKS && !_bookmarks.ContainsKey(offset))
                _bookmarks.Remove(_bookmarks.Keys.First());

            if (_bookmarks.ContainsKey(offset) && _bookmarks[offset] == color)
                _bookmarks.Remove(offset);
            else
                _bookmarks[offset] = color;

            // Закладки не изменяют данные, используем отдельное событие для обновления только визуализации
            if (!suppressEvent)
            {
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ToggleBookmarkOnSelection(long startOffset, long endOffset, Color color)
        {
            long start = Math.Min(startOffset, endOffset);
            long end = Math.Max(startOffset, endOffset);
            long selectionLength = end - start + 1;

            if (selectionLength > 5000)
            {
                Debug.WriteLine($"Selection too large for bookmarks: {selectionLength} bytes");
                return;
            }

            bool allHaveSameColor = true;
            for (long offset = start; offset <= end; offset++)
            {
                if (!_bookmarks.ContainsKey(offset) || _bookmarks[offset] != color)
                {
                    allHaveSameColor = false;
                    break;
                }
            }

            if (allHaveSameColor)
            {
                for (long offset = start; offset <= end; offset++)
                    _bookmarks.Remove(offset);
            }
            else
            {
                for (long offset = start; offset <= end && _bookmarks.Count < MAX_SINGLE_BOOKMARKS; offset++)
                    _bookmarks[offset] = color;
            }

            // Закладки не изменяют данные
            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveBookmarksByColor(Color color)
        {
            var offsetsToRemove = _bookmarks.Where(x => x.Value == color).Select(x => x.Key).ToList();
            if (offsetsToRemove.Count == 0) return;

            foreach (var offset in offsetsToRemove)
                _bookmarks.Remove(offset);

            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveAllBookmarks()
        {
            if (_bookmarks.Count > 0)
            {
                _bookmarks.Clear();
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ToggleBookmarkRange(long offset, long length, Color color, bool suppressEvent = false)
        {
            if (_bookmarkRanges.Count >= MAX_RANGE_BOOKMARKS && !_bookmarkRanges.ContainsKey(offset))
                _bookmarkRanges.Remove(_bookmarkRanges.Keys.First());

            if (_bookmarkRanges.ContainsKey(offset) && _bookmarkRanges[offset].color == color && _bookmarkRanges[offset].length == length)
                _bookmarkRanges.Remove(offset);
            else
                _bookmarkRanges[offset] = (color, length);

            // Закладки не изменяют данные, используем отдельное событие для обновления только визуализации
            if (!suppressEvent)
            {
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveBookmarkRangesByColor(Color color)
        {
            var offsetsToRemove = _bookmarkRanges.Where(x => x.Value.color == color).Select(x => x.Key).ToList();
            if (offsetsToRemove.Count == 0) return;

            foreach (var offset in offsetsToRemove)
                _bookmarkRanges.Remove(offset);

            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveAllBookmarkRanges()
        {
            if (_bookmarkRanges.Count > 0)
            {
                _bookmarkRanges.Clear();
                BookmarksChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Начинает пакетное редактирование. Все изменения до EndBatch() будут объединены в одну команду undo.
        /// </summary>
        public void BeginBatch()
        {
            _isBatching = true;
            _batchChanges.Clear();
            _batchOldValues.Clear();
        }

        /// <summary>
        /// Завершает пакетное редактирование и создаёт единую команду undo для всех изменений.
        /// ИСПРАВЛЕН БАГ: теперь правильно сохраняются старые значения для undo.
        /// </summary>
        public void EndBatch()
        {
            _isBatching = false;
            if (_batchChanges.Count == 0 || _isUndoRedoInProgress)
            {
                _batchChanges.Clear();
                _batchOldValues.Clear();
                return;
            }

            // Группируем изменения: берём последнее значение для каждого offset
            var finalChanges = _batchChanges
                .GroupBy(c => c.offset)
                .Select(g => 
                {
                    var offset = g.Key;
                    var newValue = g.Last().value;
                    var oldValue = _batchOldValues.ContainsKey(offset) 
                        ? _batchOldValues[offset] 
                        : ReadOriginalByte(offset); // Fallback на всякий случай
                    return (offset, oldValue, newValue);
                })
                .Where(c => c.oldValue != c.newValue) // Пропускаем неизменённые
                .ToList();

            if (finalChanges.Count > 0)
            {
                var command = new UniversalEditCommand(this, finalChanges);
                // НЕ вызываем command.Execute() - данные уже записаны через WriteByteDirect
                AddToUndoStack(command);

                long minOffset = finalChanges.Min(x => x.offset);
                long maxOffset = finalChanges.Max(x => x.offset);
                DataChanged?.Invoke(this, new HexDataChangedEventArgs(minOffset, maxOffset - minOffset + 1, true));
            }

            _batchChanges.Clear();
            _batchOldValues.Clear();
        }

        // Оптимизированные методы отмены/повтора
        public void Undo()
        {
            if (!CanUndo) return;

            _isUndoRedoInProgress = true;
            try
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                UndoRedoExecuted?.Invoke(this, new HexUndoRedoEventArgs(command.GetTargetOffset(), true));
            }
            finally
            {
                _isUndoRedoInProgress = false;
            }
        }

        public void Redo()
        {
            if (!CanRedo) return;

            _isUndoRedoInProgress = true;
            try
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                UndoRedoExecuted?.Invoke(this, new HexUndoRedoEventArgs(command.GetTargetOffset(), false));
            }
            finally
            {
                _isUndoRedoInProgress = false;
            }
        }

        public void ClearUndoRedoHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        internal void AddToUndoStack(IHexEditCommand command)
        {
            _undoStack.Push(command);
            _redoStack.Clear();

            if (_undoStack.Count > MAX_UNDO_REDO_STACK_SIZE)
            {
                var newStack = new Stack<IHexEditCommand>(_undoStack.Take(MAX_UNDO_REDO_STACK_SIZE).Reverse());
                _undoStack = newStack;
            }
        }

        private void ClearDocumentState()
        {
            ClearUndoRedoHistory();
            _modifiedBytes.Clear();
            _bookmarks.Clear();
            _bookmarkRanges.Clear();
        }

        /// <summary>
        /// Очищает файловые ресурсы и удаляет временные файлы.
        /// ВАЖНО: Должен вызываться внутри lock (_fileLock)
        /// </summary>
        private void CleanupFileResources()
        {
            // Закрываем поток
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            // Очищаем буфер чтения
            InvalidateReadBuffer();

            // Удаляем временный файл если он есть
            if (_tempFilePath != null)
            {
                try
                {
                    if (File.Exists(_tempFilePath))
                        File.Delete(_tempFilePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete temp file {_tempFilePath}: {ex.Message}");
                }
                _tempFilePath = null;
            }
        }

        public void Dispose()
        {
            lock (_fileLock)
            {
                CleanupFileResources();
            }
        }
    }

    // Упрощенные классы событий
    public class HexDataChangedEventArgs : EventArgs
    {
        public long Offset { get; }
        public long Length { get; }
        public bool IsIncremental { get; }

        public HexDataChangedEventArgs(long offset, long length, bool isIncremental)
        {
            Offset = offset;
            Length = length;
            IsIncremental = isIncremental;
        }
    }

    public class HexUndoRedoEventArgs : EventArgs
    {
        public long TargetOffset { get; }
        public bool IsUndo { get; }

        public HexUndoRedoEventArgs(long targetOffset, bool isUndo)
        {
            TargetOffset = targetOffset;
            IsUndo = isUndo;
        }
    }

    public class HexFileLoadProgressEventArgs : EventArgs
    {
        public long BytesLoaded { get; }
        public long TotalBytes { get; }
        public double Progress => TotalBytes > 0 ? (double)BytesLoaded / TotalBytes : 0;
        public double Percentage => Progress * 100;

        public HexFileLoadProgressEventArgs(long bytesLoaded, long totalBytes)
        {
            BytesLoaded = bytesLoaded;
            TotalBytes = totalBytes;
        }
    }
}