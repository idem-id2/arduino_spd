# SPD Edit - Редактор SPD данных

## Описание

Модуль для редактирования SPD (Serial Presence Detect) данных модулей памяти DDR4 и DDR5 в соответствии со стандартами JEDEC.

## Стандарты

- **DDR4**: JEDEC JESD21-C (SPD for DDR4 SDRAM Modules)
- **DDR5**: JEDEC JESD400-5C (SPD for DDR5 SDRAM Modules)

## Документация

1. **[SPD_EDIT_JEDEC_COMPLIANCE.md](SPD_EDIT_JEDEC_COMPLIANCE.md)** - Полная проверка соответствия стандартам JEDEC
2. **[SPD_EDIT_FIELDS_REFERENCE.md](SPD_EDIT_FIELDS_REFERENCE.md)** - Справочник всех полей редактирования

## Структура

### Редакторы
- `BaseSpdEditor.cs` - Базовый класс с общими полями (DDR4/DDR5)
- `Ddr4SpdEditor.cs` - Редактор для DDR4
- `Ddr5SpdEditor.cs` - Редактор для DDR5

### Панель редактирования
- `SpdEditPanel.xaml` - XAML разметка (статические поля)
- `SpdEditPanel.xaml.cs` - Логика редактирования

## Поддерживаемые поля

### DDR4 (35 полей)
- Memory Module: 10 полей
- Density & Die: 7 полей
- DRAM Components: 1 поле
- Timing Parameters: 20 полей
- Module Configuration: 7 полей

### DDR5 (19 полей)
- Memory Module: 5 полей
- DRAM Components: 1 поле
- Timing Parameters: 9 полей
- Module Configuration: 4 поля

## Особенности

✅ **Полное соответствие JEDEC стандартам**
- Все байтовые смещения корректны
- Все форматы данных соответствуют спецификациям
- Parity bits сохраняются для Manufacturer ID
- Валидация всех полей

✅ **Статическая XAML разметка**
- Все поля определены в XAML для визуального редактирования
- Комментарии с указанием типов памяти (DDR4/DDR5)

✅ **Автоматическое применение изменений**
- Изменения применяются автоматически при редактировании
- Debouncing для TextBox полей (500ms)
- Мгновенное применение для ComboBox и CheckBox

## Использование

```csharp
// Загрузка SPD данных
spdEditPanel.LoadSpdData(spdData, ForcedMemoryType.Auto);

// Подписка на изменения
spdEditPanel.ChangesApplied += (changes) => {
    foreach (var change in changes) {
        // Применить изменения к EEPROM
        WriteBytes(change.Offset, change.NewData);
    }
};
```

## Проверка соответствия

Все поля были проверены на соответствие стандартам JEDEC:
- ✅ Байтовые смещения
- ✅ Форматы данных
- ✅ Битовые маски
- ✅ Валидация значений
- ✅ Порядок байт для Manufacturer ID

**Статус**: Все поля соответствуют стандартам JEDEC

---

**Версия**: 1.0  
**Дата**: 2025-01-10

