using System.Collections.Generic;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Интерфейс для декодеров SPD
    /// </summary>
    internal interface ISpdDecoder
    {
        void Populate(
            List<SpdInfoPanel.InfoItem> moduleInfo,
            List<SpdInfoPanel.InfoItem> dramInfo,
            List<SpdInfoPanel.TimingRow> timingRows);
    }
}

