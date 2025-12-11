# SPD Edit - Справочник полей редактирования

## Обзор

Полный справочник всех полей редактирования SPD с указанием:
- Байтовых смещений
- Форматов данных
- Типов полей
- Валидации
- Применимости (DDR4/DDR5)

---

## DDR4 Поля

### Memory Module

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `ModuleManufacturer` | Manufacturer | 320-321 | JEDEC ID (hex) | ComboBox | MemoryModule |
| `ModulePartNumber` | Part Number | 329-348 | ASCII (20 bytes) | TextBox | MemoryModule |
| `ModuleSerialNumber` | Serial Number | 325-328 | Hex/ASCII (4 bytes) | TextBox | MemoryModule |
| `ModuleYear` | Manufacturing Year | 323 | BCD (00-99) | ComboBox | MemoryModule |
| `ModuleWeek` | Manufacturing Week | 324 | BCD (01-52) | ComboBox | MemoryModule |
| `ModuleLocation` | Manufacturing Location | 322 | Hex (1 byte) | TextBox | MemoryModule |
| `ModuleType` | Module Type | 3 (bits 3-0) | Hex (0x0F) | ComboBox | MemoryModule |
| `SpdRevisionMajor` | SPD Revision (Major) | 1 (bits 7-4) | Hex (0x0F) | TextBox | MemoryModule |
| `SpdRevisionMinor` | SPD Revision (Minor) | 1 (bits 3-0) | Hex (0x0F) | TextBox | MemoryModule |
| `MemoryType` | Memory Type | 2 | Hex (read-only) | ComboBox | MemoryModule |

### Density & Die

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `Density` | Die Density | 4 (bits 3-0) | Code (0-9) | ComboBox | DensityDie |
| `PackageMonolithic` | Package Type | 6 (bit 7) | Boolean | CheckBox | DensityDie |
| `PackageDieCount` | Die Count | 6 (bits 6-4) | Integer (0-7) | ComboBox | DensityDie |
| `Banks` | Banks | 4 (bits 5-4) | Code (0-1) | ComboBox | DensityDie |
| `BankGroups` | Bank Groups | 4 (bits 7-6) | Code (0-2) | ComboBox | DensityDie |
| `ColumnAddresses` | Column Addresses | 5 (bits 2-0) | Code (0-3) | ComboBox | DensityDie |
| `RowAddresses` | Row Addresses | 5 (bits 6-3) | Code (0-6) | ComboBox | DensityDie |

### DRAM Components

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `DramManufacturer` | DRAM Manufacturer | 350-351 | JEDEC ID (hex) | ComboBox | DramComponents |

### Timing Parameters

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `TimingTckMtb` | tCK MTB | 18 | Byte (MTB) | TextBox | Timing |
| `TimingTckFtb` | tCK FTB | 125 | Signed byte (FTB) | TextBox | Timing |
| `TimingTaaMtb` | tAA MTB | 24 | Byte (MTB) | TextBox | Timing |
| `TimingTaaFtb` | tAA FTB | 123 | Signed byte (FTB) | TextBox | Timing |
| `TimingTrcdMtb` | tRCD MTB | 25 | Byte (MTB) | TextBox | Timing |
| `TimingTrcdFtb` | tRCD FTB | 122 | Signed byte (FTB) | TextBox | Timing |
| `TimingTrpMtb` | tRP MTB | 26 | Byte (MTB) | TextBox | Timing |
| `TimingTrpFtb` | tRP FTB | 121 | Signed byte (FTB) | TextBox | Timing |
| `TimingTras` | tRAS | 28, 27 (bits 3-0) | 12-bit value | TextBox | Timing |
| `TimingTrc` | tRC | 29, 27 (bits 7-4) | 12-bit value | TextBox | Timing |
| `TimingTrcFtb` | tRC FTB | 120 | Signed byte (FTB) | TextBox | Timing |
| `TimingTfaw` | tFAW | 37, 36 (bits 3-0) | 12-bit value | TextBox | Timing |
| `TimingTrrdSMtb` | tRRD_S MTB | 38 | Byte (MTB) | TextBox | Timing |
| `TimingTrrdSFtb` | tRRD_S FTB | 119 | Signed byte (FTB) | TextBox | Timing |
| `TimingTrrdLMtb` | tRRD_L MTB | 39 | Byte (MTB) | TextBox | Timing |
| `TimingTrrdLFtb` | tRRD_L FTB | 118 | Signed byte (FTB) | TextBox | Timing |
| `TimingCcdlMtb` | CCD_L MTB | 40 | Byte (MTB) | TextBox | Timing |
| `TimingCcdlFtb` | CCD_L FTB | 117 | Signed byte (FTB) | TextBox | Timing |
| `TimingTwr` | tWR | 42, 41 (bits 3-0) | 12-bit value | TextBox | Timing |
| `TimingTwtrs` | tWTR_S | 44, 43 (bits 3-0) | 12-bit value | TextBox | Timing |

### Module Configuration

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `ModuleRanks` | Module Ranks | 12 (bits 5-3) | Integer (1-8) | ComboBox | ModuleConfig |
| `DeviceWidth` | Device Width | 12 (bits 2-0) | Code (4,8,16,32) | ComboBox | ModuleConfig |
| `PrimaryBusWidth` | Primary Bus Width | 13 (bits 2-0) | Code (8,16,32,64) | ComboBox | ModuleConfig |
| `HasEcc` | Has ECC | 13 (bit 3) | Boolean | CheckBox | ModuleConfig |
| `RankMix` | Rank Mix | 12 (bit 6) | Boolean | CheckBox | ModuleConfig |
| `ThermalSensor` | Thermal Sensor | 14 (bit 7) | Boolean | CheckBox | ModuleConfig |
| `SupplyVoltageOperable` | Supply Voltage Operable | 11 (bit 0) | Boolean | CheckBox | ModuleConfig |

