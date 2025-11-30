using System;
using System.Windows;
using System.Windows.Media;

namespace HexEditor.HexEdit
{
    /// <summary>
    /// Метрики отображения hex-редактора. Централизованное управление размерами, позициями, координатами.
    /// 
    /// АРХИТЕКТУРНЫЕ ПРИНЦИПЫ:
    /// - Single Source of Truth: все метрики вычисляются в одном месте
    /// - DPI-aware: поддержка Per-Monitor DPI с корректным snapping
    /// - Immutable calculations: все вычисления детерминированы, кэш не требуется
    /// 
    /// ОТВЕТСТВЕННОСТИ:
    /// - Вычисление размеров ячеек (hex, ascii)
    /// - Вычисление позиций секций (offset, hex, ascii)
    /// - Конвертация offset -> прямоугольник для рендеринга и hit-testing
    /// - Snapping координат к физическим пикселям (для чёткого рендеринга)
    /// - DPI scaling
    /// </summary>
    internal class HexViewMetrics
    {
        #region Constants
        // Fallback значения для метрик шрифта (когда не удается получить из Typeface)
        private const double FALLBACK_ASCENT_PX = 10.0;
        private const double FALLBACK_DESCENT_PX = 3.0;
        private const double FALLBACK_CHAR_ADVANCE_PX = 7.0;
        
        // Layout константы
        private const double FIRST_VERTICAL_LINE_POSITION = 80.0;
        private const double SECTION_SPACING = 4.0;
        private const double HEX_CELL_PADDING = 12.0;
        private const double ASCII_CELL_PADDING = 4.0;
        private const double FIRST_NIBBLE_POSITION = 6.0;
        private const double LINE_HEIGHT_PADDING = 4.0;
        private const double MIN_LINE_HEIGHT = 16.0;
        #endregion

        private Typeface _typeface;
        private double _fontSize;
        private float _pixelsPerDip;

        public double AscentPx { get; private set; }
        public double DescentPx { get; private set; }
        public double BaselineOffsetInLine { get; private set; }
        public double CharAdvancePx { get; private set; }

        public double CharHeight { get; private set; }
        public double LineHeight { get; private set; }
        public double HexCellWidth { get; private set; }
        public double AsciiCellWidth { get; private set; }
        public double HexSectionStart { get; private set; }
        public double AsciiSectionStart { get; private set; }
        public double TotalWidth { get; private set; }
        private int _bytesPerLine = 16;
        public int BytesPerLine => _bytesPerLine;

        public double FirstNibblePosition { get; private set; }
        public double SecondNibblePosition { get; private set; }

        public double FirstVerticalLinePosition { get; private set; }
        public double HexSectionEnd { get; private set; }

        public Typeface Typeface => _typeface;
        public double FontSize => _fontSize;
        public float PixelsPerDip => _pixelsPerDip;

        public HexViewMetrics(Typeface typeface, double fontSize, float pixelsPerDip, int bytesPerLine = 16)
        {
            _typeface = typeface;
            _fontSize = fontSize;
            _pixelsPerDip = pixelsPerDip;
            _bytesPerLine = Math.Clamp(bytesPerLine, 1, 32);
            UpdateMetrics();
        }

        public void UpdateMetrics()
        {
            if (_typeface.TryGetGlyphTypeface(out var glyphTypeface))
            {
                double em = _fontSize;
                AscentPx = glyphTypeface.Baseline * em;
                DescentPx = Math.Abs(glyphTypeface.Height * em - AscentPx);
                CharHeight = AscentPx + DescentPx;

                CharAdvancePx = glyphTypeface.CharacterToGlyphMap.TryGetValue('0', out ushort zeroGlyph)
                    ? glyphTypeface.AdvanceWidths[zeroGlyph] * em
                    : _fontSize * 0.6;
            }
            else
            {
                // Используем fallback значения, если не удалось получить метрики из Typeface
                AscentPx = FALLBACK_ASCENT_PX;
                DescentPx = FALLBACK_DESCENT_PX;
                CharHeight = AscentPx + DescentPx;
                CharAdvancePx = FALLBACK_CHAR_ADVANCE_PX;
            }

            UpdateLayoutMetrics();
        }

