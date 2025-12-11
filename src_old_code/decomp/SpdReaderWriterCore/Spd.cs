using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SpdReaderWriterCore.Properties;

namespace SpdReaderWriterCore
{
	public class Spd
	{
		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DDR : ISpd
		{
			public struct BanksData
			{
				public byte Physical;

				public byte Logical;
			}

			public struct Timing
			{
				public byte Whole;

				public byte Tenth;

				public byte Hundredth;

				public byte Quarter;

				public byte Fraction;

				public float ToNanoSeconds()
				{
					float[] array = new float[4] { 0.25f, 0.33f, 0.66f, 0.75f };
					float[] array2 = new float[6] { 0f, 0.25f, 0.33f, 0.5f, 0.66f, 0.75f };
					return (float)(int)Whole + (float)(int)Quarter * 0.25f + ((10 <= Tenth && Tenth <= 13) ? array[Tenth - 10] : ((float)(int)Tenth * 0.1f)) + (float)(int)Hundredth * 0.01f + array2[Fraction];
				}

				public int ToClockCycles(Timing refTiming)
				{
					return (int)Math.Ceiling(ToNanoSeconds() / refTiming.ToNanoSeconds());
				}

				public override string ToString()
				{
					return ToNanoSeconds().ToString("F2");
				}
			}

			public struct DIMMConfigurationData
			{
				public bool DataECC;

				public bool DataParity;
			}

			public struct SDRAMWidthData
			{
				public byte Width;

				public bool Bank2;
			}

			public struct BurstLengthData
			{
				public byte Length;

				public bool Supported;
			}

			public struct CasLatenciesData
			{
				public byte Bitmask;

				public float[] ToArray()
				{
					Queue<float> queue = new Queue<float>();
					for (byte b = 0; b <= 6; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue((float)(int)b / 2f + 1f);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					float[] array = ToArray();
					foreach (float num in array)
					{
						text += string.Format("{0},", num);
					}
					return text.TrimEnd(',');
				}
			}

			public struct LatenciesData
			{
				public byte Bitmask;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 0; b <= 6; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue(b);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					for (int i = 0; i < array.Length; i++)
					{
						byte b = (byte)array[i];
						text += string.Format("{0},", b);
					}
					return text.TrimEnd(',');
				}
			}

			public struct ModulesAttributesData
			{
				public bool DifferentialClockInput;

				public bool FETSwitchExternal;

				public bool FETSwitchOnCard;

				public bool OnCardPLL;

				public bool RegisteredAddressControlInputs;

				public bool BufferedAddressControlInputs;
			}

			public struct DeviceAttributesData
			{
				public bool FastAP;

				public bool ConcurrentAutoPrecharge;

				public bool UpperVccTolerance;

				public bool LowerVccTolerance;

				public bool WeakDriver;
			}

			public int Length
			{
				get
				{
					return 256;
				}
			}

			public BytesData Bytes
			{
				get
				{
					return new BytesData
					{
						Used = RawData[0],
						Total = (ushort)Math.Pow(2.0, (int)RawData[1])
					};
				}
			}

			public int SpdBytesUsed
			{
				get
				{
					return Bytes.Used;
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public AddressingData Addressing
			{
				get
				{
					return new AddressingData
					{
						Rows = RawData[3],
						Columns = RawData[4]
					};
				}
			}

			public BanksData Banks
			{
				get
				{
					return new BanksData
					{
						Physical = RawData[5],
						Logical = RawData[17]
					};
				}
			}

			public ushort DataWidth
			{
				get
				{
					return (ushort)(RawData[6] | (RawData[7] << 8));
				}
			}

			public ulong DieDensity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * PrimarySDRAMWidth.Width);
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * (DataWidth & 0xF0) * Banks.Physical / 8);
				}
			}

			public VoltageLevel VoltageInterfaceLevel
			{
				get
				{
					return (VoltageLevel)RawData[8];
				}
			}