---

## DDR5 Поля

### Memory Module

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `ModuleManufacturer` | Module Manufacturer | 512-513 | JEDEC ID (hex) | ComboBox | MemoryModule |
| `ModulePartNumber` | Module Part Number | 521-550 | ASCII (30 bytes) | TextBox | MemoryModule |
| `ModuleSerialNumber` | Module Serial Number | 517-520 | Hex/ASCII (4 bytes) | TextBox | MemoryModule |
| `ManufacturingYear` | Manufacturing Year | 515 | BCD (00-99) | TextBox | MemoryModule |
| `ManufacturingWeek` | Manufacturing Week | 516 | BCD (01-52) | TextBox | MemoryModule |

### DRAM Components

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `DramManufacturer` | DRAM Manufacturer | 552-553 | JEDEC ID (hex) | ComboBox | DramComponents |

### Timing Parameters

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `TimingTckMtb` | tCK MTB | 20 | Byte (MTB) | TextBox | Timing |
| `TimingTckFtb` | tCK FTB | 235 | Signed byte (FTB) | TextBox | Timing |
| `TimingTaaMtb` | tAA MTB | 20 | Byte (MTB) | TextBox | Timing |
| `TimingTrcdMtb` | tRCD MTB | 21 | Byte (MTB) | TextBox | Timing |
| `TimingTrcdFtb` | tRCD FTB | 236 | Signed byte (FTB) | TextBox | Timing |
| `TimingTrpMtb` | tRP MTB | 25 | Byte (MTB) | TextBox | Timing |
| `TimingTrpFtb` | tRP FTB | 240 | Signed byte (FTB) | TextBox | Timing |
| `TimingTrasMtb` | tRAS MTB | 26 | Byte (MTB) | TextBox | Timing |
| `TimingTrasFtb` | tRAS FTB | 241 | Signed byte (FTB) | TextBox | Timing |

### Module Configuration

| Field ID | Label | Bytes | Format | Type | Category |
|----------|-------|-------|--------|------|----------|
| `ModuleRanks` | Module Ranks | 12 (bits 5-3) | Integer (1-8) | ComboBox | ModuleConfig |
| `DeviceWidth` | Device Width | 12 (bits 2-0) | Code (4,8,16,32) | ComboBox | ModuleConfig |
| `PrimaryBusWidth` | Primary Bus Width | 13 (bits 2-0) | Code (32,64) | ComboBox | ModuleConfig |
| `ThermalSensor` | Thermal Sensor | 14 (bit 7) | Boolean | CheckBox | ModuleConfig |

---

## Форматы данных

### JEDEC Manufacturer ID
- **Format**: 16-bit value (0x0000-0xFFFF)
- **Encoding**: 
  - Byte N: Continuation Code (MSB)
  - Byte N+1: Manufacturer Code (LSB)
- **Example**: 0x80AD = Byte 320=0x80, Byte 321=0xAD (DDR4)
- **Parity**: Bit 7 of each byte is parity bit (preserved)

### BCD (Binary Coded Decimal)
- **Format**: Two-digit BCD (0x00-0x99)
- **Year**: 00-99 = 2000-2099
- **Week**: 01-52
- **Example**: 0x25 = Year 2025, 0x03 = Week 3

### ASCII String
- **Format**: Null-terminated ASCII string
- **Range**: 0x20-0x7E (printable ASCII)
- **Padding**: Unused bytes filled with 0x00

### MTB/FTB Timing
- **MTB (Medium Timebase)**: Unsigned byte (0-255)
- **FTB (Fine Timebase)**: Signed byte (-128 to +127)
- **Calculation**: `Time = (MTB * MTB_period) + (FTB * FTB_period)`

### 12-bit Timing Values
- **Format**: Split across two bytes
- **LSB Byte**: Lower 8 bits
- **MSB Nibble**: Upper 4 bits in another byte
- **Example**: tRAS = (Byte[27] & 0x0F) << 8 | Byte[28]

### Bit Field Encoding
- **Single Bit**: Boolean flag (0/1)
- **Multi-bit**: Encoded value (0-N)
- **Masking**: Preserves other bits in byte

---

## Валидация

### Manufacturer ID
- ✅ Hex format (0xXXXX or XXXX)
- ✅ Range: 0x0000-0xFFFF
- ✅ Parity bits preserved

### BCD Fields
- ✅ Valid BCD digits (0-9 per nibble)
- ✅ Year: 00-99
- ✅ Week: 01-52

### Timing Values
- ✅ MTB: 0-255
- ✅ FTB: -128 to +127
- ✅ 12-bit values: 0-4095

### Code Fields
- ✅ Valid enum values only
- ✅ Range checks applied

### String Fields
- ✅ Max length enforced
- ✅ ASCII printable characters
- ✅ Null termination

---

## Примечания

1. **Parity Bits**: Manufacturer ID bytes preserve parity bits (bit 7) as per JEDEC standard
2. **Bit Preservation**: All bit field operations preserve unrelated bits
3. **Signed Values**: FTB values are signed bytes (sbyte in C#)
4. **Endianness**: All multi-byte values use little-endian format
5. **Validation**: All fields have appropriate validation before writing

---

**Версия**: 1.0  
**Дата**: 2025-01-10