        private void UpdateLayoutMetrics()
        {
            LineHeight = Math.Max(SnapLength(CharHeight + LINE_HEIGHT_PADDING), MIN_LINE_HEIGHT);
            double verticalPadding = (LineHeight - CharHeight) / 2;
            BaselineOffsetInLine = verticalPadding + AscentPx;

            HexCellWidth = SnapLength(2 * CharAdvancePx + HEX_CELL_PADDING);
            AsciiCellWidth = SnapLength(CharAdvancePx + ASCII_CELL_PADDING);

            FirstVerticalLinePosition = FIRST_VERTICAL_LINE_POSITION;
            HexSectionStart = FirstVerticalLinePosition;
            HexSectionEnd = HexSectionStart + (_bytesPerLine * HexCellWidth);
            AsciiSectionStart = SnapPosition(HexSectionEnd + SECTION_SPACING);

            TotalWidth = AsciiSectionStart + (_bytesPerLine * AsciiCellWidth) + SECTION_SPACING;

            FirstNibblePosition = FIRST_NIBBLE_POSITION;
            SecondNibblePosition = FIRST_NIBBLE_POSITION + CharAdvancePx;
        }

        public void UpdateFont(Typeface typeface, double fontSize)
        {
            _typeface = typeface;
            _fontSize = fontSize;
            UpdateMetrics();
        }

        public void SetBytesPerLine(int bytesPerLine)
        {
            bytesPerLine = Math.Clamp(bytesPerLine, 1, 32);
            if (_bytesPerLine == bytesPerLine)
                return;

            _bytesPerLine = bytesPerLine;
            UpdateLayoutMetrics();
        }

        public void UpdateDpi(float pixelsPerDip)
        {
            if (Math.Abs(_pixelsPerDip - pixelsPerDip) < 0.001f)
                return;

            _pixelsPerDip = pixelsPerDip;
            UpdateMetrics();
        }

        public double SnapLength(double value)
        {
            double physicalPixels = value * _pixelsPerDip;
            double snappedPhysicalPixels = Math.Ceiling(physicalPixels);
            return snappedPhysicalPixels / _pixelsPerDip;
        }

        public double SnapPosition(double value)
        {
            double physicalPixels = value * _pixelsPerDip;
            double snappedPhysicalPixels = Math.Round(physicalPixels);
            return snappedPhysicalPixels / _pixelsPerDip;
        }

        public Rect GetHexByteRect(long offset, long scrollOffset)
        {
            long relativeOffset = offset - scrollOffset;
            long line = relativeOffset / _bytesPerLine;
            long positionInLine = relativeOffset % _bytesPerLine;

            // ИСПРАВЛЕНИЕ: Используем одинаковый расчет для синхронизации
            double y = SnapPosition((line + 1) * LineHeight);
            double x = SnapPosition(HexSectionStart + positionInLine * HexCellWidth);

            return new Rect(x, y, HexCellWidth, LineHeight);
        }

        public Rect GetAsciiByteRect(long offset, long scrollOffset)
        {
            long relativeOffset = offset - scrollOffset;
            long line = relativeOffset / _bytesPerLine;
            long positionInLine = relativeOffset % _bytesPerLine;

            // ИСПРАВЛЕНИЕ: Используем одинаковый расчет для синхронизации
            double y = SnapPosition((line + 1) * LineHeight);
            double x = SnapPosition(AsciiSectionStart + positionInLine * AsciiCellWidth);

            return new Rect(x, y, AsciiCellWidth, LineHeight);
        }

        public double GetCaretPosition(HexInputHandler.HexNibblePosition nibblePosition)
        {
            return nibblePosition == HexInputHandler.HexNibblePosition.High
                ? FirstNibblePosition
                : SecondNibblePosition;
        }

        public double GetHexHeaderPosition(int columnIndex)
        {
            return SnapPosition(HexSectionStart + (columnIndex * HexCellWidth) + FirstNibblePosition);
        }

        public double GetAsciiHeaderPosition(int columnIndex, double glyphRunWidth)
        {
            double position = AsciiSectionStart + (columnIndex * AsciiCellWidth) + (AsciiCellWidth - glyphRunWidth) / 2;
            return SnapPosition(position);
        }

        public double GetBaselineY(double lineTop)
        {
            return SnapPosition(lineTop + BaselineOffsetInLine);
        }
    }
}