			public Timing tCKmin
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[9], 7, 4),
						Tenth = Data.SubByte(RawData[9], 3, 4)
					};
				}
			}

			public Timing tAC
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[10], 7, 4),
						Hundredth = Data.SubByte(RawData[10], 3, 4)
					};
				}
			}

			public DIMMConfigurationData DIMMConfiguration
			{
				get
				{
					return new DIMMConfigurationData
					{
						DataECC = Data.GetBit(RawData[11], 1),
						DataParity = Data.GetBit(RawData[11], 0)
					};
				}
			}

			public RefreshRateData RefreshRate
			{
				get
				{
					return new RefreshRateData
					{
						RefreshPeriod = Data.SubByte(RawData[12], 6, 7),
						SelfRefresh = Data.GetBit(RawData[12], 7)
					};
				}
			}

			public SDRAMWidthData PrimarySDRAMWidth
			{
				get
				{
					return new SDRAMWidthData
					{
						Width = Data.SubByte(RawData[13], 6, 7),
						Bank2 = Data.GetBit(RawData[13], 7)
					};
				}
			}

			public SDRAMWidthData ErrorCheckingSDRAMWidth
			{
				get
				{
					return new SDRAMWidthData
					{
						Width = Data.SubByte(RawData[14], 6, 7),
						Bank2 = Data.GetBit(RawData[14], 7)
					};
				}
			}

			public byte tCCD
			{
				get
				{
					return RawData[15];
				}
			}

			public BurstLengthData[] BurstLength
			{
				get
				{
					BurstLengthData[] array = new BurstLengthData[4];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Length = (byte)(1 << (int)b);
						array[b].Supported = Data.GetBit(RawData[16], b);
					}
					return array;
				}
			}

			public byte DeviceBanks
			{
				get
				{
					return RawData[17];
				}
			}

			public CasLatenciesData tCL
			{
				get
				{
					return new CasLatenciesData
					{
						Bitmask = RawData[18]
					};
				}
			}

			public LatenciesData CS
			{
				get
				{
					return new LatenciesData
					{
						Bitmask = RawData[19]
					};
				}
			}

			public LatenciesData WE
			{
				get
				{
					return new LatenciesData
					{
						Bitmask = RawData[20]
					};
				}
			}

			public ModulesAttributesData ModulesAttributes
			{
				get
				{
					return new ModulesAttributesData
					{
						DifferentialClockInput = Data.GetBit(RawData[21], 5),
						FETSwitchExternal = Data.GetBit(RawData[21], 4),
						FETSwitchOnCard = Data.GetBit(RawData[21], 3),
						OnCardPLL = Data.GetBit(RawData[21], 2),
						RegisteredAddressControlInputs = Data.GetBit(RawData[21], 1),
						BufferedAddressControlInputs = Data.GetBit(RawData[21], 0)
					};
				}
			}

			public DeviceAttributesData DeviceAttributes
			{
				get
				{
					return new DeviceAttributesData
					{
						FastAP = Data.GetBit(RawData[22], 7),
						ConcurrentAutoPrecharge = Data.GetBit(RawData[22], 6),
						UpperVccTolerance = Data.GetBit(RawData[22], 5),
						LowerVccTolerance = Data.GetBit(RawData[22], 4),
						WeakDriver = Data.GetBit(RawData[22], 0)
					};
				}
			}

			public Timing tCKminX05
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[23], 7, 4),
						Tenth = Data.SubByte(RawData[23], 3, 4)
					};
				}
			}

			public Timing tACmaxX05
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[24], 7, 4),
						Hundredth = Data.SubByte(RawData[24], 3, 4)
					};
				}
			}

			public Timing tCKminX1
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[25], 7, 4),
						Tenth = Data.SubByte(RawData[25], 3, 4)
					};
				}
			}

			public Timing tACmaxX1
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[26], 7, 4),
						Hundredth = Data.SubByte(RawData[26], 3, 4)
					};
				}
			}

			public Timing tRP
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[27], 7, 6),
						Quarter = Data.SubByte(RawData[27], 1, 2)
					};
				}
			}

			public Timing tRRD
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[28], 7, 6),
						Quarter = Data.SubByte(RawData[28], 1, 2)
					};
				}
			}

			public Timing tRCD
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[29], 7, 6),
						Quarter = Data.SubByte(RawData[29], 1, 2)
					};
				}
			}

			public Timing tRAS
			{
				get
				{
					return new Timing
					{
						Whole = RawData[30]
					};
				}
			}

			public ushort RankDensity
			{
				get
				{
					return (ushort)(RawData[31] * 4);
				}
			}

			public Timing tIS
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[32], 7, 4),
						Hundredth = Data.SubByte(RawData[32], 3, 4)
					};
				}
			}

			public Timing tIH
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[33], 7, 4),
						Hundredth = Data.SubByte(RawData[33], 3, 4)
					};
				}
			}

			public Timing tDS
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[34], 7, 4),
						Hundredth = Data.SubByte(RawData[34], 3, 4)
					};
				}
			}

			public Timing tDH
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[35], 7, 4),
						Hundredth = Data.SubByte(RawData[35], 3, 4)
					};
				}
			}

			public Timing tRC
			{
				get
				{
					return new Timing
					{
						Whole = RawData[41]
					};
				}
			}

			public Timing tRFC
			{
				get
				{
					return new Timing
					{
						Whole = RawData[42]
					};
				}
			}

			public Timing tCKmax
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[43], 7, 6),
						Quarter = Data.SubByte(RawData[43], 1, 2)
					};
				}
			}

			public Timing tDQSQmax
			{
				get
				{
					return new Timing
					{
						Hundredth = RawData[44]
					};
				}
			}

			public Timing tQHS
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[45], 7, 4),
						Hundredth = Data.SubByte(RawData[45], 3, 4)
					};
				}
			}

			public ModuleHeightData Height
			{
				get
				{
					float[] array = new float[3] { 1.125f, 1.25f, 1.7f };
					switch (Data.SubByte(RawData[47], 1, 2))
					{
					case 1:
						return new ModuleHeightData
						{
							Minimum = array[0],
							Maximum = array[1],
							Unit = HeightUnit.IN
						};
					case 2:
						return new ModuleHeightData
						{
							Minimum = array[2],
							Maximum = array[2],
							Unit = HeightUnit.IN
						};
					default:
						return default(ModuleHeightData);
					}
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[62], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[62], 3, 4)
					};
				}
			}

			public Crc8Data Crc
			{
				get
				{
					Crc8Data result = new Crc8Data
					{
						Contents = new byte[64]
					};
					Array.Copy(RawData, result.Contents, result.Contents.Length);
					return result;
				}
			}

			public bool CrcStatus
			{
				get
				{
					return Crc.Validate();
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					byte b = 0;
					byte manufacturerCode = 0;
					byte b2 = 64;
					while (b2 <= 71)
					{
						if (RawData[b2] == 127)
						{
							b++;
							b2++;
							continue;
						}
						manufacturerCode = RawData[b2];
						break;
					}
					return new ManufacturerIdCodeData
					{
						ContinuationCode = b,
						ManufacturerCode = manufacturerCode
					};
				}
			}

			public byte ManufacturingLocation
			{
				get
				{
					return RawData[72];
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 73;
					char[] array = new char[90 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public ushort RevisionCode
			{
				get
				{
					return (ushort)(RawData[92] | (RawData[91] << 8));
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[93],
						Week = RawData[94]
					};
				}
			}

			public SerialNumberData ModuleSerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 95, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public DDR(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public bool FixCrc()
			{
				Array.Copy(Crc.Fix(), RawData, Crc.Contents.Length);
				return Crc.Validate();
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DDR2 : ISpd
		{
			public struct ModuleAttributesData
			{
				public ModuleHeightData Height;

				public DRAMPackage Package;

				public bool CardOnCard;

				public byte Ranks;
			}

			public enum DRAMPackage
			{
				Planar,
				Stack
			}

			public struct Timing
			{
				public int Whole;

				public int Tenth;

				public int Hundredth;

				public int Quarter;

				public int Fraction;

				public float ToNanoSeconds()
				{
					float[] array = new float[4] { 0.25f, 0.33f, 0.66f, 0.75f };
					float[] array2 = new float[6] { 0f, 0.25f, 0.33f, 0.5f, 0.66f, 0.75f };
					return (float)Whole + (float)Quarter * 0.25f + ((10 <= Tenth && Tenth <= 13) ? array[Tenth - 10] : ((float)Tenth * 0.1f)) + (float)Hundredth * 0.01f + array2[Fraction];
				}

				public int ToClockCycles(Timing refTiming)
				{
					return (int)Math.Ceiling(ToNanoSeconds() / refTiming.ToNanoSeconds());
				}

				public override string ToString()
				{
					return ToNanoSeconds().ToString("F2");
				}
			}

			public struct DIMMConfigurationData
			{
				public bool AddressCommandParity;

				public bool DataECC;

				public bool DataParity;
			}

			public struct RefreshRateData
			{
				public byte RefreshPeriod;

				public float ToMicroseconds()
				{
					float num = 15.625f;
					if (RefreshPeriod == 128)
					{
						return num;
					}
					if (129 <= RefreshPeriod && RefreshPeriod <= 130)
					{
						return num * 0.25f * (float)(RefreshPeriod - 128);
					}
					if (131 <= RefreshPeriod && RefreshPeriod <= 133)
					{
						return num * (float)(1 << RefreshPeriod - 129);
					}
					throw new ArgumentOutOfRangeException("RefreshPeriod");
				}

				public override string ToString()
				{
					return ToMicroseconds().ToString("F3");
				}
			}

			public struct BurstLengthData
			{
				public byte Length;

				public bool Supported;
			}

			public struct CasLatenciesData
			{
				public byte Bitmask;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 2; b <= 7; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue(b);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					for (int i = 0; i < array.Length; i++)
					{
						byte b = (byte)array[i];
						text += string.Format("{0},", b);
					}
					return text.TrimEnd(',');
				}
			}

			public struct ModuleThicknessData
			{
				public byte Encoding;
			}

			public enum BaseModuleType
			{
				[Description("Registered Dual In-Line Memory Module")]
				RDIMM = 1,
				[Description("Unbuffered Dual In-Line Memory Module")]
				UDIMM,
				[Description("Small Outline Dual In-Line Memory Module")]
				SO_DIMM,
				[Description("Clocked SO-DIMM with 72-bit data bus")]
				_72b_SO_CDIMM,
				[Description("Registered SO-DIMM with 72-bit data bus")]
				_72b_SO_RDIMM,
				[Description("Micro Dual In-Line Memory Module")]
				Micro_DIMM,
				[Description("Mini Registered Dual In-Line Memory Module")]
				Mini_RDIMM,
				[Description("Mini Unbuffered Dual In-Line Memory Module")]
				Mini_UDIMM
			}

			public struct ModulesAttributes
			{
				public bool AnalysisProbeInstalled;

				public bool FETSwitchExternal;

				public byte PLLs;

				public byte ActiveRegisters;
			}

			public struct DeviceAttributes
			{
				public bool PartialArraySelfRefresh;

				public bool Supports50ohmODT;

				public bool WeakDriver;
			}

			public struct TemperatureData
			{
				public float Granularity;

				public byte Multiplier;

				public float ToDegrees()
				{
					return Granularity * (float)(int)Multiplier;
				}

				public override string ToString()
				{
					return ToDegrees().ToString("F3");
				}
			}

			public struct TcasemaxData
			{
				public TemperatureData Tcasemax;

				public TemperatureData DT4R4WDelta;
			}

			public enum EppProfileType
			{
				Abbreviated = 161,
				Full = 177
			}

			public struct EppFullProfileData
			{
				public byte Number;

				private byte wc
				{
					get
					{
						return (byte)(Number * 12);
					}
				}

				public bool IsOptimal
				{
					get
					{
						return Data.SubByte(RawData[103], 1, 2) == Number;
					}
				}

				public bool Enabled
				{
					get
					{
						return Data.GetBit(Data.SubByte(RawData[103], 5, 2), Number);
					}
				}

				public float VoltageLevel
				{
					get
					{
						return (float)(Data.SubByte(RawData[104 + wc], 6, 7) * 25 + 1800) / 1000f;
					}
				}

				public byte AddressCmdRate
				{
					get
					{
						return (byte)(Data.SubByte(RawData[104 + wc], 7, 1) + 1);
					}
				}

				public float AddressDriveStrength
				{
					get
					{
						return (new float[4] { 1f, 1.25f, 1.5f, 2f })[Data.SubByte(RawData[105 + wc], 1, 2)];
					}
				}

				public float ChipSelectDriveStrength
				{
					get
					{
						return (new float[4] { 1f, 1.25f, 1.5f, 2f })[Data.SubByte(RawData[105 + wc], 3, 2)];
					}
				}

				public float ClockDriveStrength
				{
					get
					{
						return (float)(int)Data.SubByte(RawData[105 + wc], 5, 2) * 0.25f + 0.75f;
					}
				}

				public float DataDriveStrength
				{
					get
					{
						return (float)(int)Data.SubByte(RawData[105 + wc], 7, 2) * 0.25f + 0.75f;
					}
				}

				public float DQSDriveStrength
				{
					get
					{
						return (float)(int)Data.SubByte(RawData[106 + wc], 1, 2) * 0.25f + 0.75f;
					}
				}

				public DelayData AddressCommandFineDelay
				{
					get
					{
						return new DelayData
						{
							Delay = Data.SubByte(RawData[107 + wc], 4, 5)
						};
					}
				}

				public float AddressCommandSetupTime
				{
					get
					{
						return (float)(Data.BoolToNum<byte>(Data.GetBit(RawData[107 + wc], 5)) + 1) / 2f;
					}
				}

				public DelayData ChipSelectDelay
				{
					get
					{
						return new DelayData
						{
							Delay = Data.SubByte(RawData[108 + wc], 4, 5)
						};
					}
				}

				public float ChipSelectSetupTime
				{
					get
					{
						return (float)(Data.SubByte(RawData[108 + wc], 5, 1) + 1) / 2f;
					}
				}

				public Timing tCK
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[109 + wc], 7, 4),
							Tenth = Data.SubByte(RawData[109 + wc], 3, 4)
						};
					}
				}

				public byte tCL
				{
					get
					{
						for (byte b = 2; b < 8; b++)
						{
							if (Data.GetBit(RawData[110 + wc] >> (int)b, 0))
							{
								return b;
							}
						}
						return 0;
					}
				}

				public Timing tRCD
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[111 + wc], 7, 6),
							Quarter = Data.SubByte(RawData[111 + wc], 1, 2)
						};
					}
				}

				public Timing tRP
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[112 + wc], 7, 6),
							Quarter = Data.SubByte(RawData[112 + wc], 1, 2)
						};
					}
				}

				public Timing tRAS
				{
					get
					{
						return new Timing
						{
							Whole = RawData[113 + wc]
						};
					}
				}

				public Timing tWR
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[114 + wc], 7, 6),
							Quarter = Data.SubByte(RawData[114 + wc], 1, 2)
						};
					}
				}

				public Timing tRC
				{
					get
					{
						return new Timing
						{
							Whole = RawData[115 + wc]
						};
					}
				}

				public override string ToString()
				{
					return (Enabled ? (string.Format("{0} MHz ", 1000f / tCK.ToNanoSeconds()) + string.Format("{0}-{1}-{2}-{3} ", tCL, tRCD.ToClockCycles(tCK), tRP.ToClockCycles(tCK), tRAS.ToClockCycles(tCK)) + string.Format("{0}V", VoltageLevel)) : "") + (IsOptimal ? "+" : "");
				}
			}

			public struct EppAbbreviatedProfileData
			{
				public byte Number;

				private byte dc
				{
					get
					{
						return (byte)(Number * 6);
					}
				}

				public bool IsOptimal
				{
					get
					{
						return Data.SubByte(RawData[103], 1, 2) == Number;
					}
				}

				public bool Enabled
				{
					get
					{
						return Data.GetBit(Data.SubByte(RawData[103], 7, 4), Number);
					}
				}

				public float VoltageLevel
				{
					get
					{
						return (float)(Data.SubByte(RawData[104 + dc], 6, 7) * 25 + 1800) / 1000f;
					}
				}

				public byte AddressCmdRate
				{
					get
					{
						return (byte)(Data.SubByte(RawData[104 + dc], 7, 1) + 1);
					}
				}

				public Timing tCK
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[105 + dc], 7, 4),
							Tenth = Data.SubByte(RawData[105 + dc], 3, 4)
						};
					}
				}

				public byte tCL
				{
					get
					{
						for (byte b = 2; b < 8; b++)
						{
							if (Data.GetBit(RawData[106 + dc] >> (int)b, 0))
							{
								return b;
							}
						}
						return 0;
					}
				}

				public Timing tRCD
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[107 + dc], 7, 6),
							Quarter = Data.SubByte(RawData[107 + dc], 1, 2)
						};
					}
				}

				public Timing tRP
				{
					get
					{
						return new Timing
						{
							Whole = Data.SubByte(RawData[108 + dc], 7, 6),
							Quarter = Data.SubByte(RawData[108 + dc], 1, 2)
						};
					}
				}

				public Timing tRAS
				{
					get
					{
						return new Timing
						{
							Whole = RawData[109 + dc]
						};
					}
				}

				public override string ToString()
				{
					return (Enabled ? string.Format("{0}MHz {1}-{2}-{3}-{4} {5}V", 1000f / tCK.ToNanoSeconds(), tCL, tRCD.ToClockCycles(tCK), tRP.ToClockCycles(tCK), tRAS.ToClockCycles(tCK), VoltageLevel) : "N/A") + (IsOptimal ? "+" : "");
				}
			}

			public struct DelayData
			{
				public byte Delay;

				public override string ToString()
				{
					return string.Format("{0}/64", Delay);
				}
			}

			public int Length
			{
				get
				{
					return 256;
				}
			}

			public BytesData Bytes
			{
				get
				{
					return new BytesData
					{
						Used = RawData[0],
						Total = (ushort)Math.Pow(2.0, (int)RawData[1])
					};
				}
			}

			int ISpd.NB
			{
				get
				{
					return Bytes.Used;
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public AddressingData Addressing
			{
				get
				{
					return new AddressingData
					{
						Rows = RawData[3],
						Columns = RawData[4]
					};
				}
			}

			public ModuleAttributesData ModuleAttributes
			{
				get
				{
					float[] array = new float[3] { 25.4f, 30f, 30.5f };
					ModuleAttributesData result = new ModuleAttributesData
					{
						Package = (DRAMPackage)Data.BoolToNum<byte>(Data.GetBit(RawData[5], 4)),
						CardOnCard = Data.GetBit(RawData[5], 3),
						Ranks = (byte)(Data.SubByte(RawData[5], 2, 3) + 1)
					};
					switch (Data.SubByte(RawData[5], 7, 3))
					{
					default:
						result.Height = new ModuleHeightData
						{
							Minimum = 0f,
							Maximum = array[0],
							Unit = HeightUnit.mm
						};
						break;
					case 1:
						result.Height = new ModuleHeightData
						{
							Minimum = array[0],
							Maximum = array[0],
							Unit = HeightUnit.mm
						};
						break;
					case 2:
						result.Height = new ModuleHeightData
						{
							Minimum = array[0],
							Maximum = array[1],
							Unit = HeightUnit.mm
						};
						break;
					case 3:
						result.Height = new ModuleHeightData
						{
							Minimum = array[1],
							Maximum = array[1],
							Unit = HeightUnit.mm
						};
						break;
					case 4:
						result.Height = new ModuleHeightData
						{
							Minimum = array[2],
							Maximum = array[2],
							Unit = HeightUnit.mm
						};
						break;
					case 5:
						result.Height = new ModuleHeightData
						{
							Minimum = array[2],
							Unit = HeightUnit.mm
						};
						break;
					}
					return result;
				}
			}

			public byte DataWidth
			{
				get
				{
					return RawData[6];
				}
			}

			public VoltageLevel VoltageInterfaceLevel
			{
				get
				{
					return (VoltageLevel)RawData[8];
				}
			}

			public Timing tCKmin
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[9], 7, 4),
						Tenth = Data.SubByte(RawData[9], 3, 4)
					};
				}
			}

			public Timing tAC
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[10], 7, 4),
						Hundredth = Data.SubByte(RawData[10], 3, 4)
					};
				}
			}

			public DIMMConfigurationData DIMMConfiguration
			{
				get
				{
					return new DIMMConfigurationData
					{
						AddressCommandParity = Data.GetBit(RawData[11], 2),
						DataECC = Data.GetBit(RawData[11], 1),
						DataParity = Data.GetBit(RawData[11], 0)
					};
				}
			}

			public RefreshRateData RefreshRate
			{
				get
				{
					return new RefreshRateData
					{
						RefreshPeriod = RawData[12]
					};
				}
			}

			public byte PrimarySDRAMWidth
			{
				get
				{
					return RawData[13];
				}
			}

			public byte ErrorCheckingSDRAMWidth
			{
				get
				{
					return RawData[14];
				}
			}

			public BurstLengthData[] BurstLength
			{
				get
				{
					BurstLengthData[] array = new BurstLengthData[2];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Length = (byte)(1 << (int)b);
						array[b].Supported = Data.GetBit(RawData[16], (byte)(b + 2));
					}
					return array;
				}
			}

			public byte DeviceBanks
			{
				get
				{
					return RawData[17];
				}
			}

			public CasLatenciesData tCL
			{
				get
				{
					return new CasLatenciesData
					{
						Bitmask = RawData[18]
					};
				}
			}

			public ModuleThicknessData Thickness
			{
				get
				{
					return new ModuleThicknessData
					{
						Encoding = Data.SubByte(RawData[19], 2, 3)
					};
				}
			}

			public BaseModuleType DimmType
			{
				get
				{
					return (BaseModuleType)Data.SubByte(RawData[20], 5, 6);
				}
			}

			public ModulesAttributes SDRAMModulesAttributes
			{
				get
				{
					return new ModulesAttributes
					{
						AnalysisProbeInstalled = Data.GetBit(RawData[21], 6),
						FETSwitchExternal = Data.GetBit(RawData[21], 4),
						PLLs = Data.SubByte(RawData[20], 3, 2),
						ActiveRegisters = Data.SubByte(RawData[20], 1, 2)
					};
				}
			}

			public DeviceAttributes GeneralAttributes
			{
				get
				{
					return new DeviceAttributes
					{
						PartialArraySelfRefresh = Data.GetBit(RawData[22], 2),
						Supports50ohmODT = Data.GetBit(RawData[22], 1),
						WeakDriver = Data.GetBit(RawData[22], 0)
					};
				}
			}

			public Timing tCKminX1
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[23], 7, 4),
						Tenth = Data.SubByte(RawData[23], 3, 4)
					};
				}
			}

			public Timing tACmaxX1
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[24], 7, 4),
						Hundredth = Data.SubByte(RawData[24], 3, 4)
					};
				}
			}

			public Timing tCKminX2
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[25], 7, 4),
						Tenth = Data.SubByte(RawData[25], 3, 4)
					};
				}
			}

			public Timing tACmaxX2
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[26], 7, 4),
						Hundredth = Data.SubByte(RawData[26], 3, 4)
					};
				}
			}

			public Timing tRP
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[27], 7, 6),
						Quarter = Data.SubByte(RawData[27], 1, 2)
					};
				}
			}

			public Timing tRRD
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[28], 7, 6),
						Quarter = Data.SubByte(RawData[28], 1, 2)
					};
				}
			}

			public Timing tRCD
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[29], 7, 6),
						Quarter = Data.SubByte(RawData[29], 1, 2)
					};
				}
			}

			public Timing tRAS
			{
				get
				{
					return new Timing
					{
						Whole = RawData[30]
					};
				}
			}

			public ushort RankDensity
			{
				get
				{
					byte b = RawData[31];
					if (b > 16)
					{
						return (ushort)(b * 4);
					}
					return (ushort)(b * 1024);
				}
			}

			public ulong DieDensity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * PrimarySDRAMWidth);
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * (DataWidth & 0xF0) * ModuleAttributes.Ranks / 8);
				}
			}

			public Timing tIS
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[32], 7, 4),
						Hundredth = Data.SubByte(RawData[32], 3, 4)
					};
				}
			}

			public Timing tIH
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[33], 7, 4),
						Hundredth = Data.SubByte(RawData[33], 3, 4)
					};
				}
			}

			public Timing tDS
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[34], 7, 4),
						Hundredth = Data.SubByte(RawData[34], 3, 4)
					};
				}
			}

			public Timing tDH
			{
				get
				{
					return new Timing
					{
						Tenth = Data.SubByte(RawData[35], 7, 4),
						Hundredth = Data.SubByte(RawData[35], 3, 4)
					};
				}
			}

			public Timing tWR
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[36], 7, 6),
						Quarter = Data.SubByte(RawData[36], 1, 2)
					};
				}
			}

			public Timing tWTR
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[37], 7, 6),
						Quarter = Data.SubByte(RawData[37], 1, 2)
					};
				}
			}

			public Timing tRTP
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[38], 7, 6),
						Quarter = Data.SubByte(RawData[38], 1, 2)
					};
				}
			}

			public Timing tRC
			{
				get
				{
					return new Timing
					{
						Whole = RawData[41],
						Fraction = Data.SubByte(RawData[40], 7, 4)
					};
				}
			}

			public Timing tRFC
			{
				get
				{
					return new Timing
					{
						Whole = RawData[42],
						Fraction = Data.SubByte(RawData[40], 3, 3)
					};
				}
			}

			public Timing tCKmax
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[43], 7, 4),
						Tenth = Data.SubByte(RawData[43], 3, 4)
					};
				}
			}

			public Timing tDQSQmax
			{
				get
				{
					return new Timing
					{
						Hundredth = RawData[44]
					};
				}
			}

			public Timing tQHS
			{
				get
				{
					return new Timing
					{
						Hundredth = RawData[45]
					};
				}
			}

			public byte PLLRelockTime
			{
				get
				{
					return RawData[46];
				}
			}

			public TcasemaxData Tcasemax
			{
				get
				{
					return new TcasemaxData
					{
						Tcasemax = new TemperatureData
						{
							Granularity = 2f,
							Multiplier = Data.SubByte(RawData[47], 7, 4)
						},
						DT4R4WDelta = new TemperatureData
						{
							Granularity = 0.4f,
							Multiplier = Data.SubByte(RawData[47], 3, 4)
						}
					};
				}
			}

			public TemperatureData PsiTADRAM
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.5f,
						Multiplier = RawData[48]
					};
				}
			}

			public TemperatureData DT0
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.3f,
						Multiplier = Data.SubByte(RawData[49], 7, 6)
					};
				}
			}

			public bool DoubleRefreshRateRequired
			{
				get
				{
					return Data.GetBit(RawData[49], 1);
				}
			}

			public bool HighTemperatureSelfRefreshSupported
			{
				get
				{
					return Data.GetBit(RawData[49], 0);
				}
			}

			public TemperatureData DT2N
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.1f,
						Multiplier = RawData[50]
					};
				}
			}

			public TemperatureData DT2P
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.015f,
						Multiplier = RawData[51]
					};
				}
			}

			public TemperatureData DT3N
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.15f,
						Multiplier = RawData[52]
					};
				}
			}

			public TemperatureData DT3Pfast
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.05f,
						Multiplier = RawData[53]
					};
				}
			}

			public TemperatureData DT3Pslow
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.025f,
						Multiplier = RawData[54]
					};
				}
			}

			public TemperatureData DT4R
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.4f,
						Multiplier = Data.SubByte(RawData[55], 7, 7)
					};
				}
			}

			public bool DT4R4WMode
			{
				get
				{
					return Data.GetBit(RawData[55], 0);
				}
			}

			public TemperatureData DT5B
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.5f,
						Multiplier = RawData[56]
					};
				}
			}

			public TemperatureData DT7
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.5f,
						Multiplier = RawData[57]
					};
				}
			}

			public TemperatureData PsiTAPLL
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.5f,
						Multiplier = RawData[58]
					};
				}
			}

			public TemperatureData PsiTARegister
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.5f,
						Multiplier = RawData[59]
					};
				}
			}

			public TemperatureData DTPLLActive
			{
				get
				{
					return new TemperatureData
					{
						Granularity = 0.25f,
						Multiplier = RawData[60]
					};
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[62], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[62], 3, 4)
					};
				}
			}

			public Crc8Data Crc
			{
				get
				{
					Crc8Data result = new Crc8Data
					{
						Contents = new byte[64]
					};
					Array.Copy(RawData, result.Contents, result.Contents.Length);
					return result;
				}
			}

			public bool CrcStatus
			{
				get
				{
					return Crc.Validate();
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					byte b = 0;
					byte manufacturerCode = 0;
					byte b2 = 64;
					while (b2 <= 71)
					{
						if (RawData[b2] == 127)
						{
							b++;
							b2++;
							continue;
						}
						manufacturerCode = RawData[b2];
						break;
					}
					return new ManufacturerIdCodeData
					{
						ContinuationCode = b,
						ManufacturerCode = manufacturerCode
					};
				}
			}

			public byte ManufacturingLocation
			{
				get
				{
					return RawData[72];
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 73;
					char[] array = new char[90 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public byte ModuleManufacturingLocation
			{
				get
				{
					return RawData[72];
				}
			}

			public ushort RevisionCode
			{
				get
				{
					return (ushort)(RawData[92] | (RawData[91] << 8));
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[93],
						Week = RawData[94]
					};
				}
			}

			public SerialNumberData ModuleSerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 95, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public bool EppPresence
			{
				get
				{
					return Data.MatchArray(RawData, ProfileId.EPP, 99);
				}
			}

			public EppProfileType EppType
			{
				get
				{
					return (EppProfileType)RawData[102];
				}
			}

			public EppFullProfileData[] EppFullProfile
			{
				get
				{
					EppFullProfileData[] array = new EppFullProfileData[(EppType == EppProfileType.Full) ? 2 : 0];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Number = b;
					}
					return array;
				}
			}

			public EppAbbreviatedProfileData[] EppAbbreviatedProfile
			{
				get
				{
					EppAbbreviatedProfileData[] array = new EppAbbreviatedProfileData[(EppType == EppProfileType.Abbreviated) ? 4 : 0];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Number = b;
					}
					return array;
				}
			}

			public DDR2(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public bool FixCrc()
			{
				Array.Copy(Crc.Fix(), RawData, Crc.Contents.Length);
				return CrcStatus;
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DDR3 : ISpd
		{
			public struct ModuleTypeData
			{
				public bool HybridMedia;

				public BaseModuleType BaseModuleType;

				public override string ToString()
				{
					return BaseModuleType.ToString().TrimStart('_').Replace("_", "-")
						.Trim() ?? "";
				}
			}

			public enum BaseModuleType
			{
				Undefined,
				[Description("Registered Dual In-Line Memory Module")]
				RDIMM,
				[Description("Unbuffered Dual In-Line Memory Module")]
				UDIMM,
				[Description("Unbuffered 64-bit Small Outline Dual In-Line Memory Module")]
				SO_DIMM,
				[Description("Micro Dual In-Line Memory Module")]
				Micro_DIMM,
				[Description("Mini Registered Dual In-Line Memory Module")]
				Mini_RDIMM,
				[Description("Mini Unbuffered Dual In-Line Memory Module")]
				Mini_UDIMM,
				[Description("Clocked 72-bit Mini Dual In-Line Memory Module")]
				Mini_CDIMM,
				[Description("Unbuffered 72-bit Small Outline Dual In-Line Memory Module")]
				_72b_SO_UDIMM,
				[Description("Registered 72-bit Small Outline Dual In-Line Memory Module")]
				_72b_SO_RDIMM,
				[Description("Clocked 72-bit Small Outline Dual In-Line Memory Module")]
				_72b_SO_CDIMM,
				[Description("Load Reduced Dual In-Line Memory Module")]
				LRDIMM,
				[Description("Unbuffered 16-bit Small Outline Dual In-Line Memory Module")]
				_16b_SO_DIMM,
				[Description("Unbuffered 32-bit Small Outline Dual In-Line Memory Module")]
				_32b_SO_DIMM
			}

			public struct DensityBanksData
			{
				public byte BankAddress;

				public ushort TotalCapacityPerDie;
			}

			public struct ModuleNominalVoltageData
			{
				public float Voltage;

				public bool Operable;
			}

			public struct ModuleOrganizationData
			{
				public byte PackageRankCount;

				public byte DeviceWidth;
			}

			public struct TimebaseData
			{
				public float Dividend;

				public float Divisor;

				public float ToNanoSeconds()
				{
					return Dividend / Divisor;
				}

				public override string ToString()
				{
					return string.Format("{0:F4}", ToNanoSeconds());
				}
			}

			public struct Timing
			{
				public int Medium;

				public sbyte Fine;

				public static int operator /(Timing t1, Timing t2)
				{
					return (int)Math.Ceiling(((float)t1.Medium * MTB.Dividend / MTB.Divisor + (float)t1.Fine * FTB.Dividend / FTB.Divisor) / 1000f / (((float)t2.Medium * MTB.Dividend / MTB.Divisor + (float)t2.Fine * FTB.Dividend / FTB.Divisor) / 1000f));
				}

				public static int operator /(int d1, Timing t1)
				{
					return (int)((float)d1 / (((float)t1.Medium * MTB.Dividend / MTB.Divisor + (float)t1.Fine * FTB.Dividend / FTB.Divisor) / 1000f));
				}

				public int ToClockCycles(Timing refTiming)
				{
					return (int)Math.Ceiling(ToNanoSeconds() / refTiming.ToNanoSeconds());
				}

				public float ToNanoSeconds()
				{
					return (float)Medium * (MTB.Dividend / MTB.Divisor) + (float)Fine * (FTB.Dividend / FTB.Divisor) / 1000f;
				}

				public override string ToString()
				{
					return string.Format("{0:F3}", ToNanoSeconds());
				}
			}

			public struct CasLatenciesData
			{
				public ushort Bitmask;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 0; b < 16; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue(b + 4);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					for (int i = 0; i < array.Length; i++)
					{
						byte b = (byte)array[i];
						text += string.Format("{0},", b);
					}
					return text.TrimEnd(',');
				}
			}

			public enum XmpProfileName
			{
				Enthusiast,
				Extreme
			}

			public struct Xmp10ProfileData
			{
				public struct VoltageLevelData
				{
					public byte Integer;

					public byte Fraction;

					public override string ToString()
					{
						return string.Format("{0:F2}", (float)(int)Integer + (float)(int)Fraction * 0.05f);
					}
				}

				public struct CmdTurnAroundTimeOptimization
				{
					public TurnAroundAdjustment Adjustment;

					public byte Clocks;
				}

				public enum TurnAroundAdjustment
				{
					PullIn,
					PushOut
				}

				public struct CmdTurnAroundTimeOptimizationData
				{
					public CmdTurnAroundTimeOptimization ReadToWrite;

					public CmdTurnAroundTimeOptimization WriteToRead;

					public CmdTurnAroundTimeOptimization BackToBack;
				}

				private byte mB;

				private byte Yc
				{
					get
					{
						return (byte)(Number * 35);
					}
				}

				public byte Number
				{
					get
					{
						return mB;
					}
					set
					{
						if (value > 1)
						{
							throw new ArgumentOutOfRangeException();
						}
						mB = value;
					}
				}

				public bool Enabled
				{
					get
					{
						return Data.GetBit(RawData[178], Number);
					}
				}

				public XmpProfileName Name
				{
					get
					{
						return (XmpProfileName)Number;
					}
				}

				public byte ChannelConfig
				{
					get
					{
						return Data.SubByte(RawData[178], (byte)(Number * 2 + 1), 2);
					}
				}

				public byte Version
				{
					get
					{
						return RawData[179];
					}
				}

				public TimebaseData MediumTimebase
				{
					get
					{
						return new TimebaseData
						{
							Dividend = (int)RawData[180 + Number],
							Divisor = (int)RawData[181 + Number]
						};
					}
				}

				public VoltageLevelData VDDVoltage
				{
					get
					{
						return new VoltageLevelData
						{
							Integer = Data.SubByte(RawData[185 + Yc], 6, 2),
							Fraction = Data.SubByte(RawData[185 + Yc], 4, 5)
						};
					}
				}

				public Timing tCKmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[186 + Yc],
							Fine = (sbyte)RawData[211 + Yc]
						};
					}
				}

				public Timing tAAmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[187 + Yc],
							Fine = (sbyte)RawData[212 + Yc]
						};
					}
				}

				public CasLatenciesData CasLatencies
				{
					get
					{
						return new CasLatenciesData
						{
							Bitmask = (ushort)(RawData[188 + Yc] | (RawData[189 + Yc] << 8))
						};
					}
				}

				public Timing tRPmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[191 + Yc],
							Fine = (sbyte)RawData[213 + Yc]
						};
					}
				}

				public Timing tRCDmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[192 + Yc],
							Fine = (sbyte)RawData[214 + Yc]
						};
					}
				}

				public Timing tWRmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[193 + Yc]
						};
					}
				}

				public Timing tRASmin
				{
					get
					{
						return new Timing
						{
							Medium = (short)(RawData[195 + Yc] | (Data.SubByte(RawData[194 + Yc], 3, 4) << 8))
						};
					}
				}

				public Timing tRCmin
				{
					get
					{
						return new Timing
						{
							Medium = (short)(RawData[196 + Yc] | (Data.SubByte(RawData[194 + Yc], 7, 4) << 8)),
							Fine = (sbyte)RawData[215 + Yc]
						};
					}
				}

				public Timing tREFI
				{
					get
					{
						return new Timing
						{
							Medium = (short)(RawData[197 + Yc] | (RawData[198 + Yc] << 8))
						};
					}
				}

				public Timing tRFCmin
				{
					get
					{
						return new Timing
						{
							Medium = (short)(RawData[199 + Yc] | (RawData[200 + Yc] << 8))
						};
					}
				}

				public Timing tRTPmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[201 + Yc]
						};
					}
				}

				public Timing tRRDmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[202 + Yc]
						};
					}
				}

				public Timing tFAWmin
				{
					get
					{
						return new Timing
						{
							Medium = (short)(RawData[204 + Yc] | (RawData[203 + Yc] << 8))
						};
					}
				}

				public Timing tWTRmin
				{
					get
					{
						return new Timing
						{
							Medium = RawData[205 + Yc]
						};
					}
				}

				public CmdTurnAroundTimeOptimizationData CmdTurnAroundTimeOptimizations
				{
					get
					{
						return new CmdTurnAroundTimeOptimizationData
						{
							ReadToWrite = new CmdTurnAroundTimeOptimization
							{
								Adjustment = (TurnAroundAdjustment)Data.BoolToNum<byte>(Data.GetBit(RawData[206 + Yc], 7)),
								Clocks = Data.SubByte(RawData[206 + Yc], 6, 3)
							},
							WriteToRead = new CmdTurnAroundTimeOptimization
							{
								Adjustment = (TurnAroundAdjustment)Data.BoolToNum<byte>(Data.GetBit(RawData[206 + Yc], 3)),
								Clocks = Data.SubByte(RawData[206 + Yc], 2, 3)
							},
							BackToBack = new CmdTurnAroundTimeOptimization
							{
								Adjustment = (TurnAroundAdjustment)Data.BoolToNum<byte>(Data.GetBit(RawData[207 + Yc], 3)),
								Clocks = Data.SubByte(RawData[207 + Yc], 2, 3)
							}
						};
					}
				}

				public VoltageLevelData MemoryControllerVoltage
				{
					get
					{
						return new VoltageLevelData
						{
							Integer = Data.SubByte(RawData[210 + Yc], 7, 3),
							Fraction = Data.SubByte(RawData[210 + Yc], 4, 5)
						};
					}
				}

				public Timing SystemCmdRateMode
				{
					get
					{
						return new Timing
						{
							Medium = RawData[208 + Yc]
						};
					}
				}

				public override string ToString()
				{
					if (Enabled)
					{
						return string.Format("{0} MHz ", 1000f / tCKmin.ToNanoSeconds()) + string.Format("{0}-", tAAmin.ToClockCycles(tCKmin)) + string.Format("{0}-", tRCDmin.ToClockCycles(tCKmin)) + string.Format("{0}-", tRPmin.ToClockCycles(tCKmin)) + string.Format("{0} ", tRASmin.ToClockCycles(tCKmin)) + string.Format("{0}V", VDDVoltage);
					}
					return "";
				}
			}

			public int Length
			{
				get
				{
					return 256;
				}
			}

			public BytesData Bytes
			{
				get
				{
					ushort used;
					switch (Data.SubByte(RawData[0], 3, 4))
					{
					case 1:
						used = 128;
						break;
					case 2:
						used = 176;
						break;
					case 3:
						used = 256;
						break;
					default:
						used = 0;
						break;
					}
					return new BytesData
					{
						Used = used,
						Total = (ushort)((Data.SubByte(RawData[0], 6, 3) == 1) ? 256u : 0u)
					};
				}
			}

			public int SpdBytesUsed
			{
				get
				{
					return Bytes.Used;
				}
			}

			public bool CrcCoverage
			{
				get
				{
					return Data.GetBit(RawData[0], 7);
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[1], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[1], 3, 4)
					};
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public ModuleTypeData ModuleType
			{
				get
				{
					return new ModuleTypeData
					{
						HybridMedia = Data.GetBit(RawData[3], 4),
						BaseModuleType = (BaseModuleType)Data.SubByte(RawData[3], 3, 4)
					};
				}
			}

			public DensityBanksData DensityBanks
			{
				get
				{
					return new DensityBanksData
					{
						BankAddress = (byte)Math.Pow(2.0, Data.SubByte(RawData[4], 6, 3) + 3),
						TotalCapacityPerDie = (ushort)(1 << Data.SubByte(RawData[4], 3, 4) + 8)
					};
				}
			}

			public AddressingData Addressing
			{
				get
				{
					return new AddressingData
					{
						Rows = (byte)(Data.SubByte(RawData[5], 5, 3) + 12),
						Columns = (byte)(Data.SubByte(RawData[5], 2, 3) + 9)
					};
				}
			}

			public ModuleNominalVoltageData[] ModuleNominalVoltage
			{
				get
				{
					float[] array = new float[3] { 1.5f, 1.35f, 1.25f };
					ModuleNominalVoltageData[] array2 = new ModuleNominalVoltageData[array.Length];
					for (int i = 0; i < array2.Length; i++)
					{
						array2[i].Voltage = array[i];
						array2[i].Operable = ((i == 0) ? (!Data.GetBit(RawData[6], (byte)i)) : Data.GetBit(RawData[6], (byte)i));
					}
					return array2;
				}
			}

			public ModuleOrganizationData ModuleOrganization
			{
				get
				{
					byte b = Data.SubByte(RawData[7], 5, 3);
					return new ModuleOrganizationData
					{
						PackageRankCount = (byte)((b == 4) ? 8u : ((uint)(b + 1))),
						DeviceWidth = (byte)(4 << (int)Data.SubByte(RawData[7], 2, 3))
					};
				}
			}

			public BusWidthData BusWidth
			{
				get
				{
					return new BusWidthData
					{
						Extension = Data.GetBit(RawData[8], 7),
						PrimaryBusWidth = (byte)(1 << Data.SubByte(RawData[8], 2, 3) + 3)
					};
				}
			}

			public ulong DieDensity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DensityBanks.BankAddress * ModuleOrganization.DeviceWidth);
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DensityBanks.BankAddress * ModuleOrganization.PackageRankCount * (BusWidth.PrimaryBusWidth & 0xF0) / 8);
				}
			}

			public ulong TotalModuleCapacityProgrammed
			{
				get
				{
					return (ulong)((long)(DensityBanks.TotalCapacityPerDie / 8 * BusWidth.PrimaryBusWidth / ModuleOrganization.DeviceWidth * ModuleOrganization.PackageRankCount) * 1024L * 1024);
				}
			}

			public static TimebaseData FTB
			{
				get
				{
					return new TimebaseData
					{
						Dividend = (int)Data.SubByte(RawData[9], 7, 4),
						Divisor = (int)Data.SubByte(RawData[9], 3, 4)
					};
				}
			}

			public static TimebaseData MTB
			{
				get
				{
					return new TimebaseData
					{
						Dividend = (int)RawData[10],
						Divisor = (int)RawData[11]
					};
				}
			}

			public Timing tCKmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[12],
						Fine = (sbyte)RawData[34]
					};
				}
			}

			public CasLatenciesData tCL
			{
				get
				{
					return new CasLatenciesData
					{
						Bitmask = (ushort)(RawData[14] | (RawData[15] << 8))
					};
				}
			}

			[Category("Timings")]
			[DisplayName("Minimum CAS Latency Time (tAAmin)")]
			[Description("This byte defines the minimum CAS Latency in medium timebase (MTB) units.")]
			public Timing tAAmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[16],
						Fine = (sbyte)RawData[35]
					};
				}
			}

			public Timing tWRmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[17]
					};
				}
			}

			public Timing tRCDmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[18],
						Fine = (sbyte)RawData[36]
					};
				}
			}

			public Timing tRRDmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[19]
					};
				}
			}

			public Timing tRPmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[20],
						Fine = (sbyte)RawData[37]
					};
				}
			}

			public Timing tRASmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[22] | (Data.SubByte(RawData[21], 3, 4) << 8))
					};
				}
			}

			public Timing tRCmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[23] | (Data.SubByte(RawData[21], 7, 4) << 8))
					};
				}
			}

			public Timing tRFCmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[24] | (RawData[25] << 8))
					};
				}
			}

			public Timing tRTPmin
			{
				get
				{
					return new Timing
					{
						Medium = RawData[27]
					};
				}
			}

			public Timing tFAWmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[29] | (Data.SubByte(RawData[28], 3, 4) << 8))
					};
				}
			}

			public PrimaryPackageTypeData SDRAMDeviceType
			{
				get
				{
					return new PrimaryPackageTypeData
					{
						Monolithic = !Data.GetBit(RawData[33], 7),
						DieCount = (byte)(Data.SubByte(RawData[33], 6, 3) << Data.SubByte(RawData[33], 6, 3) - 1),
						SignalLoading = (SignalLoadingData)Data.SubByte(RawData[33], 1, 2)
					};
				}
			}

			public MaximumActivateFeaturesData SDRAMMaximumActiveCount
			{
				get
				{
					return new MaximumActivateFeaturesData
					{
						MaximumActivateWindow = (ushort)(8192 >> (int)Data.SubByte(RawData[41], 5, 2)),
						MaximumActivateCount = (MaximumActivateCount)Data.SubByte(RawData[41], 3, 4)
					};
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					return new ManufacturerIdCodeData
					{
						ContinuationCode = RawData[117],
						ManufacturerCode = RawData[118]
					};
				}
			}

			public byte ModuleManufacturingLocation
			{
				get
				{
					return RawData[119];
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[120],
						Week = RawData[121]
					};
				}
			}

			public SerialNumberData ModuleSerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 122, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public Crc16Data Crc
			{
				get
				{
					Crc16Data result = new Crc16Data
					{
						Contents = new byte[(CrcCoverage ? 117 : 126) + 2]
					};
					Array.Copy(RawData, result.Contents, result.Contents.Length);
					Array.Copy(RawData, 126, result.Contents, result.Contents.Length - 2, 2);
					return result;
				}
			}

			public bool CrcStatus
			{
				get
				{
					return Crc.Validate();
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 128;
					char[] array = new char[145 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public ManufacturerIdCodeData DramManufacturerIdCode
			{
				get
				{
					return new ManufacturerIdCodeData
					{
						ContinuationCode = RawData[148],
						ManufacturerCode = RawData[149]
					};
				}
			}

			public ReferenceRawCardData RawCardExtension
			{
				get
				{
					int num = Data.SubByte(RawData[60], 7, 3);
					byte b = Data.SubByte(RawData[62], 4, 5);
					return new ReferenceRawCardData
					{
						Extension = Data.GetBit(RawData[62], 7),
						Revision = (byte)((num == 0) ? Data.SubByte(RawData[62], 6, 2) : num),
						Name = ((b == 31) ? ReferenceRawCardName.ZZ : ((ReferenceRawCardName)(b + (Data.BoolToNum<byte>(Data.GetBit(RawData[62], 7)) << 5))))
					};
				}
			}

			public ModuleHeightData ModuleHeight
			{
				get
				{
					return new ModuleHeightData
					{
						Minimum = (int)(byte)(Data.SubByte(RawData[60], 4, 5) + 14),
						Maximum = (int)(byte)(Data.SubByte(RawData[60], 4, 5) + 15),
						Unit = HeightUnit.mm
					};
				}
			}

			public ModuleMaximumThicknessSideData ModuleMaximumThickness
			{
				get
				{
					return new ModuleMaximumThicknessSideData
					{
						Back = new ModuleHeightData
						{
							Minimum = (int)Data.SubByte(RawData[61], 7, 4),
							Maximum = (int)(byte)(Data.SubByte(RawData[61], 7, 4) + 1),
							Unit = HeightUnit.mm
						},
						Front = new ModuleHeightData
						{
							Minimum = (int)Data.SubByte(RawData[61], 3, 4),
							Maximum = (int)(byte)(Data.SubByte(RawData[61], 3, 4) + 1),
							Unit = HeightUnit.mm
						}
					};
				}
			}

			public bool XmpPresence
			{
				get
				{
					return Data.MatchArray(RawData, ProfileId.XMP, 176);
				}
			}

			public Xmp10ProfileData[] XmpProfile
			{
				get
				{
					Xmp10ProfileData[] array = new Xmp10ProfileData[2];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Number = b;
					}
					return array;
				}
			}

			public DDR3(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public bool FixCrc()
			{
				ushort num = Data.Crc16(Data.TrimArray(RawData, CrcCoverage ? 117 : 126, Data.TrimPosition.End), 4129);
				RawData[126] = (byte)num;
				RawData[127] = (byte)(num >> 8);
				return CrcStatus;
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DDR4 : ISpd
		{
			public struct ModuleTypeData
			{
				public bool Hybrid;

				public bool HybridMedia;

				public BaseModuleType BaseModuleType;

				public override string ToString()
				{
					return Data.GetEnumDescription(BaseModuleType) ?? "";
				}
			}

			public enum BaseModuleType
			{
				[Description("Extended DIMM")]
				Extended_DIMM = 0,
				[Description("RDIMM")]
				RDIMM = 1,
				[Description("UDIMM")]
				UDIMM = 2,
				[Description("SO-DIMM")]
				SO_DIMM = 3,
				[Description("LRDIMM")]
				LRDIMM = 4,
				[Description("Mini-RDIMM")]
				Mini_RDIMM = 5,
				[Description("Mini-UDIMM")]
				Mini_UDIMM = 6,
				[Description("72b-SO-RDIMM")]
				_72b_SO_RDIMM = 8,
				[Description("72b-SO-UDIMM")]
				_72b_SO_UDIMM = 9,
				[Description("16b-SO-DIMM")]
				_16b_SO_DIMM = 12,
				[Description("32b-SO-DIMM")]
				_32b_SO_DIMM = 13
			}

			public struct DensityBanksData
			{
				public byte BankGroup;

				public byte BankAddress;

				public ushort TotalCapacityPerDie;
			}

			public struct SecondaryPackageTypeData
			{
				public bool Monolithic;

				public byte DieCount;

				public byte DensityRatio;

				public SignalLoadingData SignalLoading;

				public override string ToString()
				{
					if (!Monolithic)
					{
						return Data.GetEnumDescription(SignalLoading);
					}
					return "Monolithic";
				}
			}

			public struct ModuleNominalVoltageData
			{
				public bool Endurant;

				public bool Operable;
			}

			public struct ModuleOrganizationData
			{
				public RankMix RankMix;

				public byte PackageRankCount;

				public byte DeviceWidth;
			}

			public enum RankMix
			{
				Symmetrical,
				Asymmetrical
			}

			public struct Timing
			{
				public int Medium;

				public sbyte Fine;

				public static int operator /(Timing t1, Timing t2)
				{
					return (int)Math.Ceiling((float)(t1.Medium * Timebase.Medium + t1.Fine * Timebase.Fine) / 1000f / ((float)(t2.Medium * Timebase.Medium + t2.Fine * Timebase.Fine) / 1000f));
				}

				public static int operator /(int d1, Timing t1)
				{
					return (int)((float)d1 / ((float)(t1.Medium * Timebase.Medium + t1.Fine * Timebase.Fine) / 1000f));
				}

				public int ToClockCycles(Timing t1)
				{
					return (int)Math.Ceiling(ToNanoSeconds() / ((float)(t1.Medium * Timebase.Medium + t1.Fine * Timebase.Fine) / 1000f));
				}

				public float ToNanoSeconds()
				{
					return (float)(Medium * Timebase.Medium + Fine * Timebase.Fine) / 1000f;
				}

				public override string ToString()
				{
					return string.Format("{0:F3}", ToNanoSeconds());
				}
			}

			public struct CasLatenciesData
			{
				public uint Bitmask;

				public bool HighRange;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 0; b < 29; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue((byte)(b + 7 + (HighRange ? 16 : 0)));
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					for (int i = 0; i < array.Length; i++)
					{
						byte b = (byte)array[i];
						text += string.Format("{0},", b);
					}
					return text.TrimEnd(',');
				}
			}

			public enum AddressMappingType
			{
				Standard,
				Mirrored,
				None
			}

			public enum XmpProfileName
			{
				Enthusiast,
				Extreme
			}

			public struct Xmp20ProfileData
			{
				private byte lc;

				private ushort Pb
				{
					get
					{
						return (ushort)(Number * 63);
					}
				}

				public byte Number
				{
					get
					{
						return lc;
					}
					set
					{
						if (value > 1)
						{
							throw new ArgumentOutOfRangeException();
						}
						lc = value;
					}
				}

				public bool Enabled
				{
					get
					{
						return Data.GetBit(RawData[386], Number);
					}
				}

				public XmpProfileName Name
				{
					get
					{
						return (XmpProfileName)Number;
					}
				}

				public byte Version
				{
					get
					{
						return RawData[387];
					}
				}

				public Timing tCKAVGmin
				{
					get
					{
						return TimingAdjustable((short)(396 + Pb), (ushort)(431 + Pb));
					}
				}

				public Timing tAAmin
				{
					get
					{
						return TimingAdjustable((short)(401 + Pb), (ushort)(430 + Pb));
					}
				}

				public CasLatenciesData CasLatencies
				{
					get
					{
						return new CasLatenciesData
						{
							Bitmask = (ushort)(RawData[399 + Pb] | (RawData[398 + Pb] << 8) | (RawData[397 + Pb] << 16))
						};
					}
				}

				public Timing tRCDmin
				{
					get
					{
						return TimingAdjustable((short)(402 + Pb), (ushort)(429 + Pb));
					}
				}

				public Timing tRPmin
				{
					get
					{
						return TimingAdjustable((short)(403 + Pb), (ushort)(428 + Pb));
					}
				}

				public Timing tRASmin
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[405 + Pb] | (Data.SubByte(RawData[404 + Pb], 7, 4) << 8)));
					}
				}

				public Timing tRCmin
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[406 + Pb] | (Data.SubByte(RawData[404 + Pb], 3, 4) << 8)), (sbyte)RawData[427 + Pb]);
					}
				}

				public Timing tFAWmin
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[414 + Pb] | (Data.SubByte(RawData[413 + Pb], 3, 4) << 8)));
					}
				}

				public Timing tRRD_Smin
				{
					get
					{
						return TimingAdjustable((short)(415 + Pb), (ushort)(426 + Pb));
					}
				}

				public Timing tRRD_Lmin
				{
					get
					{
						return TimingAdjustable((short)(416 + Pb), (ushort)(425 + Pb));
					}
				}

				public Timing tRFC1min
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[407 + Pb] | (RawData[408 + Pb] << 8)));
					}
				}

				public Timing tRFC2min
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[409 + Pb] | (RawData[410 + Pb] << 8)));
					}
				}

				public Timing tRFC4min
				{
					get
					{
						return TimingLongAdjustable((short)(RawData[411 + Pb] | (RawData[412 + Pb] << 8)));
					}
				}

				public XmpVoltageData Voltage
				{
					get
					{
						return new XmpVoltageData
						{
							Value = RawData[393 + Pb]
						};
					}
				}

				public byte ChannelConfig
				{
					get
					{
						return (byte)(Data.SubByte(RawData[386 + Pb], 3, 2) + 1);
					}
				}

				public override string ToString()
				{
					if (Enabled)
					{
						return string.Format("{0} MHz ", 1000f / tCKAVGmin.ToNanoSeconds()) + string.Format("{0}-", tAAmin.ToClockCycles(tCKAVGmin)) + string.Format("{0}-", tRCDmin.ToClockCycles(tCKAVGmin)) + string.Format("{0}-", tRPmin.ToClockCycles(tCKAVGmin)) + string.Format("{0} ", tRASmin.ToClockCycles(tCKAVGmin)) + string.Format("{0}V", Voltage);
					}
					return "";
				}
			}

			public struct XmpVoltageData
			{
				public byte Value;

				public override string ToString()
				{
					return ((float)(int)Data.BoolToNum<byte>(Data.GetBit(Value, 7)) + (float)(int)Data.SubByte(Value, 6, 7) / 100f).ToString(CultureInfo.InvariantCulture);
				}
			}

			public int Length
			{
				get
				{
					return 512;
				}
			}

			public BytesData Bytes
			{
				get
				{
					return new BytesData
					{
						Used = (ushort)(Data.SubByte(RawData[0], 3, 4) * 128),
						Total = (ushort)(Data.SubByte(RawData[0], 6, 3) * 256)
					};
				}
			}

			public int SpdBytesUsed
			{
				get
				{
					return Bytes.Used;
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[1], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[1], 3, 4)
					};
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public ModuleTypeData ModuleType
			{
				get
				{
					return new ModuleTypeData
					{
						Hybrid = Data.GetBit(RawData[3], 7),
						HybridMedia = Data.GetBit(RawData[3], 4),
						BaseModuleType = (BaseModuleType)Data.SubByte(RawData[3], 3, 4)
					};
				}
			}

			public DensityBanksData DensityBanks
			{
				get
				{
					byte b = Data.SubByte(RawData[4], 3, 4);
					return new DensityBanksData
					{
						BankGroup = (byte)(Data.SubByte(RawData[4], 7, 2) * 2),
						BankAddress = (byte)(1 << Data.SubByte(RawData[4], 5, 2) + 2),
						TotalCapacityPerDie = ((!Data.GetBit(RawData[4], 3)) ? ((ushort)(2 << b + 7)) : ((ushort)(3 << b + 4)))
					};
				}
			}

			public AddressingData Addressing
			{
				get
				{
					return new AddressingData
					{
						Rows = (byte)(Data.SubByte(RawData[5], 5, 3) + 12),
						Columns = (byte)(Data.SubByte(RawData[5], 2, 3) + 9)
					};
				}
			}

			public PrimaryPackageTypeData PrimaryPackageType
			{
				get
				{
					return new PrimaryPackageTypeData
					{
						Monolithic = !Data.GetBit(RawData[6], 7),
						DieCount = (byte)(Data.SubByte(RawData[6], 6, 3) + 1),
						SignalLoading = (SignalLoadingData)Data.SubByte(RawData[6], 1, 2)
					};
				}
			}

			public MaximumActivateFeaturesData MaximumActivateFeatures
			{
				get
				{
					return new MaximumActivateFeaturesData
					{
						MaximumActivateWindow = (ushort)(8192 >> (int)Data.SubByte(RawData[7], 5, 2)),
						MaximumActivateCount = (MaximumActivateCount)Data.SubByte(RawData[7], 3, 4)
					};
				}
			}

			public SecondaryPackageTypeData SecondaryPackageType
			{
				get
				{
					return new SecondaryPackageTypeData
					{
						Monolithic = !Data.GetBit(RawData[10], 7),
						DieCount = (byte)(Data.SubByte(RawData[10], 6, 3) + 1),
						DensityRatio = Data.SubByte(RawData[10], 3, 2),
						SignalLoading = (SignalLoadingData)Data.SubByte(RawData[10], 1, 2)
					};
				}
			}

			public ModuleNominalVoltageData ModuleNominalVoltage
			{
				get
				{
					return new ModuleNominalVoltageData
					{
						Endurant = Data.GetBit(RawData[11], 1),
						Operable = Data.GetBit(RawData[11], 0)
					};
				}
			}

			public ModuleOrganizationData ModuleOrganization
			{
				get
				{
					return new ModuleOrganizationData
					{
						RankMix = (RankMix)Data.BoolToNum<byte>(Data.GetBit(RawData[12], 6)),
						PackageRankCount = (byte)(Data.SubByte(RawData[12], 5, 3) + 1),
						DeviceWidth = (byte)(4 << (int)Data.SubByte(RawData[12], 2, 3))
					};
				}
			}

			public BusWidthData BusWidth
			{
				get
				{
					return new BusWidthData
					{
						Extension = Data.GetBit(RawData[13], 3),
						PrimaryBusWidth = (byte)(1 << Data.SubByte(RawData[13], 2, 3) + 3)
					};
				}
			}

			public ulong DieDensity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DensityBanks.BankAddress * DensityBanks.BankGroup * ModuleOrganization.DeviceWidth);
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DensityBanks.BankAddress * DensityBanks.BankGroup * ((PrimaryPackageType.SignalLoading != SignalLoadingData.Single_Load_Stack) ? 1 : PrimaryPackageType.DieCount) * BusWidth.PrimaryBusWidth * ModuleOrganization.PackageRankCount / 8);
				}
			}

			public ulong TotalModuleCapacityProgrammed
			{
				get
				{
					return (ulong)((long)(DensityBanks.TotalCapacityPerDie / 8 * BusWidth.PrimaryBusWidth / ModuleOrganization.DeviceWidth * ModuleOrganization.PackageRankCount * ((PrimaryPackageType.SignalLoading != SignalLoadingData.Single_Load_Stack) ? 1 : PrimaryPackageType.DieCount)) * 1024L * 1024);
				}
			}

			public bool ThermalSensor
			{
				get
				{
					return Data.GetBit(RawData[14], 7);
				}
				set
				{
					Data.SetBit(RawData[14], 7, value);
				}
			}

			public static Timing Timebase
			{
				get
				{
					return new Timing
					{
						Medium = ((Data.SubByte(RawData[15], 3, 2) == 0) ? 125 : 0),
						Fine = ((Data.SubByte(RawData[15], 1, 2) == 0) ? ((sbyte)1) : ((sbyte)0))
					};
				}
			}

			public Timing tCKAVGmin
			{
				get
				{
					return TimingAdjustable(18, 125);
				}
			}

			public Timing tCKAVGmax
			{
				get
				{
					return TimingAdjustable(19, 124);
				}
			}

			public CasLatenciesData tCL
			{
				get
				{
					return new CasLatenciesData
					{
						HighRange = Data.GetBit(RawData[23], 7),
						Bitmask = (uint)(RawData[20] | (RawData[21] << 8) | (RawData[22] << 16) | (RawData[23] << 24))
					};
				}
			}

			public Timing tAAmin
			{
				get
				{
					return TimingAdjustable(24, 123);
				}
			}

			public Timing tRCDmin
			{
				get
				{
					return TimingAdjustable(25, 122);
				}
			}

			public Timing tRPmin
			{
				get
				{
					return TimingAdjustable(26, 121);
				}
			}

			public Timing tRASmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[28] | (Data.SubByte(RawData[27], 3, 4) << 8))
					};
				}
			}

			public Timing tRCmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[29] | (Data.SubByte(RawData[27], 7, 4) << 8)),
						Fine = (sbyte)RawData[120]
					};
				}
			}

			public Timing tRFC1min
			{
				get
				{
					return TimingLongAdjustable((short)(RawData[30] | (RawData[31] << 8)));
				}
			}

			public Timing tRFC2min
			{
				get
				{
					return TimingLongAdjustable((short)(RawData[32] | (RawData[33] << 8)));
				}
			}

			public Timing tRFC4min
			{
				get
				{
					return TimingLongAdjustable((short)(RawData[34] | (RawData[35] << 8)));
				}
			}

			public Timing tFAWmin
			{
				get
				{
					return TimingLongAdjustable((short)(RawData[37] | (Data.SubByte(RawData[36], 3, 4) << 8)));
				}
			}

			public Timing tRRD_Smin
			{
				get
				{
					return TimingAdjustable(38, 119);
				}
			}

			public Timing tRRD_Lmin
			{
				get
				{
					return TimingAdjustable(39, 118);
				}
			}

			public Timing tCCD_Lmin
			{
				get
				{
					return TimingAdjustable(40, 117);
				}
			}

			public Timing tWRmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[42] | (Data.SubByte(RawData[41], 3, 4) << 8))
					};
				}
			}

			public Timing tWTR_Smin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[44] | (Data.SubByte(RawData[43], 3, 4) << 8))
					};
				}
			}

			public Timing tWTR_Lmin
			{
				get
				{
					return new Timing
					{
						Medium = (short)(RawData[45] | (Data.SubByte(RawData[43], 7, 4) << 8))
					};
				}
			}

			public Crc16Data[] Crc
			{
				get
				{
					byte b = 2;
					byte b2 = 128;
					Crc16Data[] array = new Crc16Data[b];
					for (byte b3 = 0; b3 < b; b3++)
					{
						array[b3].Contents = new byte[b2];
						Array.Copy(RawData, b2 * b3, array[b3].Contents, 0, b2);
					}
					return array;
				}
			}

			public bool CrcStatus
			{
				get
				{
					Crc16Data[] crc = Crc;
					foreach (Crc16Data crc16Data in crc)
					{
						if (!crc16Data.Validate())
						{
							return false;
						}
					}
					return true;
				}
			}

			public byte RawCardExtension
			{
				get
				{
					byte b = Data.SubByte(RawData[128], 7, 3);
					if (b > 0)
					{
						return (byte)(b + 3);
					}
					return ReferenceRawCard.Revision;
				}
			}

			public ModuleHeightData ModuleHeight
			{
				get
				{
					return new ModuleHeightData
					{
						Minimum = (int)(byte)(Data.SubByte(RawData[128], 4, 5) + 15),
						Maximum = (int)(byte)(Data.SubByte(RawData[128], 4, 5) + 16),
						Unit = HeightUnit.mm
					};
				}
			}

			public ModuleMaximumThicknessSideData ModuleMaximumThickness
			{
				get
				{
					return new ModuleMaximumThicknessSideData
					{
						Back = new ModuleHeightData
						{
							Minimum = (int)Data.SubByte(RawData[129], 7, 4),
							Maximum = (int)(byte)(Data.SubByte(RawData[129], 7, 4) + 1),
							Unit = HeightUnit.mm
						},
						Front = new ModuleHeightData
						{
							Minimum = (int)Data.SubByte(RawData[129], 3, 4),
							Maximum = (int)(byte)(Data.SubByte(RawData[129], 3, 4) + 1),
							Unit = HeightUnit.mm
						}
					};
				}
			}

			public ReferenceRawCardData ReferenceRawCard
			{
				get
				{
					bool bit = Data.GetBit(RawData[130], 7);
					ReferenceRawCardData result = new ReferenceRawCardData
					{
						Extension = bit,
						Revision = Data.SubByte(RawData[130], 6, 2)
					};
					byte b = Data.SubByte(RawData[130], 4, 5);
					result.Name = ((b == 31) ? ReferenceRawCardName.ZZ : ((ReferenceRawCardName)(b + (bit ? 32 : 0))));
					return result;
				}
			}

			public AddressMappingType AddressMapping
			{
				get
				{
					if (ModuleType.BaseModuleType != BaseModuleType.RDIMM && ModuleType.BaseModuleType != BaseModuleType.LRDIMM)
					{
						return AddressMappingType.None;
					}
					return (AddressMappingType)Data.BoolToNum<byte>(Data.GetBit(RawData[136], 0));
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					return new ManufacturerIdCodeData
					{
						ContinuationCode = RawData[320],
						ManufacturerCode = RawData[321]
					};
				}
			}

			public byte ManufacturingLocation
			{
				get
				{
					return RawData[322];
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[323],
						Week = RawData[324]
					};
				}
			}

			public SerialNumberData SerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 325, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 329;
					char[] array = new char[348 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public ManufacturerIdCodeData DramManufacturerIdCode
			{
				get
				{
					return new ManufacturerIdCodeData
					{
						ContinuationCode = RawData[350],
						ManufacturerCode = RawData[351]
					};
				}
			}

			public bool XmpPresence
			{
				get
				{
					return Data.MatchArray(RawData, ProfileId.XMP, 384);
				}
			}

			public Xmp20ProfileData[] XmpProfile
			{
				get
				{
					Xmp20ProfileData[] array = new Xmp20ProfileData[2];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Number = b;
					}
					return array;
				}
			}

			public DDR4(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public static Timing TimingAdjustable(short mediumOffset, ushort fineOffset)
			{
				return new Timing
				{
					Medium = RawData[mediumOffset],
					Fine = (sbyte)RawData[fineOffset]
				};
			}

			public static Timing TimingLongAdjustable(short mediumOffset)
			{
				return new Timing
				{
					Medium = mediumOffset
				};
			}

			public static Timing TimingLongAdjustable(short mediumOffset, sbyte fineOffset)
			{
				return new Timing
				{
					Medium = mediumOffset,
					Fine = fineOffset
				};
			}

			public bool FixCrc()
			{
				byte b = 2;
				for (byte b2 = 0; b2 < b; b2++)
				{
					Array.Copy(Crc[b2].Fix(), 0, RawData, Crc[b2].Contents.Length * b2, Crc[b2].Contents.Length);
				}
				return CrcStatus;
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DDR5 : ISpd
		{
			public struct ModuleTypeData
			{
				public bool Hybrid;

				public HybridMediaData HybridMedia;

				public BaseModuleType BaseModuleType;

				public override string ToString()
				{
					return Data.GetEnumDescription(BaseModuleType) ?? "";
				}
			}

			public enum HybridMediaData
			{
				[Description("Not hybrid")]
				NONE,
				[Description("NVDIMM-N Hybrid")]
				NVDIMMN,
				[Description("NVDIMM-P Hybrid")]
				NVDIMMP
			}

			public enum BaseModuleType
			{
				[Description("RDIMM")]
				RDIMM = 1,
				[Description("UDIMM")]
				UDIMM = 2,
				[Description("SO-DIMM")]
				SO_DIMM = 3,
				[Description("LRDIMM")]
				LRDIMM = 4,
				[Description("DDIMM")]
				DDIMM = 10,
				[Description("Solder down")]
				Solder_Down = 10
			}

			public struct DensityPackageData
			{
				public byte DiePerPackageCount;

				public byte DieDensity;
			}

			public struct BankGroupsData
			{
				public byte BankGroupCount;

				public byte BankPerBankGroupCount;
			}

			public struct ModuleOrganizationData
			{
				public RankMix RankMix;

				public int PackageRankCount;
			}

			public enum RankMix
			{
				Symmetrical,
				Asymmetrical
			}

			public struct ChannelBusWidthData
			{
				public byte ChannelCount;

				public byte BusWidthExtension;

				public byte PrimaryBusWidthPerChannel;
			}

			public enum XmpProfileType
			{
				Performance,
				Extreme,
				Fastest,
				User1,
				User2
			}

			public struct Xmp30ProfileData
			{
				public static ushort Length = 64;

				public static ushort Offset = 640;

				public XmpProfileType Type;

				public string Name;
			}

			[StructLayout(LayoutKind.Sequential, Size = 1)]
			public struct ExpoProfileData
			{
				public static ushort Length = 128;

				public static ushort Offset = 832;
			}

			private const int LB = 4;

			public int Length
			{
				get
				{
					return 1024;
				}
			}

			public BytesData Bytes
			{
				get
				{
					return new BytesData
					{
						Used = (ushort)(128 * (ushort)Math.Pow(2.0, (int)Data.SubByte(RawData[0], 6, 3)))
					};
				}
			}

			public int SpdBytesUsed
			{
				get
				{
					return Bytes.Used;
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[1], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[1], 3, 4)
					};
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public ModuleTypeData ModuleType
			{
				get
				{
					return new ModuleTypeData
					{
						Hybrid = Data.GetBit(RawData[3], 7),
						HybridMedia = (HybridMediaData)Data.SubByte(RawData[3], 6, 3),
						BaseModuleType = (BaseModuleType)Data.SubByte(RawData[3], 3, 4)
					};
				}
			}

			public DensityPackageData[] DensityPackage
			{
				get
				{
					DensityPackageData[] array = new DensityPackageData[2];
					byte[] array2 = new byte[9] { 0, 4, 8, 12, 16, 24, 32, 48, 64 };
					for (byte b = 0; b < array.Length; b++)
					{
						byte b2 = Data.SubByte(RawData[4 + 4 * b], 7, 4);
						array[b].DiePerPackageCount = (byte)((b2 == 0) ? 1 : b2);
						array[b].DieDensity = array2[Data.SubByte(RawData[4 + 4 * b], 3, 4)];
					}
					return array;
				}
			}

			public AddressingData[] Addressing
			{
				get
				{
					AddressingData[] array = new AddressingData[2];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Columns = (byte)(Data.SubByte(RawData[5 + 4 * b], 7, 3) + 10);
						array[b].Rows = (byte)(Data.SubByte(RawData[5 + 4 * b], 4, 5) + 16);
					}
					return array;
				}
			}

			public byte[] IoWidth
			{
				get
				{
					byte[] array = new byte[2];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = Data.SubByte(RawData[6 + 4 * i], 7, 3);
					}
					return array;
				}
			}

			public BankGroupsData[] BankGroups
			{
				get
				{
					BankGroupsData[] array = new BankGroupsData[2];
					for (int i = 0; i < array.Length; i++)
					{
						array[i].BankGroupCount = (byte)Math.Pow(2.0, (int)Data.SubByte(RawData[7 + 4 * i], 7, 3));
						array[i].BankPerBankGroupCount = (byte)Math.Pow(2.0, (int)Data.SubByte(RawData[7 + 4 * i], 2, 3));
					}
					return array;
				}
			}

			public ModuleOrganizationData ModuleOrganization
			{
				get
				{
					return new ModuleOrganizationData
					{
						RankMix = (RankMix)Data.BoolToNum<byte>(Data.GetBit(RawData[234], 6)),
						PackageRankCount = Data.SubByte(RawData[234], 5, 3) + 1
					};
				}
			}

			public ChannelBusWidthData ChannelBusWidth
			{
				get
				{
					return new ChannelBusWidthData
					{
						ChannelCount = (byte)Math.Pow(2.0, (int)Data.SubByte(RawData[235], 6, 2)),
						BusWidthExtension = (byte)(Data.SubByte(RawData[235], 4, 2) * 4),
						PrimaryBusWidthPerChannel = (byte)((1 << Data.SubByte(RawData[235], 2, 3) + 3) & 0xF8)
					};
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					if (ModuleOrganization.RankMix != RankMix.Symmetrical)
					{
						return 0uL;
					}
					return (ulong)(ChannelBusWidth.ChannelCount * ChannelBusWidth.PrimaryBusWidthPerChannel / IoWidth[0] * DensityPackage[0].DiePerPackageCount * DensityPackage[0].DieDensity / 8 * ModuleOrganization.PackageRankCount);
				}
			}

			public Crc16Data[] Crc
			{
				get
				{
					int num = 1;
					if (ExpoPresence)
					{
						num++;
					}
					if (XmpPresence)
					{
						num++;
						for (int i = 0; i <= 5; i++)
						{
							if (RawData[i * Xmp30ProfileData.Length + Xmp30ProfileData.Offset] == 48)
							{
								num++;
							}
						}
					}
					Crc16Data[] array = new Crc16Data[num];
					ushort num2 = 512;
					array[0].Contents = new byte[num2];
					Array.Copy(RawData, array[0].Contents, array[0].Contents.Length);
					if (num > 1)
					{
						byte b = 1;
						byte b2 = 1;
						while (b < num)
						{
							int num3 = Xmp30ProfileData.Offset + (b2 - 1) * Xmp30ProfileData.Length;
							b2++;
							if (XmpPresence && (RawData[num3] == 48 || num3 == Xmp30ProfileData.Offset))
							{
								array[b].Contents = new byte[Xmp30ProfileData.Length];
							}
							else
							{
								if (!ExpoPresence || num3 != ExpoProfileData.Offset)
								{
									continue;
								}
								array[b].Contents = new byte[ExpoProfileData.Length];
							}
							Array.Copy(RawData, num3, array[b].Contents, 0, array[b].Contents.Length);
							b++;
						}
					}
					return array;
				}
			}

			public bool CrcStatus
			{
				get
				{
					Crc16Data[] crc = Crc;
					foreach (Crc16Data crc16Data in crc)
					{
						if (!crc16Data.Validate())
						{
							return false;
						}
					}
					return true;
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					return new ManufacturerIdCodeData
					{
						ContinuationCode = RawData[512],
						ManufacturerCode = RawData[513]
					};
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[515],
						Week = RawData[516]
					};
				}
			}

			public SerialNumberData SerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 517, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 521;
					char[] array = new char[550 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public bool XmpPresence
			{
				get
				{
					return Data.MatchArray(RawData, ProfileId.XMP, Xmp30ProfileData.Offset);
				}
			}

			public bool ExpoPresence
			{
				get
				{
					return Data.MatchArray(RawData, ProfileId.EXPO, ExpoProfileData.Offset);
				}
			}

			public DDR5(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public bool FixCrc()
			{
				int num = Crc.Length;
				if (num > 0)
				{
					Array.Copy(Crc[0].Fix(), RawData, Crc[0].Contents.Length);
				}
				if (num > 1)
				{
					byte b = 1;
					byte b2 = 1;
					while (b < num)
					{
						int num2 = Xmp30ProfileData.Offset + (b2 - 1) * Xmp30ProfileData.Length;
						b2++;
						if (ExpoPresence && Crc[b].Contents.Length == ExpoProfileData.Length && Data.MatchArray(Crc[b].Contents, ProfileId.EXPO, 0))
						{
							num2 = ExpoProfileData.Offset;
						}
						else if (!XmpPresence || Crc[b].Contents.Length != Xmp30ProfileData.Length || (RawData[num2] != 48 && num2 != Xmp30ProfileData.Offset))
						{
							continue;
						}
						Array.Copy(Crc[b].Fix(), 0, RawData, num2, Crc[b].Contents.Length);
						b++;
					}
				}
				return CrcStatus;
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		public enum RamType : byte
		{
			UNKNOWN = 0,
			SDRAM = 4,
			DDR = 7,
			DDR2 = 8,
			[Description("DDR2 Fully-Buffered DIMM")]
			DDR2_FB_DIMM = 9,
			[Description("DDR2 Fully-Buffered DIMM Probe")]
			DDR2_FB_DIMMP = 10,
			DDR3 = 11,
			LPDDR3 = 15,
			DDR4 = 12,
			DDR4E = 14,
			LPDDR4 = 16,
			LPDDR4X = 17,
			DDR5 = 18,
			LPDDR5 = 19,
			[Description("DDR5 NVDIMM-P")]
			DDR5_NVDIMM_P = 20,
			LPDDR5X = 21
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct DataLength
		{
			public static ushort[] Length = new ushort[4] { 0, 256, 512, 1024 };

			public const ushort Unknown = 0;

			public const ushort Minimum = 256;

			public const ushort DDR4 = 512;

			public const ushort DDR5 = 1024;
		}

		public struct SpdRevisionData
		{
			public byte EncodingLevel;

			public byte AdditionsLevel;

			public override string ToString()
			{
				return string.Format("{0:D1}.{1:D1}", EncodingLevel, AdditionsLevel);
			}
		}

		public struct BytesData
		{
			public ushort Used;

			public ushort Total;

			public override string ToString()
			{
				return string.Format("{0}/{1}", Used, Total);
			}
		}

		public struct BusWidthData
		{
			public bool Extension;

			public byte PrimaryBusWidth;

			public override string ToString()
			{
				return ((byte)(PrimaryBusWidth + (Extension ? 8 : 0))).ToString();
			}
		}

		public struct AddressingData
		{
			public byte Rows;

			public byte Columns;
		}

		public struct PrimaryPackageTypeData
		{
			public bool Monolithic;

			public byte DieCount;

			public SignalLoadingData SignalLoading;

			public override string ToString()
			{
				if (!Monolithic)
				{
					return Data.GetEnumDescription(SignalLoading);
				}
				return "Monolithic";
			}
		}

		public enum SignalLoadingData
		{
			[Description("Not Specified")]
			Not_Specified,
			[Description("Multi Load Stack")]
			Multi_Load_Stack,
			[Description("Single Load Stack")]
			Single_Load_Stack
		}

		public enum CapacityPrefix : ulong
		{
			[Description("kilo")]
			K = 0x400uL,
			[Description("mega")]
			M = 0x100000uL,
			[Description("giga")]
			G = 0x40000000uL,
			[Description("tera")]
			T = 0x10000000000uL,
			[Description("peta")]
			P = 0x4000000000000uL,
			[Description("exa")]
			E = 0x1000000000000000uL
		}

		public struct MaximumActivateFeaturesData
		{
			public ushort MaximumActivateWindow;

			public MaximumActivateCount MaximumActivateCount;
		}

		public enum MaximumActivateCount
		{
			[Description("Untested MAC")]
			Untested,
			[Description("700 K")]
			_700K,
			[Description("600 K")]
			_600K,
			[Description("500 K")]
			_500K,
			[Description("400 K")]
			_400K,
			[Description("300 K")]
			_300K,
			[Description("200 K")]
			_200K,
			Reserved,
			[Description("Unlimited MAC")]
			Unlimited
		}

		public enum VoltageLevel
		{
			[Description("TTL/5 V tolerant")]
			TTL,
			[Description("LVTTL (not 5 V tolerant)")]
			LVTTL,
			[Description("HSTL 1.5 V")]
			HSTL,
			[Description("SSTL 3.3 V")]
			SSTL33,
			[Description("SSTL 2.5 V")]
			SSTL25,
			[Description("SSTL 1.8 V")]
			SSTL18
		}

		public struct ManufacturerIdCodeData
		{
			public byte ContinuationCode;

			public byte ManufacturerCode;

			public ushort ManufacturerId
			{
				get
				{
					return (ushort)((ContinuationCode << 8) | ManufacturerCode);
				}
			}

			public override string ToString()
			{
				return GetManufacturerName(ManufacturerId);
			}

			public static bool operator ==(ManufacturerIdCodeData d1, ManufacturerIdCodeData d2)
			{
				if (d1.ContinuationCode == d2.ContinuationCode)
				{
					return d1.ManufacturerCode == d2.ManufacturerCode;
				}
				return false;
			}

			public static bool operator !=(ManufacturerIdCodeData d1, ManufacturerIdCodeData d2)
			{
				if (d1.ContinuationCode == d2.ContinuationCode)
				{
					return d1.ManufacturerCode != d2.ManufacturerCode;
				}
				return true;
			}

			public override bool Equals(object o)
			{
				if (o != null && o.GetType() != typeof(ManufacturerIdCodeData))
				{
					return false;
				}
				if (o != null && ContinuationCode.Equals(((ManufacturerIdCodeData)o).ContinuationCode))
				{
					return ManufacturerCode.Equals(((ManufacturerIdCodeData)o).ManufacturerCode);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return ManufacturerId;
			}
		}

		public struct DateCodeData
		{
			public byte Year;

			public byte Week;

			public override string ToString()
			{
				ushort num = (ushort)(Data.ByteToBinaryCodedDecimal(Year) + 2000);
				byte b = Data.ByteToBinaryCodedDecimal(Week);
				if (0 >= b || b >= 53)
				{
					return "";
				}
				return string.Format("{0:D4}/{1:D2}", num, b);
			}
		}

		public struct SerialNumberData
		{
			public byte[] SerialNumber;

			public override string ToString()
			{
				StringBuilder stringBuilder = new StringBuilder();
				byte[] serialNumber = SerialNumber;
				foreach (byte b in serialNumber)
				{
					stringBuilder.Append(string.Format("{0:X2}", b));
				}
				return stringBuilder.ToString();
			}
		}

		public struct Crc16Data
		{
			public byte[] Contents;

			public ushort Checksum
			{
				get
				{
					return Data.Crc16(Data.TrimArray(Contents, Contents.Length - 2, Data.TrimPosition.End), 4129);
				}
			}

			public bool Validate()
			{
				return (ushort)((Contents[Contents.Length - 1] << 8) | Contents[Contents.Length - 2]) == Checksum;
			}

			public byte[] Fix()
			{
				if (!Validate())
				{
					Contents[Contents.Length - 1] = (byte)(Checksum >> 8);
					Contents[Contents.Length - 2] = (byte)Checksum;
				}
				return Contents;
			}

			public override string ToString()
			{
				return ((CrcStatus)Data.BoolToNum<byte>(Validate())/*cast due to .constrained prefix*/).ToString();
			}
		}

		public struct Crc8Data
		{
			public byte[] Contents;

			public byte Checksum
			{
				get
				{
					return Data.Crc(Data.TrimArray(Contents, Contents.Length - 1, Data.TrimPosition.End));
				}
			}

			public bool Validate()
			{
				return Contents[Contents.Length - 1] == Checksum;
			}

			public byte[] Fix()
			{
				if (!Validate())
				{
					Contents[Contents.Length - 1] = Checksum;
				}
				return Contents;
			}

			public override string ToString()
			{
				return ((CrcStatus)Data.BoolToNum<byte>(Validate())/*cast due to .constrained prefix*/).ToString();
			}
		}

		public enum CrcStatus
		{
			OK = 1,
			Bad = 0
		}

		public struct ReferenceRawCardData
		{
			public bool Extension;

			public byte Revision;

			public ReferenceRawCardName Name;
		}

		public enum ReferenceRawCardName
		{
			A,
			B,
			C,
			D,
			E,
			F,
			G,
			H,
			J,
			K,
			L,
			M,
			N,
			P,
			R,
			T,
			U,
			V,
			W,
			Y,
			AA,
			AB,
			AC,
			AD,
			AE,
			AF,
			AG,
			AH,
			AJ,
			AK,
			AL,
			AM,
			AN,
			AP,
			AR,
			AT,
			AU,
			AV,
			AW,
			AY,
			BA,
			BB,
			BC,
			BD,
			BE,
			BF,
			BG,
			BH,
			BJ,
			BK,
			BL,
			BM,
			BN,
			BP,
			BR,
			BT,
			BU,
			BV,
			BW,
			BY,
			CA,
			CB,
			ZZ
		}

		public struct ModuleHeightData
		{
			public float Minimum;

			public float Maximum;

			public HeightUnit Unit;

			public override string ToString()
			{
				string arg = "";
				if (Minimum.Equals(Maximum))
				{
					arg = string.Format("{0}", Maximum);
				}
				else if (Minimum < Maximum)
				{
					arg = string.Format("{0}-{1}", Minimum, Maximum);
				}
				else if (Minimum > Maximum)
				{
					arg = string.Format("{0}+", Minimum);
				}
				return string.Format("{0} {1}", arg, Unit);
			}
		}

		public enum HeightUnit
		{
			mm,
			IN
		}

		public struct ModuleMaximumThicknessSideData
		{
			public ModuleHeightData Back;

			public ModuleHeightData Front;
		}

		public struct RefreshRateData
		{
			public byte RefreshPeriod;

			public bool SelfRefresh;

			public float ToMicroseconds()
			{
				byte b = Data.SetBit(RefreshPeriod, 7, false);
				float num = 15.625f;
				if (b == 0)
				{
					return num;
				}
				if (1 <= b && b <= 2)
				{
					return num * 0.25f * (float)(int)b;
				}
				if (3 <= b && b <= 5)
				{
					return (float)((double)num * Math.Pow(2.0, b - 1));
				}
				throw new ArgumentOutOfRangeException("RefreshPeriod");
			}

			public override string ToString()
			{
				return ToMicroseconds().ToString("F3");
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct ProfileId
		{
			public static byte[] XMP = new byte[2] { 12, 74 };

			public static byte[] EPP
			{
				get
				{
					return Data.ReverseArray(Encoding.ASCII.GetBytes("NVm"));
				}
			}

			public static byte[] EXPO
			{
				get
				{
					return Encoding.ASCII.GetBytes("EXPO");
				}
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct SDRAM : ISpd
		{
			public struct Timing
			{
				public int Whole;

				public int Tenth;

				public int Hundredth;

				public int Quarter;

				public float ToNanoSeconds()
				{
					return (float)Whole + (float)Tenth * 0.1f + (float)Quarter * 0.25f + (float)Hundredth * 0.01f;
				}

				public int ToClockCycles(Timing refTiming)
				{
					return (int)Math.Ceiling(ToNanoSeconds() / refTiming.ToNanoSeconds());
				}

				public override string ToString()
				{
					return ToNanoSeconds().ToString("F2");
				}
			}

			public struct DIMMConfigurationData
			{
				public bool DataECC;

				public bool DataParity;
			}

			public struct SDRAMWidthData
			{
				public byte Width;

				public bool Bank2;
			}

			public struct BurstLengthData
			{
				public byte Length;

				public bool Supported;
			}

			public struct CasLatenciesData
			{
				public byte Bitmask;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 0; b <= 6; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue(b + 1);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					foreach (int num in array)
					{
						text += string.Format("{0},", num);
					}
					return text.TrimEnd(',');
				}
			}

			public struct LatenciesData
			{
				public byte Bitmask;

				public int[] ToArray()
				{
					Queue<int> queue = new Queue<int>();
					for (byte b = 0; b <= 6; b++)
					{
						if (Data.GetBit(Bitmask, b))
						{
							queue.Enqueue(b);
						}
					}
					return queue.ToArray();
				}

				public override string ToString()
				{
					string text = "";
					int[] array = ToArray();
					for (int i = 0; i < array.Length; i++)
					{
						byte b = (byte)array[i];
						text += string.Format("{0},", b);
					}
					return text.TrimEnd(',');
				}
			}

			public struct ModulesAttributesData
			{
				public bool RedundantRowAddress;

				public bool DifferentialClockInput;

				public bool RegisteredDQMBInputs;

				public bool BufferedDQMBInputs;

				public bool OnCardPLL;

				public bool RegisteredAddressControlInputs;

				public bool BufferedAddressControlInputs;
			}

			public struct DeviceAttributesData
			{
				public bool UpperVccTolerance;

				public bool LowerVccTolerance;

				public bool Write1ReadBurst;

				public bool PrechargeAll;

				public bool AutoPrecharge;

				public bool EarlyRasPrecharge;
			}

			public int Length
			{
				get
				{
					return 256;
				}
			}

			public BytesData Bytes
			{
				get
				{
					return new BytesData
					{
						Used = RawData[0],
						Total = (ushort)(1 << (int)RawData[1])
					};
				}
			}

			public int SpdBytesUsed
			{
				get
				{
					return Bytes.Used;
				}
			}

			public RamType DramDeviceType
			{
				get
				{
					return (RamType)RawData[2];
				}
			}

			public AddressingData Addressing
			{
				get
				{
					return new AddressingData
					{
						Rows = RawData[3],
						Columns = RawData[4]
					};
				}
			}

			public byte ModuleRanks
			{
				get
				{
					return RawData[5];
				}
			}

			public ushort DataWidth
			{
				get
				{
					return (ushort)(RawData[6] | (RawData[7] << 8));
				}
			}

			public ulong DieDensity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * PrimarySDRAMWidth.Width);
				}
			}

			public ulong TotalModuleCapacity
			{
				get
				{
					return (ulong)((1L << (int)Addressing.Rows) * (1L << (int)Addressing.Columns) * DeviceBanks * (DataWidth & 0xF0) * ModuleRanks / 8);
				}
			}

			public VoltageLevel VoltageInterfaceLevel
			{
				get
				{
					return (VoltageLevel)RawData[8];
				}
			}

			public Timing tCKmin
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[9], 7, 4),
						Tenth = Data.SubByte(RawData[9], 3, 4)
					};
				}
			}

			public Timing tAC
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[10], 7, 4),
						Tenth = Data.SubByte(RawData[10], 3, 4)
					};
				}
			}

			public DIMMConfigurationData DIMMConfiguration
			{
				get
				{
					return new DIMMConfigurationData
					{
						DataECC = Data.GetBit(RawData[11], 1),
						DataParity = Data.GetBit(RawData[11], 0)
					};
				}
			}

			public RefreshRateData RefreshRate
			{
				get
				{
					return new RefreshRateData
					{
						RefreshPeriod = Data.SubByte(RawData[12], 6, 7),
						SelfRefresh = Data.GetBit(RawData[12], 7)
					};
				}
			}

			public SDRAMWidthData PrimarySDRAMWidth
			{
				get
				{
					return new SDRAMWidthData
					{
						Width = Data.SubByte(RawData[13], 6, 7),
						Bank2 = Data.GetBit(RawData[13], 7)
					};
				}
			}

			public SDRAMWidthData ErrorCheckingSDRAMWidth
			{
				get
				{
					return new SDRAMWidthData
					{
						Width = Data.SubByte(RawData[14], 6, 7),
						Bank2 = Data.GetBit(RawData[14], 7)
					};
				}
			}

			public byte tCCD
			{
				get
				{
					return RawData[15];
				}
			}

			public BurstLengthData[] BurstLength
			{
				get
				{
					BurstLengthData[] array = new BurstLengthData[4];
					for (byte b = 0; b < array.Length; b++)
					{
						array[b].Length = (byte)(1 << (int)b);
						array[b].Supported = Data.GetBit(RawData[16], b);
					}
					return array;
				}
			}

			public byte DeviceBanks
			{
				get
				{
					return RawData[17];
				}
			}

			public CasLatenciesData tCL
			{
				get
				{
					return new CasLatenciesData
					{
						Bitmask = RawData[18]
					};
				}
			}

			public LatenciesData CS
			{
				get
				{
					return new LatenciesData
					{
						Bitmask = RawData[19]
					};
				}
			}

			public LatenciesData WE
			{
				get
				{
					return new LatenciesData
					{
						Bitmask = RawData[20]
					};
				}
			}

			public ModulesAttributesData ModulesAttributes
			{
				get
				{
					return new ModulesAttributesData
					{
						RedundantRowAddress = Data.GetBit(RawData[21], 6),
						DifferentialClockInput = Data.GetBit(RawData[21], 5),
						RegisteredDQMBInputs = Data.GetBit(RawData[21], 4),
						BufferedDQMBInputs = Data.GetBit(RawData[21], 3),
						OnCardPLL = Data.GetBit(RawData[21], 2),
						RegisteredAddressControlInputs = Data.GetBit(RawData[21], 1),
						BufferedAddressControlInputs = Data.GetBit(RawData[21], 0)
					};
				}
			}

			public DeviceAttributesData DeviceAttributes
			{
				get
				{
					return new DeviceAttributesData
					{
						UpperVccTolerance = Data.GetBit(RawData[22], 5),
						LowerVccTolerance = Data.GetBit(RawData[22], 4),
						Write1ReadBurst = Data.GetBit(RawData[22], 3),
						PrechargeAll = Data.GetBit(RawData[22], 2),
						AutoPrecharge = Data.GetBit(RawData[22], 1),
						EarlyRasPrecharge = Data.GetBit(RawData[22], 0)
					};
				}
			}

			public Timing tCKminX1
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[23], 7, 4),
						Tenth = Data.SubByte(RawData[23], 3, 4)
					};
				}
			}

			public Timing tACmaxX1
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[24], 7, 4),
						Tenth = Data.SubByte(RawData[24], 3, 4)
					};
				}
			}

			public Timing tCKminX2
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[25], 7, 6),
						Quarter = Data.SubByte(RawData[25], 1, 2)
					};
				}
			}

			public Timing tACmaxX2
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[26], 7, 6),
						Quarter = Data.SubByte(RawData[26], 1, 2)
					};
				}
			}

			public Timing tRP
			{
				get
				{
					return new Timing
					{
						Whole = RawData[27]
					};
				}
			}

			public Timing tRRD
			{
				get
				{
					return new Timing
					{
						Whole = RawData[28]
					};
				}
			}

			public Timing tRCD
			{
				get
				{
					return new Timing
					{
						Whole = (sbyte)RawData[29]
					};
				}
			}

			public Timing tRAS
			{
				get
				{
					return new Timing
					{
						Whole = (sbyte)RawData[30]
					};
				}
			}

			public uint RowDensity
			{
				get
				{
					for (byte b = 7; b != 0; b--)
					{
						if (Data.GetBit(RawData[31], b))
						{
							return (uint)(4 << (int)b);
						}
					}
					return (uint)(RawData[31] * 4);
				}
			}

			public Timing tIS
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[32], 6, 3) * ((!Data.GetBit(RawData[32], 7)) ? 1 : (-1)),
						Tenth = Data.SubByte(RawData[32], 3, 4)
					};
				}
			}

			public Timing tIH
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[33], 6, 3) * ((!Data.GetBit(RawData[33], 7)) ? 1 : (-1)),
						Tenth = Data.SubByte(RawData[33], 3, 4)
					};
				}
			}

			public Timing tDS
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[34], 6, 3) * ((!Data.GetBit(RawData[34], 7)) ? 1 : (-1)),
						Tenth = Data.SubByte(RawData[34], 3, 4)
					};
				}
			}

			public Timing tDH
			{
				get
				{
					return new Timing
					{
						Whole = Data.SubByte(RawData[35], 6, 3) * ((!Data.GetBit(RawData[35], 7)) ? 1 : (-1)),
						Tenth = Data.SubByte(RawData[35], 3, 4)
					};
				}
			}

			public SpdRevisionData SpdRevision
			{
				get
				{
					return new SpdRevisionData
					{
						EncodingLevel = Data.SubByte(RawData[62], 7, 4),
						AdditionsLevel = Data.SubByte(RawData[62], 3, 4)
					};
				}
			}

			public Crc8Data Crc
			{
				get
				{
					Crc8Data result = new Crc8Data
					{
						Contents = new byte[64]
					};
					Array.Copy(RawData, result.Contents, result.Contents.Length);
					return result;
				}
			}

			public bool CrcStatus
			{
				get
				{
					return Crc.Validate();
				}
			}

			public ManufacturerIdCodeData ManufacturerIdCode
			{
				get
				{
					byte b = 0;
					byte manufacturerCode = 0;
					byte b2 = 64;
					while (b2 <= 71)
					{
						if (RawData[b2] == 127)
						{
							b++;
							b2++;
							continue;
						}
						manufacturerCode = RawData[b2];
						break;
					}
					return new ManufacturerIdCodeData
					{
						ContinuationCode = b,
						ManufacturerCode = manufacturerCode
					};
				}
			}

			public byte ManufacturingLocation
			{
				get
				{
					return RawData[72];
				}
			}

			public string PartNumber
			{
				get
				{
					int num = 73;
					char[] array = new char[90 - num + 1];
					Array.Copy(RawData, num, array, 0, array.Length);
					return Data.BytesToString(array).Trim();
				}
			}

			public ushort RevisionCode
			{
				get
				{
					return (ushort)(RawData[92] | (RawData[91] << 8));
				}
			}

			public DateCodeData ModuleManufacturingDate
			{
				get
				{
					return new DateCodeData
					{
						Year = RawData[93],
						Week = RawData[94]
					};
				}
			}

			public SerialNumberData ModuleSerialNumber
			{
				get
				{
					byte[] array = new byte[4];
					Array.Copy(RawData, 95, array, 0, array.Length);
					return new SerialNumberData
					{
						SerialNumber = array
					};
				}
			}

			public SDRAM(byte[] P_0)
			{
				if (P_0.Length == Length)
				{
					RawData = P_0;
					return;
				}
				throw new DataException();
			}

			public override string ToString()
			{
				return (GetManufacturerName(ManufacturerIdCode.ManufacturerId) + " " + PartNumber).Trim();
			}

			public bool FixCrc()
			{
				Array.Copy(Crc.Fix(), RawData, Crc.Contents.Length);
				return CrcStatus;
			}

			bool ISpd.FixCrc()
			{
				//ILSpy generated this explicit interface implementation from .override directive in FixCrc
				return this.FixCrc();
			}
		}

		[CompilerGenerated]
		private static byte[] Eb;

		public static byte[] RawData
		{
			[CompilerGenerated]
			get
			{
				return Eb;
			}
			[CompilerGenerated]
			set
			{
				Eb = value;
			}
		}

		public static RamType GetRamType(Arduino device)
		{
			if (device == null)
			{
				throw new NullReferenceException("Invalid device");
			}
			if (!device.IsConnected)
			{
				throw new IOException("Device not connected (" + device.PortName + ")");
			}
			if (device.DetectDdr5())
			{
				return RamType.DDR5;
			}
			if (device.DetectDdr4())
			{
				return RamType.DDR4;
			}
			try
			{
				byte b = Eeprom.Read(device, 2);
				return (RamType)(Enum.IsDefined(typeof(RamType), b) ? b : 0);
			}
			catch
			{
				throw new Exception(string.Format("Unable to detect RAM type at {0} on {1}", device.I2CAddress, device.PortName));
			}
		}

		public static RamType GetRamType(byte[] input)
		{
			if (input.Length < 3 || !Enum.IsDefined(typeof(RamType), (RamType)input[2]))
			{
				return RamType.UNKNOWN;
			}
			return (RamType)input[2];
		}

		public static int GetSpdSize(RamType ramType)
		{
			switch (ramType)
			{
			case RamType.SDRAM:
			case RamType.DDR:
			case RamType.DDR2:
			case RamType.DDR2_FB_DIMM:
			case RamType.DDR3:
				return 256;
			case RamType.DDR4:
			case RamType.DDR4E:
			case RamType.LPDDR3:
			case RamType.LPDDR4:
				return 512;
			case RamType.DDR5:
				return 1024;
			default:
				return 0;
			}
		}

		public static bool ValidateSpd(byte[] input)
		{
			if (input == null || input.Length < 256)
			{
				return false;
			}
			int num = input.Length;
			if (num != 256)
			{
				if (num != 512)
				{
					if (num == 1024 && GetRamType(input) == RamType.DDR5)
					{
						goto IL_0041;
					}
				}
				else if (GetRamType(input) == RamType.DDR4)
				{
					goto IL_0041;
				}
				return false;
			}
			if (GetRamType(input) != RamType.DDR3 && GetRamType(input) != RamType.DDR2 && GetRamType(input) != RamType.DDR)
			{
				return GetRamType(input) == RamType.SDRAM;
			}
			return true;
			IL_0041:
			return true;
		}

		public static ushort GetManufacturerId(byte[] input)
		{
			ISpd spd = null;
			switch (GetRamType(input))
			{
			case RamType.DDR5:
				spd = new DDR5(input);
				break;
			case RamType.DDR4:
				spd = new DDR4(input);
				break;
			case RamType.DDR2_FB_DIMM:
			case RamType.DDR3:
				spd = new DDR3(input);
				break;
			case RamType.DDR2:
				spd = new DDR2(input);
				break;
			case RamType.DDR:
				spd = new DDR(input);
				break;
			case RamType.SDRAM:
				spd = new SDRAM(input);
				break;
			}
			if (spd == null)
			{
				return 0;
			}
			return spd.ManufacturerIdCode.ManufacturerId;
		}

		public static ManufacturerIdCodeData FindManufacturerId(string name)
		{
			for (byte b = 0; b < Resources.Database.IdCodes.Length; b++)
			{
				string[] array = Data.BytesToString(Data.Gzip(Resources.Database.IdCodes[b], Data.GzipMethod.Decompress)).Split('\n');
				for (byte b2 = 0; b2 < array.Length; b2++)
				{
					if (Data.StringContains(array[b2], name))
					{
						return new ManufacturerIdCodeData
						{
							ContinuationCode = b,
							ManufacturerCode = Data.SetBit(b2, 7, Data.GetParity(b2, Data.Parity.Odd) == 1)
						};
					}
				}
			}
			return default(ManufacturerIdCodeData);
		}

		public static string GetManufacturerName(ushort input)
		{
			byte b = Data.SetBit((byte)(input >> 8), 7, false);
			byte b2 = Data.SetBit((byte)input, 7, false);
			if (b > Resources.Database.IdCodes.Length - 1)
			{
				return "";
			}
			string[] array = Data.BytesToString(Data.Gzip(Resources.Database.IdCodes[b], Data.GzipMethod.Decompress)).Split('\n');
			if (b2 > array.Length)
			{
				return "";
			}
			return array[b2 - 1];
		}

		public static string GetModulePartNumberName(byte[] input)
		{
			ISpd spd = null;
			RamType ramType = GetRamType(input);
			switch (ramType)
			{
			case RamType.DDR5:
				spd = new DDR5(input);
				break;
			case RamType.DDR4:
				spd = new DDR4(input);
				break;
			case RamType.DDR2_FB_DIMM:
			case RamType.DDR3:
				spd = new DDR3(input);
				break;
			case RamType.DDR2:
				spd = new DDR2(input);
				break;
			case RamType.DDR:
				spd = new DDR(input);
				break;
			case RamType.SDRAM:
				spd = new SDRAM(input);
				break;
			}
			if (ramType == RamType.DDR2 || ramType == RamType.DDR)
			{
				if (GetManufacturerId(input) != 408)
				{
					ManufacturerIdCodeData value = FindManufacturerId("Kingston");
					ManufacturerIdCodeData? obj = ((spd != null) ? new ManufacturerIdCodeData?(spd.ManufacturerIdCode) : ((ManufacturerIdCodeData?)null));
					if (!(value == obj))
					{
						goto IL_00f1;
					}
				}
				return Data.BytesToString(Data.TrimArray(input, 16, Data.TrimPosition.Start));
			}
			goto IL_00f1;
			IL_00f1:
			if (spd == null)
			{
				return string.Empty;
			}
			return spd.PartNumber;
		}
	}
}
