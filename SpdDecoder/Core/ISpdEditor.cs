using System.Collections.Generic;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Интерфейс для редактирования SPD данных
    /// </summary>
    internal interface ISpdEditor
    {
        /// <summary>
        /// Загружает SPD данные для редактирования
        /// </summary>
        void LoadData(byte[] data);

        /// <summary>
        /// Получает список редактируемых полей
        /// </summary>
        List<EditField> GetEditFields();

        /// <summary>
        /// Применяет изменения из полей редактирования
        /// </summary>
        /// <returns>Список изменений байтов</returns>
        List<SpdEditPanel.ByteChange> ApplyChanges(Dictionary<string, string> fieldValues);

        /// <summary>
        /// Валидирует значения полей
        /// </summary>
        Dictionary<string, string> ValidateFields(Dictionary<string, string> fieldValues);
    }

    /// <summary>
    /// Описание редактируемого поля
    /// </summary>
    internal class EditField
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public EditFieldType Type { get; set; }
        public string ToolTip { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public bool IsReadOnly { get; set; }
        public List<ComboBoxItem>? ComboBoxItems { get; set; }
        /// <summary>
        /// Категория поля для группировки в UI (MemoryModule, DramComponents, Timing, ModuleConfig, etc.)
        /// </summary>
        public string Category { get; set; } = "Common";
    }

    /// <summary>
    /// Тип поля редактирования
    /// </summary>
    internal enum EditFieldType
    {
        TextBox,
        ComboBox,
        CheckBox,
        Numeric
    }

    /// <summary>
    /// Элемент ComboBox
    /// </summary>
    internal class ComboBoxItem
    {
        public string Content { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}

