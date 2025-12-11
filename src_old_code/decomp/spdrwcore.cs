
// F:\bios\SPD\soft\SPD_RW\20230205_RSWP\spdrwcore.dll
// spdrwcore, Version=2.23.2.5, Culture=neutral, PublicKeyToken=null
// Global type: <Module>
// Architecture: x86
// Runtime: v4.0.30319
// Hash algorithm: SHA1

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using SpdReaderWriterCore.Driver;
using SpdReaderWriterCore.Properties;

[assembly: AssemblyTitle("SpdReaderWriterCore")]
[assembly: AssemblyDescription("SPD-RW Core DLL")]
[assembly: AssemblyCompany("A213M")]
[assembly: AssemblyProduct("SPD-RW")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: Guid("57E94C2C-B6FA-4217-BF9A-DE4BB0AF0513")]
[assembly: AssemblyFileVersion("2.23.02.05")]
[assembly: TargetFramework(".NETFramework,Version=v4.0", FrameworkDisplayName = ".NET Framework 4")]
[assembly: AssemblyVersion("2.23.2.5")]
[module: UnverifiableCode]
internal class _003CModule_003E
{
	static _003CModule_003E()
	{
	}
}
namespace SpdReaderWriterCore
{
	public class Core
	{
	}
	public class Data
	{
		public enum Parity : byte
		{
			Odd,
			Even
		}

		public enum Direction
		{
			Greater = 1,
			Lower = -1
		}

		public enum GzipMethod
		{
			Compress,
			Decompress
		}

		public enum TrimPosition
		{
			Start,
			End
		}

		public static ushort Crc16(byte[] input, ushort poly)
		{
			ushort num = 0;
			foreach (byte b in input)
			{
				num ^= (ushort)(b << 8);
				for (byte b2 = 0; b2 < 8; b2++)
				{
					num = (ushort)((num << 1) ^ (GetBit(num, 15) ? poly : 0));
				}
			}
			return num;
		}

		public static byte Crc(byte[] input)
		{
			byte b = 0;
			foreach (byte b2 in input)
			{
				b += b2;
			}
			return b;
		}

		public static byte GetParity(object input, Parity parityType)
		{
			int num = CountBits(input);
			ulong num2 = Convert.ToUInt64(input) & GenerateBitmask<ulong>(num);
			byte b = 0;
			for (int i = 0; i < num; i++)
			{
				b ^= (byte)((num2 >> i) & 1);
			}
			return (byte)(b ^ (~(uint)parityType & 1));
		}

		public static bool GetBit(object input, int position)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return (((Convert.ToInt64(input) & GenerateBitmask<long>(CountBits(input))) >> position) & 1) == 1;
		}

		public static T SetBit<T>(T input, int position, bool value)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			if (position > CountBits(input))
			{
				throw new ArgumentOutOfRangeException("position");
			}
			return (T)Convert.ChangeType(value ? (Convert.ToUInt64((T)Convert.ChangeType(input, typeof(T))) | (uint)(1 << position)) : (Convert.ToUInt64((T)Convert.ChangeType(input, typeof(T))) & (ulong)(~(1 << position))), typeof(T));
		}

		public static T SubByte<T>(T input, int position)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return SubByte(input, position, position + 1);
		}

		public static T SubByte<T>(T input, int position, int count)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			if (count < 1)
			{
				throw new ArgumentOutOfRangeException("count");
			}
			if (position > CountBits(input))
			{
				throw new ArgumentOutOfRangeException("position");
			}
			if (position + 1 < count)
			{
				throw new ArgumentOutOfRangeException("count");
			}
			object obj = Convert.ChangeType(input, typeof(T));
			object obj2 = Convert.ChangeType(GenerateBitmask<T>(count), typeof(T));
			int num = position - count + 1;
			object obj3 = null;
			if (typeof(T) == typeof(byte))
			{
				obj3 = ((byte)obj >> num) & (byte)obj2;
			}
			else if (typeof(T) == typeof(sbyte))
			{
				obj3 = ((sbyte)obj >> num) & (sbyte)obj2;
			}
			else if (typeof(T) == typeof(short))
			{
				obj3 = ((short)obj >> num) & (short)obj2;
			}
			else if (typeof(T) == typeof(ushort))
			{
				obj3 = ((ushort)obj >> num) & (ushort)obj2;
			}
			else if (typeof(T) == typeof(int))
			{
				obj3 = ((int)obj >> num) & (int)obj2;
			}
			else if (typeof(T) == typeof(uint))
			{
				obj3 = ((uint)obj >> num) & (uint)obj2;
			}
			else if (typeof(T) == typeof(long))
			{
				obj3 = ((long)obj >> num) & (long)obj2;
			}
			else if (typeof(T) == typeof(ulong))
			{
				obj3 = ((ulong)obj >> num) & (ulong)obj2;
			}
			if (obj3 != null)
			{
				return (T)Convert.ChangeType(obj3, typeof(T));
			}
			throw new InvalidDataException("T");
		}

		public static int CountBits<T>(T input)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			try
			{
				return Marshal.SizeOf((object)input) * 8;
			}
			catch
			{
				return 0;
			}
		}

		public static int CountBytes(Type input)
		{
			if (input == typeof(bool))
			{
				return 1;
			}
			return Marshal.SizeOf(input);
		}

		public static T GenerateBitmask<T>(int count)
		{
			return (T)Convert.ChangeType(Math.Pow(2.0, count) - 1.0, typeof(T));
		}

		public static T BoolToNum<T>(bool input)
		{
			return (T)Convert.ChangeType(input ? 1 : 0, typeof(T));
		}

		public static bool IsEven<T>(T input)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return !GetBit(input, 0);
		}

		public static bool IsOdd<T>(T input)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return GetBit(input, 0);
		}

		public static int ToEven<T>(T input, Direction dir)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return (int)(Convert.ToInt32(input) + ((!IsEven(input)) ? dir : ((Direction)0)));
		}

		public static int ToOdd<T>(T input, Direction dir)
		{
			if (!IsNumeric(input))
			{
				throw new InvalidDataException("input");
			}
			return (int)(Convert.ToInt32(input) + ((!IsOdd(input)) ? dir : ((Direction)0)));
		}

		public static bool IsNumeric<T>(T input)
		{
			TypeCode typeCode = Type.GetTypeCode(input.GetType());
			if ((uint)(typeCode - 5) <= 10u)
			{
				return true;
			}
			return false;
		}

		public static bool StringContains(string inputString, string substring)
		{
			return inputString.IndexOf(substring, 0, StringComparison.CurrentCultureIgnoreCase) != -1;
		}

		public static bool ValidateHex(string input)
		{
			try
			{
				int.Parse(input, NumberStyles.AllowHexSpecifier);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool ValidateHex(char input)
		{
			if (65 > (input & -33) || (input & -33) > 70)
			{
				if ('0' <= input)
				{
					return input <= '9';
				}
				return false;
			}
			return true;
		}

		public static byte HexStringToByte(string input)
		{
			return Convert.ToByte(input, 16);
		}

		public static string BytesToHexString(byte[] input)
		{
			StringBuilder stringBuilder = new StringBuilder(input.Length * 2);
			foreach (byte b in input)
			{
				stringBuilder.Append(string.Format("{0:X2}", b));
			}
			return stringBuilder.ToString();
		}

		public static byte[] Gzip(byte[] input, GzipMethod method)
		{
			if (method != GzipMethod.Compress)
			{
				return vA(input);
			}
			return Qb(input);
		}

		public static byte[] GzipPeek(byte[] input, int outputSize)
		{
			return RB(input, outputSize, true);
		}

		private static byte[] Qb(byte[] P_0)
		{
			using (MemoryStream memoryStream = new MemoryStream(P_0))
			{
				using (MemoryStream memoryStream2 = new MemoryStream())
				{
					using (GZipStream gZipStream = new GZipStream(memoryStream2, CompressionMode.Compress))
					{
						byte[] array = new byte[16384];
						int num;
						do
						{
							num = memoryStream.Read(array, 0, array.Length);
							gZipStream.Write(array, 0, num);
						}
						while (num > 0);
					}
					return memoryStream2.ToArray();
				}
			}
		}

		private static byte[] vA(byte[] P_0)
		{
			return RB(P_0, 16384, false);
		}

		private static byte[] RB(byte[] P_0, int P_1, bool P_2)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (MemoryStream stream = new MemoryStream(P_0))
				{
					using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
					{
						byte[] array = new byte[P_1];
						int num;
						do
						{
							num = gZipStream.Read(array, 0, array.Length);
							if (num > 0)
							{
								memoryStream.Write(array, 0, num);
								if (P_2)
								{
									break;
								}
							}
						}
						while (num > 0);
					}
					return memoryStream.ToArray();
				}
			}
		}

		public static string BytesToString<T>(T[] input)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < input.Length; i++)
			{
				stringBuilder.Append(((char)Convert.ChangeType(input[i], typeof(char))).ToString());
			}
			return stringBuilder.ToString();
		}

		public static bool IsAscii<T>(T input)
		{
			if (input == null)
			{
				throw new NullReferenceException("input");
			}
			byte b = (byte)Convert.ChangeType(input, typeof(byte));
			if (32 <= b)
			{
				return b <= 126;
			}
			return false;
		}

		public static byte ByteToBinaryCodedDecimal(byte input)
		{
			return (byte)((input & 0xF) + ((input >> 4) & 0xF) * 10);
		}

		public static byte BinaryCodedDecimalToByte(byte input)
		{
			if (input > 99)
			{
				throw new ArgumentOutOfRangeException("input");
			}
			byte b = (byte)(input / 10);
			return (byte)(((byte)(input - b * 10) & 0xF) | (b << 4));
		}

		public static T[] ConsecutiveArray<T>(int start, int stop, int step)
		{
			Queue<T> queue = new Queue<T>();
			int num = start;
			if (start < stop)
			{
				do
				{
					queue.Enqueue((T)Convert.ChangeType(num, typeof(T)));
					num += step;
				}
				while (num <= stop);
			}
			else
			{
				do
				{
					queue.Enqueue((T)Convert.ChangeType(num, typeof(T)));
					num -= step;
				}
				while (num >= stop);
			}
			return queue.ToArray();
		}

		public static T[] RepetitiveArray<T>(T element, int count)
		{
			T[] array = new T[count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = element;
			}
			return array;
		}

		public static byte[] RandomArray(int count)
		{
			return RandomArray(count, 0, 255);
		}

		public static byte[] RandomArray(int count, int min, int max)
		{
			byte[] array = new byte[count];
			Random random = new Random();
			for (int i = 0; i < count; i++)
			{
				array[i] = (byte)random.Next(min, max);
			}
			return array;
		}

		public static int FindArray<T>(T[] source, T[] pattern)
		{
			return FindArray(source, pattern, 0);
		}

		public static int FindArray<T>(T[] source, T[] pattern, int start)
		{
			if (source == null)
			{
				throw new NullReferenceException("source");
			}
			if (pattern == null)
			{
				throw new NullReferenceException("pattern");
			}
			if (pattern.Length > source.Length)
			{
				throw new ArgumentOutOfRangeException("pattern cannot be greater than source");
			}
			int num = source.Length - pattern.Length + 1;
			for (int i = start; i < num; i++)
			{
				if (!source[i].Equals(pattern[0]))
				{
					continue;
				}
				int num2 = pattern.Length - 1;
				while (num2 >= 1 && source[i + num2].Equals(pattern[num2]))
				{
					if (num2 == 1)
					{
						return i;
					}
					num2--;
				}
			}
			return -1;
		}

		public static bool MatchArray<T>(T[] source, T[] pattern, int offset)
		{
			if (source == null)
			{
				throw new NullReferenceException("source");
			}
			if (pattern == null)
			{
				throw new NullReferenceException("pattern");
			}
			if (pattern.Length > source.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			if (pattern.Length + offset > source.Length)
			{
				throw new IndexOutOfRangeException("offset");
			}
			T[] array = new T[pattern.Length];
			Array.Copy(source, offset, array, 0, pattern.Length);
			return CompareArray(array, pattern);
		}

		public static bool CompareArray<T1, T2>(T1[] a1, T2[] a2)
		{
			if (a1 == null)
			{
				throw new ArgumentNullException("a1");
			}
			if (a2 == null)
			{
				throw new ArgumentNullException("a2");
			}
			if (typeof(T1) != typeof(T2))
			{
				return false;
			}
			if (a1.Length == a2.Length)
			{
				for (int i = 0; i < a1.Length; i++)
				{
					if (!a1[i].Equals(a2[i]))
					{
						return false;
					}
				}
			}
			return true;
		}

		public static T[] TrimArray<T>(T[] input, int newSize, TrimPosition trimPosition)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			if (newSize == input.Length)
			{
				return input;
			}
			if (newSize > input.Length)
			{
				throw new ArgumentOutOfRangeException("newSize cannot be greater than input");
			}
			T[] array = new T[newSize];
			if (trimPosition == TrimPosition.End)
			{
				Array.Copy(input, array, newSize);
			}
			else
			{
				Array.Copy(input, input.Length - newSize, array, 0, newSize);
			}
			return array;
		}

		public static T[] SubArray<T>(T[] input, uint start, uint length)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			if (start + length > input.Length)
			{
				throw new ArgumentOutOfRangeException("length");
			}
			if (start == 0 && length == input.Length)
			{
				return input;
			}
			T[] array = new T[length];
			for (int i = 0; i < length; i++)
			{
				array[i] = input[i + start];
			}
			return array;
		}

		public static T[] MergeArray<T>(T[] a1, T[] a2)
		{
			if (a1 == null)
			{
				throw new NullReferenceException("a1");
			}
			if (a2 == null)
			{
				throw new NullReferenceException("a2");
			}
			if (a1.Length == 0)
			{
				return a2;
			}
			if (a2.Length == 0)
			{
				return a1;
			}
			T[] array = new T[a1.Length + a2.Length];
			Array.Copy(a1, array, a1.Length);
			Array.Copy(a2, 0, array, a1.Length, a2.Length);
			return array;
		}

		public static T[] ReverseArray<T>(T[] array)
		{
			if (array == null)
			{
				throw new ArgumentNullException("array");
			}
			if (array.Length == 0)
			{
				return array;
			}
			T[] array2 = new T[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = array[array.Length - 1 - i];
			}
			return array2;
		}

		public static bool ArrayContains<T>(T[] array, T item)
		{
			if (array == null)
			{
				throw new NullReferenceException("array");
			}
			if (item == null)
			{
				throw new NullReferenceException("item");
			}
			if (array.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < array.Length; i++)
			{
				T val = array[i];
				if (val.Equals(item))
				{
					return true;
				}
			}
			return false;
		}

		public static string GetEnumDescription(Enum e)
		{
			string text = e.ToString();
			object[] customAttributes = e.GetType().GetMember(text)[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
			if (customAttributes.Length != 0)
			{
				return ((DescriptionAttribute)customAttributes[0]).Description;
			}
			return text;
		}
	}
	public interface IDriver
	{
		bool IsInstalled { get; }

		bool IsServiceRunning { get; }

		bool IsReady { get; }

		bool InstallDriver();

		bool RemoveDriver();

		bool StartDriver();

		bool StopDriver();
	}
	public interface ISpd
	{
		int Length { get; }

		int SpdBytesUsed { get; }

		Spd.RamType DramDeviceType { get; }

		Spd.ManufacturerIdCodeData ManufacturerIdCode { get; }

		string PartNumber { get; }

		Spd.DateCodeData ModuleManufacturingDate { get; }

		ulong TotalModuleCapacity { get; }

		bool CrcStatus { get; }

		bool FixCrc();
	}
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
	public class IoPort
	{
		[CompilerGenerated]
		private ushort vb;

		public ushort BaseAddress
		{
			[CompilerGenerated]
			get
			{
				return vb;
			}
			[CompilerGenerated]
			set
			{
				vb = value;
			}
		}

		public IoPort()
		{
			BaseAddress = 0;
		}

		public IoPort(ushort P_0)
		{
			BaseAddress = P_0;
		}

		public override string ToString()
		{
			return string.Format("IO port {0:X4}h", BaseAddress);
		}

		public T Read<T>(ushort offset)
		{
			object obj = null;
			if (typeof(T) == typeof(byte))
			{
				obj = Smbus.Driver.ReadIoPortByte((ushort)(BaseAddress + offset));
			}
			else if (typeof(T) == typeof(ushort))
			{
				obj = Smbus.Driver.ReadIoPortWord((ushort)(BaseAddress + offset));
			}
			else if (typeof(T) == typeof(uint))
			{
				obj = Smbus.Driver.ReadIoPortDword((ushort)(BaseAddress + offset));
			}
			if (obj != null)
			{
				return (T)Convert.ChangeType(obj, typeof(T));
			}
			throw new InvalidDataException("T");
		}

		public bool Write<T>(ushort offset, T value)
		{
			object obj = Convert.ChangeType(value, typeof(T));
			if (typeof(T) == typeof(byte))
			{
				return Smbus.Driver.WriteIoPortByteEx((ushort)(BaseAddress + offset), (byte)obj);
			}
			if (typeof(T) == typeof(ushort))
			{
				return Smbus.Driver.WriteIoPortWordEx((ushort)(BaseAddress + offset), (ushort)obj);
			}
			if (typeof(T) == typeof(uint))
			{
				return Smbus.Driver.WriteIoPortDwordEx((ushort)(BaseAddress + offset), (uint)obj);
			}
			throw new InvalidDataException("T");
		}
	}
	public class Arduino : IDisposable
	{
		public struct SerialPortSettings
		{
			public int BaudRate;

			public bool DtrEnable;

			public bool RtsEnable;

			public int Timeout;

			public SerialPortSettings(int P_0 = 115200, bool P_1 = true, bool P_2 = true, int P_3 = 10)
			{
				BaudRate = P_0;
				DtrEnable = P_1;
				RtsEnable = P_2;
				Timeout = P_3;
			}

			public override string ToString()
			{
				return string.Format("{0}", BaudRate);
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct Pin
		{
			[StructLayout(LayoutKind.Sequential, Size = 1)]
			public struct Name
			{
				public const byte HV_SWITCH = 0;

				public const byte SA1_SWITCH = 1;
			}
		}

		private struct Ab
		{
			private byte[] RA;

			public const int Pc = 35;

			public const int sc = 2;

			public byte[] UC
			{
				get
				{
					return RA;
				}
				set
				{
					if (35 < array.Length || array.Length < 2)
					{
						throw new ArgumentOutOfRangeException();
					}
					RA = array;
					eB.Set();
				}
			}

			public bool hb
			{
				get
				{
					if (RA != null)
					{
						return RA.Length >= 2;
					}
					return false;
				}
			}

			public byte XB
			{
				get
				{
					return RA[0];
				}
			}

			public byte qA
			{
				get
				{
					return RA[1];
				}
			}

			public byte[] ec
			{
				get
				{
					return Data.SubArray(RA, 2u, qA);
				}
			}

			private byte cA
			{
				get
				{
					return RA[qA + 2];
				}
			}

			public bool SB
			{
				get
				{
					return Data.Crc(ec) == cA;
				}
			}
		}

		public enum Alert
		{
			SLAVEINC = 43,
			SLAVEDEC = 45,
			CLOCKINC = 47,
			CLOCKDEC = 92
		}

		public class ArduinoEventArgs : EventArgs
		{
			[CompilerGenerated]
			private Alert WB;

			public new static readonly ArduinoEventArgs Empty;

			public Alert Notification
			{
				[CompilerGenerated]
				get
				{
					return WB;
				}
				[CompilerGenerated]
				set
				{
					WB = value;
				}
			}

			public DateTime TimeStamp
			{
				get
				{
					return DateTime.Now;
				}
			}

			static ArduinoEventArgs()
			{
				Empty = new ArduinoEventArgs();
			}
		}

		[CompilerGenerated]
		private sealed class iA
		{
			public Arduino hA;

			public byte EB;

			internal void c()
			{
				hA.e((Alert)EB);
			}
		}

		[CompilerGenerated]
		private sealed class ic
		{
			public SerialPortSettings fc;

			public Stack<Arduino> PB;

			internal void vc(string P_0)
			{
				using (Arduino arduino = new Arduino(fc, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							PB.Push(arduino);
						}
					}
					catch
					{
					}
				}
			}
		}

		[CompilerGenerated]
		private sealed class Db
		{
			public SerialPortSettings Xc;

			public Stack<Arduino> Mc;

			internal void Sc(string P_0)
			{
				using (Arduino arduino = new Arduino(Xc, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							Mc.Push(arduino);
						}
					}
					catch
					{
					}
				}
			}
		}

		[CompilerGenerated]
		private sealed class S
		{
			public SerialPortSettings sA;

			public Stack<Arduino> nA;

			internal void kB(string P_0)
			{
				using (Arduino arduino = new Arduino(sA, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							nA.Push(arduino);
						}
					}
					catch
					{
						arduino.Dispose();
					}
				}
			}
		}

		[CompilerGenerated]
		private sealed class JB
		{
			public SerialPortSettings rb;

			public Stack<Arduino> jc;

			internal void DC(string P_0)
			{
				using (Arduino arduino = new Arduino(rb, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							jc.Push(arduino);
						}
					}
					finally
					{
						arduino.Dispose();
					}
				}
			}
		}

		[CompilerGenerated]
		private sealed class PC
		{
			public SerialPortSettings Gb;

			public Stack<Arduino> u;

			internal void f(string P_0)
			{
				using (Arduino arduino = new Arduino(Gb, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							u.Push(arduino);
						}
					}
					catch
					{
					}
					finally
					{
						arduino.Dispose();
					}
				}
			}
		}

		[CompilerGenerated]
		private sealed class Tc
		{
			public SerialPortSettings eA;

			public Stack<Arduino> bA;

			internal void Yb(string P_0)
			{
				using (Arduino arduino = new Arduino(eA, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							bA.Push(arduino);
						}
					}
					catch
					{
					}
				}
			}
		}

		public static int[] BaudRates;

		[CompilerGenerated]
		private SerialPortSettings NA;

		[CompilerGenerated]
		private readonly string oc;

		private byte Oc;

		[CompilerGenerated]
		private int qc;

		[CompilerGenerated]
		private EventHandler kb;

		[CompilerGenerated]
		private EventHandler FA;

		private SerialPort pB;

		private Ab gB;

		private byte[] ZB;

		private int v;

		private int Ob;

		private byte[] fC;

		private int bc = -1;

		private readonly object sC = new object();

		private readonly object zb = new object();

		private static readonly AutoResetEvent eB;

		public int BytesReceived
		{
			get
			{
				return v;
			}
		}

		public int BytesSent
		{
			get
			{
				return Ob;
			}
		}

		public byte[] Addresses
		{
			get
			{
				if (fC == null)
				{
					fC = Scan();
				}
				if (fC.Length == 0)
				{
					I2CAddress = 0;
				}
				return fC;
			}
		}

		public ushort I2CClock
		{
			get
			{
				if (!GetI2CClock())
				{
					return 100;
				}
				return 400;
			}
		}

		public int FirmwareVersion
		{
			get
			{
				return fA();
			}
		}

		public static int IncludedFirmwareVersion
		{
			get
			{
				return aB();
			}
		}

		public static int RequiredFirmwareVersion
		{
			get
			{
				return IncludedFirmwareVersion;
			}
		}

		public string Name
		{
			get
			{
				return sb();
			}
			set
			{
				YB(value);
			}
		}

		public bool IsConnected
		{
			get
			{
				try
				{
					return pB != null && pB.IsOpen;
				}
				catch
				{
					return false;
				}
			}
		}

		public SerialPortSettings PortSettings
		{
			[CompilerGenerated]
			get
			{
				return NA;
			}
			[CompilerGenerated]
			set
			{
				NA = value;
			}
		}

		public string PortName
		{
			[CompilerGenerated]
			get
			{
				return oc;
			}
		}

		public byte I2CAddress
		{
			get
			{
				return Oc;
			}
			set
			{
				Oc = value;
				if (Eeprom.ValidateAddress(Oc))
				{
					DataLength = GetSpdSize();
				}
			}
		}

		public int DataLength
		{
			[CompilerGenerated]
			get
			{
				return qc;
			}
			[CompilerGenerated]
			private set
			{
				qc = num;
			}
		}

		public int BytesToRead
		{
			get
			{
				try
				{
					return pB.BytesToRead;
				}
				catch
				{
					return 0;
				}
			}
		}

		public int BytesToWrite
		{
			get
			{
				try
				{
					return pB.BytesToWrite;
				}
				catch
				{
					return 0;
				}
			}
		}

		public byte RswpTypeSupport
		{
			get
			{
				try
				{
					if (bc == -1)
					{
						bc = GetRswpSupport();
					}
					return (byte)bc;
				}
				catch
				{
					throw new Exception("Unable to get supported RAM type");
				}
			}
		}

		public bool RswpPresent
		{
			get
			{
				return RswpTypeSupport > 0;
			}
		}

		public event EventHandler AlertReceived
		{
			[CompilerGenerated]
			add
			{
				EventHandler eventHandler = kb;
				EventHandler eventHandler2;
				do
				{
					eventHandler2 = eventHandler;
					EventHandler value2 = (EventHandler)Delegate.Combine(eventHandler2, value);
					eventHandler = Interlocked.CompareExchange(ref kb, value2, eventHandler2);
				}
				while ((object)eventHandler != eventHandler2);
			}
			[CompilerGenerated]
			remove
			{
				EventHandler eventHandler = kb;
				EventHandler eventHandler2;
				do
				{
					eventHandler2 = eventHandler;
					EventHandler value2 = (EventHandler)Delegate.Remove(eventHandler2, value);
					eventHandler = Interlocked.CompareExchange(ref kb, value2, eventHandler2);
				}
				while ((object)eventHandler != eventHandler2);
			}
		}

		public event EventHandler ConnectionLost
		{
			[CompilerGenerated]
			add
			{
				EventHandler eventHandler = FA;
				EventHandler eventHandler2;
				do
				{
					eventHandler2 = eventHandler;
					EventHandler value2 = (EventHandler)Delegate.Combine(eventHandler2, value);
					eventHandler = Interlocked.CompareExchange(ref FA, value2, eventHandler2);
				}
				while ((object)eventHandler != eventHandler2);
			}
			[CompilerGenerated]
			remove
			{
				EventHandler eventHandler = FA;
				EventHandler eventHandler2;
				do
				{
					eventHandler2 = eventHandler;
					EventHandler value2 = (EventHandler)Delegate.Remove(eventHandler2, value);
					eventHandler = Interlocked.CompareExchange(ref FA, value2, eventHandler2);
				}
				while ((object)eventHandler != eventHandler2);
			}
		}

		public Arduino()
		{
			PortSettings = default(SerialPortSettings);
		}

		public Arduino(string P_0)
		{
			PortSettings = default(SerialPortSettings);
			oc = P_0;
		}

		public Arduino(SerialPortSettings P_0)
		{
			PortSettings = P_0;
		}

		public Arduino(SerialPortSettings P_0, string P_1)
		{
			PortSettings = P_0;
			oc = P_1;
		}

		public Arduino(SerialPortSettings P_0, string P_1, byte P_2)
		{
			PortSettings = P_0;
			oc = P_1;
			I2CAddress = P_2;
		}

		public override string ToString()
		{
			if (PortName != null)
			{
				return string.Format("{0}:{1}", PortName, PortSettings.BaudRate);
			}
			return "N/A";
		}

		~Arduino()
		{
			Dispose();
		}

		public bool Connect()
		{
			lock (sC)
			{
				if (IsConnected)
				{
					return IsConnected;
				}
				pB = new SerialPort
				{
					PortName = PortName,
					BaudRate = PortSettings.BaudRate,
					DtrEnable = PortSettings.DtrEnable,
					RtsEnable = PortSettings.RtsEnable,
					ReadTimeout = 1000,
					WriteTimeout = 1000,
					ReceivedBytesThreshold = 2
				};
				pB.DataReceived += uc;
				pB.ErrorReceived += yb;
				try
				{
					pB.Open();
					Ob = 0;
					v = 0;
					try
					{
						if (Test())
						{
							Thread thread = new Thread(FB);
							thread.Priority = ThreadPriority.BelowNormal;
							thread.Start();
						}
					}
					catch
					{
						Dispose();
						throw new Exception("Device failed to pass communication test");
					}
				}
				catch (Exception ex)
				{
					throw new Exception("Unable to connect (" + PortName + "): " + ex.Message);
				}
			}
			return IsConnected;
		}

		public bool Disconnect()
		{
			lock (sC)
			{
				if (!IsConnected)
				{
					return !IsConnected;
				}
				try
				{
					pB.DataReceived -= uc;
					pB.ErrorReceived -= yb;
					Dispose();
				}
				catch (Exception ex)
				{
					throw new Exception("Unable to disconnect (" + PortName + "): " + ex.Message);
				}
				return !IsConnected;
			}
		}

		public void Dispose()
		{
			lock (sC)
			{
				if (IsConnected)
				{
					pB.Close();
				}
				if (pB != null)
				{
					pB.DataReceived -= uc;
					pB.ErrorReceived -= yb;
					pB = null;
				}
				fC = null;
				bc = -1;
			}
		}

		void IDisposable.Dispose()
		{
			//ILSpy generated this explicit interface implementation from .override directive in Dispose
			this.Dispose();
		}

		public bool Test()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(116);
				}
				catch
				{
					throw new Exception("Unable to test " + PortName);
				}
			}
		}

		public byte GetRswpSupport()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<byte>(102);
				}
				catch
				{
					throw new Exception("Unable to get " + PortName + " supported RAM");
				}
			}
		}

		public bool GetRswpSupport(byte rswpTypeBitmask)
		{
			return (GetRswpSupport() & rswpTypeBitmask) == rswpTypeBitmask;
		}

		public byte[] Scan()
		{
			Queue<byte> queue = new Queue<byte>();
			lock (sC)
			{
				try
				{
					if (IsConnected)
					{
						byte b = ExecuteCommand<byte>(115);
						if (b == 0)
						{
							return new byte[0];
						}
						for (byte b2 = 0; b2 <= 7; b2++)
						{
							if (Data.GetBit(b, b2))
							{
								queue.Enqueue((byte)(80 + b2));
							}
						}
					}
				}
				catch
				{
					throw new Exception("Unable to scan I2C bus on " + PortName);
				}
			}
			return queue.ToArray();
		}

		public bool SetI2CClock(bool fastMode)
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(99, fastMode);
				}
				catch
				{
					throw new Exception("Unable to set I2C clock mode on " + PortName);
				}
			}
		}

		public bool GetI2CClock()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(99, 63);
				}
				catch
				{
					throw new Exception("Unable to get I2C clock mode on " + PortName);
				}
			}
		}

		public bool FactoryReset()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(45);
				}
				catch
				{
					throw new Exception("Unable to reset device settings on " + PortName);
				}
			}
		}

		public bool ResetConfigPins()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(100);
				}
				catch
				{
					throw new Exception("Unable to reset pin state on " + PortName);
				}
			}
		}

		public bool GetOfflineMode()
		{
			lock (sC)
			{
				try
				{
					return GetRswpSupport(32);
				}
				catch
				{
					throw new Exception("Unable to get offline mode status on " + PortName);
				}
			}
		}

		public bool ProbeAddress()
		{
			if (Eeprom.ValidateAddress(I2CAddress))
			{
				return ProbeAddress(I2CAddress);
			}
			return false;
		}

		public bool ProbeAddress(byte address)
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(97, address);
				}
				catch
				{
					throw new Exception(string.Format("Unable to probe address {0} on {1}", address, PortName));
				}
			}
		}

		private void Fc()
		{
			lock (sC)
			{
				try
				{
					if (BytesToRead > 0)
					{
						pB.DiscardInBuffer();
					}
					if (BytesToWrite > 0)
					{
						pB.DiscardOutBuffer();
					}
				}
				catch
				{
					throw new Exception("Unable to clear " + PortName + " buffer");
				}
			}
		}

		private int fA()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<int>(118);
				}
				catch
				{
					throw new Exception("Unable to get firmware version on " + PortName);
				}
			}
		}

		private static int aB()
		{
			try
			{
				byte[] input = Data.GzipPeek(Resources.Firmware.SpdReaderWriter_ino, 1024);
				int num = Data.CountBytes(typeof(int)) * 2;
				MatchCollection matchCollection = new Regex(string.Format("([\\d]{{{0}}})", num)).Matches(Data.BytesToString(input));
				if (matchCollection.Count > 0 && matchCollection[0].Length == num)
				{
					return int.Parse(matchCollection[0].Value);
				}
				throw new Exception();
			}
			catch
			{
				throw new Exception("Unable to get included firmware version number");
			}
		}

		private bool YB(string P_0)
		{
			if (P_0 == null)
			{
				throw new ArgumentNullException("name");
			}
			if (P_0.Length == 0)
			{
				throw new ArgumentException("Name can't be blank");
			}
			if (P_0.Length > 16)
			{
				throw new ArgumentOutOfRangeException(string.Format("Name can't be longer than {0} characters", (byte)16));
			}
			lock (sC)
			{
				try
				{
					string text = P_0.Trim();
					if (text == sb())
					{
						return false;
					}
					byte[] a = new byte[2]
					{
						110,
						(byte)text.Length
					};
					return ExecuteCommand<bool>(Data.MergeArray(a, Encoding.ASCII.GetBytes(text)));
				}
				catch
				{
					throw new Exception("Unable to assign name to " + PortName);
				}
			}
		}

		private string sb()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<string>(110, 63).Trim();
				}
				catch
				{
					throw new Exception("Unable to get " + PortName + " name");
				}
			}
		}

		public static Arduino[] Find(SerialPortSettings settings)
		{
			Stack<Arduino> bA = new Stack<Arduino>();
			Parallel.ForEach(SerialPort.GetPortNames().Distinct().ToArray(), delegate(string P_0)
			{
				using (Arduino arduino = new Arduino(settings, P_0))
				{
					try
					{
						if (arduino.Connect())
						{
							bA.Push(arduino);
						}
					}
					catch
					{
					}
				}
			});
			return bA.ToArray();
		}

		public bool DetectDdr4()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(52, I2CAddress);
				}
				catch
				{
					throw new Exception("Error detecting DDR4 on " + PortName);
				}
			}
		}

		public bool DetectDdr5()
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(53, I2CAddress);
				}
				catch
				{
					throw new Exception("Error detecting DDR5 on " + PortName);
				}
			}
		}

		public byte ReadSpd5Hub(byte register)
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<byte>(104, I2CAddress, register, 63);
				}
				catch
				{
					throw new Exception("Unable to read SPD5 hub on " + PortName);
				}
			}
		}

		public bool WriteSpd5Hub(byte register, byte value)
		{
			lock (sC)
			{
				try
				{
					return ExecuteCommand<bool>(104, I2CAddress, register, 1, value);
				}
				catch
				{
					throw new Exception("Unable to read SPD5 hub on " + PortName);
				}
			}
		}

		public ushort GetSpdSize()
		{
			lock (sC)
			{
				try
				{
					return Spd.DataLength.Length[ExecuteCommand<byte>(122, I2CAddress)];
				}
				catch
				{
					throw new Exception(string.Format("Unable to get SPD size on {0}:{1}", PortName, I2CAddress));
				}
			}
		}

		protected virtual void OnAlertReceived(ArduinoEventArgs e)
		{
			EventHandler eventHandler = kb;
			if (eventHandler != null)
			{
				eventHandler(this, e);
			}
		}

		private void uc(object P_0, SerialDataReceivedEventArgs P_1)
		{
			lock (zb)
			{
				if (P_0 != pB || !IsConnected)
				{
					return;
				}
				Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
				while (BytesToRead < pB.ReceivedBytesThreshold)
				{
					Thread.Sleep(10);
				}
				ZB = new byte[35];
				v += pB.Read(ZB, 0, pB.ReceivedBytesThreshold);
				byte b = ZB[0];
				if (b != 38)
				{
					if (b == 64)
					{
						byte EB = ZB[1];
						if (Enum.IsDefined(typeof(Alert), (Alert)EB))
						{
							new Thread((ThreadStart)delegate
							{
								e((Alert)EB);
							}).Start();
						}
					}
				}
				else
				{
					while (BytesToRead < ZB[1] + 1)
					{
					}
					v += pB.Read(ZB, 2, ZB[1] + 1);
					gB.UC = ZB;
				}
				ZB = null;
			}
		}

		private void e(Alert P_0)
		{
			OnAlertReceived(new ArduinoEventArgs
			{
				Notification = P_0
			});
			if (P_0 == Alert.SLAVEDEC || P_0 == Alert.SLAVEINC)
			{
				fC = Scan();
				bc = GetRswpSupport();
			}
		}

		private void yb(object P_0, SerialErrorReceivedEventArgs P_1)
		{
			if (P_0 != null && P_0.GetType() == typeof(SerialPort))
			{
				throw new Exception("Error received on " + ((SerialPort)P_0).PortName);
			}
		}

		private void FB()
		{
			Thread.Sleep(2000);
			while (IsConnected)
			{
				Thread.Sleep(50);
			}
			OnConnectionLost(EventArgs.Empty);
			Dispose();
		}

		protected virtual void OnConnectionLost(EventArgs e)
		{
			EventHandler eventHandler = FA;
			if (eventHandler != null)
			{
				eventHandler(this, e);
			}
		}

		public T ExecuteCommand<T>(byte command)
		{
			return ExecuteCommand<T>(new byte[1] { command });
		}

		public T ExecuteCommand<T>(byte command, byte p1)
		{
			return ExecuteCommand<T>(new byte[2] { command, p1 });
		}

		public T ExecuteCommand<T>(byte command, bool p1)
		{
			return ExecuteCommand<T>(new byte[2]
			{
				command,
				Data.BoolToNum<byte>(p1)
			});
		}

		public T ExecuteCommand<T>(byte command, byte p1, byte p2)
		{
			return ExecuteCommand<T>(new byte[3] { command, p1, p2 });
		}

		public T ExecuteCommand<T>(byte command, byte p1, bool p2)
		{
			return ExecuteCommand<T>(new byte[3]
			{
				command,
				p1,
				Data.BoolToNum<byte>(p2)
			});
		}

		public T ExecuteCommand<T>(byte command, byte p1, byte p2, byte p3)
		{
			return ExecuteCommand<T>(new byte[4] { command, p1, p2, p3 });
		}

		public T ExecuteCommand<T>(byte command, byte p1, byte p2, bool p3)
		{
			return ExecuteCommand<T>(new byte[4]
			{
				command,
				p1,
				p2,
				Data.BoolToNum<byte>(p3)
			});
		}

		public T ExecuteCommand<T>(byte command, byte p1, byte p2, byte p3, byte p4)
		{
			return ExecuteCommand<T>(new byte[5] { command, p1, p2, p3, p4 });
		}

		public T ExecuteCommand<T>(byte[] command)
		{
			byte[] array = NC(command);
			if (typeof(T).IsArray)
			{
				return (T)Convert.ChangeType(array, typeof(T));
			}
			if (typeof(T) == typeof(bool))
			{
				return (T)Convert.ChangeType(array[0] == 1, typeof(T));
			}
			if (typeof(T) == typeof(short))
			{
				return (T)Convert.ChangeType(BitConverter.ToInt16(Data.SubArray(array, 0u, 2u), 0), TypeCode.Int16);
			}
			if (typeof(T) == typeof(ushort))
			{
				return (T)Convert.ChangeType(BitConverter.ToUInt16(Data.SubArray(array, 0u, 2u), 0), TypeCode.UInt16);
			}
			if (typeof(T) == typeof(int))
			{
				return (T)Convert.ChangeType(BitConverter.ToInt32(Data.SubArray(array, 0u, 4u), 0), TypeCode.Int32);
			}
			if (typeof(T) == typeof(uint))
			{
				return (T)Convert.ChangeType(BitConverter.ToUInt32(Data.SubArray(array, 0u, 4u), 0), TypeCode.UInt32);
			}
			if (typeof(T) == typeof(long))
			{
				return (T)Convert.ChangeType(BitConverter.ToInt64(Data.SubArray(array, 0u, 8u), 0), TypeCode.Int64);
			}
			if (typeof(T) == typeof(ulong))
			{
				return (T)Convert.ChangeType(BitConverter.ToUInt64(Data.SubArray(array, 0u, 8u), 0), TypeCode.UInt64);
			}
			if (typeof(T) == typeof(string))
			{
				return (T)Convert.ChangeType(Data.BytesToString(array), typeof(T));
			}
			return (T)Convert.ChangeType(array[0], typeof(T));
		}

		private byte[] NC(byte[] P_0)
		{
			if (P_0.Length == 0)
			{
				throw new ArgumentException("Value cannot be null or whitespace.", "command");
			}
			if (!IsConnected)
			{
				throw new InvalidOperationException("Device is not connected");
			}
			lock (sC)
			{
				try
				{
					Fc();
					pB.BaseStream.Write(P_0, 0, P_0.Length);
					pB.BaseStream.Flush();
					Ob += P_0.Length;
					if (!eB.WaitOne(PortSettings.Timeout * 1000))
					{
						throw new System.TimeoutException(PortName + " response timeout");
					}
					if (gB.XB != 38)
					{
						throw new InvalidDataException("Invalid response header");
					}
					if (!gB.SB)
					{
						throw new DataException("Response CRC error");
					}
					return gB.ec;
				}
				catch
				{
					throw new IOException(PortName + " failed to execute command " + Data.BytesToHexString(P_0));
				}
				finally
				{
					gB = default(Ab);
					eB.Reset();
				}
			}
		}

		static Arduino()
		{
			BaudRates = new int[17]
			{
				300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600,
				115200, 230400, 250000, 460800, 500000, 1000000, 2000000
			};
			eB = new AutoResetEvent(false);
		}
	}
	public class Eeprom
	{
		[CompilerGenerated]
		private static byte x;

		private static byte i
		{
			[CompilerGenerated]
			get
			{
				return x;
			}
			[CompilerGenerated]
			set
			{
				x = b;
			}
		}

		public static byte Read(Smbus smbus, ushort offset)
		{
			Q(smbus, offset);
			IB(smbus, ref offset);
			return smbus.ReadByte(smbus, smbus.I2CAddress, offset);
		}

		public static byte[] Read(Smbus smbus, ushort offset, int count)
		{
			if (count == 0)
			{
				throw new ArgumentOutOfRangeException("count");
			}
			byte[] array = new byte[count];
			for (ushort num = 0; num < count; num++)
			{
				array[num] = Read(smbus, (ushort)(num + offset));
			}
			return array;
		}

		public static bool Write(Smbus smbus, ushort offset, byte value)
		{
			Q(smbus, offset);
			IB(smbus, ref offset);
			return smbus.WriteByte(smbus, smbus.I2CAddress, offset, value);
		}

		public static bool Write(Smbus smbus, ushort offset, byte[] value)
		{
			for (ushort num = 0; num < value.Length; num++)
			{
				if (!Write(smbus, (ushort)(num + offset), value[num]))
				{
					return false;
				}
			}
			return true;
		}

		public static bool Update(Smbus smbus, ushort offset, byte value)
		{
			if (!Verify(smbus, offset, value))
			{
				return Write(smbus, offset, value);
			}
			return true;
		}

		public static bool Update(Smbus smbus, ushort offset, byte[] value)
		{
			if (!Verify(smbus, offset, value))
			{
				return Write(smbus, offset, value);
			}
			return true;
		}

		public static bool Verify(Smbus smbus, ushort offset, byte value)
		{
			return Read(smbus, offset) == value;
		}

		public static bool Verify(Smbus smbus, ushort offset, byte[] value)
		{
			return Data.CompareArray(Read(smbus, offset, value.Length), value);
		}

		public static void ResetPageAddress(Smbus smbus)
		{
			GB(smbus, 0);
		}

		private static void GB(Smbus P_0, byte P_1)
		{
			if (P_0.MaxSpdSize != 256)
			{
				if ((P_0.MaxSpdSize == 512 && P_1 > 1) || (P_0.MaxSpdSize == 1024 && P_1 > 15))
				{
					throw new ArgumentOutOfRangeException("eepromPageNumber");
				}
				if (P_0.IsDdr5Present)
				{
					P_0.WriteByte(P_0, P_0.I2CAddress, 11, P_1);
				}
				else
				{
					P_0.WriteByte(P_0, (byte)(54 + P_1));
				}
				i = P_1;
			}
		}

		private static void Q(Smbus P_0, ushort P_1)
		{
			if (P_0.MaxSpdSize >= 256)
			{
				if (P_1 > P_0.MaxSpdSize)
				{
					throw new IndexOutOfRangeException("Invalid offset");
				}
				if (P_0.MaxSpdSize < 512)
				{
					return;
				}
			}
			byte b = (byte)(P_1 >> (P_0.IsDdr5Present ? 7 : 8));
			if (b != i)
			{
				GB(P_0, b);
			}
		}

		private static void IB(Smbus P_0, ref ushort P_1)
		{
			P_1 = (P_0.IsDdr5Present ? ((byte)((P_1 % 128) | 0x80)) : P_1);
		}

		public static bool Overwrite(Smbus smbus, ushort offset)
		{
			return Write(smbus, offset, Read(smbus, offset));
		}

		public static bool GetRswp(Smbus smbus, byte block)
		{
			byte[] array = new byte[4] { 99, 105, 107, 97 };
			block = (byte)((block <= 3) ? block : 0);
			try
			{
				return !smbus.ReadByte(smbus, (byte)(array[block] >> 1));
			}
			catch
			{
				return true;
			}
		}

		public static bool GetPswp(Smbus smbus)
		{
			return !smbus.ReadByte(smbus, (byte)(0x30 | (smbus.I2CAddress & 7)));
		}

		public static byte Read(Arduino arduino, ushort offset)
		{
			jC(arduino, offset);
			try
			{
				return arduino.ExecuteCommand<byte>(114, arduino.I2CAddress, (byte)(offset >> 8), (byte)offset, 1);
			}
			catch
			{
				throw new Exception(string.Format("Unable to read byte 0x{0:X4} at {1}:{2}", offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static byte[] Read(Arduino arduino, ushort offset, byte count)
		{
			if (count == 0)
			{
				throw new ArgumentOutOfRangeException("count");
			}
			jC(arduino, offset);
			try
			{
				return arduino.ExecuteCommand<byte[]>(114, arduino.I2CAddress, (byte)(offset >> 8), (byte)offset, count);
			}
			catch
			{
				throw new Exception(string.Format("Unable to read byte # 0x{0:X4} at {1}:{2}", offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Write(Arduino arduino, ushort offset, byte value)
		{
			jC(arduino, offset);
			try
			{
				return arduino.ExecuteCommand<bool>(119, arduino.I2CAddress, (byte)(offset >> 8), (byte)offset, value);
			}
			catch
			{
				throw new Exception(string.Format("Unable to write \"0x{0:X2}\" to # 0x{1:X4} at {2}:{3}", value, offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Write(Arduino arduino, ushort offset, byte[] value)
		{
			jC(arduino, offset);
			ac(value);
			byte[] a = new byte[5]
			{
				103,
				arduino.I2CAddress,
				(byte)(offset >> 8),
				(byte)offset,
				(byte)value.Length
			};
			try
			{
				return arduino.ExecuteCommand<bool>(Data.MergeArray(a, value));
			}
			catch
			{
				throw new Exception(string.Format("Unable to write page of {0} byte(s) to # 0x{1:X4} at {2}:{3}", value.Length, offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Update(Arduino arduino, ushort offset, byte value)
		{
			try
			{
				return Verify(arduino, offset, value) || Write(arduino, offset, value);
			}
			catch
			{
				throw new Exception(string.Format("Unable to update byte # 0x{0:X4} with \"0x{1:X2}\" at {2}:{3}", offset, value, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Update(Arduino arduino, ushort offset, byte[] value)
		{
			ac(value);
			try
			{
				return Verify(arduino, offset, value) || Write(arduino, offset, value);
			}
			catch
			{
				throw new Exception(string.Format("Unable to update page at # 0x{0:X4} with \"0x{1:X2}\" at {2}:{3}", offset, value, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Verify(Arduino arduino, ushort offset, byte value)
		{
			try
			{
				return Read(arduino, offset) == value;
			}
			catch
			{
				throw new Exception(string.Format("Unable to verify byte # 0x{0:X4} at {1}:{2}", offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Verify(Arduino arduino, ushort offset, byte[] value)
		{
			try
			{
				return Data.CompareArray(Read(arduino, offset, (byte)value.Length), value);
			}
			catch
			{
				throw new Exception(string.Format("Unable to verify bytes # 0x{0:X4}-0x{1:X4} at {2}:{3}", offset, offset + value.Length, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool Overwrite(Arduino arduino, ushort offset)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(111, arduino.I2CAddress, (byte)(offset >> 8), (byte)offset);
			}
			catch
			{
				throw new Exception(string.Format("Unable to perform offset # 0x{0:X4} write test at {1}:{2}", offset, arduino.PortName, arduino.I2CAddress));
			}
		}

		public static bool SetRswp(Arduino arduino, byte block)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(98, arduino.I2CAddress, block, true);
			}
			catch
			{
				throw new Exception("Unable to set RSWP on " + arduino.PortName);
			}
		}

		public static bool GetRswp(Arduino arduino, byte block)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(98, arduino.I2CAddress, block, 63);
			}
			catch
			{
				throw new Exception(string.Format("Unable to get block {0} RSWP status on {1}", block, arduino.PortName));
			}
		}

		public static bool ClearRswp(Arduino arduino)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(98, arduino.I2CAddress, 0, false);
			}
			catch
			{
				throw new Exception("Unable to clear RSWP on " + arduino.PortName);
			}
		}

		public static bool SetPswp(Arduino arduino)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(108, arduino.I2CAddress, true);
			}
			catch
			{
				throw new Exception("Unable to set PSWP on " + arduino.PortName);
			}
		}

		public static bool GetPswp(Arduino arduino)
		{
			try
			{
				return arduino.ExecuteCommand<bool>(108, arduino.I2CAddress, 63);
			}
			catch
			{
				throw new Exception("Unable to get PSWP status on " + arduino.PortName);
			}
		}

		private static void jC(Arduino P_0, ushort P_1)
		{
			if (P_1 >= P_0.DataLength)
			{
				throw new IndexOutOfRangeException("Invalid offset");
			}
		}

		private static void ac(byte[] P_0)
		{
			if (P_0.Length == 0 || P_0.Length > 16)
			{
				throw new ArgumentOutOfRangeException(string.Format("Invalid page size ({0})", P_0.Length));
			}
		}

		public static bool ValidateAddress(int address)
		{
			return address >> 3 == 10;
		}
	}
	public class Smbus : IDisposable
	{
		public struct DeviceInfo
		{
			public VendorId VendorId;

			public DeviceId DeviceId;
		}

		public enum VendorId : ushort
		{
			AMD = 4130,
			ATI = 4098,
			Intel = 32902,
			Nvidia = 4318,
			SiS = 4153,
			VIA = 4358
		}

		public enum DeviceId : ushort
		{
			ICH = 9232,
			ICH0 = 9248,
			ICH2 = 9280,
			ICH2M = 9292,
			ICH3 = 9344,
			ICH3M = 9356,
			ICH4 = 9408,
			ICH4M = 9420,
			CICH = 9296,
			ICH5 = 9424,
			ICH6M = 9793,
			ICH6W = 9794,
			ICH7DH = 10160,
			ICH7 = 10168,
			ICH7M = 10169,
			ICH7MDH = 10173,
			ICH8 = 10256,
			ICH8ME = 10257,
			ICH8DH = 10258,
			ICH8DO = 10260,
			ICH8M = 10261,
			ICH9DH = 10514,
			ICH9DO = 10516,
			ICH9R = 10518,
			ICH9ME = 10519,
			ICH9 = 10520,
			ICH9M = 10521,
			ICH10DO = 14868,
			ICH10R = 14870,
			ICH10 = 14872,
			ICH10D = 14874,
			H55 = 15110,
			H57 = 15112,
			P55 = 15106,
			Q57 = 15114,
			B65 = 7248,
			B75 = 7753,
			H61 = 7260,
			H67 = 7242,
			H77 = 7754,
			P67 = 7238,
			Q65 = 7244,
			Q67 = 7246,
			Q75 = 7752,
			Q77 = 7751,
			Z68 = 7236,
			Z75 = 7750,
			Z77 = 7748,
			B85 = 35920,
			H81 = 35932,
			H87 = 35914,
			H97 = 36038,
			Q85 = 35916,
			Q87 = 35918,
			Z87 = 35908,
			Z97 = 36036,
			HM55 = 15113,
			HM57 = 15115,
			HM65 = 7241,
			HM67 = 7243,
			HM70 = 7774,
			HM75 = 7773,
			HM76 = 7769,
			HM77 = 7767,
			HM86 = 35913,
			HM87 = 35915,
			HM97 = 36035,
			NM10 = 10172,
			NM70 = 7775,
			PM55 = 15107,
			QM57 = 15111,
			QM67 = 7247,
			QM77 = 7765,
			QM87 = 35919,
			QS57 = 15119,
			QS67 = 7245,
			QS77 = 7766,
			UM67 = 7239,
			UM77 = 7768,
			B150 = 41288,
			B250 = 41672,
			B360 = 41736,
			B365 = 41676,
			C232 = 41290,
			C236 = 41289,
			C242 = 41738,
			C246 = 41737,
			CM236 = 41296,
			CM238 = 41300,
			CM246 = 41742,
			H110 = 41283,
			H170 = 41284,
			H270 = 41668,
			H310 = 41731,
			H310D = 17294,
			H310M = 41674,
			H370 = 41732,
			HM170 = 41294,
			HM175 = 41298,
			HM370 = 41741,
			Q150 = 41287,
			Q170 = 41286,
			Q250 = 41671,
			Q270 = 41670,
			Q370 = 41734,
			QM170 = 41293,
			QM175 = 41299,
			QM370 = 41740,
			Z170 = 41285,
			Z270 = 41669,
			Z370 = 41673,
			Z390 = 41733,
			B460 = 41928,
			B560 = 17287,
			C252 = 17292,
			C256 = 17293,
			H410 = 41946,
			H470 = 1668,
			H510 = 17288,
			H570 = 17286,
			Q470 = 1671,
			Q570 = 17284,
			W480 = 1687,
			W580 = 17295,
			Z490 = 1669,
			Z590 = 17285,
			X299 = 41682,
			C422 = 41683,
			FCH = 30987,
			Hudson2 = 30731,
			nForce2 = 100,
			nForce2_Ultra = 132,
			nForce3_Pro150 = 212,
			nForce3_250Gb = 228,
			nForce4 = 82,
			nForce4_MCP04 = 52,
			MCP51 = 612,
			MCP55 = 872,
			MCP61 = 1003,
			MCP65 = 1094,
			MCP67 = 1346,
			MCP73 = 2008,
			MCP78S = 1874,
			MCP79 = 2722
		}

		public enum SkylakeXDeviceId : ushort
		{
			CpuImcSmbus = 8325
		}

		private enum KA : byte
		{
			Unknown,
			Default,
			SkylakeX
		}

		public struct SmbusData
		{
			internal byte lC;

			internal byte qb;

			internal ushort Ic;

			internal SmbusAccessMode HB;

			internal SmbusDataCommand rA;

			internal byte Bb;

			internal byte Cc;

			internal SmbStatus FC;
		}

		public enum SmbusAccessMode : byte
		{
			Read,
			Write
		}

		public enum SmbusDataCommand : byte
		{
			Quick,
			Byte,
			ByteData,
			WordData
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		private struct TC
		{
			internal static readonly byte EA = 10;

			internal static readonly byte Qc = 0;
		}

		public enum SmbStatus : byte
		{
			Ready,
			Busy,
			Error,
			Success,
			Timeout,
			Aborted
		}

		[CompilerGenerated]
		private static WinRing0 zB;

		[CompilerGenerated]
		private DeviceInfo z;

		private byte jb;

		private byte gA;

		[CompilerGenerated]
		private bool dC;

		[CompilerGenerated]
		private ushort Nb;

		[CompilerGenerated]
		private bool qB;

		[CompilerGenerated]
		private PciDevice tC;

		[CompilerGenerated]
		private IoPort hC;

		[CompilerGenerated]
		private bool SC;

		[CompilerGenerated]
		private byte[] uB;

		private object VA = new object();

		public static WinRing0 Driver
		{
			[CompilerGenerated]
			get
			{
				return zB;
			}
			[CompilerGenerated]
			private set
			{
				zB = winRing;
			}
		}

		public string Version
		{
			get
			{
				byte[] array = new byte[4];
				if (Driver != null && Driver.IsReady)
				{
					Driver.GetDriverVersion(ref array[0], ref array[1], ref array[2], ref array[3]);
				}
				return string.Format("{0}.{1}.{2}.{3}", array[0], array[1], array[2], array[3]);
			}
		}

		public DeviceInfo Info
		{
			[CompilerGenerated]
			get
			{
				return z;
			}
			[CompilerGenerated]
			private set
			{
				z = deviceInfo;
			}
		}

		public byte BusNumber
		{
			get
			{
				return jb;
			}
			set
			{
				jb = value;
				Eeprom.ResetPageAddress(this);
				I2CAddress = gA;
			}
		}

		public byte I2CAddress
		{
			get
			{
				return gA;
			}
			set
			{
				gA = value;
				IsDdr5Present = Eeprom.ValidateAddress(gA) && ProbeAddress((byte)(0x48 | (7 & value)));
				Eeprom.ResetPageAddress(this);
				MaxSpdSize = (ushort)(Eeprom.ValidateAddress(gA) ? JA(gA) : 0);
			}
		}

		public bool IsDdr5Present
		{
			[CompilerGenerated]
			get
			{
				return dC;
			}
			[CompilerGenerated]
			private set
			{
				dC = flag;
			}
		}

		public ushort MaxSpdSize
		{
			[CompilerGenerated]
			get
			{
				return Nb;
			}
			[CompilerGenerated]
			private set
			{
				Nb = nb;
			}
		}

		public bool SpdWriteDisabled
		{
			[CompilerGenerated]
			get
			{
				return qB;
			}
			[CompilerGenerated]
			private set
			{
				qB = flag;
			}
		}

		public PciDevice pciDevice
		{
			[CompilerGenerated]
			get
			{
				return tC;
			}
			[CompilerGenerated]
			private set
			{
				tC = pciDevice;
			}
		}

		public IoPort ioPort
		{
			[CompilerGenerated]
			get
			{
				return hC;
			}
			[CompilerGenerated]
			private set
			{
				hC = ioPort;
			}
		}

		public bool IsConnected
		{
			[CompilerGenerated]
			get
			{
				return SC;
			}
			[CompilerGenerated]
			private set
			{
				SC = sC;
			}
		}

		public byte[] SMBuses
		{
			[CompilerGenerated]
			get
			{
				return uB;
			}
			[CompilerGenerated]
			private set
			{
				uB = array;
			}
		}

		public byte[] Addresses
		{
			get
			{
				return Scan();
			}
		}

		private KA oB
		{
			get
			{
				if (Ib(Info.DeviceId))
				{
					return KA.SkylakeX;
				}
				if (PA(Info.DeviceId))
				{
					return KA.Default;
				}
				return KA.Unknown;
			}
		}

		public Smbus()
		{
			HC();
		}

		~Smbus()
		{
			Dispose();
		}

		public void Dispose()
		{
			ioPort = null;
			pciDevice = null;
			if (!IsConnected)
			{
				SMBuses = null;
				IsConnected = false;
				SpdWriteDisabled = false;
			}
		}

		void IDisposable.Dispose()
		{
			//ILSpy generated this explicit interface implementation from .override directive in Dispose
			this.Dispose();
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", Info.VendorId, Info.DeviceId);
		}

		private bool PA(DeviceId P_0)
		{
			return Enum.IsDefined(typeof(DeviceId), P_0);
		}

		private bool Ib(DeviceId P_0)
		{
			return new DeviceId[2]
			{
				DeviceId.X299,
				DeviceId.C422
			}.Contains(P_0);
		}

		private void HC()
		{
			try
			{
				if (Driver == null)
				{
					Driver = new WinRing0();
				}
				if (!Driver.IsReady)
				{
					throw new Exception("Driver initialization failure.");
				}
			}
			catch (Exception ex)
			{
				throw new Exception("WinRing0 initialization failure: " + ex.Message);
			}
			Info = GetDeviceInfo();
			VendorId vendorId = Info.VendorId;
			if (vendorId != VendorId.AMD)
			{
				if (vendorId == VendorId.Nvidia)
				{
					goto IL_00a5;
				}
				if (vendorId == VendorId.Intel)
				{
					if (oB != KA.SkylakeX)
					{
						goto IL_00a5;
					}
					pciDevice = new PciDevice(PciDevice.FindDeviceById((ushort)Info.VendorId, 8325));
				}
			}
			else
			{
				pciDevice = new PciDevice(PciDevice.FindDeviceByClass(12, 5));
				if ((pciDevice.DeviceId == 30987 && pciDevice.RevisionId >= 73) || (pciDevice.DeviceId == 30731 && pciDevice.RevisionId >= 65))
				{
					IoPort obj = new IoPort();
					obj.Write((ushort)3286, (byte)0);
					byte b = obj.Read<byte>(3287);
					obj.Write((ushort)3286, (byte)1);
					byte b2 = obj.Read<byte>(3287);
					if (Data.GetBit(b, 4))
					{
						ushort num = (ushort)(b2 << 8);
						if (num != 0)
						{
							ioPort = new IoPort(num);
						}
					}
				}
			}
			goto IL_01d0;
			IL_00a5:
			if (oB == KA.Default)
			{
				pciDevice = new PciDevice(PciDevice.FindDeviceByClass(12, 5));
				ushort num2 = pciDevice.Read<ushort>(PciDevice.RegisterOffset.BaseAddress[4]);
				SpdWriteDisabled = Data.GetBit(pciDevice.Read<byte>(64u), 4);
				if (Data.GetBit(num2, 0))
				{
					ioPort = new IoPort(Data.SetBit(num2, 0, false));
				}
			}
			goto IL_01d0;
			IL_01d0:
			if (oB != KA.Unknown)
			{
				SMBuses = FindBus();
				IsConnected = SMBuses.Length != 0;
				if (IsConnected)
				{
					BusNumber = SMBuses[0];
				}
			}
		}

		public DeviceInfo GetDeviceInfo()
		{
			DeviceInfo result = default(DeviceInfo);
			PciDevice pciDevice = new PciDevice();
			result.VendorId = (VendorId)pciDevice.VendorId;
			switch (result.VendorId)
			{
			case VendorId.Intel:
				try
				{
					uint num2 = PciDevice.FindDeviceByClass(6, 1);
					result.DeviceId = (DeviceId)new PciDevice(num2).DeviceId;
				}
				catch
				{
					result.DeviceId = (DeviceId)0;
				}
				break;
			case VendorId.AMD:
			case VendorId.Nvidia:
			{
				uint num = PciDevice.FindDeviceByClass(12, 5);
				result.DeviceId = (DeviceId)new PciDevice(num).DeviceId;
				break;
			}
			}
			return result;
		}

		public byte[] FindBus()
		{
			byte busNumber = BusNumber;
			try
			{
				Queue<byte> queue = new Queue<byte>();
				for (byte b = 0; b <= 1; b++)
				{
					BusNumber = b;
					if (TryScan())
					{
						queue.Enqueue(b);
					}
				}
				return queue.ToArray();
			}
			catch
			{
				return new byte[0];
			}
			finally
			{
				BusNumber = busNumber;
			}
		}

		public bool TryScan()
		{
			for (byte b = 80; b <= 87; b++)
			{
				if (ProbeAddress(b))
				{
					return true;
				}
			}
			return n(this, true).Length != 0;
		}

		public bool ProbeAddress(byte slaveAddress)
		{
			return ReadByte(this, slaveAddress);
		}

		public byte[] Scan()
		{
			return n(this, false);
		}

		private byte[] n(Smbus P_0, bool P_1)
		{
			Queue<byte> queue = new Queue<byte>();
			for (byte b = 0; b <= 7; b++)
			{
				try
				{
					if (ReadByte(P_0, (byte)(b + 80)))
					{
						queue.Enqueue((byte)(b + 80));
						if (P_1)
						{
							break;
						}
					}
				}
				catch
				{
				}
			}
			return queue.ToArray();
		}

		public bool ReadByte(Smbus controller, byte slaveAddress)
		{
			SmbusData smbusData = new SmbusData
			{
				lC = controller.BusNumber,
				qb = slaveAddress,
				HB = SmbusAccessMode.Read,
				rA = SmbusDataCommand.Byte
			};
			try
			{
				return xA(ref smbusData);
			}
			catch
			{
				return false;
			}
		}

		public byte ReadByte(Smbus controller, byte slaveAddress, ushort offset)
		{
			SmbusData smbusData = new SmbusData
			{
				lC = controller.BusNumber,
				qb = slaveAddress,
				Ic = offset,
				HB = SmbusAccessMode.Read,
				rA = SmbusDataCommand.ByteData
			};
			try
			{
				xA(ref smbusData);
				return smbusData.Cc;
			}
			catch
			{
				throw new IOException(string.Format("Read error: {0}:{1}:{2}", controller, slaveAddress, offset));
			}
		}

		public bool WriteByte(Smbus controller, byte slaveAddress)
		{
			SmbusData smbusData = new SmbusData
			{
				lC = controller.BusNumber,
				qb = slaveAddress,
				HB = SmbusAccessMode.Write,
				rA = SmbusDataCommand.Byte
			};
			try
			{
				return xA(ref smbusData);
			}
			catch
			{
				return false;
			}
		}

		public bool WriteByte(Smbus controller, byte slaveAddress, ushort offset, byte value)
		{
			SmbusData smbusData = new SmbusData
			{
				lC = controller.BusNumber,
				qb = slaveAddress,
				Ic = offset,
				Bb = value,
				HB = SmbusAccessMode.Write,
				rA = SmbusDataCommand.ByteData
			};
			try
			{
				return xA(ref smbusData);
			}
			catch
			{
				return false;
			}
		}

		private bool xA(ref SmbusData P_0)
		{
			lock (VA)
			{
				if (oB == KA.Unknown)
				{
					return false;
				}
				if (oB == KA.SkylakeX)
				{
					if (P_0.HB == SmbusAccessMode.Write)
					{
						pciDevice.Write((byte)(182 + P_0.lC * 4), P_0.Bb);
					}
					pciDevice.Write((byte)(156 + P_0.lC * 4), (uint)(0x20480000 | ((P_0.qb | ((P_0.HB == SmbusAccessMode.Write) ? 128 : 0)) << 8) | (byte)P_0.Ic));
					if (P_0.HB == SmbusAccessMode.Write)
					{
						Thread.Sleep(Eeprom.ValidateAddress(P_0.qb) ? TC.EA : TC.Qc);
					}
					if (!fb(new SmbStatus[3]
					{
						SmbStatus.Ready,
						SmbStatus.Success,
						SmbStatus.Error
					}, 1000))
					{
						P_0.FC = SmbStatus.Timeout;
						return false;
					}
					P_0.FC = GetBusStatus();
					if (P_0.FC == SmbStatus.Error)
					{
						return false;
					}
					if (P_0.HB == SmbusAccessMode.Read)
					{
						P_0.Cc = pciDevice.Read<byte>((byte)(180 + P_0.lC * 4));
					}
				}
				else if (oB == KA.Default && Info.VendorId == VendorId.Nvidia)
				{
					if (P_0.lC > 0)
					{
						return false;
					}
					ioPort.Write(2, (byte)(P_0.qb << 1));
					ioPort.Write(3, (byte)P_0.Ic);
					if (P_0.HB == SmbusAccessMode.Write)
					{
						ioPort.Write(4, P_0.Bb);
					}
					byte b = 0;
					b = (byte)((uint)b | ((P_0.HB == SmbusAccessMode.Read) ? 1u : 0u));
					switch (P_0.rA)
					{
					case SmbusDataCommand.Quick:
						b |= 2;
						break;
					case SmbusDataCommand.Byte:
						b |= 4;
						break;
					default:
						b |= 6;
						break;
					case SmbusDataCommand.WordData:
						b |= 8;
						break;
					}
					ioPort.Write(0, b);
					if (P_0.HB == SmbusAccessMode.Write)
					{
						Thread.Sleep(Eeprom.ValidateAddress(P_0.qb) ? (TC.EA * 2) : TC.Qc);
					}
					if (!fb(new SmbStatus[2]
					{
						SmbStatus.Success,
						SmbStatus.Error
					}, 1000))
					{
						P_0.FC = SmbStatus.Timeout;
						return false;
					}
					P_0.FC = GetBusStatus();
					if (P_0.FC == SmbStatus.Error)
					{
						return false;
					}
					if (P_0.HB == SmbusAccessMode.Read)
					{
						P_0.Cc = ioPort.Read<byte>(4);
					}
				}
				else if (oB == KA.Default && Info.VendorId != VendorId.Nvidia)
				{
					if (P_0.lC > 0 && Info.VendorId == VendorId.Intel)
					{
						return false;
					}
					byte b2 = 0;
					if (Info.VendorId == VendorId.AMD)
					{
						b2 = (byte)(BusNumber * 20);
					}
					byte value = 30;
					ioPort.Write(b2, value);
					if (!tB(SmbStatus.Ready, 1000))
					{
						P_0.FC = SmbStatus.Timeout;
						return false;
					}
					ioPort.Write((byte)(4 + b2), (byte)((uint)(P_0.qb << 1) | ((P_0.HB == SmbusAccessMode.Read) ? 1u : 0u)));
					if (P_0.HB == SmbusAccessMode.Write)
					{
						ioPort.Write((byte)(5 + b2), P_0.Bb);
					}
					ioPort.Write((byte)(3 + b2), (byte)P_0.Ic);
					byte b3;
					switch (P_0.rA)
					{
					case SmbusDataCommand.Quick:
						b3 = 0;
						break;
					case SmbusDataCommand.Byte:
						b3 = 4;
						break;
					default:
						b3 = 8;
						break;
					case SmbusDataCommand.WordData:
						b3 = 12;
						break;
					}
					ioPort.Write((byte)(2 + b2), (byte)(1 | b3 | 0x40));
					if (P_0.HB == SmbusAccessMode.Write)
					{
						Thread.Sleep(Eeprom.ValidateAddress(P_0.qb) ? TC.EA : TC.Qc);
					}
					if (!fb(new SmbStatus[3]
					{
						SmbStatus.Ready,
						SmbStatus.Success,
						SmbStatus.Error
					}, 1000))
					{
						ioPort.Write((ushort)(byte)(2 + b2), (byte)2);
						P_0.FC = SmbStatus.Timeout;
						return false;
					}
					P_0.FC = GetBusStatus();
					if (P_0.FC == SmbStatus.Error)
					{
						return false;
					}
					if (P_0.HB == SmbusAccessMode.Read)
					{
						P_0.Cc = ioPort.Read<byte>(5);
					}
				}
				return true;
			}
		}

		public SmbStatus GetBusStatus()
		{
			if (oB == KA.SkylakeX)
			{
				byte b = pciDevice.Read<byte>((byte)(168 + BusNumber * 4));
				if ((b & 1) > 0)
				{
					return SmbStatus.Busy;
				}
				if ((b & 4) > 0)
				{
					if ((b & 2) > 0)
					{
						return SmbStatus.Error;
					}
					if ((b & -4 & 3) == 0)
					{
						return SmbStatus.Success;
					}
				}
				return SmbStatus.Ready;
			}
			if (Info.VendorId == VendorId.Nvidia)
			{
				switch (ioPort.Read<byte>(1))
				{
				case 128:
					return SmbStatus.Success;
				case 16:
				case 31:
					return SmbStatus.Error;
				}
			}
			if (oB == KA.Default)
			{
				byte b = ioPort.Read<byte>(0);
				if ((b & 0x1F) == 0)
				{
					return SmbStatus.Ready;
				}
				if ((b & 1) == 1)
				{
					return SmbStatus.Busy;
				}
				if ((b & 0x1C) > 0)
				{
					return SmbStatus.Error;
				}
				if ((b & 2) == 2)
				{
					return SmbStatus.Success;
				}
			}
			return SmbStatus.Error;
		}

		private bool tB(SmbStatus P_0, int P_1)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.ElapsedMilliseconds < P_1)
			{
				if (GetBusStatus() == P_0)
				{
					return true;
				}
			}
			return false;
		}

		private bool fb(SmbStatus[] P_0, int P_1)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.ElapsedMilliseconds < P_1)
			{
				foreach (SmbStatus smbStatus in P_0)
				{
					if (GetBusStatus() == smbStatus)
					{
						return true;
					}
				}
			}
			return false;
		}

		private ushort JA(byte P_0)
		{
			if (IsDdr5Present)
			{
				return 1024;
			}
			byte b = ReadByte(this, P_0, 2);
			byte[] input = new byte[3] { 0, 0, b };
			return (ushort)(Enum.IsDefined(typeof(Spd.RamType), (Spd.RamType)b) ? ((uint)Spd.GetSpdSize(Spd.GetRamType(input))) : 256u);
		}
	}
	public class PciDevice
	{
		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct BaseClass
		{
			public const byte Bridge = 6;

			public const byte Serial = 12;
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct SubClass
		{
			public const byte Isa = 1;

			public const byte Smbus = 5;
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct RegisterOffset
		{
			public const byte VendorId = 0;

			public const byte DeviceId = 2;

			public const byte Status = 6;

			public const byte RevisionId = 8;

			public const byte SubType = 10;

			public const byte BaseType = 11;

			public static readonly byte[] BaseAddress = new byte[6] { 16, 20, 24, 28, 32, 36 };

			public const byte SubsystemId = 46;

			public const byte SubsystemVendorId = 44;
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		private struct nb
		{
			public static uint hc;

			public static uint DA;

			public static uint TA;

			public static uint kc
			{
				get
				{
					return WinRing0.PciBusDevFunc(hc, DA, TA);
				}
			}
		}

		public ushort VendorId
		{
			get
			{
				return Read<ushort>(0u);
			}
		}

		public ushort DeviceId
		{
			get
			{
				return Read<ushort>(2u);
			}
		}

		public ushort RevisionId
		{
			get
			{
				return Read<byte>(8u);
			}
		}

		public PciDevice()
		{
			nb.hc = 0u;
			nb.DA = 0u;
			nb.TA = 0u;
		}

		public PciDevice(uint P_0)
		{
			nb.hc = WinRing0.PciGetBus(P_0);
			nb.DA = WinRing0.PciGetDev(P_0);
			nb.TA = WinRing0.PciGetFunc(P_0);
		}

		public PciDevice(byte P_0, byte P_1, byte P_2)
		{
			nb.hc = P_0;
			nb.DA = P_1;
			nb.TA = P_2;
		}

		public override string ToString()
		{
			return string.Format("PCI {0:D}/{1:D}/{2:D}", nb.hc, nb.DA, nb.TA);
		}

		public static uint FindDeviceById(ushort vendorId, ushort deviceId)
		{
			try
			{
				uint[] array = Smbus.Driver.FindPciDeviceByIdArray(vendorId, deviceId, 1);
				if (array.Length != 0 && array[0] != uint.MaxValue)
				{
					return array[0];
				}
				return 65535u;
			}
			catch
			{
				throw new IOException("PCI device not found");
			}
		}

		public static uint FindDeviceByClass(byte baseClass, byte subClass)
		{
			return FindDeviceByClass(baseClass, subClass, 0);
		}

		public static uint FindDeviceByClass(byte baseClass, byte subClass, byte programIf)
		{
			try
			{
				uint[] array = Smbus.Driver.FindPciDeviceByClassArray(baseClass, subClass, programIf, 1);
				if (array.Length != 0 && array[0] != uint.MaxValue)
				{
					return array[0];
				}
				return uint.MaxValue;
			}
			catch
			{
				throw new IOException("PCI device not found");
			}
		}

		public T Read<T>(uint offset)
		{
			object obj = null;
			uint pciAddress = nb.kc;
			if (typeof(T) == typeof(byte))
			{
				obj = Smbus.Driver.ReadPciConfigByte(pciAddress, offset);
			}
			else if (typeof(T) == typeof(ushort))
			{
				obj = Smbus.Driver.ReadPciConfigWord(pciAddress, offset);
			}
			else if (typeof(T) == typeof(uint))
			{
				obj = Smbus.Driver.ReadPciConfigDword(pciAddress, offset);
			}
			if (obj != null)
			{
				return (T)Convert.ChangeType(obj, typeof(T));
			}
			throw new InvalidDataException("T");
		}

		public bool Write<T>(uint offset, T value)
		{
			object obj = Convert.ChangeType(value, typeof(T));
			uint pciAddress = nb.kc;
			if (typeof(T) == typeof(byte))
			{
				return Smbus.Driver.WritePciConfigByteEx(pciAddress, offset, (byte)obj);
			}
			if (typeof(T) == typeof(ushort))
			{
				return Smbus.Driver.WritePciConfigWordEx(pciAddress, offset, (ushort)obj);
			}
			if (typeof(T) == typeof(uint))
			{
				return Smbus.Driver.WritePciConfigDwordEx(pciAddress, offset, (uint)obj);
			}
			throw new InvalidDataException("T");
		}
	}
}
namespace SpdReaderWriterCore.Properties
{
	public class Resources
	{
		public static class Driver
		{
			public static byte[] WinRing0_sys = new byte[8631]
			{
				31, 139, 8, 8, 138, 47, 12, 88, 2, 0,
				87, 105, 110, 82, 105, 110, 103, 48, 46, 115,
				121, 115, 0, 236, 88, 121, 84, 27, 101, 16,
				159, 13, 11, 4, 72, 76, 106, 131, 130, 162,
				6, 164, 90, 69, 113, 211, 112, 84, 168, 8,
				148, 96, 80, 142, 144, 112, 168, 5, 49, 64,
				48, 41, 33, 137, 201, 166, 138, 74, 11, 164,
				160, 113, 69, 241, 246, 121, 226, 125, 224, 173,
				175, 226, 129, 82, 170, 5, 180, 90, 235, 137,
				119, 189, 67, 169, 90, 175, 130, 136, 172, 243,
				109, 32, 47, 128, 247, 243, 169, 127, 56, 251,
				102, 231, 155, 249, 102, 126, 223, 204, 119, 45,
				161, 232, 212, 110, 8, 1, 0, 26, 153, 231,
				1, 250, 192, 79, 217, 240, 251, 164, 19, 1,
				236, 115, 200, 19, 251, 192, 163, 17, 47, 198,
				247, 81, 133, 47, 198, 151, 153, 45, 46, 165,
				195, 105, 63, 195, 105, 108, 82, 214, 25, 109,
				54, 59, 171, 172, 53, 41, 157, 110, 155, 210,
				98, 83, 230, 149, 24, 148, 77, 246, 122, 83,
				178, 84, 26, 153, 8, 255, 211, 127, 129, 118,
				68, 119, 79, 94, 201, 206, 244, 206, 241, 195,
				15, 123, 122, 175, 18, 218, 124, 175, 7, 229,
				182, 202, 47, 122, 187, 5, 251, 229, 189, 151,
				11, 178, 189, 247, 10, 65, 118, 10, 82, 111,
				169, 51, 147, 184, 192, 158, 208, 0, 20, 82,
				97, 176, 105, 25, 167, 157, 179, 237, 4, 17,
				21, 69, 137, 161, 85, 10, 208, 26, 238, 183,
				117, 44, 193, 54, 217, 116, 50, 162, 81, 208,
				10, 32, 112, 216, 44, 135, 130, 159, 90, 99,
				253, 126, 251, 20, 19, 47, 66, 168, 200, 137,
				148, 207, 74, 65, 4, 104, 24, 113, 87, 145,
				56, 5, 192, 0, 217, 216, 65, 56, 47, 31,
				140, 50, 6, 160, 27, 245, 211, 113, 220, 56,
				248, 117, 106, 149, 45, 56, 3, 50, 140, 249,
				13, 255, 100, 214, 116, 54, 139, 210, 25, 73,
				242, 69, 246, 203, 121, 164, 4, 48, 39, 59,
				235, 141, 172, 17, 96, 35, 53, 91, 187, 200,
				47, 131, 9, 199, 213, 38, 251, 221, 64, 66,
				12, 100, 174, 4, 185, 200, 111, 184, 160, 184,
				160, 140, 180, 183, 138, 208, 135, 244, 135, 16,
				185, 104, 220, 143, 146, 157, 46, 103, 29, 144,
				57, 241, 207, 13, 208, 68, 46, 194, 203, 77,
				118, 154, 172, 118, 116, 124, 10, 252, 115, 5,
				20, 145, 139, 253, 224, 111, 37, 142, 47, 231,
				118, 151, 150, 114, 26, 113, 5, 231, 160, 205,
				23, 239, 67, 65, 151, 102, 82, 199, 71, 231,
				201, 230, 90, 90, 108, 117, 236, 101, 195, 43,
				248, 104, 29, 54, 79, 27, 25, 164, 97, 27,
				161, 181, 98, 243, 114, 57, 5, 190, 134, 48,
				0, 143, 105, 26, 16, 132, 19, 203, 86, 112,
				69, 114, 47, 229, 45, 165, 57, 77, 204, 16,
				136, 1, 96, 72, 51, 61, 195, 243, 188, 122,
				96, 60, 86, 61, 144, 189, 133, 51, 249, 176,
				203, 163, 156, 179, 247, 81, 56, 57, 2, 200,
				160, 2, 129, 103, 145, 181, 4, 89, 50, 135,
				92, 46, 230, 34, 185, 92, 154, 211, 139, 101,
				140, 16, 252, 199, 64, 151, 4, 129, 34, 166,
				153, 96, 246, 133, 6, 103, 171, 254, 75, 217,
				34, 72, 80, 182, 194, 28, 182, 181, 136, 183,
				157, 158, 125, 61, 167, 145, 112, 192, 174, 66,
				237, 101, 212, 216, 68, 108, 188, 70, 26, 82,
				78, 19, 135, 32, 125, 82, 140, 30, 87, 227,
				172, 202, 113, 42, 185, 34, 133, 151, 26, 63,
				16, 53, 133, 95, 107, 64, 85, 130, 106, 140,
				95, 189, 128, 194, 40, 146, 160, 88, 61, 80,
				61, 24, 3, 243, 70, 27, 237, 241, 143, 38,
				102, 213, 168, 189, 139, 26, 27, 135, 141, 157,
				164, 17, 238, 31, 103, 57, 239, 160, 75, 249,
				104, 26, 209, 198, 15, 144, 61, 150, 77, 235,
				80, 19, 19, 77, 34, 219, 228, 215, 36, 168,
				5, 129, 251, 209, 121, 183, 2, 103, 135, 119,
				75, 184, 34, 124, 203, 55, 251, 66, 60, 31,
				134, 123, 118, 30, 178, 249, 195, 208, 168, 205,
				58, 217, 38, 77, 132, 110, 45, 205, 71, 3,
				201, 114, 184, 99, 196, 29, 78, 38, 101, 231,
				184, 210, 211, 162, 16, 177, 18, 207, 15, 34,
				180, 136, 136, 101, 31, 78, 163, 72, 218, 60,
				49, 122, 224, 192, 50, 26, 245, 234, 65, 57,
				252, 233, 81, 226, 112, 148, 36, 141, 66, 0,
				9, 9, 128, 16, 154, 221, 187, 158, 22, 137,
				156, 141, 20, 42, 30, 219, 2, 0, 21, 156,
				91, 92, 201, 173, 23, 203, 30, 88, 47, 57,
				182, 69, 33, 107, 191, 19, 141, 92, 5, 221,
				241, 10, 23, 230, 213, 76, 202, 46, 186, 26,
				245, 13, 81, 199, 145, 35, 46, 107, 191, 16,
				0, 39, 128, 219, 30, 82, 52, 185, 116, 208,
				51, 70, 121, 70, 161, 99, 96, 195, 89, 231,
				137, 219, 126, 224, 121, 25, 156, 213, 100, 88,
				11, 149, 122, 222, 61, 201, 71, 51, 164, 222,
				81, 46, 95, 172, 221, 170, 145, 1, 155, 164,
				101, 99, 181, 90, 54, 12, 21, 106, 60, 137,
				95, 39, 193, 34, 12, 124, 116, 10, 153, 223,
				184, 128, 186, 146, 168, 210, 128, 186, 10, 213,
				74, 148, 217, 40, 91, 91, 100, 176, 6, 203,
				215, 196, 224, 90, 147, 53, 30, 15, 21, 170,
				168, 193, 35, 54, 111, 111, 97, 133, 226, 185,
				53, 61, 116, 110, 218, 228, 184, 184, 60, 227,
				59, 9, 55, 37, 87, 30, 195, 13, 79, 188,
				117, 224, 200, 196, 142, 67, 17, 41, 178, 154,
				132, 7, 162, 113, 147, 120, 38, 197, 174, 217,
				248, 3, 185, 162, 24, 79, 60, 120, 6, 38,
				117, 8, 211, 165, 21, 151, 250, 113, 44, 136,
				51, 63, 206, 80, 193, 97, 114, 249, 167, 119,
				21, 199, 97, 128, 108, 147, 220, 179, 11, 42,
				159, 17, 33, 136, 108, 227, 221, 20, 192, 137,
				39, 202, 54, 118, 163, 244, 236, 146, 200, 58,
				136, 129, 211, 73, 158, 36, 155, 61, 243, 85,
				217, 249, 111, 146, 203, 117, 227, 99, 248, 126,
				242, 34, 165, 223, 212, 137, 10, 123, 102, 219,
				20, 160, 206, 158, 214, 54, 69, 19, 153, 221,
				54, 181, 145, 200, 132, 182, 169, 11, 80, 202,
				58, 78, 39, 64, 21, 18, 146, 149, 158, 119,
				136, 245, 190, 61, 63, 242, 252, 216, 8, 44,
				176, 182, 18, 235, 35, 196, 154, 47, 225, 162,
				197, 75, 40, 240, 202, 135, 40, 26, 13, 99,
				113, 148, 96, 29, 130, 80, 16, 81, 227, 187,
				230, 133, 77, 96, 216, 248, 134, 182, 169, 110,
				50, 38, 211, 54, 245, 44, 57, 155, 138, 182,
				169, 109, 68, 74, 218, 166, 240, 208, 98, 14,
				239, 18, 220, 245, 66, 88, 37, 134, 85, 234,
				125, 177, 211, 24, 119, 194, 119, 99, 3, 164,
				32, 60, 107, 88, 208, 89, 181, 108, 34, 22,
				97, 196, 64, 77, 219, 84, 30, 145, 71, 182,
				77, 13, 247, 248, 129, 70, 123, 8, 208, 229,
				139, 128, 178, 16, 136, 155, 28, 111, 153, 151,
				213, 254, 51, 136, 62, 54, 207, 164, 252, 9,
				77, 111, 180, 77, 9, 199, 249, 169, 182, 41,
				241, 205, 40, 15, 108, 155, 210, 162, 116, 31,
				191, 56, 186, 23, 151, 232, 25, 97, 129, 19,
				60, 199, 145, 233, 224, 217, 253, 121, 41, 105,
				140, 203, 230, 12, 97, 124, 40, 105, 168, 249,
				21, 175, 112, 47, 121, 215, 199, 240, 209, 203,
				201, 142, 30, 170, 57, 109, 77, 245, 160, 24,
				130, 246, 219, 110, 5, 185, 33, 43, 42, 185,
				227, 200, 103, 192, 252, 172, 240, 85, 248, 78,
				199, 191, 137, 71, 171, 75, 51, 173, 91, 11,
				102, 160, 0, 204, 217, 215, 3, 233, 64, 29,
				63, 14, 133, 228, 59, 49, 208, 18, 225, 17,
				134, 229, 199, 243, 61, 203, 72, 3, 250, 174,
				23, 227, 234, 228, 175, 244, 230, 103, 123, 243,
				29, 179, 159, 152, 221, 186, 161, 252, 148, 78,
				26, 253, 222, 36, 0, 196, 192, 71, 39, 146,
				116, 246, 116, 236, 109, 137, 224, 221, 211, 254,
				175, 13, 183, 21, 79, 131, 63, 57, 36, 115,
				79, 20, 5, 245, 124, 42, 32, 113, 121, 137,
				114, 175, 53, 81, 222, 133, 156, 180, 211, 80,
				81, 121, 51, 224, 104, 42, 205, 180, 250, 57,
				157, 215, 228, 195, 19, 203, 105, 166, 103, 47,
				109, 60, 246, 93, 154, 61, 245, 183, 2, 210,
				22, 174, 104, 79, 189, 87, 74, 154, 167, 212,
				96, 233, 220, 167, 213, 165, 91, 182, 205, 167,
				224, 153, 48, 112, 213, 146, 10, 206, 37, 86,
				167, 18, 252, 74, 46, 204, 51, 57, 179, 85,
				195, 227, 247, 96, 146, 2, 44, 255, 92, 57,
				94, 238, 197, 116, 200, 118, 181, 100, 165, 207,
				46, 194, 196, 138, 241, 212, 136, 253, 122, 13,
				209, 53, 226, 189, 217, 116, 131, 172, 227, 35,
				64, 165, 72, 222, 85, 190, 219, 107, 152, 230,
				214, 72, 60, 63, 206, 120, 53, 187, 189, 69,
				123, 216, 154, 174, 2, 232, 162, 215, 112, 133,
				157, 138, 142, 145, 174, 188, 78, 185, 87, 243,
				29, 7, 94, 141, 152, 85, 112, 111, 250, 30,
				161, 0, 39, 22, 199, 164, 206, 203, 222, 112,
				2, 194, 97, 6, 220, 168, 251, 165, 214, 22,
				30, 216, 68, 33, 161, 160, 20, 118, 83, 254,
				20, 42, 132, 20, 50, 124, 239, 18, 93, 51,
				233, 175, 116, 11, 102, 13, 72, 227, 35, 92,
				145, 196, 231, 17, 186, 36, 199, 158, 44, 97,
				247, 53, 251, 171, 123, 149, 27, 246, 117, 250,
				205, 120, 45, 123, 181, 146, 133, 240, 93, 11,
				224, 91, 136, 142, 217, 106, 197, 220, 155, 99,
				74, 84, 158, 32, 19, 126, 172, 65, 194, 94,
				56, 11, 249, 130, 47, 21, 205, 99, 101, 104,
				158, 155, 91, 92, 45, 174, 44, 81, 142, 75,
				168, 224, 10, 19, 99, 202, 245, 186, 210, 82,
				243, 6, 105, 96, 109, 133, 165, 84, 63, 235,
				205, 75, 20, 215, 123, 151, 205, 174, 54, 195,
				157, 44, 70, 239, 163, 212, 177, 156, 67, 226,
				153, 153, 97, 147, 17, 35, 197, 51, 53, 195,
				210, 153, 223, 172, 139, 239, 74, 89, 215, 85,
				245, 136, 156, 139, 194, 164, 61, 231, 210, 224,
				222, 246, 56, 69, 2, 87, 139, 125, 59, 72,
				197, 253, 245, 151, 132, 2, 146, 231, 217, 24,
				156, 139, 45, 136, 68, 79, 228, 208, 97, 104,
				233, 35, 126, 172, 154, 195, 209, 176, 12, 245,
				176, 239, 110, 212, 203, 57, 115, 12, 239, 144,
				240, 14, 188, 103, 21, 190, 147, 48, 121, 207,
				179, 146, 106, 226, 67, 50, 247, 138, 250, 66,
				200, 94, 42, 231, 10, 209, 112, 4, 127, 102,
				28, 127, 102, 12, 127, 230, 114, 95, 194, 172,
				227, 32, 13, 229, 21, 149, 6, 110, 151, 122,
				64, 253, 142, 250, 21, 245, 94, 53, 207, 239,
				88, 83, 115, 90, 245, 22, 110, 23, 247, 53,
				183, 89, 48, 143, 96, 7, 207, 127, 38, 220,
				181, 149, 107, 1, 79, 213, 33, 120, 56, 74,
				125, 22, 132, 38, 135, 18, 225, 173, 137, 98,
				125, 41, 207, 38, 42, 124, 195, 51, 2, 114,
				224, 164, 242, 47, 251, 119, 109, 166, 148, 76,
				150, 91, 244, 237, 150, 49, 82, 95, 208, 151,
				209, 91, 52, 189, 22, 248, 84, 154, 156, 70,
				97, 243, 226, 169, 50, 79, 160, 15, 31, 125,
				178, 140, 66, 79, 63, 206, 178, 50, 84, 170,
				32, 15, 236, 224, 194, 183, 9, 214, 129, 5,
				234, 80, 186, 208, 90, 137, 109, 27, 232, 133,
				247, 25, 192, 64, 13, 168, 144, 87, 32, 51,
				8, 84, 21, 236, 255, 59, 222, 11, 233, 155,
				165, 0, 239, 35, 63, 143, 188, 9, 249, 54,
				228, 110, 228, 245, 200, 145, 10, 191, 207, 74,
				108, 39, 32, 215, 34, 239, 139, 252, 221, 190,
				0, 111, 32, 247, 33, 223, 129, 124, 49, 242,
				58, 100, 29, 246, 213, 163, 60, 18, 227, 210,
				144, 103, 41, 240, 91, 72, 132, 92, 134, 60,
				44, 243, 51, 33, 45, 252, 117, 34, 147, 169,
				148, 83, 2, 174, 222, 144, 103, 120, 210, 117,
				200, 185, 245, 222, 37, 249, 247, 232, 173, 247,
				188, 242, 227, 229, 98, 10, 48, 159, 140, 42,
				179, 157, 197, 159, 228, 107, 77, 117, 108, 213,
				89, 22, 155, 211, 98, 59, 131, 169, 114, 217,
				221, 206, 58, 83, 85, 189, 213, 90, 229, 106,
				118, 85, 89, 45, 181, 85, 22, 245, 202, 180,
				170, 74, 139, 77, 79, 28, 146, 29, 245, 181,
				64, 168, 39, 10, 96, 131, 20, 128, 28, 39,
				162, 143, 10, 210, 175, 127, 79, 83, 240, 3,
				50, 192, 47, 247, 159, 24, 74, 65, 113, 232,
				175, 247, 223, 138, 125, 119, 34, 255, 155, 84,
				252, 89, 246, 147, 15, 197, 62, 147, 7, 255,
				38, 9, 7, 69, 184, 109, 58, 6, 30, 39,
				25, 225, 93, 178, 217, 125, 48, 23, 125, 20,
				158, 137, 62, 98, 223, 236, 19, 171, 69, 183,
				10, 71, 44, 156, 219, 44, 52, 38, 94, 190,
				149, 70, 81, 61, 182, 125, 18, 175, 180, 229,
				251, 66, 128, 28, 184, 15, 19, 101, 0, 116,
				144, 45, 65, 17, 248, 157, 249, 143, 159, 129,
				101, 84, 129, 61, 207, 100, 53, 177, 166, 60,
				211, 58, 75, 157, 9, 224, 240, 128, 197, 208,
				220, 84, 107, 183, 90, 234, 10, 45, 182, 70,
				0, 115, 136, 158, 181, 22, 216, 44, 108, 185,
				205, 82, 103, 175, 55, 25, 88, 178, 93, 209,
				46, 42, 106, 42, 183, 53, 25, 29, 5, 118,
				131, 195, 72, 16, 246, 136, 244, 154, 156, 188,
				26, 189, 230, 132, 2, 67, 153, 70, 95, 147,
				91, 158, 159, 143, 162, 124, 181, 54, 71, 143,
				21, 253, 74, 175, 65, 91, 162, 47, 131, 175,
				127, 165, 183, 176, 164, 248, 4, 128, 18, 28,
				171, 40, 104, 164, 171, 49, 215, 134, 213, 246,
				38, 7, 73, 87, 111, 58, 211, 109, 114, 177,
				0, 241, 104, 93, 237, 52, 25, 23, 86, 112,
				224, 156, 61, 80, 171, 84, 116, 146, 169, 204,
				82, 215, 184, 218, 238, 182, 177, 96, 99, 237,
				174, 70, 167, 205, 154, 108, 58, 27, 251, 42,
				64, 200, 68, 135, 89, 205, 166, 126, 114, 176,
				197, 159, 46, 84, 6, 219, 72, 146, 228, 142,
				211, 23, 148, 105, 130, 3, 225, 52, 191, 109,
				94, 104, 117, 176, 109, 182, 190, 104, 208, 26,
				173, 39, 152, 216, 92, 183, 43, 207, 200, 26,
				115, 155, 75, 26, 26, 92, 38, 22, 150, 17,
				187, 97, 177, 93, 155, 83, 152, 140, 247, 4,
				188, 64, 214, 166, 220, 134, 87, 72, 61, 220,
				65, 157, 100, 202, 117, 159, 177, 218, 108, 170,
				107, 212, 156, 13, 127, 27, 81, 32, 199, 119,
				12, 64, 235, 66, 59, 133, 111, 230, 23, 236,
				17, 180, 255, 14, 221, 169, 0, 168, 10, 89,
				4, 136, 182, 20, 32, 243, 108, 128, 26, 124,
				107, 64, 143, 173, 2, 40, 129, 98, 212, 11,
				240, 157, 143, 109, 66, 253, 244, 87, 51, 4,
				79, 132, 28, 10, 16, 144, 199, 131, 159, 104,
				124, 68, 11, 176, 159, 66, 3, 133, 120, 44,
				56, 103, 191, 53, 249, 40, 173, 96, 130, 2,
				212, 26, 192, 142, 62, 215, 8, 62, 12, 164,
				128, 10, 31, 34, 107, 73, 29, 112, 34, 196,
				162, 125, 53, 250, 52, 225, 99, 66, 127, 22,
				92, 194, 247, 193, 140, 154, 18, 109, 118, 168,
				71, 180, 6, 100, 19, 182, 148, 144, 139, 35,
				229, 161, 180, 162, 165, 78, 136, 112, 225, 155,
				80, 22, 200, 2, 88, 14, 48, 98, 79, 51,
				20, 163, 36, 184, 132, 74, 208, 74, 252, 11,
				49, 178, 22, 81, 154, 49, 50, 25, 189, 157,
				152, 49, 161, 12, 136, 0, 42, 144, 123, 30,
				178, 11, 234, 72, 77, 24, 199, 226, 219, 142,
				177, 72, 139, 190, 171, 72, 248, 22, 7, 197,
				86, 32, 59, 193, 21, 20, 163, 194, 145, 86,
				32, 51, 200, 169, 194, 88, 82, 160, 132, 249,
				97, 137, 47, 74, 35, 88, 231, 178, 93, 60,
				6, 70, 185, 132, 124, 9, 221, 8, 199, 97,
				108, 33, 250, 157, 65, 162, 132, 138, 29, 216,
				139, 153, 162, 197, 140, 136, 240, 11, 54, 37,
				44, 71, 235, 17, 40, 87, 0, 131, 79, 58,
				28, 61, 219, 90, 9, 202, 223, 156, 155, 100,
				236, 207, 1, 43, 62, 202, 32, 60, 151, 160,
				153, 80, 146, 252, 215, 145, 213, 65, 79, 66,
				185, 66, 109, 37, 179, 190, 22, 127, 109, 129,
				185, 177, 253, 161, 26, 87, 8, 107, 161, 3,
				167, 176, 254, 110, 92, 7, 118, 110, 118, 126,
				117, 13, 82, 64, 188, 48, 102, 193, 74, 44,
				94, 135, 60, 100, 10, 189, 140, 224, 92, 188,
				107, 145, 18, 129, 22, 118, 163, 83, 216, 79,
				46, 236, 55, 206, 219, 11, 75, 232, 7, 105,
				248, 155, 200, 129, 124, 101, 202, 77, 41, 119,
				167, 60, 154, 242, 92, 202, 33, 169, 174, 84,
				62, 85, 154, 22, 151, 182, 54, 237, 236, 180,
				206, 180, 193, 180, 229, 233, 15, 167, 143, 166,
				127, 158, 190, 55, 125, 38, 189, 240, 216, 53,
				25, 245, 25, 214, 12, 119, 198, 198, 140, 59,
				50, 122, 51, 118, 100, 188, 147, 241, 73, 198,
				174, 140, 201, 140, 144, 204, 165, 153, 9, 153,
				169, 153, 250, 204, 135, 51, 187, 87, 125, 188,
				106, 201, 113, 7, 29, 23, 157, 117, 98, 150,
				37, 203, 158, 213, 146, 117, 97, 214, 83, 199,
				15, 144, 195, 44, 7, 80, 162, 88, 197, 100,
				51, 85, 204, 233, 204, 121, 76, 43, 195, 169,
				110, 84, 221, 170, 186, 79, 181, 73, 213, 175,
				130, 127, 152, 200, 255, 164, 65, 36, 2, 166,
				253, 224, 254, 176, 136, 35, 59, 181, 157, 19,
				82, 42, 92, 212, 211, 126, 240, 253, 104, 186,
				71, 68, 81, 170, 40, 38, 34, 44, 52, 105,
				159, 16, 209, 1, 161, 192, 152, 195, 34, 147,
				194, 40, 154, 106, 79, 23, 81, 116, 207, 169,
				204, 201, 140, 58, 200, 34, 99, 150, 133, 80,
				208, 163, 188, 229, 224, 214, 56, 88, 37, 60,
				37, 80, 11, 46, 176, 11, 11, 204, 34, 103,
				145, 135, 137, 15, 194, 164, 21, 61, 69, 142,
				216, 43, 154, 110, 120, 181, 212, 168, 123, 244,
				236, 27, 198, 142, 121, 53, 198, 241, 64, 79,
				123, 108, 45, 211, 30, 210, 207, 180, 139, 110,
				239, 9, 17, 81, 34, 81, 20, 5, 72, 84,
				116, 122, 98, 252, 93, 140, 52, 144, 44, 21,
				138, 105, 213, 9, 89, 134, 148, 211, 97, 75,
				69, 185, 26, 85, 44, 179, 63, 81, 34, 151,
				202, 79, 176, 218, 107, 241, 179, 98, 57, 195,
				166, 180, 173, 59, 218, 101, 84, 237, 199, 40,
				72, 87, 212, 82, 105, 73, 45, 249, 155, 84,
				232, 90, 157, 163, 138, 103, 14, 33, 246, 144,
				165, 49, 65, 33, 243, 92, 152, 131, 247, 151,
				50, 233, 204, 177, 43, 82, 84, 76, 42, 147,
				154, 122, 42, 170, 43, 131, 84, 198, 16, 148,
				196, 137, 58, 213, 1, 76, 140, 31, 113, 73,
				177, 221, 105, 105, 118, 55, 90, 148, 69, 5,
				167, 228, 156, 154, 115, 82, 129, 106, 57, 115,
				88, 160, 128, 8, 106, 191, 88, 179, 165, 217,
				78, 56, 187, 206, 217, 236, 98, 141, 214, 38,
				163, 179, 49, 217, 98, 107, 176, 51, 237, 84,
				66, 80, 177, 88, 45, 132, 180, 83, 50, 64,
				123, 164, 168, 157, 162, 96, 235, 83, 111, 197,
				108, 55, 237, 56, 127, 213, 133, 211, 215, 70,
				228, 62, 110, 30, 217, 211, 111, 14, 127, 208,
				97, 60, 200, 245, 193, 189, 239, 237, 125, 250,
				221, 180, 125, 215, 87, 157, 222, 252, 180, 169,
				94, 118, 212, 190, 119, 84, 61, 104, 24, 57,
				231, 26, 42, 250, 130, 161, 243, 251, 219, 186,
				143, 190, 175, 236, 164, 66, 201, 219, 69, 219,
				207, 191, 121, 71, 249, 75, 35, 231, 126, 124,
				239, 243, 5, 199, 60, 153, 144, 16, 47, 181,
				189, 180, 49, 254, 189, 198, 200, 227, 55, 244,
				28, 161, 127, 54, 71, 116, 195, 37, 253, 135,
				94, 118, 235, 154, 94, 197, 79, 63, 86, 157,
				211, 120, 253, 208, 1, 159, 125, 242, 198, 200,
				244, 238, 207, 15, 186, 212, 118, 71, 82, 93,
				250, 151, 231, 78, 216, 251, 141, 233, 247, 173,
				183, 68, 30, 179, 117, 60, 229, 201, 172, 8,
				223, 221, 173, 77, 187, 15, 184, 193, 123, 206,
				229, 234, 198, 157, 134, 240, 195, 47, 124, 105,
				160, 232, 206, 143, 34, 47, 155, 174, 249, 186,
				114, 63, 95, 221, 231, 175, 148, 126, 47, 189,
				216, 123, 244, 166, 193, 75, 7, 52, 151, 105,
				91, 207, 215, 31, 189, 241, 187, 38, 39, 245,
				233, 246, 235, 70, 85, 51, 73, 119, 220, 115,
				251, 177, 27, 50, 134, 46, 60, 231, 245, 31,
				15, 239, 153, 190, 232, 253, 165, 3, 223, 180,
				254, 220, 156, 153, 199, 67, 153, 199, 113, 124,
				134, 49, 227, 74, 24, 20, 57, 147, 179, 240,
				140, 123, 100, 43, 183, 220, 87, 114, 38, 199,
				140, 228, 24, 198, 168, 196, 98, 36, 75, 142,
				144, 155, 228, 40, 103, 238, 51, 81, 27, 34,
				119, 114, 134, 68, 108, 36, 119, 185, 195, 206,
				80, 155, 218, 246, 120, 237, 190, 246, 120, 230,
				143, 153, 239, 243, 251, 61, 199, 60, 191, 207,
				251, 243, 124, 191, 191, 223, 102, 196, 102, 166,
				124, 231, 222, 147, 25, 55, 42, 169, 109, 74,
				200, 123, 42, 106, 54, 72, 38, 85, 204, 43,
				199, 73, 8, 130, 3, 167, 225, 241, 128, 55,
				64, 7, 165, 56, 235, 175, 10, 246, 95, 145,
				7, 131, 33, 16, 82, 18, 8, 45, 64, 67,
				24, 1, 182, 189, 96, 240, 214, 118, 60, 7,
				112, 16, 99, 110, 200, 126, 128, 201, 135, 225,
				153, 233, 59, 117, 94, 245, 59, 5, 227, 230,
				27, 134, 222, 75, 243, 194, 210, 90, 250, 201,
				0, 146, 216, 129, 3, 34, 10, 0, 128, 112,
				242, 145, 100, 65, 127, 254, 115, 56, 156, 179,
				140, 136, 136, 53, 214, 65, 216, 118, 91, 25,
				174, 4, 57, 8, 59, 161, 112, 34, 159, 213,
				33, 76, 104, 253, 74, 146, 164, 196, 193, 81,
				119, 208, 202, 36, 47, 213, 105, 206, 219, 27,
				83, 115, 145, 55, 89, 209, 156, 230, 24, 9,
				251, 240, 48, 185, 199, 166, 105, 149, 0, 205,
				49, 165, 38, 9, 177, 227, 130, 49, 235, 23,
				108, 151, 20, 120, 173, 110, 93, 142, 219, 20,
				153, 210, 204, 218, 100, 152, 235, 51, 146, 139,
				243, 189, 119, 69, 233, 146, 234, 122, 79, 22,
				221, 105, 187, 1, 7, 67, 222, 146, 103, 43,
				222, 13, 206, 204, 25, 167, 178, 102, 95, 226,
				11, 163, 67, 214, 215, 172, 131, 86, 60, 158,
				153, 92, 123, 201, 234, 112, 180, 82, 74, 98,
				206, 222, 160, 131, 79, 89, 184, 107, 79, 173,
				77, 174, 134, 182, 64, 227, 170, 221, 97, 129,
				173, 168, 211, 0, 165, 231, 30, 164, 5, 15,
				24, 59, 74, 191, 197, 115, 212, 115, 34, 224,
				204, 202, 163, 205, 179, 117, 170, 182, 150, 62,
				99, 77, 20, 197, 234, 122, 121, 116, 246, 224,
				133, 195, 138, 246, 190, 151, 113, 193, 153, 148,
				51, 22, 161, 183, 150, 176, 103, 216, 218, 76,
				220, 253, 106, 156, 202, 5, 45, 77, 146, 22,
				176, 75, 203, 69, 195, 199, 28, 197, 209, 102,
				249, 58, 246, 55, 154, 77, 132, 70, 248, 246,
				121, 21, 70, 200, 46, 86, 157, 76, 66, 8,
				57, 40, 108, 153, 240, 210, 107, 165, 203, 104,
				54, 166, 137, 239, 201, 182, 57, 89, 35, 202,
				127, 58, 119, 21, 143, 150, 151, 93, 39, 0,
				60, 78, 0, 184, 253, 35, 192, 59, 142, 189,
				234, 157, 45, 204, 254, 53, 192, 167, 255, 52,
				192, 132, 65, 221, 1, 24, 166, 135, 193, 224,
				136, 232, 30, 0, 152, 119, 64, 163, 223, 213,
				153, 216, 248, 9, 90, 49, 132, 40, 66, 18,
				33, 6, 16, 54, 2, 180, 8, 113, 0, 33,
				42, 133, 64, 108, 135, 128, 11, 145, 197, 157,
				195, 57, 190, 58, 124, 251, 135, 142, 37, 22,
				231, 132, 194, 186, 18, 47, 196, 6, 28, 216,
				185, 52, 195, 183, 154, 127, 239, 166, 119, 255,
				185, 63, 68, 186, 122, 22, 152, 7, 230, 197,
				179, 2, 81, 23, 53, 149, 51, 66, 194, 54,
				242, 180, 67, 155, 248, 247, 85, 26, 143, 102,
				70, 80, 42, 145, 10, 94, 187, 254, 19, 133,
				206, 41, 61, 252, 92, 90, 120, 225, 115, 122,
				65, 252, 82, 13, 41, 171, 172, 239, 143, 216,
				214, 249, 12, 80, 239, 162, 65, 34, 165, 202,
				76, 124, 148, 92, 68, 104, 114, 46, 197, 119,
				157, 111, 83, 113, 185, 183, 152, 57, 41, 202,
				190, 79, 63, 35, 113, 75, 27, 121, 158, 180,
				244, 90, 46, 71, 180, 149, 253, 190, 123, 57,
				63, 156, 237, 54, 119, 124, 30, 209, 248, 20,
				158, 215, 21, 240, 92, 39, 83, 49, 28, 82,
				48, 169, 50, 105, 81, 52, 13, 121, 212, 11,
				246, 228, 90, 169, 115, 225, 254, 96, 175, 129,
				82, 20, 171, 13, 97, 123, 253, 12, 205, 170,
				39, 160, 92, 7, 29, 167, 146, 152, 51, 6,
				163, 65, 129, 237, 137, 163, 30, 151, 178, 245,
				95, 205, 220, 149, 136, 26, 223, 48, 239, 149,
				143, 140, 234, 100, 113, 66, 156, 82, 233, 125,
				155, 219, 160, 113, 203, 192, 229, 10, 203, 190,
				39, 171, 227, 109, 70, 224, 247, 146, 15, 243,
				142, 27, 223, 66, 121, 158, 128, 153, 185, 114,
				138, 167, 210, 215, 158, 124, 127, 238, 132, 29,
				123, 224, 240, 213, 55, 50, 229, 49, 214, 247,
				85, 10, 144, 246, 114, 32, 175, 69, 245, 10,
				239, 81, 174, 143, 72, 199, 1, 248, 232, 47,
				1, 6, 67, 129, 189, 196, 24, 78, 140, 201,
				0, 82, 194, 23, 192, 70, 220, 65, 3, 97,
				130, 48, 24, 250, 77, 149, 186, 220, 25, 183,
				238, 27, 208, 133, 244, 172, 189, 158, 83, 213,
				59, 217, 6, 136, 237, 240, 124, 4, 16, 4,
				248, 147, 121, 147, 15, 249, 31, 252, 109, 158,
				137, 131, 74, 36, 249, 11, 151, 56, 235, 129,
				102, 81, 218, 19, 211, 20, 168, 35, 242, 61,
				164, 85, 60, 115, 235, 195, 134, 250, 183, 80,
				55, 19, 17, 182, 85, 227, 45, 30, 47, 61,
				46, 33, 251, 34, 153, 116, 218, 48, 126, 85,
				81, 74, 143, 9, 64, 53, 116, 104, 165, 238,
				13, 41, 47, 43, 179, 13, 29, 215, 152, 10,
				137, 122, 240, 186, 45, 227, 224, 37, 220, 221,
				222, 21, 92, 92, 241, 114, 91, 16, 92, 186,
				59, 186, 37, 228, 194, 99, 17, 233, 34, 153,
				60, 203, 7, 84, 63, 196, 141, 114, 89, 217,
				112, 35, 67, 252, 150, 174, 134, 44, 95, 220,
				42, 144, 134, 206, 121, 61, 82, 181, 40, 157,
				49, 226, 133, 162, 201, 14, 226, 34, 174, 100,
				12, 148, 174, 70, 140, 78, 212, 50, 31, 17,
				182, 158, 189, 207, 241, 114, 117, 25, 166, 130,
				222, 3, 11, 140, 241, 90, 17, 59, 146, 212,
				156, 199, 240, 221, 60, 235, 203, 180, 121, 247,
				252, 39, 17, 215, 35, 93, 234, 58, 20, 31,
				27, 12, 176, 151, 82, 81, 102, 201, 167, 229,
				247, 40, 32, 217, 102, 174, 223, 136, 53, 50,
				125, 224, 148, 72, 181, 104, 61, 117, 38, 174,
				102, 213, 96, 13, 12, 83, 238, 223, 83, 96,
				115, 168, 61, 180, 3, 202, 166, 45, 155, 106,
				125, 81, 201, 244, 118, 149, 69, 129, 207, 125,
				196, 48, 149, 1, 216, 140, 137, 13, 171, 213,
				45, 104, 167, 210, 113, 0, 237, 101, 1, 118,
				154, 151, 8, 183, 31, 22, 222, 44, 80, 234,
				99, 226, 43, 125, 28, 84, 211, 175, 79, 64,
				125, 133, 128, 250, 240, 110, 212, 193, 228, 207,
				45, 89, 123, 254, 77, 212, 145, 72, 2, 219,
				210, 8, 209, 111, 162, 142, 199, 255, 233, 43,
				243, 0, 220, 59, 87, 102, 213, 193, 218, 17,
				94, 198, 238, 31, 83, 1, 78, 29, 55, 43,
				7, 59, 215, 115, 132, 89, 1, 226, 237, 0,
				128, 240, 206, 237, 240, 237, 58, 195, 239, 29,
				241, 135, 236, 167, 38, 184, 9, 102, 192, 43,
				41, 92, 14, 237, 127, 153, 2, 181, 18, 160,
				79, 26, 76, 114, 115, 157, 120, 105, 120, 218,
				46, 26, 110, 168, 189, 240, 164, 10, 108, 52,
				205, 235, 143, 165, 58, 187, 200, 252, 248, 20,
				233, 233, 132, 123, 180, 141, 41, 93, 184, 19,
				91, 14, 160, 137, 215, 250, 82, 155, 229, 253,
				238, 199, 42, 244, 164, 223, 224, 143, 220, 162,
				17, 211, 75, 111, 117, 110, 83, 51, 183, 122,
				223, 97, 123, 166, 42, 70, 13, 214, 84, 245,
				34, 68, 165, 176, 117, 30, 101, 122, 21, 92,
				160, 110, 63, 212, 26, 42, 188, 168, 51, 145,
				150, 120, 147, 55, 214, 94, 210, 90, 142, 212,
				89, 113, 206, 226, 21, 143, 155, 187, 206, 247,
				22, 41, 208, 84, 101, 234, 83, 81, 42, 12,
				157, 225, 202, 254, 231, 226, 198, 249, 222, 71,
				78, 197, 203, 233, 46, 71, 119, 93, 14, 30,
				81, 140, 57, 231, 66, 218, 103, 132, 169, 46,
				161, 149, 159, 217, 131, 118, 83, 14, 82, 140,
				126, 6, 128, 232, 97, 214, 203, 138, 9, 96,
				231, 38, 67, 76, 162, 81, 140, 32, 223, 190,
				32, 97, 23, 61, 71, 229, 156, 140, 195, 123,
				189, 153, 84, 109, 158, 232, 173, 65, 247, 129,
				96, 184, 94, 165, 98, 234, 197, 75, 81, 174,
				19, 148, 51, 117, 195, 216, 96, 118, 186, 108,
				246, 251, 103, 218, 86, 75, 64, 51, 174, 213,
				173, 127, 145, 125, 70, 93, 119, 22, 79, 106,
				227, 213, 254, 214, 190, 58, 25, 120, 124, 227,
				37, 33, 111, 205, 127, 143, 253, 100, 121, 225,
				138, 11, 25, 242, 46, 254, 39, 52, 149, 53,
				77, 121, 11, 10, 112, 171, 199, 3, 51, 61,
				60, 203, 231, 235, 99, 134, 125, 145, 113, 55,
				21, 54, 207, 250, 74, 202, 151, 154, 254, 16,
				178, 104, 254, 254, 254, 86, 22, 173, 98, 166,
				75, 103, 195, 252, 65, 185, 133, 248, 219, 119,
				166, 172, 215, 126, 122, 139, 82, 99, 6, 110,
				6, 53, 148, 109, 12, 30, 31, 113, 216, 232,
				214, 99, 128, 125, 112, 60, 212, 82, 220, 130,
				79, 253, 81, 239, 29, 223, 61, 199, 36, 232,
				119, 125, 153, 39, 73, 68, 237, 26, 200, 228,
				35, 159, 222, 31, 189, 159, 69, 94, 123, 67,
				231, 192, 128, 159, 227, 143, 149, 236, 122, 177,
				168, 68, 248, 133, 234, 226, 110, 91, 1, 17,
				14, 143, 236, 35, 186, 201, 27, 87, 181, 121,
				101, 85, 211, 240, 124, 233, 155, 51, 222, 151,
				226, 106, 148, 218, 111, 114, 232, 121, 245, 70,
				5, 109, 140, 60, 229, 172, 205, 247, 210, 77,
				21, 65, 31, 205, 17, 106, 23, 10, 13, 214,
				252, 126, 63, 146, 139, 127, 125, 48, 207, 17,
				133, 127, 58, 220, 187, 95, 125, 188, 116, 25,
				215, 235, 247, 90, 79, 240, 238, 209, 115, 27,
				71, 213, 42, 192, 6, 126, 225, 162, 247, 163,
				158, 133, 90, 3, 233, 73, 109, 220, 209, 155,
				57, 194, 21, 231, 131, 112, 131, 67, 92, 41,
				12, 53, 235, 15, 245, 243, 105, 1, 95, 200,
				94, 2, 251, 203, 95, 176, 79, 27, 241, 214,
				55, 234, 107, 246, 255, 131, 183, 237, 78, 214,
				46, 10, 72, 0, 200, 47, 92, 225, 99, 8,
				96, 16, 112, 128, 110, 231, 76, 148, 159, 207,
				132, 56, 12, 8, 236, 220, 231, 193, 93, 167,
				55, 176, 115, 68, 113, 234, 227, 44, 29, 157,
				137, 56, 203, 185, 225, 206, 17, 50, 123, 156,
				59, 177, 243, 238, 116, 158, 21, 71, 232, 231,
				186, 211, 13, 141, 57, 177, 75, 106, 214, 24,
				199, 63, 180, 128, 214, 230, 3, 174, 108, 25,
				91, 47, 107, 243, 84, 124, 31, 129, 22, 194,
				101, 105, 125, 122, 221, 94, 167, 14, 23, 92,
				91, 177, 119, 173, 81, 93, 125, 238, 231, 59,
				128, 121, 144, 247, 208, 80, 47, 78, 34, 191,
				167, 102, 244, 85, 71, 87, 89, 198, 13, 29,
				44, 196, 231, 125, 163, 61, 227, 187, 15, 158,
				71, 141, 180, 47, 190, 58, 9, 177, 10, 138,
				11, 179, 34, 189, 26, 177, 73, 109, 255, 82,
				174, 174, 40, 196, 136, 61, 224, 30, 173, 37,
				164, 164, 225, 74, 98, 77, 33, 83, 79, 181,
				77, 20, 108, 117, 53, 221, 4, 25, 174, 39,
				115, 132, 111, 229, 233, 96, 39, 75, 188, 241,
				85, 218, 82, 144, 37, 207, 209, 239, 224, 251,
				158, 208, 105, 92, 72, 223, 212, 176, 108, 49,
				126, 86, 167, 0, 112, 153, 49, 158, 160, 208,
				118, 29, 121, 20, 117, 27, 133, 190, 62, 208,
				153, 9, 208, 173, 62, 190, 11, 167, 211, 201,
				212, 46, 55, 224, 152, 202, 143, 99, 65, 38,
				79, 243, 147, 210, 121, 84, 22, 20, 173, 251,
				29, 43, 91, 193, 169, 7, 38, 196, 81, 198,
				143, 175, 4, 106, 86, 4, 54, 150, 220, 1,
				137, 133, 161, 234, 228, 207, 182, 85, 41, 200,
				75, 195, 187, 135, 76, 237, 83, 181, 58, 142,
				171, 33, 149, 108, 141, 192, 21, 220, 250, 179,
				82, 48, 52, 216, 251, 161, 45, 5, 25, 213,
				248, 195, 143, 22, 144, 5, 224, 211, 191, 180,
				0, 152, 207, 46, 226, 243, 217, 12, 168, 212,
				203, 237, 14, 7, 31, 18, 87, 192, 155, 239,
				201, 201, 17, 53, 225, 4, 142, 239, 16, 47,
				13, 72, 2, 226, 201, 162, 201, 128, 191, 240,
				239, 17, 191, 35, 170, 79, 154, 218, 166, 31,
				78, 60, 1, 15, 132, 26, 160, 132, 146, 31,
				134, 18, 36, 11, 35, 37, 223, 109, 9, 223,
				76, 50, 190, 101, 9, 54, 9, 176, 166, 187,
				45, 114, 236, 82, 179, 24, 241, 196, 210, 137,
				38, 213, 213, 254, 201, 181, 229, 125, 194, 102,
				185, 204, 71, 93, 233, 166, 76, 6, 54, 60,
				184, 16, 150, 63, 70, 232, 154, 22, 162, 11,
				60, 109, 250, 54, 105, 75, 92, 50, 171, 146,
				220, 164, 18, 45, 48, 11, 253, 16, 169, 138,
				169, 31, 73, 95, 25, 150, 95, 50, 142, 50,
				98, 187, 236, 121, 108, 127, 175, 134, 104, 245,
				42, 251, 56, 183, 146, 247, 208, 112, 8, 202,
				215, 84, 105, 195, 166, 30, 83, 219, 59, 73,
				47, 199, 70, 141, 60, 179, 20, 189, 82, 219,
				154, 196, 177, 165, 27, 96, 119, 33, 54, 40,
				180, 132, 47, 192, 151, 165, 71, 246, 114, 78,
				129, 86, 249, 177, 137, 46, 241, 103, 143, 110,
				35, 245, 101, 139, 112, 30, 140, 82, 128, 65,
				250, 219, 123, 135, 90, 68, 200, 213, 21, 108,
				230, 213, 242, 67, 18, 39, 12, 28, 138, 6,
				6, 143, 22, 54, 28, 244, 187, 117, 42, 20,
				155, 89, 148, 185, 52, 183, 69, 218, 24, 253,
				54, 199, 215, 181, 255, 129, 146, 33, 179, 228,
				70, 165, 67, 149, 203, 92, 103, 67, 237, 133,
				222, 177, 170, 87, 41, 233, 88, 80, 123, 117,
				17, 104, 58, 34, 27, 153, 67, 209, 225, 132,
				212, 247, 23, 236, 58, 170, 235, 26, 91, 110,
				240, 38, 70, 40, 238, 226, 12, 253, 49, 73,
				121, 30, 111, 130, 37, 112, 18, 82, 2, 242,
				175, 211, 129, 67, 170, 95, 61, 173, 255, 197,
				91, 153, 232, 17, 68, 83, 16, 253, 202, 35,
				16, 192, 118, 248, 175, 76, 47, 252, 161, 47,
				20, 20, 46, 248, 128, 156, 219, 166, 106, 131,
				75, 98, 166, 235, 41, 130, 121, 49, 129, 47,
				109, 141, 241, 237, 67, 12, 112, 141, 154, 66,
				165, 87, 250, 110, 114, 107, 48, 73, 76, 37,
				157, 51, 127, 164, 167, 35, 182, 26, 198, 200,
				131, 146, 161, 216, 186, 58, 184, 230, 113, 70,
				234, 7, 233, 177, 6, 117, 114, 159, 216, 164,
				107, 173, 70, 20, 36, 63, 214, 130, 79, 70,
				27, 5, 4, 242, 26, 47, 58, 214, 223, 123,
				180, 190, 159, 103, 186, 207, 74, 171, 137, 210,
				193, 220, 121, 182, 173, 51, 65, 128, 138, 162,
				126, 105, 166, 140, 169, 75, 214, 32, 180, 197,
				124, 56, 127, 224, 5, 55, 133, 168, 135, 40,
				158, 201, 138, 18, 218, 124, 185, 93, 156, 171,
				12, 35, 89, 86, 216, 214, 28, 47, 32, 113,
				226, 216, 77, 35, 207, 173, 92, 223, 216, 181,
				138, 238, 192, 38, 137, 23, 205, 173, 167, 161,
				205, 9, 99, 15, 35, 51, 125, 98, 45, 46,
				70, 207, 149, 141, 231, 224, 220, 79, 112, 165,
				91, 191, 121, 10, 11, 175, 190, 42, 93, 146,
				23, 125, 55, 101, 139, 193, 181, 255, 142, 82,
				133, 166, 127, 155, 160, 236, 108, 215, 75, 126,
				26, 183, 172, 204, 0, 120, 205, 152, 231, 217,
				27, 11, 60, 45, 164, 126, 49, 175, 181, 202,
				110, 9, 245, 148, 6, 137, 203, 230, 116, 75,
				203, 60, 249, 174, 174, 112, 53, 242, 83, 106,
				80, 10, 224, 139, 126, 149, 26, 208, 255, 146,
				26, 144, 3, 80, 194, 23, 9, 24, 244, 217,
				43, 254, 126, 165, 239, 76, 16, 15, 102, 123,
				176, 136, 46, 241, 171, 137, 6, 16, 120, 183,
				71, 124, 51, 25, 249, 150, 71, 208, 117, 153,
				95, 123, 255, 221, 158, 155, 94, 218, 25, 32,
				23, 75, 42, 195, 98, 141, 108, 231, 145, 162,
				142, 161, 134, 0, 71, 182, 64, 207, 195, 89,
				202, 63, 5, 113, 132, 106, 212, 160, 163, 194,
				57, 70, 53, 145, 106, 163, 7, 156, 203, 21,
				157, 6, 161, 215, 52, 29, 121, 38, 98, 233,
				7, 171, 146, 95, 248, 107, 229, 93, 199, 195,
				201, 78, 132, 153, 24, 240, 8, 68, 170, 53,
				205, 163, 74, 102, 161, 238, 253, 227, 205, 254,
				115, 144, 198, 253, 131, 51, 216, 98, 78, 240,
				105, 213, 151, 163, 98, 35, 71, 90, 19, 97,
				222, 145, 42, 124, 33, 1, 33, 226, 212, 233,
				158, 102, 156, 143, 190, 135, 21, 187, 218, 219,
				4, 243, 62, 75, 232, 157, 112, 71, 165, 103,
				134, 167, 224, 252, 5, 201, 72, 124, 31, 249,
				121, 46, 184, 183, 202, 10, 30, 222, 27, 39,
				192, 62, 25, 124, 141, 165, 240, 252, 100, 42,
				90, 154, 6, 53, 146, 219, 87, 223, 200, 17,
				16, 130, 188, 48, 179, 207, 183, 14, 30, 18,
				211, 102, 45, 55, 126, 121, 177, 164, 204, 129,
				177, 86, 26, 94, 124, 131, 202, 10, 26, 240,
				98, 68, 209, 155, 54, 75, 231, 80, 208, 217,
				65, 178, 101, 211, 59, 12, 69, 30, 134, 88,
				170, 232, 201, 135, 31, 200, 156, 195, 80, 73,
				100, 172, 183, 95, 192, 242, 213, 79, 15, 213,
				35, 109, 143, 62, 198, 1, 190, 100, 222, 4,
				143, 176, 221, 241, 8, 74, 75, 42, 111, 123,
				16, 113, 99, 254, 218, 34, 188, 119, 225, 119,
				74, 255, 211, 251, 154, 28, 78, 121, 218, 114,
				27, 97, 28, 198, 233, 83, 173, 0, 35, 212,
				10, 40, 27, 71, 140, 147, 13, 130, 29, 96,
				221, 161, 148, 81, 211, 206, 26, 139, 113, 197,
				160, 9, 85, 2, 6, 235, 140, 193, 90, 226,
				236, 8, 71, 8, 0, 124, 59, 72, 114, 238,
				110, 183, 65, 113, 26, 162, 176, 118, 104, 59,
				107, 98, 175, 157, 212, 100, 219, 31, 36, 1,
				9, 81, 49, 132, 20, 0, 72, 32, 136, 254,
				240, 49, 68, 16, 195, 127, 170, 164, 249, 99,
				79, 232, 167, 121, 29, 23, 210, 150, 54, 170,
				125, 205, 107, 125, 62, 232, 10, 143, 253, 164,
				42, 199, 188, 96, 193, 189, 40, 58, 72, 245,
				156, 117, 199, 43, 91, 116, 59, 235, 139, 246,
				3, 170, 135, 103, 66, 2, 67, 227, 243, 5,
				124, 80, 57, 111, 26, 132, 232, 155, 115, 88,
				53, 156, 97, 41, 223, 81, 2, 173, 140, 33,
				218, 43, 47, 186, 130, 117, 24, 89, 103, 117,
				102, 234, 132, 151, 63, 188, 209, 91, 8, 255,
				126, 216, 177, 91, 2, 8, 61, 163, 224, 186,
				32, 247, 166, 251, 252, 104, 81, 160, 140, 33,
				82, 26, 3, 149, 13, 176, 51, 21, 212, 124,
				156, 97, 80, 225, 16, 216, 146, 176, 42, 219,
				244, 211, 70, 224, 34, 189, 108, 196, 165, 232,
				158, 65, 156, 211, 13, 69, 203, 142, 144, 6,
				229, 34, 55, 26, 255, 9, 230, 107, 221, 142,
				14, 221, 151, 152, 162, 82, 223, 60, 161, 148,
				126, 187, 112, 8, 174, 237, 106, 112, 3, 126,
				85, 230, 64, 21, 187, 120, 105, 16, 89, 240,
				189, 139, 65, 133, 3, 28, 156, 225, 57, 20,
				250, 78, 97, 109, 30, 82, 184, 123, 206, 42,
				225, 7, 117, 173, 153, 220, 243, 10, 243, 228,
				120, 201, 159, 48, 31, 238, 80, 206, 86, 237,
				177, 17, 236, 189, 34, 190, 117, 68, 240, 145,
				3, 179, 130, 26, 236, 234, 120, 167, 231, 18,
				215, 185, 217, 201, 5, 189, 91, 222, 55, 194,
				218, 119, 60, 193, 23, 204, 77, 120, 34, 28,
				219, 178, 96, 227, 132, 80, 2, 4, 19, 128,
				66, 8, 63, 65, 128, 36, 148, 226, 227, 236,
				52, 35, 12, 66, 28, 113, 142, 79, 33, 121,
				200, 112, 71, 160, 150, 95, 111, 98, 45, 204,
				63, 221, 127, 157, 245, 122, 231, 195, 116, 70,
				48, 51, 9, 216, 137, 104, 30, 84, 219, 6,
				179, 237, 46, 254, 191, 83, 120, 124, 187, 68,
				96, 251, 229, 162, 12, 132, 73, 72, 118, 26,
				144, 194, 246, 218, 136, 43, 225, 163, 0, 146,
				251, 194, 43, 172, 214, 41, 185, 76, 189, 21,
				156, 232, 250, 41, 12, 116, 236, 23, 159, 69,
				187, 204, 39, 1, 167, 118, 172, 73, 11, 208,
				0, 212, 146, 85, 147, 149, 253, 21, 119, 89,
				147, 227, 39, 177, 18, 147, 73, 17, 103, 123,
				59, 226, 94, 130, 67, 97, 108, 220, 172, 113,
				174, 34, 191, 104, 153, 40, 229, 109, 37, 19,
				181, 244, 205, 9, 74, 194, 255, 132, 63, 118,
				56, 3, 243, 150, 61, 189, 149, 128, 176, 56,
				94, 29, 101, 192, 83, 239, 190, 122, 4, 65,
				170, 105, 99, 167, 110, 248, 220, 52, 6, 227,
				249, 110, 238, 233, 33, 148, 155, 180, 117, 44,
				220, 153, 9, 190, 156, 1, 158, 175, 231, 30,
				190, 234, 239, 64, 53, 231, 19, 105, 226, 54,
				225, 57, 198, 30, 81, 226, 250, 240, 81, 113,
				70, 1, 85, 231, 126, 191, 230, 22, 138, 52,
				179, 68, 237, 99, 5, 50, 137, 66, 7, 234,
				32, 179, 49, 82, 209, 152, 159, 166, 42, 239,
				158, 47, 238, 122, 37, 251, 50, 136, 103, 252,
				110, 149, 149, 18, 226, 197, 109, 74, 236, 216,
				129, 149, 75, 10, 224, 150, 74, 36, 195, 144,
				121, 88, 94, 159, 73, 251, 153, 38, 101, 188,
				174, 88, 119, 58, 141, 145, 109, 103, 31, 218,
				40, 128, 41, 97, 166, 144, 165, 41, 55, 28,
				238, 199, 213, 31, 239, 196, 57, 219, 15, 194,
				241, 87, 54, 90, 188, 211, 52, 175, 237, 231,
				177, 227, 49, 152, 84, 172, 170, 77, 16, 16,
				99, 237, 145, 141, 230, 248, 1, 255, 88, 191,
				87, 144, 79, 172, 75, 152, 217, 147, 220, 200,
				113, 64, 139, 57, 46, 107, 210, 226, 93, 252,
				205, 96, 93, 25, 19, 179, 106, 145, 44, 114,
				137, 4, 131, 140, 1, 9, 22, 61, 38, 49,
				113, 153, 52, 31, 200, 249, 141, 146, 43, 193,
				185, 97, 237, 212, 50, 168, 232, 41, 49, 173,
				253, 188, 21, 39, 239, 191, 197, 71, 108, 193,
				53, 69, 213, 185, 27, 196, 198, 203, 92, 236,
				25, 151, 18, 125, 94, 87, 149, 59, 60, 144,
				235, 38, 149, 113, 17, 99, 4, 233, 224, 142,
				210, 152, 175, 70, 103, 79, 135, 29, 203, 129,
				107, 248, 196, 241, 86, 73, 223, 121, 75, 34,
				25, 200, 86, 198, 249, 224, 74, 14, 237, 10,
				115, 82, 175, 181, 123, 87, 94, 161, 136, 165,
				228, 170, 154, 122, 166, 88, 3, 13, 86, 110,
				117, 190, 48, 204, 43, 79, 121, 118, 8, 233,
				225, 150, 44, 199, 96, 12, 97, 147, 175, 82,
				9, 168, 128, 211, 144, 173, 177, 205, 249, 132,
				108, 57, 215, 95, 236, 83, 31, 84, 166, 225,
				102, 193, 118, 63, 28, 218, 204, 206, 179, 51,
				146, 253, 96, 166, 60, 214, 141, 143, 15, 114,
				66, 25, 95, 100, 17, 189, 19, 150, 94, 102,
				115, 195, 102, 3, 166, 223, 152, 60, 196, 159,
				43, 232, 80, 47, 221, 129, 111, 139, 93, 60,
				193, 252, 230, 200, 62, 133, 219, 188, 22, 157,
				138, 128, 254, 24, 27, 73, 229, 109, 74, 17,
				166, 23, 151, 207, 242, 233, 201, 223, 207, 141,
				149, 226, 143, 25, 171, 120, 230, 78, 55, 74,
				27, 172, 218, 13, 152, 83, 82, 235, 13, 45,
				6, 76, 134, 176, 8, 217, 54, 190, 247, 97,
				240, 43, 191, 74, 221, 129, 230, 191, 83, 143,
				57, 100, 137, 240, 133, 212, 19, 82, 181, 26,
				18, 48, 24, 192, 254, 43, 105, 206, 23, 203,
				56, 187, 86, 129, 146, 245, 0, 218, 93, 43,
				73, 212, 8, 18, 0, 4, 48, 127, 174, 170,
				72, 17, 212, 187, 154, 33, 0, 247, 231, 38,
				8, 130, 0, 226, 162, 183, 195, 168, 229, 90,
				47, 227, 116, 165, 44, 36, 57, 241, 124, 114,
				211, 229, 1, 252, 87, 214, 9, 241, 5, 131,
				24, 246, 226, 181, 173, 217, 250, 206, 121, 180,
				162, 70, 127, 152, 49, 153, 177, 68, 132, 172,
				197, 141, 164, 68, 61, 189, 169, 123, 158, 45,
				246, 240, 140, 231, 244, 72, 19, 217, 109, 182,
				229, 32, 251, 62, 26, 37, 25, 155, 251, 30,
				156, 21, 7, 184, 151, 130, 198, 94, 235, 83,
				15, 46, 133, 53, 24, 38, 49, 14, 5, 38,
				132, 211, 110, 24, 224, 86, 74, 170, 146, 201,
				166, 172, 28, 163, 133, 188, 175, 164, 215, 166,
				174, 238, 181, 47, 175, 229, 66, 157, 252, 240,
				230, 161, 177, 126, 98, 127, 106, 166, 136, 189,
				117, 216, 21, 45, 151, 146, 246, 244, 166, 8,
				51, 230, 208, 154, 147, 99, 198, 124, 209, 232,
				17, 183, 245, 67, 172, 114, 220, 155, 69, 51,
				73, 197, 198, 123, 180, 155, 220, 143, 59, 6,
				202, 11, 230, 240, 30, 92, 251, 64, 221, 243,
				30, 170, 92, 26, 124, 192, 235, 218, 49, 242,
				125, 209, 249, 238, 67, 108, 26, 25, 45, 80,
				139, 108, 202, 8, 111, 81, 42, 30, 44, 251,
				187, 120, 44, 8, 116, 238, 158, 243, 45, 131,
				113, 35, 157, 236, 163, 199, 194, 130, 89, 44,
				110, 187, 74, 189, 177, 123, 28, 32, 203, 183,
				14, 39, 17, 67, 28, 180, 186, 173, 73, 106,
				179, 40, 201, 17, 153, 232, 243, 150, 31, 233,
				139, 100, 52, 115, 225, 54, 45, 151, 172, 124,
				223, 186, 66, 229, 213, 51, 6, 79, 188, 146,
				226, 75, 210, 77, 40, 213, 59, 62, 63, 61,
				40, 194, 151, 164, 134, 176, 235, 1, 81, 3,
				120, 159, 255, 160, 68, 255, 98, 198, 96, 183,
				36, 124, 193, 76, 192, 254, 221, 18, 160, 250,
				37, 128, 129, 1, 214, 207, 45, 100, 136, 189,
				219, 139, 113, 82, 162, 132, 105, 126, 49, 64,
				12, 97, 242, 43, 125, 8, 170, 246, 246, 141,
				42, 193, 176, 115, 161, 11, 232, 246, 74, 47,
				245, 19, 201, 92, 244, 0, 190, 12, 74, 245,
				169, 23, 45, 9, 53, 130, 80, 113, 226, 211,
				0, 124, 50, 128, 191, 9, 97, 8, 214, 42,
				112, 14, 117, 106, 48, 120, 239, 33, 116, 201,
				39, 16, 46, 220, 220, 146, 64, 120, 64, 126,
				128, 219, 109, 215, 255, 252, 33, 253, 90, 216,
				37, 125, 193, 45, 177, 82, 129, 238, 220, 25,
				151, 83, 116, 157, 133, 16, 85, 92, 166, 89,
				193, 229, 130, 115, 168, 243, 207, 107, 217, 253,
				200, 82, 30, 216, 185, 92, 63, 68, 117, 211,
				150, 127, 126, 255, 53, 179, 125, 183, 146, 86,
				176, 173, 188, 198, 2, 211, 210, 206, 43, 104,
				138, 245, 229, 172, 195, 202, 249, 1, 19, 229,
				252, 28, 231, 159, 227, 168, 233, 178, 83, 197,
				46, 220, 151, 220, 84, 48, 120, 134, 253, 49,
				115, 13, 95, 61, 75, 130, 41, 67, 178, 209,
				46, 254, 196, 29, 169, 208, 37, 23, 75, 5,
				114, 109, 22, 185, 48, 173, 97, 180, 39, 245,
				128, 246, 222, 243, 92, 167, 220, 244, 196, 83,
				142, 207, 56, 219, 65, 246, 91, 3, 51, 148,
				104, 23, 254, 139, 251, 139, 139, 68, 41, 152,
				111, 2, 220, 118, 16, 88, 197, 219, 16, 244,
				114, 42, 99, 198, 147, 208, 58, 82, 4, 191,
				161, 241, 149, 81, 119, 184, 244, 52, 191, 245,
				148, 66, 90, 2, 180, 141, 172, 65, 77, 48,
				233, 123, 36, 158, 229, 167, 8, 220, 200, 115,
				37, 183, 100, 114, 122, 33, 40, 168, 68, 246,
				197, 9, 77, 171, 120, 165, 218, 115, 249, 55,
				192, 101, 157, 113, 215, 85, 13, 35, 209, 105,
				86, 23, 50, 60, 248, 228, 203, 46, 188, 225,
				120, 50, 63, 97, 115, 83, 102, 115, 227, 164,
				118, 209, 219, 83, 7, 117, 84, 101, 52, 64,
				59, 219, 207, 242, 175, 67, 158, 80, 56, 0,
				0
			};

			public static byte[] WinRing0x64_sys = new byte[7939]
			{
				31, 139, 8, 8, 141, 47, 12, 88, 2, 0,
				87, 105, 110, 82, 105, 110, 103, 48, 120, 54,
				52, 46, 115, 121, 115, 0, 236, 89, 121, 80,
				27, 85, 24, 255, 118, 147, 64, 194, 149, 84,
				137, 2, 42, 46, 53, 214, 106, 149, 46, 71,
				109, 45, 162, 4, 130, 110, 52, 45, 8, 212,
				163, 141, 66, 72, 54, 77, 36, 36, 49, 89,
				180, 212, 170, 149, 128, 53, 174, 29, 235, 53,
				94, 213, 226, 125, 85, 235, 56, 30, 32, 82,
				137, 237, 180, 69, 237, 233, 133, 86, 71, 212,
				142, 22, 169, 90, 173, 35, 86, 171, 241, 123,
				155, 53, 141, 164, 222, 255, 232, 216, 111, 231,
				123, 191, 239, 126, 223, 123, 111, 95, 66, 134,
				89, 115, 151, 129, 2, 0, 148, 200, 209, 40,
				64, 15, 196, 168, 2, 254, 152, 134, 145, 179,
				142, 126, 33, 11, 158, 209, 108, 44, 232, 161,
				44, 27, 11, 26, 92, 238, 32, 227, 15, 248,
				230, 7, 108, 173, 140, 221, 230, 245, 250, 4,
				166, 153, 103, 2, 109, 94, 198, 237, 101, 76,
				53, 245, 76, 171, 207, 193, 23, 102, 102, 166,
				25, 228, 26, 211, 102, 122, 247, 93, 50, 15,
				86, 237, 103, 106, 213, 21, 136, 231, 222, 125,
				249, 42, 65, 194, 43, 86, 249, 37, 108, 93,
				21, 144, 240, 18, 89, 95, 36, 227, 2, 9,
				235, 220, 118, 23, 201, 135, 113, 84, 91, 13,
				224, 184, 38, 5, 34, 147, 68, 14, 100, 218,
				13, 19, 33, 157, 86, 3, 100, 160, 146, 22,
				179, 169, 107, 113, 208, 17, 137, 66, 150, 101,
				26, 32, 5, 226, 28, 35, 127, 108, 179, 212,
				185, 148, 28, 169, 140, 199, 203, 66, 2, 36,
				137, 224, 192, 121, 78, 37, 66, 19, 192, 128,
				2, 177, 130, 136, 72, 121, 0, 91, 242, 33,
				78, 126, 6, 224, 72, 248, 11, 196, 96, 206,
				239, 184, 11, 5, 126, 129, 128, 184, 46, 69,
				110, 72, 77, 90, 79, 42, 225, 42, 12, 56,
				108, 130, 13, 96, 17, 21, 51, 0, 141, 156,
				1, 137, 68, 90, 230, 10, 99, 97, 144, 77,
				226, 88, 57, 46, 43, 41, 110, 67, 161, 63,
				22, 216, 36, 25, 228, 56, 93, 114, 61, 243,
				108, 115, 3, 145, 39, 18, 127, 173, 220, 219,
				33, 73, 253, 125, 84, 24, 8, 6, 236, 32,
				239, 93, 147, 28, 119, 88, 82, 189, 74, 56,
				72, 127, 137, 56, 113, 109, 61, 23, 218, 213,
				196, 133, 154, 114, 80, 121, 135, 91, 170, 55,
				166, 160, 121, 41, 55, 20, 213, 143, 105, 1,
				44, 75, 151, 27, 22, 3, 65, 147, 161, 194,
				18, 182, 26, 88, 99, 111, 197, 114, 128, 146,
				109, 156, 248, 234, 58, 147, 97, 50, 172, 55,
				25, 24, 114, 39, 162, 250, 183, 49, 190, 107,
				160, 93, 19, 202, 92, 201, 160, 62, 106, 15,
				29, 123, 63, 10, 88, 77, 229, 144, 64, 175,
				39, 181, 195, 85, 126, 46, 28, 34, 69, 17,
				134, 99, 254, 101, 10, 2, 22, 67, 45, 122,
				93, 81, 253, 21, 90, 162, 54, 24, 42, 36,
				91, 84, 127, 171, 84, 89, 28, 106, 207, 226,
				196, 235, 177, 31, 50, 155, 13, 109, 226, 26,
				46, 180, 182, 105, 222, 154, 215, 126, 33, 14,
				59, 84, 115, 97, 143, 65, 199, 133, 5, 67,
				206, 121, 184, 54, 150, 91, 186, 112, 6, 39,
				126, 205, 137, 55, 245, 144, 201, 66, 199, 193,
				226, 153, 208, 71, 3, 12, 104, 59, 223, 164,
				1, 53, 90, 219, 217, 39, 9, 89, 218, 174,
				237, 40, 136, 149, 57, 198, 222, 55, 154, 42,
				150, 27, 203, 34, 218, 37, 120, 229, 193, 216,
				247, 26, 81, 95, 216, 130, 163, 112, 118, 57,
				48, 21, 203, 181, 157, 31, 1, 64, 185, 82,
				18, 95, 34, 98, 167, 36, 222, 70, 196, 107,
				81, 20, 130, 229, 215, 19, 168, 41, 95, 70,
				160, 178, 124, 45, 73, 206, 50, 150, 173, 17,
				52, 198, 178, 151, 181, 93, 126, 156, 10, 53,
				78, 156, 157, 99, 18, 43, 117, 226, 4, 109,
				231, 125, 152, 76, 124, 157, 55, 161, 64, 102,
				239, 154, 131, 179, 127, 30, 166, 70, 68, 52,
				124, 51, 82, 134, 41, 177, 248, 179, 212, 98,
				157, 206, 34, 70, 184, 240, 34, 3, 179, 115,
				31, 218, 71, 246, 80, 7, 118, 62, 78, 156,
				111, 253, 134, 115, 46, 113, 246, 202, 78, 81,
				181, 39, 31, 32, 76, 113, 235, 83, 149, 128,
				246, 215, 36, 251, 25, 57, 235, 65, 5, 52,
				53, 186, 211, 249, 185, 51, 76, 141, 42, 118,
				93, 75, 153, 194, 169, 35, 79, 82, 100, 253,
				54, 178, 232, 38, 34, 154, 36, 241, 99, 64,
				113, 67, 55, 217, 173, 33, 50, 246, 190, 71,
				198, 158, 225, 110, 226, 188, 229, 151, 245, 137,
				242, 250, 58, 59, 36, 97, 64, 184, 162, 92,
				125, 47, 110, 143, 187, 156, 67, 208, 118, 53,
				96, 65, 113, 174, 46, 244, 131, 58, 112, 129,
				5, 59, 195, 83, 11, 173, 249, 206, 40, 106,
				170, 197, 115, 148, 230, 80, 68, 109, 18, 35,
				98, 36, 50, 162, 54, 70, 118, 42, 66, 195,
				169, 218, 231, 182, 26, 67, 195, 71, 71, 134,
				85, 189, 164, 115, 83, 250, 0, 190, 9, 147,
				77, 225, 6, 3, 19, 213, 103, 101, 2, 76,
				89, 51, 54, 116, 196, 246, 171, 63, 86, 0,
				12, 143, 124, 139, 33, 88, 177, 15, 237, 3,
				35, 35, 177, 158, 164, 245, 79, 16, 242, 177,
				43, 114, 72, 3, 109, 31, 138, 70, 229, 151,
				35, 131, 232, 213, 62, 111, 84, 58, 191, 28,
				89, 141, 226, 117, 70, 229, 23, 35, 207, 146,
				244, 133, 58, 181, 120, 177, 154, 244, 214, 246,
				188, 220, 23, 190, 119, 147, 255, 176, 47, 185,
				167, 142, 12, 242, 74, 183, 165, 246, 81, 216,
				208, 104, 94, 232, 71, 90, 200, 8, 125, 71,
				163, 129, 38, 6, 77, 217, 78, 33, 173, 79,
				137, 34, 246, 57, 106, 14, 79, 29, 173, 58,
				208, 233, 205, 32, 219, 52, 52, 122, 188, 168,
				154, 121, 20, 246, 52, 24, 197, 230, 243, 20,
				145, 81, 157, 168, 58, 62, 110, 208, 132, 6,
				168, 176, 42, 23, 245, 146, 237, 228, 222, 110,
				10, 95, 196, 70, 245, 231, 100, 144, 179, 245,
				24, 56, 78, 20, 12, 181, 120, 147, 68, 171,
				161, 2, 175, 19, 219, 40, 95, 167, 138, 122,
				233, 246, 136, 23, 168, 241, 230, 206, 163, 165,
				11, 74, 58, 31, 78, 143, 139, 27, 137, 216,
				181, 93, 208, 224, 135, 65, 84, 191, 25, 53,
				82, 32, 225, 62, 138, 26, 109, 49, 23, 249,
				136, 225, 210, 95, 54, 135, 113, 58, 252, 192,
				88, 15, 106, 192, 78, 6, 70, 51, 37, 53,
				196, 64, 15, 133, 199, 144, 112, 135, 113, 113,
				184, 153, 92, 228, 51, 70, 212, 136, 21, 74,
				45, 43, 7, 254, 78, 18, 206, 83, 242, 215,
				231, 89, 203, 133, 207, 199, 79, 11, 23, 126,
				88, 248, 115, 184, 240, 2, 198, 216, 128, 75,
				102, 66, 223, 235, 140, 226, 59, 102, 113, 55,
				39, 238, 213, 118, 145, 155, 40, 26, 51, 180,
				171, 140, 234, 178, 33, 109, 71, 8, 200, 182,
				105, 184, 171, 247, 2, 100, 192, 162, 203, 196,
				157, 220, 82, 19, 21, 229, 202, 163, 81, 45,
				92, 229, 170, 46, 25, 224, 196, 55, 163, 250,
				215, 211, 48, 171, 70, 93, 93, 188, 35, 52,
				66, 9, 39, 144, 225, 240, 208, 94, 90, 80,
				25, 159, 161, 70, 39, 137, 53, 25, 156, 248,
				19, 39, 238, 222, 243, 224, 104, 110, 92, 113,
				162, 150, 190, 223, 245, 0, 22, 226, 196, 13,
				81, 253, 10, 172, 85, 221, 185, 163, 45, 157,
				44, 164, 54, 156, 131, 107, 82, 245, 144, 87,
				151, 28, 25, 75, 206, 112, 6, 57, 195, 10,
				78, 92, 100, 224, 240, 0, 24, 163, 149, 172,
				50, 153, 156, 78, 231, 50, 137, 151, 113, 101,
				153, 123, 243, 0, 218, 38, 112, 145, 136, 206,
				57, 22, 137, 70, 219, 232, 61, 107, 184, 200,
				160, 110, 68, 3, 0, 241, 12, 220, 142, 25,
				22, 49, 243, 77, 140, 181, 136, 170, 117, 8,
				92, 200, 97, 96, 112, 7, 182, 246, 142, 73,
				31, 195, 95, 107, 246, 135, 71, 143, 85, 166,
				197, 52, 43, 152, 128, 135, 75, 193, 13, 118,
				68, 43, 156, 135, 146, 23, 234, 164, 113, 62,
				176, 208, 8, 69, 200, 197, 200, 44, 0, 196,
				226, 125, 16, 252, 85, 86, 240, 15, 242, 14,
				210, 223, 167, 109, 231, 196, 240, 51, 196, 68,
				170, 149, 117, 151, 140, 139, 101, 60, 77, 198,
				7, 100, 124, 70, 198, 73, 50, 222, 36, 163,
				78, 198, 180, 58, 24, 79, 241, 191, 197, 105,
				228, 57, 200, 215, 51, 200, 25, 0, 117, 245,
				166, 122, 231, 195, 159, 152, 159, 183, 236, 173,
				186, 171, 57, 235, 179, 173, 83, 219, 95, 161,
				208, 239, 152, 105, 117, 249, 4, 252, 89, 113,
				49, 111, 23, 172, 151, 185, 189, 1, 183, 119,
				62, 107, 13, 250, 218, 2, 118, 222, 234, 240,
				120, 172, 193, 246, 160, 213, 227, 110, 182, 218,
				90, 29, 39, 151, 90, 207, 115, 123, 235, 72,
				4, 254, 237, 217, 12, 72, 148, 146, 2, 37,
				74, 26, 148, 207, 61, 12, 117, 196, 45, 217,
				0, 159, 35, 163, 44, 97, 162, 111, 5, 234,
				47, 72, 62, 196, 113, 62, 23, 234, 93, 146,
				79, 198, 20, 26, 82, 234, 104, 150, 202, 77,
				131, 92, 65, 3, 185, 14, 53, 228, 54, 164,
				66, 110, 105, 10, 228, 22, 235, 7, 168, 108,
				53, 100, 59, 210, 32, 187, 65, 3, 217, 165,
				40, 215, 233, 252, 148, 154, 6, 245, 211, 202,
				127, 207, 139, 75, 193, 255, 150, 138, 239, 251,
				224, 164, 59, 166, 0, 108, 188, 144, 217, 230,
				124, 35, 26, 133, 255, 25, 169, 117, 0, 155,
				145, 133, 2, 128, 33, 196, 252, 108, 128, 38,
				148, 13, 136, 141, 200, 21, 40, 187, 16, 111,
				70, 54, 160, 188, 28, 113, 3, 178, 26, 229,
				45, 136, 63, 32, 239, 98, 0, 148, 122, 128,
				15, 145, 57, 180, 79, 198, 187, 82, 131, 188,
				131, 129, 131, 244, 31, 33, 78, 84, 125, 53,
				28, 141, 154, 123, 127, 185, 16, 92, 215, 128,
				160, 50, 151, 69, 218, 166, 90, 150, 170, 222,
				66, 23, 215, 195, 40, 0, 22, 143, 161, 36,
				130, 185, 100, 192, 220, 19, 149, 8, 192, 124,
				204, 128, 89, 107, 138, 112, 97, 213, 147, 36,
				110, 108, 11, 74, 43, 81, 26, 121, 248, 165,
				104, 20, 255, 6, 232, 169, 133, 56, 173, 61,
				7, 32, 135, 1, 232, 78, 176, 209, 117, 56,
				48, 255, 142, 239, 167, 22, 218, 236, 115, 86,
				249, 90, 253, 30, 94, 224, 235, 248, 75, 218,
				248, 160, 0, 240, 52, 101, 246, 85, 5, 120,
				155, 192, 215, 183, 183, 54, 251, 60, 110, 187,
				197, 237, 109, 1, 120, 36, 110, 55, 241, 151,
				186, 237, 60, 192, 106, 180, 152, 120, 76, 30,
				23, 169, 87, 213, 9, 30, 179, 215, 45, 204,
				241, 186, 237, 62, 7, 95, 47, 144, 47, 49,
				128, 23, 229, 248, 253, 21, 46, 87, 204, 106,
				157, 227, 109, 181, 249, 205, 190, 122, 191, 141,
				88, 230, 161, 101, 86, 130, 126, 29, 125, 54,
				95, 217, 54, 191, 202, 197, 219, 91, 170, 23,
				0, 120, 5, 95, 176, 37, 224, 245, 20, 242,
				11, 208, 59, 21, 56, 155, 167, 158, 23, 42,
				219, 130, 38, 155, 96, 171, 108, 175, 113, 58,
				131, 188, 0, 122, 98, 63, 51, 217, 206, 25,
				45, 133, 248, 29, 10, 123, 83, 26, 27, 171,
				26, 131, 126, 222, 238, 118, 186, 237, 141, 46,
				155, 215, 225, 225, 3, 112, 144, 254, 71, 68,
				129, 14, 199, 28, 188, 78, 227, 237, 20, 142,
				236, 1, 236, 26, 37, 0, 135, 82, 83, 19,
				128, 85, 145, 84, 16, 109, 165, 56, 158, 11,
				245, 208, 136, 99, 53, 212, 161, 100, 134, 26,
				152, 141, 186, 25, 199, 51, 80, 38, 212, 175,
				252, 242, 39, 82, 143, 70, 86, 1, 196, 241,
				116, 185, 142, 18, 31, 122, 92, 237, 23, 209,
				64, 97, 61, 1, 2, 242, 239, 130, 51, 16,
				61, 192, 131, 25, 53, 39, 248, 48, 230, 118,
				41, 134, 133, 82, 40, 194, 135, 96, 51, 142,
				0, 103, 65, 46, 218, 171, 48, 166, 21, 31,
				30, 227, 5, 8, 162, 189, 1, 92, 168, 49,
				104, 243, 129, 3, 171, 57, 145, 121, 148, 24,
				168, 196, 153, 76, 136, 30, 180, 216, 165, 140,
				32, 142, 132, 78, 3, 109, 188, 150, 31, 108,
				232, 105, 135, 217, 136, 173, 178, 191, 6, 173,
				36, 222, 130, 153, 205, 88, 165, 29, 51, 11,
				49, 58, 128, 29, 19, 154, 9, 26, 160, 226,
				189, 155, 144, 131, 96, 39, 107, 194, 60, 1,
				71, 31, 230, 34, 37, 253, 6, 66, 194, 81,
				157, 144, 123, 46, 114, 0, 130, 9, 57, 69,
				56, 83, 49, 50, 139, 60, 77, 154, 43, 19,
				40, 105, 127, 4, 18, 139, 104, 195, 76, 185,
				219, 228, 57, 48, 43, 40, 245, 75, 232, 30,
				40, 199, 92, 11, 240, 232, 193, 44, 105, 197,
				126, 244, 98, 167, 104, 113, 97, 69, 56, 128,
				141, 129, 201, 104, 61, 30, 177, 24, 88, 124,
				166, 195, 73, 178, 52, 3, 152, 223, 219, 27,
				68, 6, 140, 224, 193, 135, 73, 168, 23, 148,
				52, 30, 145, 71, 188, 148, 156, 14, 70, 18,
				170, 148, 214, 86, 35, 199, 186, 99, 107, 139,
				239, 141, 247, 79, 173, 177, 88, 58, 139, 90,
				8, 72, 231, 223, 6, 118, 16, 226, 187, 243,
				91, 103, 80, 10, 234, 164, 156, 113, 39, 145,
				116, 14, 38, 100, 10, 163, 108, 16, 72, 126,
				107, 145, 12, 160, 196, 177, 1, 189, 54, 233,
				77, 243, 32, 38, 190, 11, 19, 148, 79, 41,
				225, 31, 147, 252, 63, 7, 154, 6, 182, 35,
				191, 63, 69, 115, 194, 53, 220, 53, 99, 153,
				84, 42, 221, 221, 145, 255, 36, 154, 30, 163,
				41, 170, 40, 157, 213, 164, 168, 166, 100, 41,
				232, 60, 21, 176, 174, 148, 180, 41, 41, 148,
				146, 234, 152, 78, 83, 202, 238, 185, 236, 249,
				108, 73, 130, 69, 203, 30, 171, 160, 160, 155,
				185, 47, 127, 241, 145, 112, 170, 244, 212, 64,
				51, 182, 239, 147, 22, 40, 32, 159, 70, 30,
				182, 32, 161, 166, 50, 123, 146, 171, 224, 254,
				27, 133, 109, 43, 124, 75, 110, 220, 188, 194,
				244, 213, 189, 123, 170, 238, 126, 183, 187, 35,
				183, 153, 237, 80, 244, 179, 29, 244, 131, 221,
				10, 154, 162, 233, 116, 10, 144, 40, 253, 116,
				67, 193, 35, 108, 102, 188, 89, 74, 133, 109,
				217, 165, 46, 21, 115, 148, 41, 135, 210, 149,
				213, 69, 185, 236, 225, 68, 73, 59, 84, 119,
				166, 199, 215, 140, 95, 135, 238, 249, 94, 198,
				123, 233, 73, 65, 91, 209, 97, 108, 54, 113,
				165, 31, 154, 89, 211, 76, 126, 79, 74, 174,
				42, 99, 81, 1, 123, 52, 177, 43, 14, 205,
				73, 72, 249, 85, 8, 155, 127, 120, 38, 59,
				157, 61, 165, 184, 180, 136, 157, 198, 78, 155,
				54, 23, 213, 25, 9, 42, 91, 159, 208, 196,
				89, 181, 69, 121, 108, 78, 172, 226, 132, 217,
				190, 128, 187, 189, 173, 197, 205, 204, 50, 95,
				96, 156, 107, 60, 219, 92, 52, 153, 157, 20,
				95, 128, 134, 58, 44, 215, 229, 110, 247, 17,
				174, 176, 7, 218, 131, 130, 205, 211, 106, 11,
				180, 20, 186, 189, 78, 31, 219, 65, 77, 76,
				92, 44, 165, 2, 69, 7, 165, 5, 180, 167,
				209, 29, 20, 5, 235, 94, 124, 39, 103, 51,
				191, 117, 201, 169, 215, 237, 187, 83, 83, 217,
				235, 26, 220, 221, 239, 74, 125, 202, 111, 59,
				42, 248, 193, 202, 247, 191, 93, 253, 222, 201,
				135, 92, 105, 109, 106, 95, 205, 59, 180, 39,
				30, 242, 144, 245, 169, 250, 193, 133, 183, 83,
				250, 107, 215, 47, 233, 191, 122, 217, 73, 79,
				52, 156, 109, 201, 120, 119, 214, 230, 37, 247,
				110, 157, 179, 105, 240, 242, 143, 87, 190, 98,
				158, 218, 55, 113, 98, 65, 166, 119, 83, 103,
				193, 251, 45, 105, 167, 95, 213, 125, 124, 221,
				90, 35, 125, 247, 13, 253, 199, 220, 116, 255,
				188, 199, 179, 127, 252, 193, 186, 176, 101, 249,
				250, 188, 79, 118, 188, 53, 184, 111, 215, 167,
				71, 221, 232, 125, 104, 138, 125, 250, 23, 151,
				143, 249, 250, 109, 211, 159, 184, 210, 157, 54,
				117, 221, 104, 105, 223, 105, 154, 157, 143, 46,
				110, 221, 149, 119, 119, 120, 225, 205, 37, 45,
				195, 245, 169, 199, 93, 183, 233, 231, 230, 204,
				60, 30, 202, 60, 142, 227, 134, 97, 220, 97,
				80, 202, 185, 114, 135, 103, 220, 131, 14, 183,
				220, 134, 36, 114, 207, 144, 117, 12, 99, 132,
				216, 24, 215, 186, 23, 229, 150, 80, 174, 68,
				110, 66, 151, 51, 114, 228, 182, 238, 82, 145,
				228, 42, 119, 216, 25, 182, 77, 109, 123, 188,
				118, 95, 123, 60, 243, 199, 204, 247, 249, 253,
				158, 99, 158, 223, 231, 253, 121, 190, 223, 223,
				239, 161, 118, 206, 115, 202, 216, 15, 22, 139,
				103, 153, 167, 109, 166, 186, 245, 151, 105, 162,
				66, 133, 43, 234, 98, 30, 170, 196, 170, 251,
				125, 143, 16, 14, 124, 239, 132, 1, 189, 236,
				76, 25, 132, 109, 11, 101, 223, 202, 130, 251,
				202, 54, 135, 93, 234, 219, 228, 75, 255, 16,
				49, 6, 125, 184, 228, 183, 29, 187, 157, 171,
				216, 115, 224, 116, 206, 149, 106, 106, 100, 25,
				121, 127, 85, 221, 22, 241, 140, 154, 89, 245,
				20, 49, 94, 112, 160, 27, 56, 28, 224, 11,
				208, 147, 81, 88, 6, 171, 131, 130, 215, 20,
				65, 32, 48, 152, 132, 24, 76, 7, 208, 226,
				71, 128, 237, 0, 8, 180, 179, 27, 47, 0,
				28, 132, 152, 27, 124, 8, 96, 246, 99, 236,
				54, 125, 167, 201, 171, 153, 85, 52, 101, 182,
				101, 228, 187, 178, 40, 34, 163, 99, 144, 14,
				192, 9, 29, 56, 192, 98, 0, 0, 136, 164,
				31, 75, 23, 12, 230, 191, 128, 197, 186, 200,
				138, 138, 218, 96, 28, 69, 236, 118, 149, 225,
				134, 151, 131, 136, 51, 10, 43, 250, 73, 29,
				34, 248, 214, 47, 36, 73, 66, 24, 28, 77,
				71, 157, 92, 242, 114, 189, 39, 133, 7, 18,
				234, 60, 120, 211, 149, 205, 104, 79, 16, 179,
				79, 76, 144, 123, 111, 155, 214, 10, 208, 158,
				80, 105, 149, 20, 63, 41, 152, 176, 121, 209,
				110, 69, 137, 215, 250, 250, 165, 164, 109, 209,
				89, 237, 188, 109, 198, 133, 65, 99, 133, 36,
				255, 187, 1, 42, 158, 234, 155, 253, 121, 244,
				103, 237, 135, 29, 141, 120, 203, 186, 215, 124,
				155, 93, 88, 114, 206, 228, 205, 143, 227, 138,
				227, 35, 55, 55, 108, 194, 215, 188, 187, 77,
				194, 198, 89, 29, 229, 170, 165, 37, 23, 28,
				12, 187, 248, 84, 69, 122, 105, 234, 145, 5,
				90, 186, 2, 45, 235, 246, 66, 2, 59, 113,
				103, 1, 74, 31, 26, 184, 5, 15, 8, 51,
				201, 176, 195, 35, 231, 51, 29, 98, 190, 246,
				104, 219, 178, 65, 221, 206, 202, 239, 69, 43,
				69, 169, 38, 162, 144, 222, 1, 180, 36, 164,
				236, 224, 127, 9, 27, 145, 75, 57, 103, 17,
				117, 125, 5, 99, 206, 214, 97, 226, 21, 88,
				231, 92, 41, 104, 101, 146, 182, 132, 89, 89,
				45, 153, 56, 225, 36, 97, 123, 254, 142, 158,
				195, 149, 39, 38, 194, 207, 248, 14, 94, 46,
				142, 149, 95, 174, 61, 157, 6, 19, 118, 84,
				218, 49, 225, 101, 208, 201, 150, 213, 110, 185,
				33, 65, 115, 11, 121, 186, 78, 140, 255, 108,
				193, 58, 206, 86, 81, 126, 19, 15, 240, 20,
				30, 224, 206, 159, 1, 222, 115, 172, 117, 223,
				91, 34, 236, 95, 2, 124, 246, 79, 3, 140,
				31, 212, 61, 128, 33, 8, 52, 26, 75, 64,
				247, 8, 192, 178, 7, 26, 195, 190, 206, 132,
				198, 143, 208, 138, 195, 196, 96, 82, 48, 113,
				0, 191, 225, 161, 133, 73, 0, 48, 49, 105,
				24, 108, 55, 4, 92, 9, 44, 238, 29, 206,
				241, 197, 225, 187, 63, 244, 172, 48, 88, 103,
				20, 198, 141, 112, 33, 54, 224, 200, 222, 165,
				25, 191, 214, 252, 123, 55, 189, 255, 207, 253,
				33, 210, 247, 230, 129, 69, 96, 81, 34, 47,
				20, 229, 161, 173, 154, 19, 25, 189, 85, 168,
				27, 213, 202, 127, 176, 250, 220, 100, 110, 44,
				165, 10, 137, 96, 216, 15, 47, 41, 244, 206,
				32, 112, 11, 55, 98, 138, 127, 100, 16, 196,
				173, 212, 145, 176, 202, 251, 63, 196, 180, 47,
				230, 16, 13, 44, 27, 166, 82, 170, 205, 37,
				199, 41, 196, 70, 165, 23, 80, 28, 239, 121,
				147, 137, 45, 184, 206, 194, 73, 81, 241, 93,
				182, 185, 228, 117, 93, 248, 183, 36, 229, 97,
				5, 28, 241, 214, 14, 7, 239, 230, 127, 111,
				217, 103, 230, 244, 99, 108, 203, 83, 104, 97,
				111, 200, 143, 122, 185, 202, 49, 224, 162, 25,
				181, 25, 139, 146, 183, 224, 71, 3, 32, 31,
				174, 181, 6, 87, 238, 15, 14, 90, 40, 101,
				241, 250, 72, 182, 87, 221, 182, 172, 8, 1,
				213, 6, 178, 41, 42, 201, 133, 115, 32, 91,
				162, 208, 206, 212, 73, 111, 207, 91, 6, 207,
				231, 110, 75, 198, 77, 109, 153, 13, 40, 94,
				141, 235, 57, 236, 12, 59, 163, 54, 240, 166,
				160, 89, 235, 186, 161, 107, 192, 225, 131, 143,
				215, 167, 58, 140, 65, 239, 165, 30, 20, 158,
				60, 119, 29, 229, 115, 10, 114, 222, 141, 83,
				34, 147, 161, 254, 244, 251, 11, 167, 236, 217,
				67, 39, 130, 94, 203, 86, 38, 216, 212, 168,
				21, 193, 29, 20, 136, 46, 47, 107, 86, 249,
				78, 114, 253, 140, 116, 18, 128, 139, 255, 28,
				96, 16, 25, 112, 128, 16, 67, 9, 49, 41,
				64, 130, 255, 2, 216, 8, 59, 104, 193, 204,
				96, 70, 163, 192, 217, 114, 215, 172, 41, 155,
				193, 97, 125, 112, 255, 198, 171, 5, 117, 196,
				233, 14, 64, 124, 143, 231, 99, 128, 32, 192,
				159, 206, 155, 126, 52, 248, 155, 223, 230, 153,
				48, 168, 4, 146, 63, 115, 9, 75, 111, 219,
				195, 42, 52, 9, 173, 161, 122, 162, 223, 129,
				219, 37, 114, 119, 62, 108, 105, 126, 13, 245,
				243, 162, 34, 118, 26, 188, 165, 83, 229, 39,
				37, 229, 71, 211, 73, 222, 26, 37, 175, 43,
				75, 35, 152, 1, 84, 115, 151, 78, 230, 129,
				200, 202, 138, 10, 187, 168, 41, 173, 217, 200,
				184, 251, 175, 58, 114, 190, 241, 196, 222, 30,
				88, 195, 38, 149, 174, 118, 132, 67, 101, 250,
				226, 219, 34, 47, 54, 138, 202, 148, 200, 22,
				90, 221, 167, 250, 62, 105, 146, 203, 26, 201,
				13, 143, 12, 92, 9, 138, 92, 245, 216, 41,
				146, 33, 91, 184, 252, 72, 221, 162, 124, 206,
				152, 151, 204, 150, 244, 27, 108, 108, 64, 206,
				112, 249, 122, 236, 228, 116, 61, 203, 49, 17,
				155, 249, 26, 142, 241, 245, 85, 136, 154, 45,
				13, 36, 52, 225, 242, 154, 248, 177, 180, 39,
				133, 140, 199, 23, 89, 199, 111, 44, 122, 221,
				121, 28, 251, 195, 85, 215, 134, 46, 229, 70,
				195, 97, 246, 114, 42, 202, 60, 197, 27, 119,
				250, 149, 224, 108, 115, 63, 92, 73, 52, 54,
				189, 239, 156, 74, 181, 108, 51, 107, 158, 84,
				183, 110, 184, 1, 130, 168, 14, 209, 20, 33,
				143, 118, 70, 117, 145, 177, 233, 202, 103, 218,
				120, 168, 152, 222, 172, 181, 40, 242, 171, 129,
				77, 80, 25, 130, 206, 51, 179, 97, 116, 250,
				4, 237, 213, 186, 142, 216, 94, 182, 0, 57,
				47, 74, 198, 56, 76, 136, 108, 23, 169, 12,
				50, 243, 149, 55, 134, 215, 13, 25, 224, 81,
				95, 195, 163, 62, 177, 31, 117, 16, 249, 143,
				86, 172, 253, 255, 38, 234, 112, 56, 158, 109,
				25, 152, 216, 87, 81, 199, 225, 254, 244, 149,
				121, 0, 238, 189, 43, 179, 234, 97, 236, 241,
				47, 99, 175, 159, 83, 1, 78, 61, 119, 107,
				71, 123, 183, 11, 248, 218, 157, 112, 59, 0,
				32, 178, 119, 59, 124, 251, 206, 240, 123, 71,
				252, 33, 251, 153, 41, 238, 130, 57, 208, 106,
				10, 215, 163, 135, 198, 51, 200, 172, 5, 24,
				210, 70, 210, 220, 221, 166, 199, 141, 206, 218,
				199, 67, 141, 116, 151, 30, 215, 130, 140, 223,
				242, 6, 99, 168, 44, 151, 89, 26, 207, 144,
				156, 77, 185, 75, 215, 146, 209, 139, 61, 181,
				227, 72, 52, 253, 202, 64, 122, 187, 114, 200,
				235, 68, 21, 66, 230, 53, 238, 216, 117, 90,
				113, 68, 118, 187, 75, 135, 134, 153, 245, 251,
				46, 59, 243, 218, 4, 13, 72, 107, 237, 104,
				164, 90, 113, 251, 34, 202, 52, 8, 84, 164,
				233, 48, 214, 30, 37, 178, 172, 55, 125, 35,
				245, 26, 111, 162, 131, 148, 141, 2, 137, 139,
				242, 130, 197, 115, 30, 119, 47, 189, 239, 44,
				50, 200, 50, 85, 169, 207, 196, 169, 49, 246,
				196, 168, 6, 95, 72, 154, 226, 123, 127, 117,
				54, 89, 65, 127, 53, 190, 247, 82, 196, 51,
				229, 132, 11, 174, 36, 131, 198, 232, 123, 101,
				116, 138, 115, 52, 182, 238, 170, 225, 202, 241,
				221, 0, 17, 3, 196, 102, 85, 57, 5, 228,
				210, 106, 132, 78, 53, 78, 16, 228, 59, 24,
				46, 226, 138, 112, 82, 205, 207, 17, 58, 224,
				203, 172, 142, 124, 140, 216, 32, 59, 72, 4,
				193, 14, 168, 148, 82, 47, 123, 198, 185, 77,
				83, 206, 53, 76, 96, 34, 216, 233, 111, 177,
				215, 152, 119, 172, 151, 17, 205, 185, 221, 107,
				255, 139, 236, 51, 233, 123, 29, 246, 161, 62,
				183, 62, 212, 62, 216, 32, 11, 77, 110, 241,
				20, 246, 213, 254, 247, 216, 79, 87, 20, 169,
				186, 152, 163, 232, 26, 124, 74, 91, 85, 219,
				148, 183, 168, 8, 187, 126, 50, 52, 215, 219,
				167, 114, 177, 41, 97, 194, 31, 158, 116, 77,
				105, 219, 210, 95, 74, 177, 220, 244, 251, 200,
				101, 179, 247, 53, 59, 121, 116, 202, 185, 174,
				61, 205, 139, 223, 40, 44, 37, 223, 204, 154,
				181, 217, 120, 249, 6, 165, 193, 2, 92, 11,
				111, 174, 216, 26, 57, 249, 204, 113, 171, 15,
				193, 8, 249, 224, 116, 180, 173, 180, 13, 151,
				249, 16, 241, 142, 239, 174, 83, 26, 217, 241,
				193, 220, 211, 196, 98, 246, 205, 164, 138, 87,
				159, 214, 76, 214, 228, 145, 215, 95, 209, 59,
				50, 28, 232, 244, 176, 154, 29, 145, 136, 74,
				133, 94, 188, 87, 218, 103, 39, 32, 202, 225,
				125, 235, 152, 126, 250, 86, 144, 46, 175, 188,
				250, 13, 28, 95, 246, 246, 156, 175, 103, 82,
				157, 74, 231, 53, 14, 196, 229, 129, 184, 240,
				173, 103, 79, 57, 235, 239, 92, 214, 207, 20,
				181, 149, 203, 23, 238, 20, 142, 138, 208, 254,
				238, 16, 156, 139, 127, 115, 164, 208, 9, 133,
				123, 58, 49, 112, 72, 115, 170, 124, 21, 59,
				16, 248, 10, 33, 120, 91, 238, 194, 150, 156,
				70, 21, 200, 48, 48, 70, 172, 38, 174, 59,
				202, 6, 200, 78, 235, 224, 142, 223, 206, 23,
				169, 250, 54, 28, 59, 50, 198, 149, 193, 88,
				183, 249, 192, 224, 14, 29, 224, 15, 62, 128,
				103, 127, 245, 51, 246, 233, 98, 223, 248, 199,
				125, 201, 254, 127, 240, 182, 221, 203, 218, 197,
				0, 73, 0, 254, 153, 43, 252, 28, 2, 104,
				24, 20, 160, 223, 59, 19, 229, 167, 51, 193,
				132, 0, 129, 189, 251, 252, 102, 223, 233, 13,
				237, 157, 80, 156, 6, 88, 43, 39, 23, 2,
				206, 10, 238, 216, 11, 248, 204, 30, 235, 69,
				232, 188, 63, 157, 103, 197, 226, 251, 185, 237,
				117, 179, 69, 159, 218, 39, 53, 27, 180, 211,
				31, 90, 64, 251, 147, 35, 110, 108, 57, 59,
				227, 245, 133, 106, 254, 143, 136, 150, 98, 228,
				233, 252, 6, 220, 95, 101, 78, 20, 133, 173,
				57, 184, 213, 169, 175, 255, 24, 232, 63, 140,
				190, 95, 248, 192, 8, 145, 36, 121, 167, 191,
				110, 242, 121, 87, 111, 69, 206, 21, 61, 12,
				216, 239, 125, 139, 3, 211, 187, 15, 62, 114,
				198, 186, 30, 207, 79, 131, 173, 195, 147, 162,
				173, 73, 130, 98, 183, 169, 29, 198, 21, 26,
				74, 34, 141, 217, 67, 238, 210, 89, 129, 203,
				154, 3, 82, 235, 138, 153, 251, 239, 33, 227,
				32, 235, 235, 217, 38, 240, 24, 132, 236, 49,
				190, 181, 167, 35, 61, 135, 147, 207, 5, 209,
				149, 19, 89, 241, 200, 29, 135, 30, 124, 76,
				175, 117, 49, 123, 91, 203, 170, 237, 92, 119,
				131, 18, 192, 117, 158, 233, 20, 133, 174, 219,
				179, 71, 113, 55, 81, 182, 63, 12, 247, 228,
				2, 244, 235, 141, 183, 161, 244, 122, 185, 186,
				149, 134, 28, 179, 119, 146, 14, 195, 211, 223,
				242, 147, 208, 123, 87, 23, 149, 108, 6, 158,
				168, 88, 195, 106, 134, 166, 36, 81, 38, 79,
				173, 133, 106, 87, 133, 182, 148, 101, 17, 137,
				71, 163, 26, 20, 45, 59, 106, 149, 20, 101,
				160, 125, 99, 166, 14, 153, 58, 93, 39, 53,
				224, 42, 118, 198, 160, 42, 110, 131, 121, 105,
				136, 45, 200, 247, 129, 29, 5, 41, 213, 212,
				131, 159, 45, 32, 15, 192, 101, 127, 110, 1,
				16, 191, 125, 196, 223, 97, 51, 164, 210, 172,
				180, 23, 138, 56, 42, 161, 132, 51, 163, 201,
				207, 23, 51, 225, 4, 78, 238, 17, 47, 3,
				72, 1, 18, 233, 98, 233, 64, 176, 200, 239,
				17, 191, 39, 170, 143, 154, 218, 165, 31, 74,
				56, 1, 15, 152, 26, 160, 36, 35, 23, 34,
				195, 75, 22, 66, 66, 190, 223, 18, 190, 154,
				100, 124, 205, 18, 144, 41, 144, 214, 219, 109,
				10, 236, 210, 243, 104, 137, 212, 242, 233, 86,
				245, 245, 161, 153, 141, 213, 131, 34, 231, 11,
				88, 228, 220, 232, 103, 77, 134, 183, 188, 185,
				96, 86, 15, 99, 245, 77, 139, 109, 139, 124,
				144, 131, 219, 116, 101, 174, 185, 181, 105, 238,
				210, 169, 22, 232, 165, 33, 176, 116, 213, 236,
				67, 146, 231, 70, 149, 158, 231, 226, 140, 217,
				46, 249, 156, 56, 52, 160, 37, 118, 111, 157,
				125, 138, 91, 197, 119, 108, 34, 18, 229, 111,
				170, 178, 133, 108, 66, 215, 15, 204, 48, 40,
				176, 81, 195, 205, 87, 226, 215, 234, 219, 211,
				56, 118, 244, 67, 236, 47, 38, 134, 71, 149,
				241, 133, 248, 31, 238, 151, 191, 148, 95, 164,
				83, 121, 98, 186, 87, 162, 251, 209, 77, 184,
				129, 124, 9, 214, 155, 73, 26, 48, 204, 126,
				115, 247, 104, 155, 40, 185, 166, 18, 114, 81,
				227, 78, 100, 234, 180, 161, 99, 201, 240, 136,
				92, 113, 243, 55, 129, 215, 207, 68, 97, 114,
				75, 114, 87, 22, 118, 72, 90, 226, 223, 228,
				251, 187, 13, 221, 87, 49, 98, 145, 218, 170,
				118, 172, 117, 93, 232, 105, 174, 191, 56, 240,
				162, 246, 121, 70, 54, 134, 168, 243, 94, 9,
				209, 219, 216, 91, 240, 124, 138, 46, 103, 184,
				65, 176, 96, 175, 156, 190, 91, 98, 165, 225,
				235, 4, 225, 36, 143, 57, 134, 19, 82, 138,
				60, 190, 120, 75, 224, 196, 167, 4, 228, 95,
				166, 3, 71, 213, 191, 120, 90, 255, 139, 183,
				50, 193, 35, 8, 166, 32, 246, 133, 71, 192,
				128, 221, 240, 95, 153, 94, 248, 67, 95, 40,
				42, 94, 242, 35, 114, 233, 152, 173, 143, 40,
				75, 120, 219, 68, 17, 193, 139, 14, 29, 183,
				59, 135, 235, 28, 99, 132, 106, 213, 21, 171,
				60, 55, 112, 87, 216, 128, 72, 161, 171, 233,
				93, 248, 175, 250, 56, 97, 238, 65, 152, 120,
				80, 178, 20, 59, 65, 35, 27, 222, 230, 210,
				223, 203, 188, 104, 214, 36, 247, 75, 76, 11,
				107, 55, 166, 32, 126, 88, 15, 58, 29, 111,
				28, 18, 202, 123, 110, 217, 169, 233, 238, 163,
				205, 67, 60, 111, 7, 173, 117, 90, 41, 29,
				205, 92, 230, 59, 122, 82, 4, 168, 40, 154,
				86, 230, 42, 152, 123, 229, 13, 163, 218, 204,
				38, 238, 12, 143, 114, 83, 136, 121, 139, 225,
				152, 173, 41, 201, 158, 92, 234, 148, 224, 170,
				64, 75, 85, 20, 119, 60, 73, 22, 144, 60,
				117, 226, 154, 177, 207, 78, 129, 127, 226, 70,
				85, 95, 104, 171, 228, 232, 147, 246, 179, 100,
				79, 82, 94, 60, 184, 154, 235, 151, 104, 225,
				17, 191, 80, 49, 149, 143, 245, 58, 197, 149,
				109, 243, 250, 41, 36, 230, 94, 144, 76, 89,
				97, 252, 237, 140, 29, 70, 183, 161, 44, 149,
				42, 237, 224, 14, 65, 249, 249, 222, 113, 126,
				90, 247, 188, 220, 16, 104, 221, 11, 31, 203,
				43, 75, 60, 109, 36, 129, 9, 175, 116, 42,
				174, 11, 247, 151, 135, 75, 200, 231, 247, 201,
				200, 62, 62, 222, 80, 188, 126, 245, 99, 106,
				80, 14, 224, 74, 126, 149, 26, 48, 252, 146,
				26, 144, 3, 100, 248, 47, 98, 16, 209, 39,
				175, 248, 251, 149, 190, 11, 94, 60, 232, 221,
				193, 34, 184, 196, 175, 38, 26, 136, 64, 251,
				61, 226, 171, 201, 200, 215, 60, 130, 190, 215,
				44, 236, 253, 113, 154, 107, 151, 117, 115, 136,
				92, 173, 168, 140, 74, 181, 110, 185, 60, 43,
				233, 26, 107, 14, 113, 98, 11, 245, 17, 202,
				83, 125, 25, 206, 17, 165, 85, 103, 27, 23,
				195, 49, 169, 13, 215, 152, 60, 226, 82, 169,
				236, 60, 66, 22, 166, 237, 196, 51, 157, 200,
				48, 82, 155, 62, 26, 172, 83, 248, 3, 14,
				74, 122, 42, 218, 196, 144, 71, 224, 170, 70,
				235, 34, 170, 108, 158, 204, 107, 104, 234, 73,
				240, 2, 184, 229, 208, 200, 28, 166, 148, 19,
				116, 86, 125, 124, 82, 252, 217, 177, 246, 84,
				136, 239, 85, 53, 190, 200, 144, 72, 9, 234,
				108, 159, 243, 156, 143, 190, 131, 148, 186, 57,
				32, 35, 120, 187, 83, 6, 166, 189, 80, 217,
				185, 49, 25, 216, 96, 65, 82, 98, 255, 71,
				129, 62, 75, 94, 237, 242, 130, 66, 7, 146,
				4, 216, 103, 34, 194, 14, 23, 127, 59, 147,
				105, 43, 67, 139, 122, 86, 48, 216, 212, 194,
				17, 18, 9, 191, 56, 119, 208, 191, 1, 26,
				153, 208, 97, 163, 48, 117, 105, 185, 172, 194,
				145, 169, 94, 6, 90, 122, 133, 202, 154, 44,
				100, 244, 153, 178, 47, 93, 158, 222, 209, 112,
				203, 17, 210, 85, 211, 44, 198, 18, 111, 35,
				12, 85, 252, 204, 131, 15, 164, 46, 209, 168,
				52, 82, 214, 155, 163, 144, 59, 154, 103, 199,
				154, 224, 118, 114, 141, 88, 192, 159, 212, 23,
				239, 17, 118, 123, 30, 65, 105, 69, 229, 235,
				64, 68, 216, 88, 190, 180, 8, 223, 125, 248,
				157, 49, 248, 248, 190, 38, 135, 82, 158, 181,
				218, 69, 24, 139, 118, 254, 88, 43, 64, 240,
				181, 2, 10, 233, 132, 118, 70, 194, 216, 1,
				214, 61, 74, 153, 180, 237, 109, 48, 104, 55,
				180, 45, 190, 74, 64, 99, 92, 208, 24, 43,
				172, 61, 254, 8, 1, 128, 111, 15, 73, 206,
				253, 237, 72, 20, 167, 17, 10, 67, 88, 2,
				35, 244, 218, 75, 77, 118, 253, 65, 10, 144,
				20, 19, 135, 73, 3, 128, 36, 140, 224, 15,
				63, 135, 48, 66, 248, 79, 149, 52, 127, 236,
				9, 67, 180, 175, 146, 34, 59, 110, 76, 234,
				134, 93, 222, 92, 12, 15, 224, 113, 152, 81,
				231, 88, 20, 44, 186, 27, 71, 15, 190, 183,
				96, 211, 245, 220, 206, 182, 147, 117, 180, 243,
				136, 186, 208, 92, 100, 104, 84, 242, 29, 1,
				63, 84, 254, 235, 102, 97, 134, 39, 249, 172,
				90, 46, 144, 140, 227, 148, 64, 59, 83, 164,
				238, 218, 104, 111, 132, 30, 19, 235, 188, 222,
				92, 131, 200, 234, 135, 215, 136, 165, 152, 239,
				38, 156, 250, 36, 129, 40, 115, 37, 183, 37,
				133, 215, 125, 223, 78, 150, 132, 202, 26, 193,
				101, 208, 100, 242, 33, 246, 166, 130, 218, 141,
				57, 134, 85, 142, 161, 109, 41, 235, 242, 173,
				47, 183, 66, 151, 25, 228, 99, 61, 227, 251,
				71, 176, 206, 87, 148, 173, 186, 34, 155, 85,
				75, 220, 105, 131, 167, 89, 194, 250, 156, 28,
				251, 60, 153, 227, 50, 95, 63, 166, 148, 121,
				179, 116, 20, 170, 235, 102, 120, 5, 26, 36,
				123, 164, 150, 93, 162, 60, 156, 52, 226, 174,
				71, 120, 241, 48, 7, 103, 76, 62, 133, 129,
				115, 116, 135, 183, 52, 246, 174, 139, 90, 204,
				55, 250, 54, 204, 94, 133, 197, 133, 10, 188,
				228, 143, 89, 132, 186, 84, 111, 169, 247, 35,
				5, 7, 2, 36, 118, 142, 9, 62, 114, 100,
				81, 210, 128, 4, 77, 245, 248, 172, 112, 93,
				152, 159, 89, 66, 92, 247, 189, 18, 221, 185,
				231, 9, 254, 32, 110, 252, 19, 225, 216, 149,
				5, 27, 39, 152, 18, 192, 155, 0, 25, 24,
				255, 147, 8, 144, 34, 163, 248, 121, 118, 154,
				9, 2, 38, 140, 56, 199, 199, 144, 60, 114,
				162, 43, 84, 39, 112, 32, 181, 30, 18, 156,
				29, 188, 201, 250, 67, 207, 131, 108, 38, 16,
				11, 49, 200, 153, 96, 30, 84, 187, 6, 179,
				235, 46, 193, 191, 83, 120, 124, 189, 68, 96,
				251, 229, 162, 140, 248, 73, 72, 118, 90, 34,
				165, 221, 181, 1, 55, 252, 71, 137, 72, 225,
				51, 175, 176, 222, 164, 228, 50, 245, 85, 114,
				166, 31, 162, 48, 212, 115, 88, 238, 142, 119,
				93, 76, 3, 206, 236, 89, 147, 14, 160, 5,
				104, 164, 171, 167, 171, 6, 43, 239, 179, 38,
				167, 143, 98, 37, 36, 147, 162, 46, 14, 246,
				132, 189, 120, 135, 66, 35, 221, 109, 176, 110,
				162, 191, 104, 153, 32, 229, 93, 37, 19, 180,
				244, 213, 9, 74, 252, 255, 132, 54, 58, 154,
				67, 124, 229, 207, 238, 164, 192, 44, 78, 222,
				139, 51, 228, 105, 242, 90, 63, 6, 35, 209,
				70, 218, 107, 26, 253, 104, 154, 128, 246, 121,
				183, 240, 244, 40, 202, 93, 198, 38, 17, 234,
				194, 12, 93, 205, 1, 45, 54, 113, 79, 4,
				5, 59, 82, 45, 248, 93, 53, 113, 159, 246,
				121, 193, 30, 91, 230, 246, 224, 81, 105, 78,
				17, 85, 207, 161, 192, 39, 109, 20, 55, 206,
				167, 234, 158, 40, 146, 77, 21, 62, 210, 0,
				158, 79, 144, 142, 71, 191, 156, 173, 190, 253,
				109, 105, 239, 115, 249, 241, 112, 158, 169, 219,
				181, 214, 42, 176, 209, 155, 148, 152, 23, 71,
				214, 60, 149, 64, 109, 213, 112, 198, 49, 179,
				232, 194, 65, 147, 78, 243, 86, 85, 156, 190,
				120, 95, 54, 173, 177, 93, 207, 160, 173, 113,
				8, 115, 202, 92, 241, 225, 214, 130, 24, 104,
				32, 215, 80, 178, 51, 231, 252, 16, 17, 150,
				191, 186, 197, 226, 157, 182, 89, 253, 16, 143,
				61, 143, 225, 140, 114, 109, 125, 138, 128, 56,
				107, 191, 124, 60, 199, 247, 184, 70, 131, 1,
				65, 62, 241, 94, 17, 22, 31, 114, 99, 167,
				97, 29, 150, 164, 188, 25, 139, 119, 201, 215,
				34, 244, 101, 77, 206, 223, 19, 205, 35, 151,
				76, 49, 204, 25, 150, 60, 140, 96, 22, 151,
				144, 189, 225, 7, 254, 118, 171, 44, 32, 162,
				32, 186, 147, 90, 22, 21, 63, 43, 174, 115,
				136, 183, 234, 116, 205, 27, 92, 236, 14, 84,
				91, 76, 147, 187, 89, 124, 170, 194, 213, 129,
				105, 37, 213, 239, 85, 109, 165, 227, 125, 133,
				62, 18, 89, 87, 113, 38, 34, 61, 172, 28,
				173, 217, 122, 252, 173, 183, 209, 39, 242, 161,
				90, 126, 73, 188, 181, 50, 89, 111, 136, 165,
				66, 217, 42, 56, 239, 7, 228, 211, 173, 177,
				164, 13, 216, 120, 245, 22, 22, 139, 90, 73,
				173, 107, 104, 230, 138, 55, 211, 98, 20, 214,
				23, 139, 163, 47, 23, 170, 206, 143, 193, 189,
				221, 211, 21, 24, 207, 129, 217, 20, 107, 213,
				66, 170, 160, 180, 164, 27, 108, 11, 126, 145,
				59, 46, 77, 30, 131, 154, 35, 170, 180, 220,
				135, 49, 125, 15, 198, 182, 111, 21, 218, 27,
				203, 127, 56, 175, 250, 162, 15, 151, 28, 238,
				140, 58, 231, 113, 88, 44, 43, 58, 187, 2,
				121, 5, 185, 5, 49, 104, 73, 31, 227, 47,
				16, 116, 108, 146, 233, 194, 117, 36, 46, 159,
				98, 121, 125, 236, 160, 210, 77, 94, 139, 30,
				101, 192, 224, 5, 27, 113, 245, 77, 74, 81,
				230, 209, 75, 150, 124, 8, 197, 154, 130, 68,
				105, 254, 132, 23, 85, 221, 94, 244, 147, 116,
				17, 234, 125, 128, 25, 37, 53, 98, 108, 57,
				100, 38, 242, 176, 176, 93, 203, 123, 63, 198,
				192, 202, 32, 234, 46, 91, 254, 172, 38, 244,
				81, 43, 152, 63, 184, 9, 159, 170, 213, 17,
				131, 64, 0, 230, 95, 73, 115, 62, 91, 198,
				217, 183, 10, 148, 142, 0, 232, 246, 173, 36,
				81, 195, 136, 1, 34, 128, 229, 83, 85, 69,
				2, 163, 222, 215, 12, 6, 184, 63, 53, 129,
				97, 120, 16, 75, 226, 123, 40, 145, 232, 240,
				231, 78, 21, 34, 210, 186, 238, 161, 245, 85,
				73, 9, 42, 95, 88, 39, 216, 31, 68, 148,
				193, 48, 74, 138, 124, 112, 49, 80, 15, 227,
				245, 44, 83, 150, 100, 240, 52, 167, 218, 168,
				122, 37, 177, 110, 35, 121, 78, 43, 167, 177,
				83, 155, 107, 64, 169, 252, 134, 87, 23, 91,
				153, 201, 17, 64, 116, 90, 175, 78, 237, 238,
				125, 243, 42, 105, 237, 90, 110, 94, 101, 162,
				4, 199, 12, 87, 249, 70, 207, 213, 208, 114,
				85, 142, 170, 183, 98, 150, 209, 10, 38, 106,
				236, 151, 51, 26, 100, 26, 167, 185, 103, 5,
				110, 182, 123, 120, 52, 60, 164, 186, 31, 168,
				24, 194, 192, 34, 20, 48, 64, 239, 71, 186,
				38, 85, 233, 147, 154, 129, 209, 181, 229, 120,
				138, 229, 42, 76, 61, 110, 66, 217, 83, 173,
				161, 127, 16, 172, 211, 185, 96, 89, 110, 38,
				95, 211, 225, 213, 195, 98, 228, 243, 62, 196,
				189, 43, 202, 210, 40, 182, 133, 172, 131, 118,
				187, 164, 177, 58, 172, 16, 51, 16, 64, 67,
				251, 253, 73, 79, 231, 46, 198, 219, 167, 114,
				113, 23, 138, 38, 186, 239, 203, 191, 182, 247,
				155, 69, 32, 187, 86, 2, 122, 84, 141, 168,
				77, 178, 20, 231, 86, 134, 120, 37, 245, 21,
				117, 111, 110, 156, 204, 93, 149, 172, 20, 202,
				175, 187, 106, 239, 120, 145, 189, 235, 195, 22,
				185, 158, 16, 181, 46, 227, 140, 191, 107, 101,
				181, 230, 54, 177, 154, 116, 17, 172, 209, 231,
				238, 120, 11, 76, 86, 73, 229, 104, 134, 63,
				113, 31, 190, 84, 239, 250, 244, 244, 200, 96,
				254, 196, 117, 248, 93, 247, 9, 26, 192, 249,
				253, 7, 37, 250, 103, 51, 6, 251, 37, 225,
				15, 98, 6, 14, 237, 151, 0, 213, 47, 1,
				4, 4, 176, 126, 106, 33, 133, 29, 216, 93,
				140, 147, 22, 195, 79, 243, 139, 3, 226, 50,
				38, 191, 210, 71, 13, 122, 131, 247, 217, 27,
				229, 216, 218, 51, 174, 246, 205, 189, 84, 25,
				205, 250, 222, 82, 0, 174, 130, 140, 234, 99,
				47, 58, 98, 106, 24, 190, 226, 196, 221, 0,
				112, 233, 0, 238, 26, 152, 49, 66, 167, 200,
				37, 202, 185, 217, 240, 189, 183, 176, 167, 95,
				40, 84, 228, 73, 91, 10, 254, 1, 5, 2,
				238, 55, 221, 254, 243, 135, 244, 107, 97, 55,
				92, 217, 168, 17, 129, 218, 167, 45, 110, 46,
				149, 95, 230, 233, 48, 169, 148, 60, 67, 87,
				231, 24, 134, 101, 15, 18, 80, 49, 183, 248,
				33, 6, 27, 140, 104, 173, 79, 124, 167, 167,
				237, 52, 179, 197, 58, 88, 109, 98, 3, 241,
				168, 50, 112, 87, 74, 98, 212, 183, 101, 168,
				229, 183, 30, 203, 41, 49, 113, 21, 144, 6,
				225, 14, 57, 7, 157, 140, 168, 126, 75, 172,
				154, 59, 19, 91, 179, 129, 8, 136, 48, 200,
				138, 105, 68, 198, 249, 203, 237, 32, 75, 34,
				182, 24, 141, 150, 77, 71, 125, 113, 97, 18,
				52, 22, 146, 170, 83, 43, 231, 151, 227, 12,
				253, 219, 119, 242, 180, 213, 167, 164, 153, 70,
				223, 115, 26, 231, 217, 143, 133, 139, 102, 190,
				169, 66, 142, 53, 102, 78, 93, 111, 46, 74,
				249, 177, 182, 190, 60, 235, 182, 81, 215, 234,
				18, 107, 215, 227, 151, 212, 53, 155, 125, 153,
				172, 131, 156, 117, 99, 122, 163, 237, 52, 71,
				162, 113, 242, 226, 249, 70, 239, 212, 124, 31,
				170, 243, 102, 196, 211, 246, 229, 194, 181, 90,
				178, 199, 117, 114, 60, 42, 159, 186, 10, 184,
				216, 102, 195, 203, 142, 85, 192, 99, 149, 236,
				151, 133, 87, 34, 185, 115, 94, 61, 123, 154,
				25, 130, 164, 185, 149, 7, 9, 126, 129, 74,
				118, 186, 198, 221, 255, 226, 101, 201, 69, 134,
				72, 22, 150, 209, 227, 26, 20, 68, 123, 219,
				79, 235, 35, 3, 107, 208, 56, 0, 0
			};
		}

		public static class Firmware
		{
			public static byte[] SpdReaderWriter_ino = new byte[8264]
			{
				31, 139, 8, 8, 0, 0, 0, 0, 0, 0,
				0, 148, 89, 109, 115, 219, 54, 12, 254, 190,
				187, 253, 7, 108, 187, 91, 156, 45, 137, 223,
				226, 46, 75, 215, 109, 126, 81, 18, 223, 18,
				219, 179, 148, 228, 186, 174, 151, 163, 37, 218,
				214, 42, 75, 158, 68, 39, 205, 122, 221, 111,
				31, 0, 74, 166, 24, 219, 105, 231, 107, 227,
				19, 245, 224, 1, 8, 2, 32, 9, 87, 191,
				251, 242, 11, 192, 79, 59, 13, 86, 97, 156,
				192, 68, 100, 50, 0, 199, 25, 141, 135, 87,
				224, 142, 122, 144, 74, 17, 200, 20, 68, 28,
				192, 67, 26, 42, 153, 50, 254, 223, 255, 241,
				97, 129, 179, 36, 133, 228, 94, 166, 126, 148,
				248, 239, 100, 154, 49, 225, 168, 11, 115, 145,
				6, 15, 34, 149, 32, 99, 53, 95, 101, 161,
				200, 84, 246, 229, 23, 44, 50, 150, 203, 36,
				59, 5, 128, 185, 82, 203, 236, 180, 90, 157,
				133, 8, 153, 28, 249, 201, 162, 90, 23, 141,
				69, 179, 138, 246, 29, 142, 217, 190, 195, 91,
				99, 155, 187, 90, 46, 147, 84, 157, 174, 229,
				166, 73, 186, 90, 100, 71, 242, 126, 38, 88,
				248, 44, 140, 131, 81, 146, 169, 106, 179, 214,
				106, 182, 142, 143, 89, 170, 151, 196, 66, 201,
				83, 163, 109, 41, 30, 151, 34, 58, 90, 200,
				234, 34, 124, 119, 156, 170, 230, 34, 183, 107,
				228, 158, 66, 111, 8, 131, 161, 7, 78, 175,
				239, 129, 119, 209, 119, 225, 172, 127, 233, 192,
				245, 224, 210, 113, 93, 120, 61, 188, 134, 223,
				6, 195, 91, 184, 189, 104, 123, 252, 212, 30,
				59, 40, 210, 31, 156, 127, 197, 12, 221, 225,
				224, 172, 127, 126, 61, 110, 119, 80, 200, 117,
				60, 15, 223, 184, 12, 234, 15, 224, 107, 119,
				25, 232, 73, 233, 57, 185, 82, 169, 48, 158,
				101, 71, 243, 175, 89, 11, 89, 241, 93, 149,
				254, 126, 19, 198, 126, 180, 10, 36, 252, 116,
				27, 166, 242, 104, 254, 115, 121, 72, 47, 161,
				53, 248, 44, 53, 84, 171, 80, 60, 51, 121,
				32, 167, 97, 44, 225, 236, 246, 238, 198, 25,
				67, 163, 214, 104, 214, 240, 15, 227, 206, 194,
				116, 193, 139, 134, 11, 154, 133, 73, 12, 241,
				106, 49, 145, 41, 84, 94, 227, 231, 234, 170,
				215, 219, 39, 6, 4, 142, 219, 87, 48, 118,
				111, 71, 144, 233, 53, 129, 73, 168, 22, 34,
				123, 151, 25, 254, 94, 111, 220, 130, 187, 206,
				77, 165, 181, 207, 220, 195, 233, 52, 162, 241,
				69, 18, 72, 11, 117, 204, 168, 99, 141, 186,
				185, 184, 1, 63, 137, 85, 154, 68, 22, 168,
				201, 160, 230, 26, 244, 189, 219, 174, 23, 192,
				76, 27, 69, 81, 221, 2, 12, 36, 72, 229,
				44, 204, 208, 13, 37, 107, 174, 198, 53, 128,
				218, 251, 90, 141, 9, 122, 242, 62, 244, 37,
				120, 143, 75, 249, 18, 174, 48, 98, 192, 13,
				103, 113, 56, 13, 125, 17, 43, 232, 60, 42,
				89, 150, 172, 179, 100, 125, 83, 242, 82, 138,
				79, 136, 190, 96, 209, 23, 101, 81, 94, 33,
				204, 1, 159, 178, 230, 17, 188, 112, 33, 161,
				43, 150, 98, 18, 70, 161, 122, 180, 244, 214,
				73, 184, 195, 194, 253, 70, 23, 213, 205, 132,
				255, 136, 246, 6, 178, 32, 235, 38, 241, 52,
				156, 173, 82, 161, 112, 181, 44, 217, 6, 201,
				118, 89, 86, 107, 28, 165, 137, 146, 62, 225,
				56, 103, 7, 55, 87, 208, 161, 156, 205, 224,
				205, 15, 167, 181, 183, 150, 112, 147, 132, 123,
				219, 133, 167, 79, 132, 235, 173, 211, 19, 91,
				250, 152, 164, 29, 146, 222, 106, 38, 28, 194,
				5, 186, 156, 11, 197, 101, 226, 139, 8, 250,
				49, 46, 214, 84, 32, 176, 63, 180, 152, 78,
				144, 169, 206, 161, 249, 201, 9, 55, 106, 132,
				61, 102, 108, 55, 146, 34, 133, 113, 30, 5,
				248, 178, 213, 0, 39, 77, 209, 112, 87, 9,
				181, 202, 144, 101, 177, 64, 245, 101, 249, 99,
				210, 213, 180, 194, 67, 131, 13, 136, 120, 8,
				164, 149, 92, 172, 38, 7, 224, 205, 101, 186,
				16, 17, 205, 133, 156, 82, 214, 98, 37, 219,
				232, 170, 223, 133, 218, 164, 94, 195, 48, 250,
				233, 39, 104, 50, 3, 15, 70, 236, 129, 64,
				107, 84, 24, 86, 208, 239, 61, 137, 103, 235,
				109, 32, 148, 48, 188, 4, 185, 27, 12, 209,
				170, 86, 189, 118, 2, 144, 75, 145, 113, 249,
				44, 158, 96, 61, 151, 177, 245, 173, 88, 120,
				168, 130, 39, 23, 75, 112, 101, 156, 37, 169,
				54, 131, 83, 180, 216, 56, 150, 98, 38, 193,
				215, 222, 203, 202, 212, 109, 114, 255, 139, 110,
				81, 109, 96, 68, 192, 118, 16, 164, 50, 203,
				160, 102, 33, 235, 132, 116, 182, 35, 235, 6,
				57, 30, 181, 129, 144, 58, 12, 169, 186, 89,
				80, 109, 92, 97, 23, 215, 34, 99, 151, 225,
				112, 217, 174, 166, 225, 64, 96, 13, 50, 29,
				6, 252, 169, 36, 211, 105, 38, 21, 61, 213,
				14, 235, 141, 31, 246, 161, 66, 51, 174, 82,
				209, 161, 63, 141, 125, 139, 143, 173, 255, 209,
				226, 171, 111, 231, 171, 55, 78, 14, 27, 173,
				86, 206, 103, 179, 52, 136, 165, 99, 177, 52,
				182, 179, 52, 90, 47, 14, 155, 39, 205, 173,
				44, 77, 98, 169, 91, 44, 205, 237, 44, 205,
				147, 227, 67, 92, 117, 195, 98, 120, 80, 136,
				125, 212, 40, 86, 132, 157, 201, 105, 62, 161,
				20, 135, 218, 231, 248, 8, 190, 179, 24, 217,
				75, 39, 187, 24, 235, 187, 189, 100, 177, 176,
				151, 218, 187, 88, 26, 59, 189, 100, 179, 176,
				151, 106, 187, 88, 154, 187, 189, 4, 230, 243,
				93, 217, 99, 93, 36, 32, 206, 23, 166, 216,
				104, 214, 103, 63, 155, 30, 179, 67, 120, 180,
				61, 132, 71, 183, 163, 14, 86, 142, 90, 189,
				206, 83, 96, 88, 121, 23, 130, 126, 32, 99,
				133, 251, 143, 76, 161, 171, 247, 67, 252, 14,
				36, 84, 38, 33, 78, 233, 135, 67, 156, 71,
				197, 104, 181, 117, 218, 197, 164, 55, 232, 154,
				45, 242, 235, 94, 2, 113, 162, 192, 23, 169,
				252, 26, 38, 188, 175, 177, 168, 214, 189, 45,
				217, 156, 118, 175, 243, 218, 115, 120, 182, 123,
				233, 222, 58, 46, 13, 228, 118, 220, 247, 156,
				2, 179, 247, 176, 103, 118, 24, 173, 193, 6,
				142, 218, 231, 26, 56, 43, 1, 185, 4, 25,
				160, 219, 109, 15, 58, 215, 46, 104, 165, 153,
				6, 186, 190, 136, 121, 199, 156, 148, 202, 55,
				13, 116, 47, 135, 221, 223, 52, 212, 103, 104,
				129, 2, 62, 190, 110, 158, 60, 208, 75, 29,
				167, 141, 158, 163, 227, 223, 158, 96, 153, 117,
				177, 82, 50, 83, 37, 104, 127, 128, 199, 63,
				111, 60, 188, 36, 250, 165, 134, 142, 194, 184,
				32, 181, 144, 200, 231, 120, 218, 144, 160, 112,
				20, 134, 32, 44, 17, 79, 9, 44, 145, 61,
				1, 196, 139, 85, 164, 204, 28, 236, 48, 219,
				155, 20, 162, 124, 92, 155, 68, 88, 192, 181,
				139, 204, 110, 189, 169, 220, 98, 136, 114, 51,
				105, 23, 139, 49, 142, 62, 131, 96, 136, 167,
				70, 94, 30, 38, 72, 52, 193, 144, 51, 104,
				83, 218, 118, 209, 160, 125, 229, 24, 221, 177,
				22, 29, 136, 133, 220, 212, 130, 74, 220, 254,
				112, 144, 67, 239, 53, 244, 92, 42, 60, 182,
				22, 135, 83, 3, 246, 28, 215, 51, 188, 74,
				131, 61, 84, 205, 81, 186, 138, 67, 159, 143,
				11, 182, 27, 199, 206, 104, 56, 246, 72, 96,
				202, 2, 249, 30, 82, 156, 196, 66, 153, 217,
				167, 212, 158, 227, 57, 93, 198, 31, 51, 158,
				7, 113, 174, 184, 110, 113, 38, 121, 166, 246,
				225, 215, 8, 180, 214, 2, 45, 91, 192, 222,
				156, 47, 174, 59, 99, 231, 156, 4, 230, 121,
				160, 249, 62, 197, 153, 222, 165, 183, 159, 107,
				221, 254, 31, 37, 151, 254, 99, 252, 148, 103,
				120, 22, 254, 35, 13, 250, 172, 221, 245, 134,
				227, 215, 58, 250, 246, 14, 53, 250, 76, 248,
				42, 73, 31, 129, 12, 83, 197, 89, 35, 43,
				221, 24, 16, 147, 31, 152, 48, 249, 82, 92,
				46, 219, 4, 103, 192, 215, 29, 62, 35, 155,
				209, 34, 190, 237, 209, 94, 223, 37, 48, 151,
				25, 51, 90, 100, 131, 61, 122, 94, 100, 200,
				47, 121, 86, 175, 166, 211, 240, 61, 136, 32,
				144, 1, 168, 164, 168, 64, 156, 41, 169, 84,
				171, 20, 131, 117, 149, 166, 24, 197, 156, 66,
				118, 189, 226, 204, 138, 209, 246, 173, 83, 184,
				184, 185, 115, 111, 251, 94, 247, 2, 109, 40,
				242, 150, 104, 85, 50, 155, 69, 146, 47, 37,
				24, 204, 110, 187, 70, 52, 70, 12, 239, 32,
				133, 92, 125, 67, 140, 95, 111, 177, 132, 252,
				188, 76, 48, 2, 50, 203, 1, 163, 225, 192,
				117, 96, 239, 219, 61, 51, 218, 190, 116, 198,
				30, 123, 224, 215, 210, 232, 245, 128, 174, 160,
				3, 246, 11, 19, 235, 51, 91, 36, 84, 153,
				17, 45, 187, 106, 187, 191, 209, 153, 19, 15,
				157, 244, 127, 93, 25, 59, 171, 172, 178, 95,
				92, 216, 214, 214, 192, 195, 92, 198, 108, 114,
				152, 193, 60, 156, 205, 79, 225, 164, 113, 120,
				210, 60, 128, 147, 23, 135, 39, 63, 216, 83,
				16, 145, 76, 85, 57, 10, 47, 219, 55, 14,
				214, 63, 216, 251, 126, 239, 201, 104, 207, 193,
				209, 195, 210, 40, 215, 96, 198, 86, 159, 142,
				50, 246, 207, 63, 247, 44, 101, 180, 106, 38,
				30, 237, 98, 114, 233, 12, 206, 61, 116, 254,
				139, 47, 191, 240, 231, 34, 205, 131, 151, 42,
				202, 27, 243, 250, 237, 75, 155, 111, 147, 170,
				231, 220, 244, 187, 78, 113, 95, 199, 48, 108,
				176, 183, 138, 28, 138, 18, 174, 31, 188, 180,
				25, 166, 138, 220, 72, 18, 123, 26, 87, 195,
				94, 158, 147, 72, 3, 204, 212, 9, 21, 96,
				211, 35, 44, 110, 80, 188, 239, 232, 61, 199,
				144, 152, 44, 117, 61, 195, 1, 42, 93, 149,
				55, 60, 175, 87, 122, 7, 83, 17, 101, 69,
				124, 25, 206, 105, 42, 255, 94, 201, 216, 231,
				34, 22, 198, 170, 217, 184, 83, 250, 213, 155,
				183, 240, 10, 62, 64, 189, 70, 159, 3, 56,
				230, 111, 248, 88, 184, 232, 60, 74, 38, 34,
				130, 123, 145, 134, 98, 18, 145, 244, 170, 16,
				15, 27, 126, 151, 201, 95, 229, 76, 181, 183,
				47, 245, 22, 26, 227, 180, 68, 100, 212, 107,
				161, 19, 148, 145, 18, 247, 130, 5, 29, 219,
				243, 61, 19, 37, 160, 44, 100, 221, 43, 132,
				198, 24, 241, 44, 18, 247, 178, 155, 172, 98,
				213, 213, 137, 189, 22, 207, 159, 139, 254, 68,
				50, 101, 236, 154, 66, 102, 144, 152, 19, 192,
				22, 190, 75, 145, 49, 89, 193, 135, 207, 159,
				75, 54, 73, 146, 104, 237, 12, 203, 174, 39,
				198, 25, 135, 232, 158, 135, 45, 105, 44, 176,
				205, 216, 33, 230, 47, 2, 231, 189, 244, 87,
				20, 42, 182, 24, 59, 51, 160, 45, 78, 82,
				243, 13, 194, 120, 185, 82, 69, 113, 164, 100,
				158, 72, 148, 1, 201, 210, 50, 48, 222, 40,
				50, 191, 131, 117, 85, 166, 111, 154, 13, 90,
				79, 189, 19, 22, 53, 97, 146, 4, 143, 48,
				225, 247, 155, 114, 151, 50, 158, 169, 121, 217,
				139, 195, 149, 34, 221, 169, 37, 30, 49, 12,
				216, 152, 56, 144, 239, 57, 210, 184, 84, 242,
				241, 19, 50, 140, 111, 31, 119, 66, 186, 221,
				98, 140, 231, 207, 240, 129, 26, 106, 62, 210,
				40, 64, 189, 92, 4, 94, 210, 16, 121, 163,
				56, 24, 209, 61, 91, 143, 230, 182, 145, 199,
				104, 224, 35, 213, 233, 30, 178, 231, 113, 109,
				183, 13, 232, 37, 186, 42, 77, 5, 182, 91,
				114, 96, 142, 64, 171, 56, 67, 88, 251, 7,
				218, 23, 156, 193, 129, 78, 178, 3, 24, 94,
				123, 163, 107, 15, 62, 30, 208, 92, 77, 159,
				106, 13, 61, 235, 28, 192, 224, 250, 242, 242,
				0, 250, 131, 28, 8, 57, 116, 42, 101, 48,
				17, 254, 59, 198, 82, 149, 37, 94, 206, 108,
				155, 150, 94, 25, 94, 157, 150, 180, 133, 223,
				241, 209, 144, 67, 23, 173, 163, 145, 100, 90,
				89, 155, 188, 15, 213, 141, 49, 76, 207, 125,
				22, 191, 79, 194, 128, 138, 204, 106, 137, 85,
				255, 3, 141, 172, 175, 66, 171, 37, 248, 140,
				103, 143, 224, 11, 46, 77, 149, 194, 153, 33,
				170, 170, 189, 196, 175, 159, 214, 202, 241, 233,
				251, 239, 153, 6, 0, 104, 148, 154, 81, 37,
				165, 225, 219, 35, 90, 169, 3, 176, 134, 104,
				85, 200, 24, 128, 143, 172, 95, 31, 53, 240,
				101, 198, 157, 159, 88, 68, 21, 109, 107, 169,
				52, 40, 201, 33, 243, 87, 66, 91, 234, 92,
				174, 15, 234, 2, 255, 1, 238, 92, 121, 83,
				152, 27, 164, 19, 57, 11, 99, 166, 48, 147,
				99, 1, 21, 46, 100, 178, 82, 84, 184, 107,
				71, 181, 58, 186, 193, 63, 90, 75, 161, 9,
				244, 237, 105, 76, 133, 75, 162, 94, 19, 155,
				105, 181, 52, 41, 89, 22, 230, 52, 174, 232,
				90, 56, 147, 170, 159, 39, 54, 123, 100, 159,
				157, 15, 118, 157, 224, 181, 219, 133, 183, 224,
				92, 7, 94, 61, 149, 54, 86, 217, 23, 28,
				216, 40, 148, 36, 140, 42, 126, 95, 137, 88,
				97, 83, 17, 93, 99, 163, 52, 191, 37, 102,
				52, 152, 235, 200, 246, 234, 12, 20, 77, 165,
				154, 94, 169, 149, 253, 165, 68, 170, 16, 144,
				82, 113, 231, 252, 86, 169, 136, 179, 69, 152,
				233, 35, 59, 0, 157, 186, 243, 21, 235, 180,
				175, 123, 119, 227, 182, 231, 32, 65, 241, 6,
				185, 205, 138, 32, 179, 14, 9, 174, 105, 197,
				106, 98, 68, 44, 178, 181, 198, 91, 17, 42,
				138, 219, 66, 41, 181, 163, 41, 172, 227, 252,
				10, 130, 111, 66, 189, 217, 132, 255, 8, 149,
				27, 241, 48, 15, 35, 9, 149, 175, 72, 37,
				134, 243, 199, 53, 91, 119, 46, 253, 119, 235,
				31, 47, 104, 240, 155, 112, 26, 83, 93, 186,
				187, 107, 223, 140, 239, 238, 214, 134, 242, 93,
				167, 146, 159, 198, 112, 2, 134, 149, 66, 136,
				72, 89, 90, 98, 109, 158, 26, 247, 224, 35,
				8, 32, 196, 147, 147, 151, 154, 175, 143, 21,
				97, 198, 63, 206, 60, 146, 140, 46, 197, 1,
				115, 178, 146, 188, 200, 22, 37, 154, 151, 246,
				227, 58, 207, 163, 36, 225, 52, 127, 62, 205,
				108, 191, 241, 134, 193, 107, 197, 33, 56, 133,
				10, 207, 79, 220, 139, 48, 18, 147, 8, 85,
				152, 132, 23, 105, 38, 243, 187, 64, 30, 84,
				198, 117, 87, 9, 186, 57, 73, 77, 92, 114,
				60, 231, 163, 198, 78, 170, 253, 105, 66, 23,
				27, 123, 171, 202, 56, 223, 217, 12, 158, 139,
				173, 140, 77, 40, 236, 251, 106, 151, 129, 214,
				54, 9, 175, 116, 225, 102, 51, 33, 191, 34,
				24, 155, 55, 192, 228, 226, 220, 61, 217, 67,
				168, 252, 57, 84, 42, 116, 170, 220, 103, 109,
				180, 34, 172, 72, 35, 76, 211, 141, 27, 23,
				90, 185, 200, 76, 23, 228, 84, 143, 177, 22,
				194, 105, 119, 241, 103, 130, 92, 239, 114, 77,
				166, 177, 97, 19, 153, 94, 137, 197, 196, 200,
				79, 82, 81, 194, 26, 42, 211, 77, 217, 164,
				162, 52, 222, 77, 103, 23, 26, 10, 22, 115,
				38, 50, 244, 69, 15, 166, 76, 158, 223, 51,
				118, 83, 115, 16, 76, 36, 46, 39, 209, 23,
				180, 20, 250, 250, 130, 172, 12, 127, 185, 9,
				99, 41, 97, 6, 212, 162, 235, 208, 115, 202,
				138, 41, 152, 195, 118, 193, 110, 218, 66, 101,
				102, 26, 227, 234, 190, 155, 114, 221, 108, 11,
				194, 89, 168, 68, 164, 183, 80, 67, 107, 218,
				65, 150, 201, 97, 156, 203, 237, 160, 102, 105,
				211, 34, 122, 34, 203, 85, 121, 151, 164, 233,
				97, 104, 13, 37, 107, 120, 188, 204, 69, 207,
				207, 46, 206, 14, 158, 209, 19, 30, 122, 126,
				118, 38, 166, 93, 84, 22, 27, 98, 11, 231,
				147, 161, 124, 46, 213, 198, 15, 146, 134, 184,
				232, 16, 149, 105, 111, 52, 104, 55, 169, 249,
				25, 169, 220, 23, 226, 86, 145, 97, 230, 118,
				82, 153, 22, 95, 63, 239, 116, 73, 123, 78,
				241, 83, 168, 12, 182, 116, 146, 12, 185, 105,
				61, 89, 235, 145, 61, 44, 243, 82, 191, 67,
				147, 105, 57, 5, 178, 220, 93, 51, 212, 166,
				75, 101, 81, 247, 130, 244, 184, 199, 34, 159,
				96, 110, 61, 203, 220, 218, 202, 220, 250, 44,
				102, 187, 123, 101, 120, 75, 93, 47, 171, 120,
				44, 131, 22, 10, 60, 27, 87, 220, 248, 178,
				132, 254, 35, 223, 74, 122, 149, 8, 130, 240,
				95, 41, 47, 6, 100, 80, 134, 167, 198, 131,
				195, 193, 184, 92, 60, 152, 24, 141, 137, 122,
				64, 68, 37, 14, 96, 68, 52, 38, 250, 223,
				173, 165, 123, 190, 169, 105, 26, 25, 247, 197,
				131, 79, 167, 187, 214, 174, 165, 171, 186, 30,
				223, 127, 179, 16, 190, 153, 144, 26, 182, 118,
				19, 28, 58, 233, 33, 228, 209, 161, 105, 22,
				91, 180, 44, 157, 182, 6, 128, 210, 53, 219,
				218, 168, 3, 104, 198, 151, 145, 158, 210, 130,
				42, 147, 254, 249, 115, 46, 241, 49, 42, 205,
				185, 23, 136, 198, 21, 161, 176, 123, 197, 57,
				181, 150, 105, 13, 170, 198, 68, 23, 46, 197,
				196, 204, 196, 118, 140, 161, 182, 108, 36, 149,
				215, 22, 212, 45, 45, 7, 51, 69, 149, 32,
				73, 92, 74, 40, 75, 194, 221, 178, 210, 87,
				139, 92, 91, 1, 64, 133, 245, 235, 163, 145,
				48, 13, 110, 148, 13, 43, 213, 78, 97, 230,
				2, 144, 23, 100, 117, 83, 168, 63, 149, 185,
				124, 125, 99, 155, 124, 117, 19, 113, 55, 40,
				185, 148, 9, 119, 30, 199, 163, 84, 172, 172,
				179, 163, 252, 221, 215, 45, 224, 238, 56, 59,
				216, 119, 209, 24, 27, 12, 123, 179, 150, 86,
				227, 175, 116, 64, 196, 244, 82, 168, 15, 112,
				154, 221, 21, 122, 237, 90, 200, 181, 119, 183,
				95, 27, 219, 137, 145, 9, 93, 187, 122, 249,
				227, 162, 25, 77, 116, 17, 89, 113, 94, 47,
				246, 210, 139, 4, 74, 93, 10, 66, 218, 215,
				251, 251, 181, 202, 42, 75, 121, 37, 128, 84,
				122, 54, 4, 68, 163, 170, 107, 114, 43, 43,
				166, 26, 71, 114, 23, 246, 216, 106, 85, 117,
				249, 37, 79, 48, 179, 1, 116, 10, 250, 42,
				64, 100, 50, 122, 169, 191, 120, 111, 85, 203,
				161, 149, 253, 166, 230, 72, 218, 66, 240, 162,
				222, 239, 94, 53, 238, 141, 154, 172, 33, 106,
				213, 149, 53, 103, 98, 103, 133, 168, 235, 80,
				170, 78, 195, 129, 215, 74, 224, 80, 207, 178,
				197, 245, 114, 205, 232, 19, 25, 165, 39, 95,
				196, 238, 130, 95, 28, 194, 228, 124, 152, 137,
				175, 5, 105, 148, 49, 251, 194, 157, 88, 207,
				19, 21, 158, 73, 131, 134, 78, 252, 242, 248,
				178, 181, 45, 173, 83, 73, 212, 220, 200, 101,
				222, 102, 55, 176, 77, 13, 151, 207, 192, 29,
				111, 70, 35, 23, 37, 44, 240, 135, 79, 84,
				69, 74, 19, 53, 30, 188, 105, 117, 129, 202,
				171, 12, 181, 213, 37, 0, 149, 79, 101, 170,
				227, 26, 125, 138, 31, 166, 13, 22, 225, 144,
				22, 82, 99, 131, 108, 12, 74, 4, 12, 103,
				2, 224, 163, 126, 170, 140, 231, 226, 233, 6,
				218, 224, 191, 189, 170, 107, 41, 30, 117, 177,
				5, 19, 181, 51, 8, 252, 23, 129, 233, 34,
				208, 46, 20, 192, 116, 132, 128, 34, 223, 26,
				61, 234, 6, 87, 95, 162, 4, 249, 215, 79,
				14, 130, 201, 249, 189, 159, 215, 251, 101, 247,
				36, 136, 210, 243, 131, 38, 53, 0, 28, 62,
				128, 140, 94, 81, 143, 253, 31, 94, 177, 235,
				233, 22, 15, 185, 131, 195, 170, 91, 106, 118,
				140, 123, 99, 94, 10, 144, 21, 135, 187, 36,
				75, 78, 134, 185, 22, 0, 116, 12, 239, 201,
				123, 92, 170, 227, 140, 191, 116, 152, 154, 81,
				121, 53, 97, 74, 111, 104, 121, 198, 96, 70,
				102, 21, 89, 63, 206, 152, 19, 42, 112, 33,
				12, 124, 187, 240, 217, 3, 160, 202, 193, 110,
				116, 157, 252, 70, 95, 171, 248, 253, 111, 121,
				41, 76, 233, 26, 186, 4, 24, 213, 200, 233,
				102, 94, 62, 237, 107, 222, 199, 45, 217, 104,
				162, 235, 224, 2, 32, 248, 11, 171, 7, 101,
				184, 242, 135, 203, 112, 229, 168, 12, 168, 177,
				78, 23, 224, 236, 103, 11, 128, 253, 114, 21,
				225, 225, 77, 194, 254, 210, 246, 163, 132, 196,
				230, 216, 62, 196, 230, 169, 109, 142, 11, 173,
				182, 225, 1, 197, 228, 252, 18, 174, 136, 174,
				218, 187, 45, 24, 8, 40, 27, 226, 149, 12,
				60, 48, 50, 212, 155, 62, 160, 248, 144, 210,
				8, 116, 195, 101, 151, 19, 195, 11, 81, 39,
				62, 176, 178, 16, 30, 76, 121, 22, 22, 88,
				209, 184, 164, 161, 101, 249, 130, 31, 101, 157,
				40, 75, 150, 185, 43, 207, 29, 200, 3, 255,
				102, 232, 3, 196, 58, 52, 30, 108, 222, 242,
				116, 243, 203, 13, 179, 253, 28, 167, 96, 84,
				142, 171, 219, 155, 40, 195, 255, 65, 14, 6,
				51, 122, 31, 18, 81, 236, 59, 70, 91, 162,
				79, 159, 232, 220, 27, 52, 37, 221, 98, 191,
				4, 128, 172, 170, 172, 91, 81, 36, 28, 77,
				116, 213, 94, 65, 197, 156, 121, 37, 181, 234,
				118, 14, 146, 213, 72, 59, 226, 42, 39, 211,
				203, 134, 238, 76, 41, 250, 195, 49, 56, 247,
				124, 36, 246, 80, 54, 88, 34, 98, 68, 114,
				44, 129, 200, 149, 114, 106, 52, 166, 182, 20,
				232, 16, 129, 150, 135, 144, 33, 78, 131, 40,
				29, 132, 253, 136, 96, 41, 111, 179, 132, 181,
				115, 142, 183, 222, 148, 62, 183, 206, 90, 0,
				24, 67, 218, 242, 151, 194, 104, 234, 61, 251,
				245, 242, 163, 88, 221, 227, 210, 95, 203, 14,
				220, 197, 21, 184, 160, 178, 136, 48, 102, 7,
				70, 83, 231, 246, 175, 87, 113, 137, 141, 145,
				206, 159, 111, 253, 143, 215, 24, 190, 108, 132,
				70, 107, 176, 208, 191, 111, 21, 116, 247, 158,
				76, 124, 134, 159, 151, 173, 196, 183, 127, 63,
				82, 144, 252, 41, 65, 1, 38, 174, 205, 85,
				61, 107, 143, 175, 172, 231, 252, 54, 34, 221,
				140, 206, 47, 157, 220, 172, 235, 139, 252, 233,
				162, 68, 62, 43, 62, 47, 218, 15, 171, 50,
				81, 163, 102, 10, 254, 138, 206, 80, 228, 155,
				50, 100, 134, 85, 11, 69, 25, 72, 82, 13,
				234, 121, 20, 180, 138, 199, 234, 186, 33, 52,
				162, 50, 104, 18, 62, 149, 202, 133, 123, 161,
				207, 138, 104, 32, 51, 114, 23, 33, 164, 253,
				108, 178, 224, 245, 221, 126, 127, 70, 201, 249,
				216, 222, 236, 149, 136, 212, 200, 11, 113, 129,
				76, 196, 158, 169, 244, 227, 113, 148, 199, 112,
				172, 104, 76, 106, 70, 70, 133, 102, 51, 26,
				92, 163, 11, 52, 208, 5, 196, 219, 164, 118,
				3, 114, 39, 28, 90, 154, 70, 198, 39, 40,
				229, 208, 2, 181, 25, 247, 161, 248, 250, 125,
				181, 198, 157, 165, 205, 112, 68, 199, 106, 2,
				109, 38, 213, 136, 143, 27, 195, 62, 183, 220,
				7, 30, 196, 3, 32, 99, 109, 138, 179, 224,
				3, 91, 21, 38, 193, 90, 142, 44, 207, 76,
				252, 192, 104, 13, 97, 151, 167, 229, 201, 79,
				191, 62, 6, 150, 17, 89, 122, 49, 80, 17,
				219, 224, 68, 95, 146, 149, 86, 239, 172, 251,
				7, 60, 137, 134, 100, 127, 1, 198, 134, 64,
				39, 53, 83, 61, 223, 133, 82, 147, 177, 60,
				153, 200, 147, 48, 243, 198, 101, 125, 108, 46,
				46, 249, 129, 212, 32, 60, 107, 79, 219, 13,
				30, 220, 242, 77, 121, 74, 20, 10, 12, 105,
				84, 210, 215, 55, 231, 229, 244, 217, 45, 111,
				82, 72, 157, 71, 172, 233, 215, 93, 139, 115,
				153, 57, 113, 20, 188, 254, 101, 37, 195, 124,
				214, 15, 22, 14, 195, 43, 70, 227, 144, 191,
				96, 64, 144, 47, 28, 110, 33, 12, 7, 34,
				223, 119, 198, 76, 18, 243, 131, 183, 185, 141,
				150, 241, 59, 187, 63, 183, 92, 121, 209, 29,
				113, 131, 63, 230, 157, 59, 79, 238, 231, 92,
				32, 253, 67, 139, 2, 2, 204, 173, 57, 19,
				192, 251, 168, 63, 254, 124, 105, 244, 93, 231,
				45, 58, 236, 215, 131, 177, 95, 189, 179, 113,
				69, 207, 143, 126, 119, 117, 147, 3, 137, 211,
				200, 22, 228, 236, 127, 36, 127, 218, 197, 19,
				24, 91, 110, 100, 186, 130, 68, 23, 209, 12,
				13, 164, 170, 194, 208, 55, 14, 22, 129, 71,
				218, 3, 184, 232, 40, 75, 157, 227, 93, 224,
				55, 102, 6, 243, 186, 182, 61, 187, 97, 60,
				97, 79, 200, 38, 198, 83, 74, 138, 164, 77,
				171, 67, 132, 77, 204, 72, 216, 239, 67, 165,
				200, 179, 182, 121, 138, 8, 251, 150, 133, 42,
				153, 239, 137, 167, 125, 108, 109, 250, 27, 90,
				126, 194, 224, 9, 166, 83, 166, 166, 115, 175,
				159, 233, 220, 203, 31, 167, 200, 168, 232, 122,
				28, 164, 71, 152, 156, 224, 235, 205, 246, 195,
				6, 114, 245, 59, 53, 140, 48, 244, 8, 19,
				127, 97, 183, 22, 116, 208, 185, 104, 199, 209,
				252, 51, 67, 25, 58, 147, 114, 31, 203, 54,
				195, 93, 155, 2, 10, 246, 163, 50, 191, 202,
				57, 152, 98, 18, 84, 117, 238, 214, 105, 22,
				155, 15, 249, 68, 198, 31, 252, 111, 171, 155,
				67, 124, 169, 238, 200, 150, 156, 134, 97, 191,
				18, 30, 128, 22, 202, 208, 46, 247, 66, 97,
				184, 239, 99, 56, 31, 24, 134, 41, 36, 133,
				48, 165, 41, 205, 150, 27, 190, 29, 73, 150,
				34, 219, 138, 155, 150, 227, 1, 134, 129, 221,
				196, 150, 101, 89, 150, 101, 93, 33, 192, 99,
				205, 61, 241, 181, 211, 199, 38, 131, 36, 203,
				218, 182, 17, 169, 0, 9, 89, 25, 109, 174,
				38, 34, 183, 39, 225, 199, 212, 175, 177, 40,
				253, 240, 3, 111, 194, 145, 59, 118, 156, 191,
				231, 236, 56, 45, 3, 60, 137, 247, 158, 191,
				251, 236, 254, 139, 174, 112, 77, 157, 128, 51,
				77, 8, 182, 138, 119, 37, 44, 78, 33, 77,
				89, 232, 254, 183, 8, 123, 3, 238, 166, 79,
				171, 217, 30, 26, 233, 215, 145, 148, 134, 220,
				158, 162, 225, 0, 127, 66, 205, 161, 165, 164,
				118, 71, 62, 255, 157, 195, 171, 156, 183, 234,
				87, 18, 122, 42, 158, 134, 48, 10, 101, 146,
				31, 117, 38, 213, 233, 106, 78, 1, 71, 81,
				32, 10, 53, 169, 233, 66, 84, 147, 181, 146,
				165, 31, 221, 178, 66, 95, 13, 90, 194, 212,
				192, 18, 137, 196, 65, 44, 238, 6, 145, 171,
				169, 249, 253, 16, 65, 149, 123, 191, 118, 123,
				41, 98, 146, 1, 4, 150, 181, 180, 53, 89,
				186, 125, 71, 83, 205, 233, 161, 127, 93, 159,
				228, 239, 86, 181, 31, 63, 29, 203, 68, 164,
				87, 24, 218, 254, 216, 11, 161, 86, 27, 159,
				180, 225, 88, 3, 153, 123, 191, 199, 131, 7,
				174, 40, 106, 8, 151, 72, 31, 20, 175, 107,
				182, 207, 115, 154, 73, 38, 29, 189, 98, 180,
				21, 27, 74, 43, 170, 247, 174, 193, 138, 40,
				214, 179, 198, 220, 160, 49, 208, 52, 158, 31,
				161, 219, 196, 216, 112, 180, 181, 58, 175, 253,
				229, 220, 46, 68, 71, 195, 184, 227, 209, 220,
				32, 106, 77, 129, 115, 65, 38, 160, 145, 184,
				244, 68, 226, 44, 116, 170, 66, 2, 137, 7,
				166, 135, 226, 3, 168, 179, 73, 16, 32, 181,
				87, 49, 103, 50, 35, 210, 82, 108, 199, 137,
				134, 241, 52, 170, 138, 111, 146, 212, 2, 207,
				52, 197, 109, 141, 91, 112, 52, 112, 157, 251,
				109, 184, 83, 36, 127, 239, 61, 68, 172, 149,
				11, 158, 67, 221, 55, 179, 16, 232, 127, 107,
				63, 105, 100, 61, 119, 67, 195, 9, 247, 192,
				193, 39, 36, 77, 8, 183, 38, 47, 255, 21,
				248, 134, 243, 201, 242, 11, 21, 23, 154, 77,
				22, 11, 160, 186, 48, 115, 143, 161, 236, 7,
				199, 106, 118, 88, 24, 75, 253, 172, 105, 62,
				86, 76, 0, 16, 218, 87, 120, 180, 178, 14,
				115, 151, 139, 60, 189, 203, 73, 163, 193, 199,
				32, 212, 185, 168, 13, 95, 98, 121, 236, 232,
				62, 54, 150, 73, 31, 205, 78, 30, 87, 59,
				111, 234, 162, 33, 80, 236, 28, 84, 116, 119,
				9, 169, 45, 37, 205, 246, 210, 204, 6, 51,
				17, 249, 56, 144, 9, 37, 186, 191, 65, 217,
				140, 107, 253, 71, 84, 119, 5, 204, 185, 199,
				154, 29, 251, 227, 47, 201, 62, 211, 4, 217,
				209, 151, 85, 186, 74, 92, 48, 99, 156, 181,
				202, 72, 161, 81, 94, 204, 38, 95, 32, 37,
				133, 127, 151, 181, 145, 206, 206, 10, 247, 35,
				56, 190, 112, 72, 58, 189, 108, 101, 34, 205,
				166, 99, 146, 152, 157, 22, 57, 67, 189, 252,
				7, 37, 167, 184, 250, 78, 245, 59, 216, 126,
				91, 130, 50, 228, 236, 0, 172, 247, 169, 107,
				219, 144, 139, 6, 246, 249, 138, 155, 255, 225,
				25, 211, 44, 48, 218, 247, 211, 167, 71, 215,
				137, 224, 205, 131, 163, 239, 204, 193, 96, 142,
				5, 93, 108, 238, 98, 228, 42, 231, 57, 85,
				118, 153, 125, 121, 186, 193, 34, 235, 239, 228,
				18, 6, 156, 83, 206, 107, 117, 56, 110, 199,
				11, 10, 174, 135, 245, 165, 208, 60, 46, 75,
				125, 128, 127, 130, 103, 84, 2, 234, 223, 242,
				148, 121, 75, 51, 22, 122, 51, 105, 65, 228,
				3, 89, 169, 154, 213, 81, 68, 73, 11, 32,
				100, 239, 87, 104, 48, 47, 64, 244, 204, 102,
				213, 167, 2, 221, 22, 144, 123, 86, 45, 178,
				106, 81, 44, 155, 108, 237, 9, 190, 196, 101,
				193, 85, 193, 7, 171, 5, 58, 110, 55, 102,
				103, 193, 121, 60, 38, 36, 144, 222, 193, 147,
				99, 138, 48, 150, 130, 210, 74, 13, 156, 201,
				149, 87, 69, 77, 85, 84, 160, 80, 219, 10,
				107, 237, 125, 113, 114, 4, 80, 145, 28, 35,
				146, 15, 213, 220, 229, 236, 16, 206, 208, 30,
				172, 255, 244, 187, 10, 158, 96, 216, 81, 118,
				33, 27, 102, 187, 89, 183, 52, 242, 212, 110,
				50, 83, 37, 21, 238, 71, 5, 197, 176, 167,
				11, 137, 192, 223, 122, 81, 188, 198, 66, 51,
				185, 59, 169, 152, 185, 217, 228, 151, 230, 109,
				106, 28, 240, 50, 61, 193, 227, 250, 68, 23,
				143, 73, 2, 1, 33, 191, 201, 81, 69, 83,
				153, 193, 171, 32, 190, 193, 132, 150, 240, 122,
				30, 150, 237, 227, 80, 194, 131, 72, 253, 50,
				87, 63, 151, 53, 177, 32, 17, 142, 248, 51,
				128, 37, 70, 240, 167, 248, 6, 32, 166, 130,
				35, 20, 226, 19, 226, 190, 60, 9, 112, 229,
				222, 11, 64, 44, 21, 38, 196, 58, 64, 149,
				28, 68, 250, 41, 193, 146, 241, 31, 33, 118,
				223, 3, 216, 125, 75, 225, 227, 48, 66, 53,
				3, 95, 46, 83, 218, 196, 215, 212, 92, 57,
				0, 222, 14, 7, 84, 226, 137, 254, 221, 161,
				127, 143, 57, 237, 84, 251, 188, 207, 179, 177,
				246, 236, 13, 65, 127, 231, 121, 28, 224, 31,
				208, 221, 219, 207, 46, 240, 111, 187, 217, 80,
				172, 37, 124, 161, 171, 65, 71, 213, 68, 6,
				97, 157, 248, 190, 141, 58, 186, 46, 190, 242,
				150, 198, 1, 210, 159, 86, 243, 7, 31, 85,
				236, 231, 254, 176, 130, 253, 159, 163, 42, 131,
				174, 193, 35, 72, 18, 214, 184, 34, 216, 226,
				99, 76, 4, 25, 24, 212, 77, 4, 131, 109,
				73, 158, 41, 151, 160, 130, 37, 157, 110, 230,
				61, 32, 96, 120, 209, 183, 185, 156, 109, 119,
				22, 6, 105, 52, 145, 244, 198, 22, 153, 193,
				155, 249, 205, 246, 155, 57, 189, 27, 205, 102,
				239, 208, 108, 3, 206, 54, 219, 41, 185, 99,
				251, 192, 186, 189, 182, 189, 162, 4, 74, 178,
				51, 85, 136, 27, 80, 93, 55, 250, 119, 135,
				254, 109, 103, 231, 53, 92, 172, 80, 233, 241,
				139, 108, 87, 159, 12, 61, 51, 224, 173, 34,
				47, 94, 195, 93, 11, 50, 155, 119, 122, 23,
				65, 241, 125, 252, 232, 234, 14, 252, 60, 249,
				232, 52, 231, 114, 50, 199, 138, 125, 49, 39,
				167, 217, 205, 110, 7, 217, 220, 186, 153, 120,
				133, 211, 124, 70, 168, 97, 215, 163, 19, 28,
				16, 15, 44, 189, 7, 117, 100, 18, 203, 106,
				237, 115, 131, 40, 172, 108, 172, 48, 240, 86,
				135, 161, 161, 8, 89, 211, 228, 206, 210, 242,
				99, 195, 213, 220, 27, 142, 48, 151, 76, 133,
				128, 117, 235, 106, 186, 7, 201, 128, 150, 135,
				153, 121, 217, 37, 100, 217, 119, 35, 126, 13,
				69, 49, 182, 186, 252, 105, 113, 249, 125, 30,
				90, 149, 149, 175, 247, 37, 174, 128, 221, 44,
				190, 153, 230, 20, 104, 71, 200, 251, 45, 143,
				21, 75, 49, 107, 123, 143, 152, 255, 183, 189,
				232, 88, 101, 130, 24, 176, 125, 63, 246, 215,
				190, 62, 230, 94, 27, 221, 83, 89, 217, 138,
				140, 180, 48, 78, 10, 114, 195, 245, 157, 7,
				5, 178, 203, 58, 241, 11, 21, 254, 132, 120,
				105, 206, 55, 66, 215, 72, 227, 32, 11, 143,
				75, 146, 5, 101, 115, 163, 36, 81, 89, 40,
				19, 70, 205, 252, 169, 73, 64, 90, 182, 195,
				165, 205, 171, 109, 57, 141, 177, 173, 28, 33,
				235, 33, 65, 225, 70, 229, 231, 165, 211, 222,
				178, 119, 169, 0, 176, 102, 69, 88, 188, 53,
				44, 69, 210, 146, 73, 143, 163, 196, 82, 25,
				119, 221, 6, 10, 96, 125, 31, 83, 103, 69,
				70, 129, 130, 31, 64, 96, 118, 47, 187, 133,
				121, 220, 36, 79, 3, 83, 53, 0, 217, 218,
				148, 102, 58, 96, 252, 117, 186, 133, 140, 220,
				62, 246, 49, 99, 210, 233, 150, 179, 62, 24,
				171, 225, 227, 204, 51, 158, 122, 214, 59, 243,
				180, 159, 84, 247, 217, 179, 86, 19, 227, 84,
				171, 229, 235, 130, 72, 71, 76, 68, 183, 144,
				251, 247, 240, 95, 119, 235, 157, 15, 80, 128,
				223, 191, 118, 77, 31, 77, 167, 125, 22, 186,
				17, 193, 155, 3, 71, 111, 175, 156, 32, 239,
				156, 165, 92, 81, 135, 90, 248, 1, 61, 39,
				143, 191, 212, 2, 31, 112, 41, 133, 171, 125,
				137, 161, 69, 135, 33, 86, 51, 180, 44, 55,
				175, 206, 73, 123, 95, 221, 48, 254, 18, 144,
				64, 60, 154, 180, 138, 13, 188, 118, 13, 236,
				230, 109, 66, 33, 107, 217, 106, 43, 116, 79,
				144, 212, 195, 43, 2, 85, 255, 97, 138, 24,
				12, 190, 121, 80, 3, 7, 25, 245, 234, 219,
				117, 124, 208, 121, 83, 91, 164, 11, 54, 86,
				115, 61, 98, 217, 172, 91, 235, 90, 61, 216,
				224, 120, 60, 222, 98, 102, 72, 156, 154, 93,
				119, 183, 219, 69, 177, 144, 97, 50, 42, 72,
				10, 91, 100, 120, 228, 209, 69, 56, 57, 0,
				9, 8, 194, 225, 244, 211, 201, 28, 88, 86,
				42, 150, 34, 159, 29, 196, 162, 117, 7, 173,
				22, 38, 195, 139, 150, 7, 216, 101, 61, 44,
				146, 74, 117, 149, 59, 124, 39, 162, 86, 107,
				76, 255, 14, 85, 61, 37, 59, 56, 142, 58,
				173, 112, 43, 220, 121, 116, 137, 110, 232, 133,
				171, 28, 20, 89, 42, 160, 3, 0, 73, 60,
				43, 231, 157, 231, 108, 250, 202, 46, 100, 235,
				152, 49, 206, 116, 228, 79, 251, 184, 76, 138,
				97, 182, 234, 121, 212, 171, 31, 93, 13, 148,
				145, 186, 46, 6, 73, 214, 249, 127, 214, 122,
				212, 172, 117, 106, 169, 71, 255, 100, 89, 17,
				5, 126, 94, 59, 221, 184, 156, 186, 61, 94,
				146, 117, 8, 71, 255, 167, 75, 47, 189, 70,
				86, 214, 176, 132, 160, 210, 218, 41, 145, 67,
				222, 235, 9, 188, 249, 232, 106, 76, 36, 170,
				245, 189, 9, 171, 66, 17, 219, 128, 249, 237,
				14, 40, 240, 51, 190, 14, 71, 38, 77, 121,
				27, 73, 18, 91, 54, 208, 151, 42, 177, 50,
				123, 100, 116, 54, 40, 175, 132, 111, 92, 5,
				168, 188, 20, 71, 196, 227, 103, 151, 31, 102,
				99, 250, 132, 192, 227, 103, 87, 239, 1, 31,
				241, 207, 55, 239, 61, 214, 95, 174, 94, 212,
				159, 31, 61, 190, 216, 86, 43, 137, 122, 100,
				211, 217, 228, 141, 91, 52, 53, 56, 247, 112,
				12, 188, 137, 42, 224, 62, 89, 157, 13, 16,
				66, 208, 239, 139, 195, 61, 36, 187, 232, 181,
				211, 206, 27, 251, 121, 120, 58, 232, 122, 167,
				154, 228, 88, 16, 93, 110, 149, 234, 7, 87,
				51, 23, 77, 243, 10, 76, 19, 27, 42, 234,
				204, 164, 205, 142, 77, 145, 67, 41, 160, 164,
				137, 40, 64, 199, 191, 1, 215, 77, 130, 200,
				243, 198, 75, 135, 137, 14, 199, 135, 128, 198,
				69, 40, 201, 60, 38, 158, 194, 189, 2, 15,
				79, 195, 175, 247, 188, 167, 35, 12, 13, 212,
				5, 247, 201, 37, 24, 170, 60, 255, 140, 123,
				124, 50, 5, 138, 72, 112, 8, 237, 120, 225,
				57, 221, 151, 56, 186, 48, 158, 132, 232, 7,
				14, 231, 157, 208, 215, 220, 144, 23, 6, 96,
				173, 97, 11, 74, 74, 143, 110, 98, 89, 133,
				68, 25, 187, 90, 252, 57, 95, 223, 87, 154,
				177, 78, 131, 126, 80, 190, 196, 104, 241, 43,
				33, 21, 160, 164, 53, 77, 136, 104, 187, 122,
				59, 8, 223, 156, 110, 222, 140, 248, 13, 87,
				71, 217, 13, 165, 162, 6, 146, 168, 70, 19,
				200, 20, 50, 131, 187, 218, 199, 104, 220, 95,
				162, 250, 9, 42, 77, 177, 196, 196, 29, 62,
				172, 171, 41, 201, 34, 52, 185, 75, 141, 195,
				80, 254, 176, 88, 34, 192, 247, 40, 170, 139,
				167, 98, 69, 100, 79, 219, 176, 229, 240, 130,
				251, 164, 193, 46, 254, 199, 41, 37, 70, 40,
				101, 99, 15, 182, 119, 174, 94, 68, 183, 107,
				52, 35, 156, 78, 181, 204, 217, 161, 129, 51,
				96, 23, 172, 26, 213, 121, 22, 198, 105, 219,
				233, 131, 135, 105, 109, 150, 156, 38, 67, 158,
				119, 25, 96, 145, 212, 181, 50, 150, 102, 112,
				214, 184, 205, 233, 46, 166, 149, 186, 240, 84,
				23, 180, 169, 7, 199, 208, 225, 99, 193, 25,
				230, 205, 193, 242, 221, 54, 67, 2, 49, 110,
				112, 61, 159, 157, 58, 171, 110, 0, 124, 233,
				27, 97, 218, 172, 14, 35, 212, 249, 169, 37,
				195, 180, 201, 64, 166, 96, 90, 196, 125, 4,
				230, 249, 14, 124, 22, 229, 247, 237, 53, 35,
				251, 152, 248, 76, 31, 91, 45, 130, 13, 120,
				82, 85, 170, 128, 117, 148, 243, 108, 201, 70,
				17, 196, 14, 103, 77, 244, 144, 2, 74, 77,
				248, 198, 154, 60, 247, 139, 53, 20, 99, 225,
				3, 92, 23, 79, 86, 132, 201, 125, 14, 19,
				194, 226, 165, 16, 139, 171, 132, 124, 156, 118,
				24, 170, 46, 160, 235, 17, 233, 0, 112, 154,
				153, 149, 8, 183, 105, 64, 146, 228, 38, 11,
				175, 105, 70, 135, 137, 10, 59, 171, 30, 227,
				171, 49, 68, 129, 242, 205, 60, 155, 100, 243,
				226, 19, 165, 209, 176, 110, 203, 217, 50, 92,
				164, 135, 222, 16, 190, 107, 162, 145, 168, 81,
				162, 56, 15, 127, 160, 201, 57, 92, 122, 229,
				128, 218, 250, 5, 122, 226, 38, 1, 172, 65,
				22, 57, 245, 168, 59, 114, 180, 100, 68, 249,
				58, 252, 245, 166, 98, 251, 193, 154, 167, 228,
				102, 161, 173, 101, 38, 81, 129, 236, 203, 240,
				171, 87, 36, 155, 83, 156, 214, 206, 89, 91,
				7, 19, 142, 128, 82, 180, 149, 204, 144, 220,
				234, 165, 206, 188, 53, 223, 233, 107, 177, 172,
				224, 93, 26, 57, 73, 114, 82, 162, 68, 109,
				163, 91, 141, 173, 207, 45, 119, 152, 71, 252,
				68, 166, 39, 139, 173, 144, 27, 110, 247, 103,
				16, 22, 8, 239, 187, 37, 245, 215, 225, 17,
				20, 82, 77, 140, 90, 195, 171, 214, 97, 219,
				189, 253, 198, 189, 40, 125, 145, 168, 105, 148,
				206, 26, 182, 138, 90, 12, 112, 98, 206, 6,
				19, 1, 30, 56, 108, 24, 139, 144, 249, 2,
				154, 97, 51, 146, 173, 212, 210, 92, 37, 196,
				172, 184, 206, 114, 33, 109, 76, 129, 107, 147,
				170, 132, 15, 241, 61, 175, 78, 72, 198, 166,
				204, 250, 128, 154, 100, 23, 52, 37, 106, 183,
				73, 130, 58, 155, 42, 214, 139, 93, 94, 152,
				121, 218, 4, 40, 130, 29, 110, 182, 154, 240,
				200, 195, 26, 221, 92, 242, 117, 60, 5, 166,
				166, 223, 217, 254, 55, 174, 247, 114, 55, 5,
				158, 99, 52, 136, 53, 13, 217, 9, 42, 147,
				57, 219, 109, 221, 80, 112, 73, 215, 232, 211,
				67, 42, 206, 47, 60, 195, 150, 220, 8, 178,
				60, 61, 61, 4, 225, 125, 42, 9, 82, 117,
				17, 7, 220, 130, 149, 223, 233, 72, 186, 24,
				251, 6, 225, 48, 227, 103, 134, 151, 247, 42,
				176, 0, 242, 203, 2, 89, 89, 219, 102, 71,
				2, 112, 158, 175, 55, 234, 4, 154, 192, 90,
				251, 116, 92, 159, 92, 133, 70, 50, 227, 56,
				194, 43, 78, 63, 206, 76, 206, 32, 164, 24,
				7, 216, 122, 22, 94, 29, 247, 59, 187, 238,
				203, 164, 113, 81, 218, 26, 238, 66, 77, 92,
				203, 208, 147, 64, 73, 212, 178, 247, 115, 224,
				141, 0, 163, 154, 206, 166, 214, 75, 106, 24,
				50, 197, 148, 115, 249, 36, 68, 51, 134, 66,
				242, 30, 220, 133, 22, 94, 61, 56, 126, 34,
				14, 247, 238, 213, 249, 192, 72, 111, 178, 58,
				167, 236, 130, 176, 116, 150, 113, 163, 76, 112,
				1, 126, 248, 112, 154, 244, 210, 70, 105, 66,
				196, 125, 239, 234, 20, 179, 22, 238, 21, 46,
				14, 212, 107, 191, 204, 226, 26, 253, 89, 42,
				253, 63, 152, 172, 234, 34, 138, 98, 192, 93,
				231, 127, 94, 64, 70, 222, 172, 142, 183, 162,
				98, 219, 238, 27, 71, 37, 190, 25, 67, 147,
				39, 67, 14, 77, 247, 121, 17, 47, 180, 174,
				5, 226, 185, 8, 96, 118, 65, 63, 234, 177,
				219, 124, 245, 163, 159, 253, 176, 165, 239, 210,
				217, 54, 89, 182, 73, 41, 114, 110, 168, 68,
				84, 115, 189, 234, 180, 42, 134, 149, 136, 182,
				230, 186, 145, 187, 62, 25, 165, 181, 18, 177,
				1, 176, 61, 9, 13, 180, 115, 10, 140, 200,
				167, 223, 57, 217, 117, 63, 254, 14, 249, 186,
				234, 196, 71, 68, 186, 129, 238, 252, 217, 234,
				205, 155, 34, 151, 171, 219, 252, 32, 170, 141,
				236, 156, 135, 235, 254, 91, 42, 146, 255, 22,
				175, 5, 72, 236, 1, 247, 124, 85, 188, 198,
				95, 233, 67, 133, 240, 11, 20, 125, 200, 53,
				198, 17, 196, 224, 23, 20, 27, 31, 75, 244,
				206, 147, 34, 23, 75, 203, 190, 16, 89, 86,
				81, 168, 169, 209, 180, 138, 171, 169, 123, 248,
				35, 244, 85, 249, 62, 79, 213, 25, 212, 153,
				226, 229, 107, 13, 178, 192, 13, 101, 156, 80,
				216, 68, 93, 80, 81, 62, 22, 103, 45, 201,
				174, 239, 114, 79, 141, 68, 138, 117, 59, 168,
				68, 134, 233, 33, 125, 64, 210, 127, 46, 52,
				31, 236, 217, 205, 126, 242, 143, 34, 216, 172,
				207, 202, 22, 45, 73, 59, 98, 18, 190, 39,
				156, 173, 250, 198, 162, 27, 133, 82, 218, 189,
				22, 245, 37, 65, 238, 240, 248, 9, 200, 8,
				19, 243, 102, 171, 147, 148, 245, 32, 89, 142,
				32, 60, 12, 172, 135, 251, 211, 81, 253, 128,
				134, 67, 69, 211, 139, 130, 193, 99, 111, 36,
				82, 162, 203, 75, 219, 53, 116, 165, 99, 211,
				217, 96, 125, 180, 136, 195, 118, 31, 207, 16,
				78, 48, 95, 201, 0, 202, 241, 247, 64, 154,
				118, 1, 239, 118, 127, 107, 195, 251, 52, 138,
				201, 156, 243, 215, 152, 36, 65, 165, 223, 228,
				149, 69, 182, 78, 249, 136, 196, 106, 187, 1,
				125, 146, 104, 27, 69, 247, 195, 227, 38, 176,
				127, 167, 31, 133, 63, 128, 196, 16, 189, 166,
				165, 232, 187, 249, 208, 142, 17, 41, 214, 255,
				180, 81, 36, 178, 162, 159, 142, 90, 231, 96,
				149, 52, 190, 181, 11, 44, 125, 13, 52, 17,
				119, 14, 25, 175, 157, 213, 134, 156, 228, 100,
				182, 102, 112, 60, 3, 99, 110, 148, 57, 48,
				88, 23, 174, 122, 89, 110, 58, 173, 177, 201,
				195, 200, 10, 7, 178, 154, 92, 87, 100, 5,
				125, 116, 121, 128, 72, 150, 123, 72, 200, 97,
				70, 37, 193, 0, 29, 108, 3, 241, 161, 160,
				232, 21, 206, 106, 68, 207, 201, 5, 38, 132,
				63, 76, 191, 201, 151, 31, 188, 4, 166, 18,
				133, 220, 62, 89, 215, 24, 79, 50, 81, 8,
				66, 68, 208, 108, 86, 190, 90, 98, 178, 206,
				10, 21, 245, 83, 200, 17, 106, 174, 172, 97,
				104, 87, 199, 100, 33, 120, 83, 212, 51, 254,
				2, 216, 163, 71, 142, 84, 208, 183, 229, 148,
				194, 109, 151, 176, 129, 247, 240, 225, 200, 120,
				4, 13, 201, 208, 62, 56, 218, 192, 15, 40,
				50, 94, 166, 199, 124, 99, 157, 125, 169, 167,
				188, 137, 186, 125, 128, 201, 145, 172, 99, 208,
				138, 104, 147, 1, 129, 216, 7, 217, 15, 24,
				125, 170, 65, 118, 244, 233, 16, 226, 65, 250,
				102, 200, 235, 162, 196, 203, 169, 245, 126, 48,
				79, 183, 87, 144, 111, 188, 140, 194, 177, 26,
				2, 144, 216, 110, 100, 104, 230, 71, 241, 252,
				180, 115, 91, 128, 170, 217, 207, 97, 78, 149,
				118, 229, 144, 71, 91, 217, 172, 99, 42, 111,
				138, 121, 177, 44, 95, 219, 169, 120, 73, 228,
				247, 231, 179, 47, 206, 190, 95, 214, 212, 173,
				200, 7, 228, 93, 147, 77, 77, 187, 152, 168,
				72, 246, 127, 244, 177, 77, 150, 249, 172, 168,
				209, 237, 128, 161, 79, 244, 41, 230, 212, 151,
				112, 100, 174, 161, 230, 206, 97, 195, 177, 151,
				20, 86, 209, 10, 30, 146, 205, 221, 139, 116,
				34, 189, 72, 127, 94, 172, 174, 59, 133, 36,
				238, 220, 226, 173, 39, 205, 177, 159, 8, 15,
				28, 210, 188, 155, 202, 94, 252, 121, 106, 155,
				69, 247, 126, 57, 202, 198, 173, 241, 133, 97,
				245, 60, 215, 112, 140, 46, 35, 31, 32, 206,
				180, 245, 213, 189, 251, 253, 206, 152, 61, 173,
				58, 91, 155, 2, 12, 180, 12, 49, 121, 237,
				90, 40, 108, 121, 136, 66, 235, 24, 160, 131,
				100, 26, 142, 194, 188, 49, 250, 136, 64, 173,
				54, 93, 62, 206, 99, 163, 226, 212, 148, 182,
				89, 87, 86, 109, 167, 195, 64, 45, 106, 248,
				54, 80, 136, 5, 66, 43, 175, 36, 121, 117,
				167, 47, 218, 36, 219, 95, 211, 51, 48, 199,
				175, 130, 0, 0
			};

			public static byte[] SpdReaderWriterSettings_h = new byte[487]
			{
				31, 139, 8, 8, 0, 0, 0, 0, 0, 0,
				0, 149, 146, 205, 106, 219, 64, 20, 133, 247,
				129, 188, 195, 193, 93, 180, 53, 181, 101, 199,
				118, 33, 134, 44, 252, 75, 54, 105, 132, 157,
				184, 116, 21, 174, 71, 87, 210, 96, 105, 70,
				204, 143, 67, 54, 125, 246, 74, 19, 59, 194,
				93, 20, 122, 23, 3, 26, 221, 239, 156, 163,
				131, 162, 238, 245, 21, 234, 153, 153, 196, 75,
				165, 177, 39, 203, 9, 86, 171, 120, 243, 248,
				128, 109, 188, 132, 97, 74, 216, 128, 84, 130,
				87, 35, 29, 155, 176, 255, 251, 63, 38, 0,
				107, 109, 160, 143, 108, 68, 161, 197, 129, 141,
				13, 130, 241, 2, 57, 153, 228, 149, 12, 131,
				149, 203, 189, 149, 100, 157, 189, 190, 10, 200,
				134, 43, 109, 167, 0, 114, 231, 42, 59, 141,
				162, 76, 214, 43, 251, 190, 208, 101, 52, 164,
				155, 114, 20, 213, 249, 122, 155, 144, 175, 247,
				179, 205, 182, 245, 85, 165, 141, 155, 126, 112,
				169, 54, 190, 180, 125, 62, 102, 20, 224, 181,
				84, 73, 172, 173, 139, 70, 131, 201, 104, 50,
				30, 7, 106, 169, 21, 57, 158, 182, 110, 21,
				189, 85, 84, 244, 75, 142, 74, 121, 24, 27,
				55, 42, 79, 185, 126, 105, 15, 65, 10, 156,
				72, 135, 35, 21, 158, 45, 164, 130, 203, 165,
				69, 42, 11, 6, 9, 161, 77, 34, 85, 6,
				167, 241, 166, 189, 129, 173, 88, 200, 84, 138,
				246, 115, 133, 86, 169, 204, 188, 33, 39, 181,
				106, 132, 187, 81, 115, 70, 93, 244, 238, 176,
				208, 101, 233, 149, 20, 225, 37, 44, 59, 87,
				139, 89, 220, 245, 208, 108, 125, 74, 56, 149,
				138, 17, 63, 110, 158, 112, 154, 45, 27, 73,
				5, 16, 69, 151, 176, 69, 92, 87, 241, 13,
				53, 66, 190, 112, 144, 22, 157, 247, 221, 78,
				31, 139, 156, 84, 198, 112, 250, 124, 247, 188,
				157, 119, 80, 183, 5, 85, 195, 71, 70, 253,
				124, 254, 49, 108, 191, 53, 158, 207, 158, 151,
				47, 155, 217, 211, 10, 192, 112, 56, 185, 25,
				12, 26, 227, 115, 134, 166, 123, 236, 201, 39,
				48, 228, 184, 181, 14, 77, 133, 0, 239, 76,
				29, 224, 193, 91, 135, 146, 156, 200, 81, 25,
				157, 25, 42, 63, 91, 216, 32, 211, 42, 244,
				219, 94, 98, 169, 236, 169, 185, 191, 203, 184,
				223, 189, 172, 126, 224, 52, 183, 205, 17, 50,
				221, 203, 44, 199, 78, 23, 142, 50, 198, 151,
				219, 221, 87, 56, 157, 101, 5, 163, 146, 234,
				2, 94, 207, 207, 240, 247, 127, 192, 41, 115,
				178, 39, 113, 184, 196, 183, 179, 225, 135, 249,
				108, 120, 198, 155, 107, 180, 110, 127, 0, 85,
				232, 57, 108, 105, 3, 0, 0
			};
		}

		public static class Database
		{
			public static byte[][] IdCodes = new byte[15][]
			{
				new byte[922]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					48, 0, 93, 84, 205, 114, 218, 48, 16, 190,
					239, 83, 232, 212, 129, 67, 211, 67, 159, 192,
					53, 164, 120, 130, 41, 197, 110, 146, 201, 77,
					49, 91, 179, 69, 150, 28, 73, 46, 248, 237,
					251, 201, 73, 26, 146, 97, 96, 240, 202, 90,
					125, 127, 171, 172, 92, 80, 86, 22, 116, 173,
					197, 55, 7, 49, 123, 186, 30, 254, 72, 12,
					3, 125, 175, 151, 180, 210, 222, 75, 160, 149,
					68, 141, 69, 42, 108, 231, 2, 126, 35, 27,
					42, 174, 106, 124, 166, 7, 31, 196, 80, 233,
					172, 51, 18, 15, 210, 168, 146, 59, 231, 133,
					3, 106, 33, 242, 145, 174, 61, 115, 104, 180,
					97, 53, 43, 93, 116, 222, 25, 61, 167, 141,
					142, 226, 172, 54, 180, 89, 230, 180, 203, 51,
					218, 233, 49, 30, 216, 89, 202, 157, 229, 179,
					182, 81, 205, 118, 174, 57, 158, 216, 152, 57,
					85, 204, 79, 180, 185, 223, 170, 217, 22, 32,
					165, 15, 40, 141, 150, 125, 106, 95, 227, 237,
					160, 10, 27, 162, 31, 58, 182, 49, 208, 141,
					184, 179, 104, 149, 59, 223, 59, 63, 157, 67,
					247, 210, 56, 79, 15, 98, 92, 75, 203, 193,
					187, 200, 205, 193, 202, 211, 192, 84, 38, 182,
					143, 18, 192, 111, 61, 52, 156, 206, 205, 234,
					79, 245, 156, 150, 103, 208, 204, 98, 135, 223,
					170, 46, 165, 241, 142, 13, 55, 209, 59, 43,
					77, 160, 181, 142, 81, 26, 86, 21, 119, 114,
					69, 155, 124, 71, 119, 250, 55, 123, 85, 77,
					68, 147, 44, 237, 203, 209, 197, 183, 146, 106,
					200, 24, 181, 167, 91, 9, 210, 36, 209, 204,
					149, 202, 203, 31, 149, 170, 19, 14, 7, 84,
					35, 85, 85, 85, 208, 116, 14, 180, 238, 47,
					87, 118, 0, 127, 80, 235, 184, 167, 219, 245,
					235, 59, 246, 221, 214, 27, 181, 26, 173, 156,
					233, 199, 77, 49, 33, 106, 156, 221, 15, 77,
					4, 229, 44, 175, 151, 107, 170, 14, 218, 247,
					148, 235, 168, 205, 24, 34, 109, 181, 213, 193,
					217, 4, 101, 81, 83, 62, 246, 158, 67, 160,
					5, 156, 64, 127, 181, 118, 45, 86, 30, 180,
					55, 98, 143, 80, 220, 96, 145, 199, 57, 253,
					170, 203, 156, 234, 3, 138, 98, 91, 85, 166,
					72, 88, 70, 193, 117, 1, 112, 242, 234, 154,
					94, 105, 243, 254, 153, 221, 236, 54, 89, 116,
					158, 211, 10, 158, 142, 201, 74, 184, 117, 132,
					132, 19, 86, 175, 27, 195, 239, 92, 170, 196,
					36, 232, 170, 138, 168, 180, 124, 73, 113, 235,
					29, 226, 244, 5, 95, 54, 234, 86, 16, 193,
					73, 200, 223, 128, 128, 13, 179, 74, 24, 222,
					35, 22, 128, 245, 220, 254, 158, 189, 59, 211,
					214, 12, 225, 133, 208, 29, 7, 100, 213, 170,
					133, 180, 2, 29, 222, 154, 11, 167, 248, 52,
					180, 52, 26, 52, 48, 7, 131, 196, 105, 245,
					106, 138, 74, 207, 40, 191, 33, 243, 87, 148,
					245, 253, 4, 188, 235, 7, 116, 68, 182, 32,
					212, 153, 210, 179, 126, 74, 64, 163, 107, 156,
					81, 75, 219, 2, 92, 160, 42, 47, 144, 95,
					57, 186, 119, 25, 173, 116, 23, 6, 219, 82,
					241, 85, 45, 56, 72, 139, 214, 112, 134, 59,
					186, 73, 196, 114, 239, 66, 232, 157, 32, 141,
					149, 51, 67, 18, 39, 80, 102, 140, 104, 219,
					240, 243, 128, 141, 19, 230, 90, 219, 61, 54,
					173, 248, 100, 56, 198, 207, 91, 221, 28, 181,
					223, 95, 24, 241, 138, 252, 162, 207, 55, 239,
					28, 76, 96, 166, 13, 159, 208, 108, 47, 154,
					202, 85, 165, 150, 255, 227, 77, 91, 246, 191,
					157, 239, 180, 253, 159, 240, 59, 177, 143, 8,
					213, 229, 75, 55, 250, 164, 131, 62, 10, 68,
					97, 54, 104, 43, 237, 33, 170, 41, 157, 84,
					47, 243, 50, 219, 97, 134, 16, 250, 109, 94,
					230, 69, 70, 235, 239, 83, 43, 53, 251, 238,
					204, 62, 77, 3, 46, 1, 231, 49, 243, 62,
					101, 25, 109, 93, 7, 85, 236, 232, 40, 243,
					94, 143, 207, 141, 194, 36, 74, 128, 32, 248,
					163, 205, 199, 112, 91, 13, 255, 32, 224, 95,
					105, 160, 244, 182, 204, 63, 35, 8, 216, 77,
					89, 232, 181, 151, 100, 138, 253, 203, 231, 55,
					175, 126, 14, 218, 72, 28, 63, 244, 217, 72,
					247, 56, 132, 203, 184, 213, 94, 219, 112, 146,
					216, 28, 94, 198, 77, 7, 53, 43, 234, 122,
					154, 108, 223, 49, 176, 152, 57, 6, 202, 90,
					103, 97, 12, 106, 26, 55, 217, 125, 254, 163,
					156, 142, 0, 23, 144, 113, 118, 164, 60, 81,
					217, 113, 96, 237, 209, 43, 43, 171, 89, 54,
					32, 5, 162, 159, 249, 205, 41, 101, 57, 4,
					6, 98, 52, 185, 144, 23, 62, 97, 231, 134,
					227, 201, 249, 35, 14, 199, 125, 231, 250, 40,
					205, 156, 30, 216, 166, 24, 125, 121, 192, 221,
					93, 239, 238, 48, 129, 12, 153, 8, 6, 63,
					186, 193, 219, 139, 100, 166, 196, 240, 30, 154,
					180, 16, 138, 22, 146, 212, 250, 64, 125, 178,
					95, 165, 123, 9, 68, 54, 67, 7, 204, 231,
					203, 153, 252, 7, 161, 208, 125, 138, 33, 6,
					0, 0
				},
				new byte[1045]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					49, 0, 101, 149, 207, 114, 218, 48, 16, 198,
					239, 251, 20, 58, 117, 200, 161, 76, 103, 250,
					4, 142, 112, 40, 13, 6, 18, 19, 250, 231,
					182, 216, 27, 163, 137, 44, 185, 146, 12, 161,
					79, 223, 149, 12, 129, 164, 55, 99, 118, 87,
					171, 239, 251, 237, 90, 42, 231, 122, 47, 230,
					182, 81, 21, 44, 48, 40, 107, 80, 139, 153,
					241, 193, 245, 45, 153, 224, 97, 54, 151, 98,
					130, 1, 197, 132, 246, 170, 34, 200, 116, 133,
					129, 180, 40, 20, 5, 170, 160, 80, 149, 179,
					98, 174, 12, 161, 131, 39, 163, 246, 99, 97,
					159, 197, 66, 194, 247, 117, 54, 21, 107, 170,
					118, 198, 106, 174, 78, 30, 110, 179, 92, 148,
					71, 31, 168, 245, 98, 52, 183, 14, 245, 13,
					44, 170, 157, 234, 96, 138, 90, 105, 178, 41,
					28, 110, 201, 7, 173, 204, 203, 57, 22, 166,
					14, 143, 67, 88, 190, 88, 60, 21, 176, 81,
					53, 217, 161, 227, 71, 187, 37, 23, 196, 173,
					245, 156, 40, 57, 70, 228, 175, 157, 35, 239,
					97, 146, 173, 179, 199, 172, 136, 45, 5, 170,
					69, 106, 147, 52, 85, 193, 89, 163, 42, 47,
					164, 117, 29, 172, 101, 57, 131, 178, 69, 46,
					81, 216, 186, 215, 232, 224, 91, 223, 236, 200,
					139, 76, 185, 202, 225, 115, 128, 57, 178, 22,
					232, 68, 73, 173, 170, 172, 169, 251, 42, 88,
					7, 15, 58, 29, 127, 175, 76, 227, 131, 53,
					80, 244, 94, 85, 41, 6, 114, 199, 229, 189,
					53, 124, 66, 219, 89, 147, 52, 44, 59, 44,
					115, 200, 249, 101, 169, 116, 44, 115, 18, 147,
					213, 53, 129, 26, 135, 220, 226, 219, 95, 165,
					213, 125, 244, 65, 140, 102, 101, 57, 187, 129,
					137, 157, 12, 97, 99, 81, 80, 107, 29, 75,
					153, 132, 26, 3, 223, 193, 106, 110, 250, 200,
					134, 85, 48, 65, 126, 246, 31, 26, 93, 182,
					108, 137, 242, 92, 14, 242, 217, 102, 84, 30,
					84, 248, 75, 78, 163, 169, 89, 123, 187, 79,
					78, 254, 80, 142, 116, 212, 236, 55, 186, 36,
					252, 168, 96, 205, 216, 28, 169, 9, 93, 103,
					149, 9, 32, 113, 171, 41, 106, 7, 229, 58,
					151, 98, 116, 106, 54, 53, 114, 3, 27, 52,
					77, 143, 174, 134, 111, 216, 168, 3, 58, 140,
					222, 125, 102, 1, 226, 63, 65, 121, 144, 241,
					128, 160, 42, 228, 39, 19, 122, 119, 228, 72,
					157, 20, 234, 3, 57, 207, 62, 238, 218, 244,
					19, 205, 81, 204, 67, 13, 223, 123, 163, 58,
					114, 98, 65, 225, 96, 221, 139, 135, 185, 218,
					170, 192, 26, 53, 145, 207, 149, 179, 44, 158,
					103, 245, 163, 242, 187, 23, 101, 68, 110, 118,
					104, 42, 170, 7, 141, 142, 176, 238, 77, 237,
					240, 131, 26, 89, 141, 29, 67, 155, 228, 154,
					171, 102, 23, 202, 142, 168, 78, 65, 99, 248,
					93, 174, 6, 42, 178, 98, 38, 47, 228, 30,
					57, 139, 41, 123, 163, 113, 114, 52, 152, 104,
					92, 45, 126, 189, 227, 59, 85, 93, 208, 161,
					179, 140, 211, 68, 53, 42, 160, 134, 162, 144,
					151, 59, 172, 69, 249, 135, 101, 34, 40, 73,
					189, 88, 145, 119, 140, 9, 220, 58, 139, 117,
					21, 165, 82, 124, 143, 230, 26, 155, 205, 215,
					15, 253, 223, 105, 122, 61, 3, 60, 90, 58,
					86, 228, 125, 192, 13, 148, 253, 1, 69, 126,
					225, 28, 214, 142, 1, 110, 41, 96, 154, 212,
					136, 101, 81, 66, 214, 18, 83, 138, 230, 205,
					1, 241, 233, 220, 241, 229, 252, 225, 66, 39,
					93, 197, 215, 47, 95, 190, 164, 23, 107, 123,
					160, 143, 227, 32, 87, 79, 76, 180, 103, 115,
					96, 229, 20, 71, 175, 18, 52, 5, 190, 170,
					86, 92, 16, 143, 190, 197, 12, 88, 19, 163,
					186, 245, 9, 6, 236, 221, 181, 216, 60, 175,
					13, 153, 100, 132, 117, 105, 31, 13, 87, 168,
					200, 212, 92, 234, 217, 186, 118, 120, 59, 248,
					44, 36, 99, 119, 157, 47, 239, 39, 215, 201,
					9, 38, 137, 157, 10, 239, 215, 90, 186, 74,
					198, 26, 136, 123, 206, 178, 41, 140, 151, 216,
					158, 94, 175, 139, 21, 165, 20, 27, 114, 193,
					41, 218, 122, 49, 109, 183, 223, 32, 187, 47,
					174, 65, 77, 56, 180, 212, 14, 230, 231, 143,
					153, 200, 202, 12, 166, 229, 236, 186, 204, 4,
					13, 125, 142, 166, 136, 145, 60, 241, 121, 3,
					89, 101, 221, 197, 0, 6, 28, 29, 94, 39,
					45, 241, 229, 234, 103, 170, 63, 99, 118, 223,
					248, 70, 163, 252, 209, 95, 103, 172, 93, 79,
					167, 89, 255, 161, 204, 192, 121, 221, 251, 216,
					189, 103, 48, 226, 56, 173, 228, 57, 191, 152,
					110, 206, 143, 83, 212, 123, 140, 241, 59, 126,
					172, 255, 90, 251, 122, 65, 182, 232, 117, 80,
					204, 6, 139, 22, 11, 39, 62, 140, 31, 115,
					96, 160, 59, 69, 186, 190, 222, 96, 67, 189,
					211, 164, 112, 59, 42, 185, 6, 63, 241, 33,
					40, 152, 90, 93, 147, 113, 216, 14, 107, 229,
					244, 201, 145, 170, 69, 55, 44, 203, 182, 103,
					96, 147, 105, 30, 22, 170, 235, 226, 42, 12,
					68, 122, 24, 207, 211, 104, 214, 177, 83, 108,
					232, 220, 123, 86, 72, 9, 115, 146, 206, 30,
					225, 23, 182, 184, 195, 119, 232, 156, 161, 78,
					236, 31, 112, 79, 192, 55, 75, 39, 15, 175,
					252, 105, 168, 139, 89, 177, 44, 255, 91, 22,
					251, 97, 163, 220, 169, 173, 35, 184, 189, 19,
					83, 107, 107, 198, 123, 151, 190, 135, 99, 200,
					59, 213, 196, 251, 100, 213, 150, 52, 35, 175,
					143, 81, 195, 129, 172, 14, 43, 114, 239, 151,
					8, 247, 123, 238, 250, 110, 249, 83, 46, 23,
					11, 120, 232, 177, 230, 78, 227, 56, 39, 127,
					46, 135, 127, 149, 203, 226, 31, 122, 252, 112,
					158, 156, 7, 0, 0
				},
				new byte[1007]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					50, 0, 101, 85, 77, 115, 218, 48, 16, 189,
					235, 87, 232, 212, 105, 14, 73, 167, 31, 127,
					192, 49, 14, 208, 66, 226, 137, 105, 105, 123,
					91, 228, 197, 222, 65, 150, 60, 146, 92, 112,
					127, 125, 87, 129, 54, 200, 189, 128, 88, 175,
					87, 239, 237, 123, 187, 228, 208, 145, 9, 214,
					24, 153, 91, 215, 91, 7, 129, 172, 17, 203,
					234, 41, 147, 75, 163, 46, 33, 172, 69, 214,
					240, 151, 172, 176, 35, 101, 77, 61, 168, 96,
					157, 200, 102, 235, 128, 135, 52, 111, 241, 163,
					44, 158, 55, 69, 46, 178, 186, 181, 74, 110,
					80, 181, 198, 106, 219, 16, 122, 177, 126, 170,
					178, 229, 44, 141, 101, 174, 70, 19, 210, 88,
					117, 164, 160, 90, 46, 138, 34, 39, 175, 172,
					172, 70, 31, 176, 243, 241, 38, 145, 105, 13,
					35, 186, 244, 141, 173, 117, 135, 239, 50, 155,
					203, 183, 91, 82, 109, 7, 230, 70, 60, 129,
					39, 63, 1, 252, 104, 127, 129, 193, 48, 137,
					22, 183, 107, 89, 89, 61, 68, 230, 94, 148,
					246, 200, 213, 231, 104, 208, 129, 102, 22, 252,
					134, 194, 90, 46, 192, 213, 71, 112, 40, 51,
					167, 218, 59, 177, 52, 92, 42, 173, 227, 229,
					188, 219, 45, 196, 6, 181, 85, 20, 70, 49,
					67, 125, 32, 35, 103, 248, 139, 84, 36, 53,
					118, 208, 160, 27, 229, 154, 148, 179, 254, 204,
					72, 228, 183, 165, 117, 33, 233, 125, 69, 59,
					116, 28, 192, 73, 83, 236, 16, 90, 13, 166,
					78, 11, 172, 65, 107, 132, 157, 158, 100, 127,
					65, 83, 83, 212, 180, 235, 6, 67, 10, 206,
					220, 230, 14, 225, 170, 215, 23, 44, 202, 118,
					253, 16, 208, 137, 10, 12, 155, 1, 18, 52,
					139, 108, 150, 63, 37, 17, 62, 123, 32, 86,
					95, 5, 7, 198, 95, 196, 57, 107, 179, 42,
					23, 217, 4, 54, 233, 216, 34, 185, 130, 93,
					44, 96, 29, 7, 99, 174, 124, 155, 143, 141,
					1, 125, 195, 14, 8, 232, 71, 51, 177, 133,
					166, 198, 200, 53, 152, 97, 15, 42, 12, 142,
					76, 35, 74, 116, 216, 240, 105, 106, 195, 188,
					133, 14, 53, 90, 115, 129, 194, 111, 247, 122,
					240, 242, 65, 131, 111, 175, 216, 138, 245, 178,
					172, 210, 107, 242, 214, 141, 30, 52, 49, 164,
					77, 197, 126, 102, 227, 38, 92, 185, 141, 193,
					89, 51, 49, 27, 77, 2, 27, 252, 253, 27,
					56, 109, 234, 171, 83, 112, 216, 161, 44, 65,
					29, 48, 252, 51, 194, 243, 195, 185, 237, 175,
					206, 32, 206, 50, 158, 237, 203, 10, 56, 99,
					247, 251, 116, 22, 3, 176, 58, 74, 86, 153,
					120, 134, 154, 32, 192, 75, 175, 239, 209, 68,
					167, 187, 131, 44, 52, 170, 112, 39, 223, 102,
					223, 138, 239, 55, 98, 133, 13, 107, 47, 170,
					30, 213, 230, 191, 225, 164, 119, 123, 35, 10,
					67, 7, 130, 228, 201, 101, 228, 158, 140, 124,
					196, 112, 228, 97, 226, 30, 102, 124, 100, 107,
					164, 121, 103, 228, 107, 236, 44, 59, 249, 30,
					204, 65, 20, 85, 117, 221, 227, 111, 196, 137,
					169, 133, 138, 19, 179, 244, 49, 187, 62, 82,
					29, 90, 177, 69, 31, 248, 231, 56, 233, 215,
					172, 42, 229, 220, 217, 161, 23, 143, 120, 228,
					183, 195, 212, 190, 121, 75, 253, 135, 248, 145,
					98, 42, 91, 187, 179, 62, 237, 153, 9, 168,
					53, 5, 198, 149, 196, 31, 173, 171, 73, 201,
					111, 171, 106, 41, 51, 110, 232, 210, 183, 214,
					208, 95, 210, 175, 110, 173, 122, 82, 200, 38,
					84, 45, 118, 83, 152, 89, 67, 250, 191, 149,
					149, 115, 132, 248, 198, 161, 155, 130, 222, 222,
					173, 238, 228, 60, 238, 178, 5, 152, 123, 10,
					103, 185, 156, 229, 12, 30, 72, 109, 119, 88,
					245, 96, 4, 71, 187, 88, 245, 253, 39, 81,
					142, 42, 174, 1, 160, 253, 48, 177, 84, 4,
					184, 27, 3, 78, 52, 193, 0, 43, 50, 135,
					20, 209, 3, 210, 8, 215, 202, 44, 229, 27,
					153, 95, 7, 170, 150, 14, 112, 65, 18, 239,
					63, 240, 121, 71, 129, 235, 53, 164, 4, 211,
					184, 221, 16, 186, 184, 97, 96, 244, 4, 103,
					215, 94, 109, 202, 197, 216, 163, 83, 44, 135,
					152, 99, 231, 131, 53, 56, 229, 158, 25, 168,
					169, 233, 228, 219, 120, 24, 13, 222, 136, 143,
					37, 184, 25, 4, 224, 75, 180, 6, 99, 79,
					211, 73, 50, 147, 200, 2, 53, 157, 226, 104,
					204, 108, 23, 151, 158, 168, 14, 227, 208, 95,
					211, 88, 208, 227, 38, 93, 80, 45, 129, 179,
					98, 61, 219, 36, 165, 206, 235, 185, 56, 237,
					40, 89, 129, 217, 187, 234, 197, 46, 205, 75,
					51, 175, 159, 20, 167, 222, 161, 247, 130, 231,
					106, 118, 177, 252, 203, 116, 185, 184, 221, 63,
					131, 239, 200, 224, 171, 117, 114, 240, 61, 193,
					213, 0, 153, 252, 235, 125, 241, 207, 81, 153,
					138, 67, 240, 250, 244, 97, 246, 37, 221, 179,
					212, 180, 175, 35, 114, 73, 23, 235, 65, 7,
					74, 181, 29, 197, 253, 115, 145, 47, 171, 248,
					159, 167, 107, 185, 165, 250, 239, 134, 97, 164,
					229, 54, 178, 87, 140, 244, 223, 46, 252, 238,
					131, 67, 232, 228, 202, 70, 85, 31, 192, 135,
					219, 60, 106, 246, 115, 80, 54, 4, 203, 5,
					28, 234, 120, 215, 51, 130, 62, 171, 9, 26,
					78, 163, 124, 249, 35, 20, 120, 33, 32, 214,
					12, 150, 75, 251, 107, 36, 17, 165, 70, 7,
					38, 252, 63, 66, 91, 248, 133, 162, 98, 66,
					185, 131, 125, 96, 127, 245, 84, 195, 31, 239,
					216, 51, 100, 110, 8, 0, 0
				},
				new byte[992]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					51, 0, 101, 85, 201, 154, 218, 48, 12, 190,
					251, 41, 124, 107, 123, 232, 169, 95, 31, 32,
					195, 50, 67, 75, 24, 74, 40, 93, 110, 194,
					209, 4, 21, 199, 78, 101, 7, 154, 121, 250,
					202, 64, 33, 164, 71, 59, 178, 254, 69, 75,
					10, 111, 209, 68, 246, 78, 61, 55, 209, 135,
					46, 232, 53, 154, 157, 243, 214, 87, 132, 65,
					61, 180, 47, 47, 96, 189, 126, 59, 245, 92,
					35, 219, 78, 231, 104, 141, 127, 167, 214, 76,
					57, 150, 4, 247, 225, 163, 14, 220, 253, 205,
					163, 245, 91, 176, 122, 238, 13, 68, 76, 24,
					100, 45, 9, 218, 26, 25, 42, 175, 71, 190,
					174, 91, 71, 242, 81, 46, 131, 154, 237, 193,
					249, 48, 188, 93, 50, 57, 131, 209, 247, 82,
					119, 106, 1, 174, 131, 254, 197, 196, 82, 68,
					61, 181, 16, 118, 186, 136, 94, 210, 163, 202,
					187, 16, 201, 248, 90, 205, 169, 218, 197, 2,
					92, 57, 204, 157, 173, 103, 247, 132, 179, 10,
					25, 117, 33, 15, 177, 14, 106, 129, 62, 135,
					138, 140, 202, 90, 150, 148, 11, 148, 116, 34,
					202, 219, 18, 157, 158, 212, 13, 177, 128, 180,
					97, 183, 39, 145, 68, 190, 26, 248, 33, 241,
					150, 66, 60, 169, 157, 251, 148, 103, 36, 200,
					37, 232, 2, 107, 225, 229, 202, 214, 8, 83,
					53, 66, 151, 212, 77, 46, 165, 72, 16, 235,
					206, 248, 187, 139, 68, 35, 232, 111, 158, 247,
					65, 253, 196, 24, 33, 201, 26, 249, 10, 157,
					233, 134, 233, 118, 212, 200, 73, 103, 133, 202,
					66, 131, 127, 250, 46, 77, 63, 106, 33, 117,
					148, 52, 201, 87, 95, 49, 212, 53, 108, 173,
					40, 38, 75, 233, 145, 52, 68, 123, 182, 38,
					165, 249, 198, 201, 56, 113, 197, 120, 118, 183,
					151, 95, 90, 50, 123, 123, 18, 244, 153, 92,
					85, 195, 159, 1, 133, 135, 231, 101, 161, 82,
					45, 58, 233, 33, 138, 15, 82, 155, 215, 161,
					245, 152, 99, 237, 185, 235, 179, 19, 70, 102,
					143, 177, 71, 177, 229, 70, 200, 173, 160, 147,
					142, 195, 45, 193, 237, 211, 24, 109, 132, 59,
					139, 158, 157, 104, 29, 214, 183, 150, 247, 195,
					203, 11, 240, 228, 79, 131, 28, 131, 158, 185,
					104, 197, 169, 216, 70, 188, 165, 207, 94, 165,
					91, 174, 112, 122, 140, 7, 50, 40, 168, 180,
					77, 198, 175, 113, 95, 123, 9, 90, 206, 82,
					196, 185, 42, 15, 32, 211, 65, 134, 125, 184,
					52, 207, 148, 24, 229, 232, 244, 60, 150, 106,
					133, 193, 11, 191, 56, 228, 178, 6, 179, 27,
					78, 221, 228, 119, 11, 226, 98, 223, 153, 145,
					119, 6, 155, 211, 235, 70, 120, 178, 42, 102,
					243, 209, 115, 174, 62, 140, 45, 108, 131, 50,
					111, 162, 150, 14, 129, 87, 114, 168, 164, 207,
					145, 225, 218, 195, 255, 42, 187, 132, 228, 109,
					80, 27, 130, 11, 67, 253, 200, 190, 109, 36,
					160, 142, 184, 87, 151, 10, 254, 147, 170, 11,
					169, 44, 52, 158, 83, 194, 152, 100, 60, 137,
					35, 104, 67, 133, 50, 168, 117, 195, 254, 112,
					133, 152, 45, 198, 95, 139, 31, 133, 126, 172,
					183, 79, 66, 149, 89, 26, 90, 205, 220, 11,
					131, 139, 247, 210, 86, 36, 80, 90, 66, 26,
					133, 117, 227, 143, 200, 107, 180, 55, 215, 159,
					186, 84, 18, 148, 57, 129, 3, 181, 117, 175,
					17, 230, 223, 251, 118, 228, 16, 2, 56, 16,
					174, 129, 42, 39, 80, 145, 201, 5, 138, 157,
					218, 128, 69, 177, 106, 208, 143, 105, 0, 143,
					112, 192, 255, 22, 142, 193, 105, 199, 195, 104,
					89, 56, 53, 176, 4, 168, 165, 220, 26, 153,
					131, 11, 80, 72, 165, 76, 134, 30, 144, 183,
					98, 231, 213, 128, 79, 80, 74, 146, 8, 124,
					175, 118, 73, 191, 28, 186, 109, 203, 149, 32,
					152, 150, 17, 44, 185, 189, 138, 176, 199, 188,
					208, 239, 245, 87, 123, 50, 54, 123, 20, 189,
					245, 150, 169, 172, 110, 131, 184, 130, 146, 188,
					42, 142, 20, 194, 150, 162, 236, 187, 87, 95,
					211, 144, 63, 126, 19, 81, 23, 22, 106, 37,
					195, 115, 68, 107, 37, 200, 10, 78, 56, 179,
					199, 116, 78, 188, 245, 219, 37, 200, 163, 119,
					42, 179, 205, 14, 164, 125, 129, 204, 89, 143,
					212, 85, 56, 134, 40, 202, 70, 130, 188, 161,
					18, 189, 0, 58, 47, 211, 211, 91, 8, 19,
					38, 19, 164, 139, 239, 39, 22, 136, 9, 19,
					45, 149, 83, 4, 147, 166, 9, 217, 157, 248,
					129, 85, 115, 232, 144, 245, 226, 86, 200, 92,
					234, 191, 161, 64, 146, 230, 109, 22, 83, 17,
					19, 33, 139, 21, 251, 94, 16, 240, 65, 132,
					220, 151, 229, 180, 78, 145, 171, 203, 148, 225,
					117, 242, 213, 98, 51, 27, 207, 50, 117, 70,
					198, 52, 9, 82, 51, 135, 255, 6, 61, 92,
					20, 156, 219, 115, 78, 209, 236, 94, 8, 237,
					224, 95, 32, 59, 206, 12, 254, 49, 169, 107,
					74, 2, 215, 91, 72, 133, 1, 139, 94, 167,
					221, 152, 26, 61, 146, 187, 13, 218, 42, 203,
					79, 243, 233, 157, 180, 191, 28, 225, 119, 127,
					95, 141, 44, 2, 23, 13, 98, 41, 242, 98,
					144, 159, 134, 216, 165, 31, 32, 10, 233, 78,
					125, 71, 43, 96, 17, 83, 119, 165, 149, 149,
					88, 168, 175, 145, 239, 9, 101, 2, 52, 116,
					56, 219, 228, 186, 74, 194, 86, 88, 182, 255,
					237, 191, 177, 143, 250, 137, 172, 189, 146, 76,
					154, 18, 251, 191, 165, 57, 213, 15, 249, 7,
					0, 0
				},
				new byte[1039]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					52, 0, 101, 85, 203, 118, 211, 48, 16, 221,
					235, 43, 180, 3, 22, 129, 5, 95, 224, 58,
					105, 8, 141, 155, 16, 133, 22, 216, 77, 229,
					169, 61, 39, 122, 248, 72, 114, 27, 243, 245,
					140, 237, 20, 28, 179, 201, 113, 228, 25, 205,
					157, 123, 239, 140, 143, 139, 67, 86, 200, 141,
					211, 62, 52, 62, 64, 194, 82, 108, 156, 243,
					47, 164, 163, 124, 164, 128, 6, 99, 20, 71,
					60, 241, 81, 27, 197, 29, 118, 171, 14, 101,
					238, 173, 109, 29, 105, 72, 228, 93, 20, 135,
					150, 211, 173, 60, 162, 174, 157, 55, 190, 34,
					228, 51, 44, 213, 43, 37, 93, 139, 165, 79,
					26, 98, 18, 138, 12, 105, 239, 100, 225, 91,
					151, 128, 248, 1, 173, 15, 29, 159, 87, 142,
					224, 58, 123, 79, 103, 178, 98, 13, 6, 126,
					67, 144, 247, 152, 94, 125, 56, 69, 241, 88,
					83, 66, 185, 50, 168, 83, 240, 92, 95, 46,
					49, 114, 54, 199, 67, 10, 228, 147, 84, 154,
					208, 37, 122, 38, 45, 238, 209, 195, 153, 24,
					86, 126, 233, 140, 161, 138, 207, 63, 229, 222,
					191, 98, 248, 87, 141, 235, 107, 48, 232, 101,
					94, 83, 35, 246, 62, 245, 249, 112, 137, 82,
					93, 76, 104, 163, 200, 23, 85, 219, 197, 107,
					150, 150, 84, 81, 2, 51, 227, 98, 114, 113,
					31, 254, 214, 244, 226, 6, 34, 150, 211, 170,
					183, 173, 209, 161, 181, 178, 32, 29, 124, 28,
					235, 112, 249, 72, 137, 94, 60, 167, 62, 251,
					96, 33, 145, 6, 185, 77, 165, 248, 177, 217,
					37, 206, 157, 246, 194, 193, 129, 235, 239, 13,
					116, 24, 196, 175, 154, 58, 114, 149, 84, 254,
					57, 189, 66, 64, 102, 36, 156, 48, 60, 80,
					228, 208, 1, 201, 190, 246, 14, 207, 242, 38,
					120, 40, 159, 192, 149, 66, 157, 186, 129, 85,
					206, 49, 237, 40, 228, 202, 49, 175, 13, 233,
					185, 192, 155, 119, 189, 69, 18, 26, 67, 21,
					243, 115, 81, 110, 64, 246, 11, 29, 163, 151,
					217, 39, 37, 182, 200, 47, 75, 121, 105, 121,
					192, 202, 228, 210, 130, 171, 156, 229, 218, 62,
					125, 17, 170, 80, 185, 124, 175, 18, 151, 135,
					80, 94, 245, 254, 129, 29, 227, 48, 66, 156,
					200, 203, 46, 98, 253, 199, 48, 156, 156, 238,
					235, 17, 185, 40, 176, 36, 96, 115, 138, 123,
					166, 88, 51, 178, 40, 247, 193, 151, 173, 30,
					97, 127, 87, 178, 224, 127, 6, 130, 120, 36,
					134, 95, 133, 145, 205, 2, 82, 29, 19, 159,
					42, 254, 97, 152, 40, 118, 141, 105, 227, 181,
					5, 11, 114, 101, 108, 144, 149, 254, 218, 198,
					36, 127, 178, 107, 171, 158, 151, 166, 77, 24,
					24, 24, 87, 158, 211, 180, 203, 127, 137, 149,
					109, 127, 115, 206, 118, 183, 222, 228, 108, 79,
					158, 35, 100, 254, 92, 83, 211, 149, 120, 223,
					90, 56, 225, 117, 193, 7, 58, 163, 17, 189,
					24, 177, 111, 233, 206, 187, 42, 62, 97, 168,
					100, 1, 129, 18, 89, 20, 183, 16, 160, 132,
					110, 106, 163, 204, 36, 106, 237, 208, 213, 134,
					133, 72, 40, 178, 67, 193, 127, 71, 127, 142,
					242, 139, 7, 96, 83, 93, 23, 91, 185, 146,
					69, 74, 105, 144, 53, 176, 92, 142, 233, 157,
					205, 32, 6, 14, 177, 226, 6, 222, 136, 216,
					226, 35, 253, 158, 55, 157, 239, 191, 79, 1,
					29, 192, 2, 247, 49, 61, 90, 170, 189, 92,
					7, 223, 54, 34, 59, 83, 156, 231, 179, 107,
					64, 119, 87, 170, 231, 53, 63, 36, 38, 99,
					152, 66, 205, 131, 41, 21, 218, 222, 84, 189,
					180, 62, 136, 194, 63, 145, 225, 69, 52, 235,
					233, 172, 209, 204, 34, 179, 197, 50, 59, 102,
					83, 52, 15, 20, 82, 11, 134, 233, 225, 5,
					243, 81, 157, 200, 152, 158, 4, 211, 75, 226,
					18, 252, 147, 248, 39, 161, 41, 255, 55, 95,
					246, 60, 219, 85, 119, 155, 251, 245, 205, 238,
					199, 164, 6, 223, 49, 72, 144, 227, 11, 8,
					82, 201, 79, 86, 88, 86, 190, 128, 211, 56,
					26, 147, 179, 199, 25, 224, 169, 101, 15, 162,
					91, 92, 134, 71, 172, 61, 252, 215, 202, 33,
					31, 213, 114, 3, 115, 192, 94, 33, 107, 19,
					106, 81, 96, 2, 231, 9, 250, 245, 44, 85,
					10, 8, 86, 108, 253, 107, 24, 10, 173, 166,
					216, 75, 178, 62, 114, 222, 26, 103, 151, 223,
					50, 156, 248, 159, 180, 1, 75, 228, 59, 222,
					246, 224, 56, 210, 235, 126, 78, 41, 142, 204,
					12, 226, 240, 122, 97, 90, 107, 112, 48, 60,
					62, 110, 212, 53, 67, 107, 72, 152, 243, 235,
					106, 38, 216, 23, 170, 106, 158, 17, 23, 41,
					117, 111, 179, 34, 51, 37, 84, 231, 124, 19,
					59, 78, 164, 10, 2, 88, 118, 43, 171, 5,
					215, 144, 135, 82, 57, 186, 212, 134, 110, 196,
					50, 156, 108, 52, 134, 89, 228, 184, 38, 6,
					250, 7, 2, 171, 97, 129, 255, 93, 238, 187,
					119, 247, 72, 230, 109, 113, 244, 56, 208, 248,
					198, 242, 205, 66, 181, 77, 64, 203, 168, 125,
					51, 149, 151, 181, 29, 117, 91, 82, 108, 12,
					116, 87, 147, 125, 68, 176, 163, 221, 47, 31,
					0, 135, 41, 93, 71, 248, 88, 211, 19, 204,
					178, 92, 236, 165, 7, 78, 56, 220, 78, 253,
					122, 3, 218, 235, 241, 170, 2, 182, 50, 7,
					139, 97, 230, 192, 99, 237, 109, 244, 78, 170,
					92, 100, 20, 42, 63, 249, 96, 82, 4, 10,
					3, 96, 213, 83, 120, 68, 35, 178, 112, 130,
					146, 93, 208, 27, 29, 19, 202, 205, 81, 86,
					188, 156, 123, 211, 222, 173, 197, 170, 45, 33,
					204, 191, 98, 183, 94, 179, 59, 86, 174, 238,
					29, 213, 51, 19, 197, 143, 174, 103, 241, 252,
					7, 150, 102, 95, 161, 63, 8, 0, 0
				},
				new byte[1081]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					53, 0, 109, 86, 193, 114, 218, 48, 16, 189,
					235, 43, 116, 234, 180, 135, 102, 218, 206, 244,
					3, 92, 135, 6, 90, 8, 20, 211, 144, 235,
					34, 54, 102, 27, 89, 114, 87, 50, 129, 30,
					250, 237, 93, 91, 56, 193, 164, 135, 204, 16,
					177, 218, 125, 111, 223, 219, 21, 69, 141, 166,
					177, 192, 250, 22, 227, 147, 231, 199, 160, 22,
					16, 153, 124, 212, 51, 172, 60, 31, 245, 219,
					197, 245, 66, 23, 199, 16, 177, 10, 239, 212,
					207, 247, 249, 142, 106, 189, 66, 179, 115, 222,
					250, 242, 168, 115, 207, 181, 42, 200, 146, 241,
					78, 207, 235, 72, 7, 117, 195, 136, 238, 129,
					208, 110, 95, 178, 230, 190, 170, 155, 101, 54,
					211, 55, 213, 102, 172, 138, 8, 92, 162, 211,
					19, 103, 148, 132, 228, 104, 109, 151, 200, 51,
					68, 242, 78, 141, 14, 6, 44, 109, 184, 9,
					47, 165, 8, 131, 158, 198, 173, 42, 242, 153,
					158, 145, 97, 31, 18, 40, 117, 31, 168, 244,
					61, 196, 46, 101, 62, 158, 44, 10, 253, 102,
					112, 182, 34, 100, 253, 81, 207, 26, 27, 201,
					180, 28, 10, 111, 155, 182, 152, 96, 91, 47,
					167, 122, 10, 155, 160, 86, 200, 176, 37, 67,
					234, 134, 74, 96, 168, 186, 155, 229, 167, 97,
					189, 133, 127, 66, 254, 106, 33, 236, 116, 129,
					85, 203, 123, 219, 152, 232, 89, 45, 174, 178,
					171, 238, 40, 241, 242, 123, 104, 193, 191, 20,
					210, 133, 4, 40, 51, 76, 215, 197, 78, 113,
					143, 246, 243, 89, 183, 230, 69, 223, 255, 236,
					70, 77, 156, 147, 92, 129, 204, 69, 189, 15,
					159, 38, 185, 180, 173, 235, 202, 10, 54, 34,
					99, 98, 207, 141, 33, 176, 103, 34, 169, 124,
					135, 54, 144, 151, 224, 170, 106, 28, 25, 72,
					196, 5, 25, 240, 131, 252, 225, 229, 55, 247,
					80, 109, 224, 148, 111, 148, 93, 23, 58, 11,
					145, 169, 169, 218, 14, 49, 12, 113, 116, 65,
					147, 10, 74, 114, 165, 94, 11, 254, 196, 73,
					110, 52, 17, 123, 78, 39, 17, 254, 32, 123,
					53, 170, 26, 139, 135, 212, 199, 247, 115, 135,
					106, 209, 216, 128, 127, 167, 147, 219, 239, 93,
					212, 88, 156, 52, 6, 210, 11, 70, 67, 2,
					91, 124, 34, 165, 4, 192, 81, 173, 119, 36,
					57, 151, 222, 60, 94, 36, 70, 139, 98, 168,
					248, 44, 249, 207, 34, 75, 40, 34, 55, 48,
					48, 81, 58, 54, 27, 180, 122, 225, 237, 49,
					202, 87, 221, 17, 46, 105, 139, 221, 167, 159,
					83, 210, 35, 139, 38, 178, 119, 100, 210, 133,
					25, 148, 174, 169, 254, 67, 220, 161, 23, 10,
					103, 205, 78, 26, 120, 231, 240, 112, 121, 90,
					68, 70, 168, 132, 151, 55, 24, 130, 231, 148,
					250, 171, 55, 226, 243, 145, 219, 129, 51, 88,
					9, 137, 208, 209, 17, 234, 122, 77, 44, 159,
					66, 138, 107, 110, 97, 159, 172, 131, 47, 224,
					68, 120, 6, 166, 68, 170, 218, 48, 153, 211,
					80, 61, 213, 158, 219, 33, 222, 82, 82, 241,
					110, 182, 42, 212, 200, 53, 198, 210, 255, 244,
					187, 35, 142, 162, 239, 43, 196, 222, 210, 86,
					23, 17, 34, 158, 90, 219, 27, 238, 59, 129,
					235, 162, 245, 116, 154, 171, 76, 110, 87, 148,
					36, 213, 63, 26, 31, 73, 120, 72, 134, 136,
					236, 58, 75, 129, 85, 217, 30, 74, 63, 144,
					66, 101, 215, 103, 38, 45, 168, 172, 64, 95,
					99, 160, 178, 245, 38, 229, 66, 0, 15, 9,
					157, 100, 107, 24, 206, 209, 221, 176, 111, 106,
					133, 89, 49, 201, 213, 236, 106, 124, 85, 200,
					232, 101, 133, 234, 250, 211, 2, 230, 139, 234,
					75, 168, 187, 142, 36, 49, 31, 159, 224, 120,
					17, 240, 133, 61, 108, 245, 18, 65, 40, 141,
					156, 152, 25, 145, 197, 209, 194, 87, 13, 219,
					53, 123, 40, 219, 171, 182, 95, 125, 157, 214,
					38, 170, 175, 249, 228, 217, 118, 119, 32, 125,
					163, 120, 148, 78, 187, 86, 232, 214, 16, 120,
					28, 238, 205, 180, 205, 106, 112, 178, 215, 164,
					29, 221, 38, 68, 78, 118, 55, 234, 7, 85,
					82, 17, 90, 37, 245, 55, 144, 40, 189, 132,
					45, 249, 254, 222, 29, 182, 45, 60, 168, 153,
					119, 17, 236, 254, 121, 255, 41, 186, 163, 61,
					197, 164, 222, 26, 108, 148, 116, 249, 14, 200,
					181, 62, 87, 217, 232, 118, 52, 191, 85, 83,
					207, 190, 234, 135, 138, 192, 246, 73, 187, 10,
					161, 38, 198, 151, 85, 212, 18, 160, 161, 108,
					253, 218, 222, 49, 232, 211, 220, 171, 49, 30,
					188, 59, 231, 183, 136, 216, 230, 76, 139, 191,
					136, 222, 28, 244, 219, 188, 184, 127, 167, 102,
					114, 209, 111, 241, 213, 132, 157, 218, 168, 231,
					46, 93, 156, 215, 141, 5, 55, 244, 75, 129,
					117, 68, 23, 89, 0, 221, 222, 169, 27, 111,
					183, 232, 42, 224, 112, 105, 219, 239, 140, 66,
					124, 240, 164, 228, 222, 236, 44, 2, 119, 185,
					51, 27, 129, 120, 56, 6, 45, 163, 209, 195,
					3, 154, 206, 35, 73, 152, 94, 140, 21, 208,
					147, 96, 121, 229, 3, 89, 101, 181, 140, 237,
					113, 240, 210, 100, 53, 24, 92, 195, 30, 135,
					216, 103, 126, 67, 246, 88, 226, 16, 214, 10,
					75, 175, 242, 240, 68, 209, 236, 6, 95, 140,
					129, 144, 245, 219, 47, 72, 191, 164, 191, 239,
					244, 36, 63, 141, 134, 68, 181, 61, 4, 121,
					76, 85, 118, 64, 59, 104, 100, 255, 26, 144,
					69, 134, 65, 190, 236, 119, 3, 46, 18, 40,
					241, 135, 224, 187, 224, 190, 196, 109, 77, 78,
					78, 165, 0, 216, 160, 230, 38, 130, 37, 3,
					170, 155, 145, 107, 42, 73, 254, 191, 124, 36,
					178, 189, 100, 60, 127, 102, 178, 192, 178, 153,
					83, 19, 246, 64, 150, 220, 163, 250, 209, 0,
					199, 94, 99, 129, 90, 161, 220, 201, 239, 39,
					167, 87, 205, 72, 38, 12, 151, 107, 13, 130,
					24, 156, 169, 255, 89, 241, 106, 233, 173, 95,
					222, 202, 127, 187, 21, 191, 210, 193, 8, 0,
					0
				},
				new byte[1060]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					54, 0, 109, 85, 193, 146, 218, 48, 12, 189,
					251, 43, 124, 234, 141, 153, 78, 251, 5, 1,
					178, 148, 46, 1, 74, 232, 182, 211, 155, 72,
					68, 208, 172, 99, 103, 108, 135, 13, 253, 250,
					202, 78, 74, 147, 165, 167, 120, 108, 233, 233,
					73, 122, 82, 178, 221, 75, 250, 188, 222, 174,
					68, 6, 87, 75, 157, 60, 98, 113, 209, 70,
					153, 234, 38, 215, 186, 16, 11, 84, 106, 213,
					82, 137, 114, 227, 75, 241, 4, 22, 74, 184,
					141, 140, 196, 146, 224, 164, 204, 191, 27, 66,
					23, 29, 191, 162, 214, 84, 136, 93, 225, 193,
					241, 55, 51, 10, 187, 240, 96, 108, 99, 44,
					120, 44, 197, 231, 13, 194, 89, 110, 209, 191,
					25, 251, 234, 196, 220, 82, 117, 241, 50, 163,
					194, 26, 61, 142, 192, 22, 29, 106, 254, 116,
					254, 7, 92, 81, 206, 173, 129, 242, 4, 186,
					140, 113, 150, 228, 26, 5, 183, 13, 233, 87,
					241, 43, 219, 229, 99, 79, 62, 206, 190, 144,
					82, 34, 107, 149, 167, 138, 170, 232, 145, 212,
					84, 27, 45, 210, 182, 185, 24, 166, 248, 72,
					125, 126, 216, 202, 253, 197, 160, 166, 78, 172,
					117, 78, 138, 10, 16, 105, 125, 66, 43, 23,
					3, 125, 98, 128, 228, 138, 29, 217, 137, 251,
					228, 61, 45, 46, 168, 140, 158, 222, 149, 21,
					190, 129, 143, 72, 117, 211, 134, 67, 126, 115,
					30, 107, 39, 126, 6, 242, 57, 214, 84, 24,
					93, 182, 133, 55, 54, 150, 124, 149, 110, 191,
					231, 235, 221, 54, 50, 203, 176, 54, 246, 22,
					17, 229, 246, 69, 68, 106, 70, 207, 85, 139,
					19, 26, 226, 0, 245, 169, 117, 125, 182, 186,
					68, 55, 42, 202, 132, 14, 159, 141, 38, 119,
					231, 144, 20, 151, 112, 209, 77, 121, 112, 28,
					208, 70, 102, 230, 68, 10, 229, 16, 52, 146,
					99, 51, 207, 200, 19, 204, 61, 117, 168, 98,
					75, 99, 252, 21, 144, 83, 156, 230, 1, 29,
					130, 101, 219, 100, 206, 125, 177, 160, 209, 83,
					225, 196, 209, 52, 13, 104, 185, 183, 164, 61,
					233, 138, 145, 34, 240, 51, 159, 59, 142, 242,
					55, 69, 134, 242, 88, 69, 221, 220, 201, 174,
					103, 59, 185, 4, 15, 114, 137, 87, 42, 48,
					70, 219, 46, 115, 153, 212, 104, 169, 128, 62,
					124, 206, 73, 115, 183, 131, 83, 100, 186, 161,
					154, 130, 248, 118, 154, 221, 106, 22, 81, 47,
					56, 84, 88, 120, 78, 61, 80, 74, 56, 117,
					46, 222, 15, 178, 124, 235, 6, 152, 44, 57,
					44, 143, 47, 50, 223, 46, 4, 119, 206, 221,
					92, 104, 96, 221, 178, 67, 204, 58, 146, 206,
					204, 149, 74, 130, 120, 254, 10, 87, 40, 229,
					106, 155, 231, 125, 227, 140, 246, 80, 225, 184,
					15, 43, 107, 218, 70, 28, 45, 149, 168, 7,
					217, 187, 33, 179, 188, 109, 144, 133, 5, 138,
					95, 196, 174, 241, 20, 219, 18, 129, 158, 90,
					223, 90, 148, 97, 16, 156, 252, 254, 220, 119,
					129, 230, 105, 146, 197, 231, 53, 19, 50, 22,
					195, 241, 133, 122, 232, 123, 189, 178, 79, 31,
					63, 126, 140, 86, 191, 208, 154, 213, 52, 193,
					21, 23, 92, 225, 84, 39, 61, 120, 3, 5,
					246, 244, 162, 225, 15, 82, 166, 32, 207, 99,
					105, 174, 112, 54, 22, 226, 45, 61, 27, 152,
					14, 71, 78, 218, 143, 224, 130, 36, 189, 13,
					242, 80, 224, 205, 125, 234, 123, 210, 251, 161,
					244, 50, 201, 57, 131, 51, 105, 242, 56, 139,
					90, 15, 98, 222, 135, 165, 131, 143, 99, 186,
					108, 53, 222, 129, 56, 129, 10, 6, 33, 76,
					197, 155, 153, 178, 141, 153, 44, 210, 245, 49,
					93, 48, 111, 235, 185, 156, 13, 55, 160, 173,
					89, 1, 226, 231, 97, 183, 77, 143, 19, 246,
					121, 240, 159, 226, 4, 221, 38, 94, 129, 76,
					255, 73, 229, 111, 141, 142, 187, 253, 33, 201,
					198, 217, 178, 108, 177, 50, 239, 150, 105, 84,
					181, 162, 51, 138, 132, 39, 137, 60, 95, 50,
					184, 243, 33, 203, 128, 115, 23, 251, 222, 188,
					141, 55, 196, 135, 169, 214, 88, 161, 109, 197,
					110, 240, 30, 126, 11, 122, 32, 54, 165, 238,
					4, 47, 65, 87, 92, 24, 106, 133, 14, 85,
					56, 195, 217, 139, 111, 45, 117, 236, 18, 67,
					239, 209, 22, 252, 18, 243, 137, 171, 118, 189,
					235, 133, 95, 128, 166, 26, 189, 101, 212, 94,
					128, 249, 44, 128, 71, 118, 160, 111, 209, 62,
					246, 12, 45, 76, 107, 8, 186, 124, 50, 118,
					24, 204, 13, 118, 96, 101, 134, 37, 65, 28,
					255, 242, 166, 113, 188, 210, 210, 142, 57, 233,
					10, 35, 4, 15, 192, 239, 139, 105, 101, 94,
					131, 245, 248, 58, 46, 57, 111, 93, 166, 76,
					109, 61, 85, 219, 113, 63, 233, 75, 212, 127,
					24, 31, 247, 159, 141, 154, 84, 224, 81, 110,
					88, 72, 69, 95, 54, 12, 110, 166, 70, 158,
					11, 125, 69, 91, 97, 127, 61, 227, 164, 171,
					190, 47, 160, 51, 232, 30, 21, 184, 48, 218,
					155, 214, 78, 99, 196, 151, 227, 27, 105, 222,
					231, 247, 142, 246, 51, 56, 16, 155, 45, 24,
					196, 62, 254, 105, 173, 103, 219, 105, 98, 95,
					243, 133, 92, 47, 22, 50, 35, 197, 229, 180,
					130, 127, 75, 222, 60, 129, 243, 114, 165, 204,
					9, 212, 48, 244, 218, 44, 201, 189, 78, 60,
					179, 214, 21, 10, 123, 41, 137, 84, 115, 90,
					183, 126, 138, 163, 249, 153, 74, 36, 230, 223,
					52, 104, 87, 224, 241, 189, 194, 190, 24, 21,
					10, 255, 152, 88, 118, 115, 225, 151, 134, 218,
					183, 182, 231, 253, 180, 94, 166, 155, 245, 79,
					113, 192, 82, 46, 169, 34, 207, 172, 22, 164,
					177, 6, 177, 68, 237, 78, 228, 199, 191, 31,
					174, 113, 221, 48, 137, 204, 112, 73, 196, 222,
					154, 43, 85, 12, 22, 246, 32, 148, 143, 241,
					254, 0, 78, 166, 33, 135, 158, 8, 0, 0
				},
				new byte[1146]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					55, 0, 109, 84, 221, 118, 211, 48, 12, 190,
					247, 83, 152, 27, 14, 92, 108, 239, 144, 254,
					144, 22, 210, 181, 52, 97, 27, 220, 169, 142,
					154, 136, 58, 118, 177, 157, 110, 229, 233, 145,
					93, 58, 146, 177, 139, 157, 211, 213, 170, 244,
					253, 73, 37, 29, 116, 47, 167, 182, 235, 122,
					67, 10, 2, 89, 35, 139, 80, 139, 76, 126,
					178, 78, 161, 92, 129, 233, 247, 160, 66, 239,
					200, 52, 233, 165, 12, 206, 154, 64, 125, 39,
					178, 130, 248, 151, 238, 40, 63, 100, 59, 210,
					228, 101, 121, 246, 1, 59, 255, 81, 148, 212,
					104, 132, 90, 46, 141, 18, 223, 118, 164, 108,
					119, 249, 104, 104, 111, 61, 164, 31, 89, 151,
					134, 197, 118, 24, 84, 155, 222, 11, 224, 198,
					191, 228, 12, 251, 224, 85, 171, 193, 212, 50,
					239, 118, 11, 113, 79, 158, 142, 206, 222, 138,
					249, 151, 21, 118, 214, 157, 197, 138, 148, 179,
					168, 81, 69, 48, 164, 60, 255, 220, 7, 10,
					125, 64, 249, 163, 154, 139, 254, 102, 167, 237,
					179, 204, 114, 49, 5, 231, 206, 178, 66, 213,
					26, 171, 109, 115, 230, 217, 137, 197, 157, 61,
					16, 136, 47, 145, 84, 69, 13, 186, 65, 9,
					163, 71, 231, 64, 62, 144, 227, 9, 222, 139,
					69, 37, 211, 60, 35, 50, 189, 131, 56, 241,
					141, 134, 5, 146, 2, 153, 163, 245, 23, 17,
					226, 240, 137, 179, 80, 23, 212, 180, 65, 100,
					243, 199, 121, 38, 166, 26, 28, 109, 218, 243,
					88, 112, 159, 200, 231, 14, 209, 200, 141, 238,
					27, 49, 67, 79, 141, 145, 153, 11, 242, 14,
					195, 147, 117, 7, 47, 86, 192, 34, 61, 178,
					88, 29, 14, 167, 39, 175, 170, 181, 44, 173,
					238, 83, 175, 43, 158, 45, 116, 62, 192, 165,
					171, 38, 22, 246, 234, 78, 122, 173, 144, 14,
					145, 143, 9, 168, 228, 2, 234, 72, 238, 14,
					78, 12, 107, 208, 60, 193, 42, 91, 48, 77,
					11, 36, 63, 245, 53, 24, 249, 90, 120, 22,
					88, 63, 99, 13, 169, 246, 115, 57, 149, 243,
					25, 255, 13, 222, 191, 176, 139, 20, 6, 93,
					7, 0, 173, 31, 202, 158, 91, 93, 163, 233,
					192, 141, 190, 125, 196, 226, 53, 164, 59, 124,
					250, 109, 13, 142, 99, 212, 162, 249, 209, 34,
					3, 68, 167, 206, 27, 251, 244, 215, 82, 102,
					101, 126, 70, 151, 191, 83, 219, 219, 97, 99,
					86, 182, 117, 140, 187, 131, 134, 223, 47, 92,
					169, 66, 45, 75, 236, 72, 89, 83, 247, 42,
					88, 39, 39, 247, 130, 181, 165, 58, 71, 112,
					163, 137, 149, 61, 166, 41, 172, 217, 145, 115,
					231, 184, 67, 125, 229, 246, 64, 218, 42, 10,
					103, 177, 113, 118, 79, 170, 165, 227, 37, 200,
					57, 58, 168, 255, 129, 32, 244, 98, 75, 1,
					15, 163, 198, 185, 29, 11, 35, 11, 234, 184,
					168, 22, 41, 251, 49, 77, 163, 242, 217, 77,
					138, 89, 34, 176, 160, 146, 116, 4, 63, 158,
					81, 158, 77, 157, 34, 192, 53, 188, 68, 230,
					132, 238, 148, 234, 167, 182, 49, 248, 44, 30,
					201, 24, 123, 130, 215, 58, 127, 211, 209, 196,
					180, 69, 214, 40, 235, 120, 70, 141, 227, 237,
					205, 150, 171, 209, 255, 5, 237, 49, 16, 71,
					244, 178, 167, 146, 249, 71, 29, 125, 138, 227,
					19, 156, 197, 22, 185, 17, 190, 100, 113, 114,
					123, 127, 43, 22, 96, 3, 195, 147, 159, 201,
					248, 150, 118, 86, 150, 138, 208, 40, 188, 24,
					56, 193, 104, 79, 86, 159, 128, 191, 170, 255,
					54, 22, 89, 141, 62, 216, 49, 205, 60, 114,
					68, 53, 54, 240, 162, 203, 106, 54, 12, 101,
					164, 148, 107, 203, 120, 248, 149, 173, 51, 9,
					60, 104, 249, 97, 241, 229, 99, 58, 10, 17,
					36, 111, 71, 175, 80, 235, 161, 44, 11, 206,
					40, 63, 139, 76, 37, 61, 74, 187, 15, 79,
					192, 124, 222, 191, 48, 218, 156, 194, 237, 101,
					43, 85, 160, 19, 222, 68, 48, 9, 195, 12,
					141, 183, 227, 4, 21, 229, 50, 61, 125, 165,
					26, 140, 88, 245, 62, 0, 247, 94, 59, 5,
					215, 118, 98, 3, 222, 211, 126, 76, 137, 137,
					54, 48, 195, 19, 41, 124, 197, 245, 3, 139,
					21, 211, 254, 49, 181, 101, 169, 142, 45, 249,
					1, 115, 22, 83, 29, 90, 187, 223, 203, 172,
					15, 182, 75, 48, 46, 201, 92, 128, 235, 172,
					57, 143, 251, 37, 180, 34, 163, 127, 33, 247,
					178, 220, 22, 162, 90, 85, 87, 31, 230, 44,
					212, 243, 136, 213, 35, 207, 231, 74, 136, 11,
					230, 173, 230, 145, 62, 212, 111, 158, 204, 9,
					156, 61, 152, 250, 146, 181, 152, 225, 65, 205,
					96, 147, 14, 145, 192, 33, 17, 56, 200, 242,
					54, 187, 21, 89, 182, 76, 128, 193, 112, 119,
					215, 252, 119, 148, 82, 199, 172, 44, 151, 89,
					250, 116, 79, 158, 129, 241, 154, 189, 196, 81,
					22, 197, 84, 172, 167, 171, 249, 106, 189, 253,
					46, 30, 80, 147, 57, 188, 156, 208, 235, 217,
					115, 7, 153, 67, 151, 220, 62, 129, 6, 163,
					90, 28, 222, 143, 237, 251, 153, 156, 98, 140,
					143, 156, 23, 247, 243, 121, 41, 215, 124, 255,
					82, 122, 118, 118, 5, 206, 191, 193, 102, 193,
					219, 43, 39, 142, 234, 6, 7, 23, 155, 15,
					7, 91, 239, 8, 34, 209, 11, 5, 16, 149,
					3, 227, 21, 154, 183, 68, 153, 159, 216, 137,
					35, 189, 218, 242, 133, 53, 55, 11, 32, 38,
					137, 42, 49, 22, 101, 7, 46, 200, 146, 157,
					132, 6, 95, 34, 85, 217, 190, 131, 223, 50,
					119, 182, 63, 138, 31, 145, 129, 30, 46, 199,
					200, 204, 13, 24, 7, 221, 120, 75, 70, 5,
					215, 123, 83, 30, 65, 141, 228, 41, 150, 213,
					252, 102, 125, 39, 151, 213, 168, 126, 105, 122,
					138, 123, 33, 22, 201, 52, 49, 161, 16, 30,
					192, 97, 18, 61, 47, 214, 147, 172, 248, 180,
					254, 118, 55, 219, 46, 231, 165, 200, 166, 155,
					165, 156, 81, 67, 1, 244, 149, 123, 102, 12,
					28, 123, 6, 35, 11, 216, 121, 222, 178, 146,
					54, 131, 185, 227, 97, 53, 194, 187, 1, 183,
					23, 9, 114, 190, 220, 111, 217, 131, 174, 195,
					232, 155, 15, 236, 226, 200, 33, 37, 54, 214,
					71, 228, 86, 78, 242, 133, 136, 130, 104, 77,
					77, 58, 83, 255, 221, 220, 243, 31, 23, 156,
					84, 31, 86, 9, 0, 0
				},
				new byte[1150]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					56, 0, 109, 85, 203, 150, 155, 48, 12, 221,
					251, 43, 188, 107, 187, 232, 227, 180, 95, 64,
					8, 67, 210, 60, 154, 19, 50, 51, 109, 119,
					14, 104, 64, 141, 177, 169, 31, 153, 208, 175,
					175, 12, 97, 8, 211, 46, 146, 16, 144, 172,
					123, 117, 175, 196, 151, 57, 223, 173, 239, 51,
					54, 71, 168, 36, 143, 192, 104, 219, 136, 28,
					216, 157, 64, 147, 87, 40, 11, 182, 1, 147,
					123, 211, 242, 172, 181, 14, 106, 203, 50, 173,
					48, 183, 124, 169, 114, 150, 212, 96, 172, 86,
					60, 242, 78, 215, 194, 33, 93, 102, 90, 250,
					112, 65, 113, 21, 168, 63, 244, 225, 95, 81,
					149, 64, 241, 79, 218, 92, 131, 98, 205, 215,
					174, 96, 89, 252, 248, 200, 50, 148, 152, 211,
					189, 141, 14, 143, 186, 99, 35, 229, 141, 40,
					217, 138, 242, 248, 74, 171, 146, 221, 237, 191,
					109, 190, 124, 26, 210, 82, 253, 140, 84, 8,
					234, 144, 87, 248, 220, 105, 67, 143, 76, 195,
					238, 12, 212, 90, 57, 190, 193, 220, 104, 62,
					135, 51, 230, 96, 187, 148, 196, 16, 100, 219,
					85, 41, 2, 135, 228, 2, 18, 9, 34, 193,
					185, 60, 181, 253, 169, 66, 138, 75, 219, 231,
					218, 158, 106, 127, 95, 234, 103, 209, 18, 46,
					7, 70, 117, 240, 133, 28, 144, 172, 197, 145,
					101, 181, 48, 142, 39, 10, 76, 25, 162, 172,
					51, 190, 6, 229, 44, 139, 154, 198, 232, 51,
					20, 124, 67, 168, 76, 219, 65, 212, 166, 59,
					129, 69, 84, 95, 78, 238, 36, 214, 130, 202,
					181, 1, 190, 198, 26, 29, 20, 108, 87, 181,
					14, 125, 205, 190, 227, 27, 161, 248, 189, 194,
					101, 60, 37, 109, 7, 24, 81, 125, 196, 223,
					61, 114, 6, 103, 216, 71, 27, 126, 128, 188,
					82, 90, 234, 14, 83, 206, 66, 243, 107, 113,
					97, 51, 239, 136, 198, 147, 108, 249, 22, 220,
					179, 54, 167, 240, 116, 84, 42, 70, 215, 242,
					52, 23, 200, 19, 9, 185, 51, 157, 210, 44,
					115, 34, 63, 13, 237, 156, 210, 152, 175, 136,
					93, 129, 130, 167, 70, 251, 134, 29, 178, 29,
					167, 126, 29, 199, 14, 45, 176, 172, 190, 143,
					5, 18, 210, 250, 100, 111, 208, 17, 59, 181,
					18, 248, 113, 112, 1, 93, 91, 48, 36, 177,
					46, 37, 116, 224, 230, 194, 98, 45, 166, 253,
					15, 96, 64, 234, 38, 52, 154, 173, 65, 84,
					39, 84, 234, 150, 243, 208, 195, 197, 114, 19,
					241, 157, 240, 146, 47, 200, 202, 112, 52, 66,
					21, 142, 167, 245, 113, 17, 0, 174, 82, 182,
					130, 214, 18, 68, 55, 38, 35, 88, 22, 254,
					228, 186, 110, 94, 85, 125, 123, 39, 172, 19,
					71, 9, 239, 88, 212, 105, 53, 102, 77, 229,
					221, 250, 179, 118, 244, 187, 162, 24, 193, 239,
					171, 35, 72, 152, 158, 213, 55, 172, 107, 209,
					242, 4, 165, 168, 145, 31, 172, 183, 21, 170,
					19, 14, 189, 219, 131, 140, 43, 108, 186, 46,
					204, 4, 158, 132, 156, 200, 178, 37, 91, 89,
					71, 0, 186, 128, 222, 100, 246, 58, 125, 29,
					197, 97, 180, 186, 202, 37, 65, 131, 98, 24,
					226, 9, 218, 239, 8, 5, 176, 217, 62, 102,
					119, 82, 216, 138, 83, 81, 246, 85, 43, 96,
					105, 124, 152, 58, 174, 171, 180, 208, 215, 177,
					228, 63, 193, 57, 113, 53, 198, 173, 166, 228,
					213, 186, 131, 115, 115, 243, 173, 125, 199, 119,
					14, 58, 98, 177, 183, 154, 173, 232, 43, 68,
					254, 246, 248, 212, 246, 54, 60, 181, 53, 20,
					56, 193, 70, 215, 64, 128, 132, 202, 97, 104,
					203, 1, 78, 104, 235, 151, 29, 2, 162, 20,
					110, 162, 196, 110, 29, 223, 128, 76, 197, 96,
					207, 144, 50, 24, 35, 197, 82, 144, 194, 175,
					232, 173, 41, 243, 225, 243, 43, 47, 172, 145,
					90, 173, 59, 52, 107, 80, 250, 172, 71, 59,
					255, 172, 168, 130, 3, 85, 222, 8, 211, 193,
					239, 105, 146, 131, 180, 87, 5, 223, 85, 218,
					245, 154, 161, 250, 188, 248, 204, 241, 118, 230,
					118, 66, 149, 122, 178, 118, 6, 106, 15, 194,
					194, 9, 90, 22, 11, 249, 62, 238, 221, 88,
					120, 235, 12, 241, 41, 96, 68, 78, 192, 9,
					102, 210, 130, 194, 203, 203, 216, 129, 166, 176,
					162, 101, 81, 158, 131, 4, 106, 230, 184, 135,
					118, 70, 7, 186, 195, 178, 93, 110, 31, 146,
					56, 202, 250, 197, 187, 187, 6, 177, 185, 246,
					191, 241, 86, 211, 36, 176, 251, 119, 173, 20,
					8, 211, 30, 178, 76, 231, 116, 52, 92, 92,
					111, 150, 52, 59, 176, 228, 33, 141, 88, 228,
					41, 150, 100, 236, 95, 28, 13, 230, 41, 8,
					195, 30, 208, 129, 181, 192, 147, 48, 28, 141,
					65, 27, 100, 102, 119, 250, 18, 202, 169, 233,
					204, 76, 140, 49, 51, 224, 64, 94, 57, 164,
					70, 52, 85, 24, 72, 150, 232, 70, 194, 165,
					159, 8, 113, 89, 163, 162, 34, 125, 197, 67,
					52, 172, 48, 182, 254, 182, 90, 178, 229, 38,
					187, 81, 109, 104, 58, 17, 183, 253, 212, 140,
					55, 100, 83, 225, 56, 68, 84, 124, 20, 143,
					100, 187, 61, 100, 236, 15, 155, 233, 86, 244,
					170, 194, 248, 188, 7, 11, 10, 69, 30, 6,
					251, 237, 94, 231, 85, 88, 38, 23, 44, 85,
					247, 186, 51, 80, 220, 90, 105, 108, 119, 111,
					168, 74, 104, 254, 195, 3, 249, 206, 107, 30,
					44, 225, 29, 24, 62, 243, 150, 120, 90, 75,
					252, 26, 247, 129, 165, 158, 28, 245, 167, 210,
					158, 103, 200, 183, 94, 255, 255, 192, 15, 44,
					54, 58, 247, 246, 181, 164, 177, 1, 226, 120,
					6, 30, 86, 143, 237, 247, 72, 154, 240, 232,
					140, 29, 245, 97, 129, 132, 81, 249, 192, 34,
					91, 10, 83, 132, 109, 93, 240, 71, 16, 210,
					85, 175, 33, 31, 12, 146, 106, 211, 137, 218,
					234, 179, 120, 63, 156, 211, 21, 248, 122, 191,
					248, 182, 103, 196, 170, 18, 200, 201, 122, 39,
					8, 236, 106, 48, 227, 212, 207, 179, 245, 224,
					205, 72, 157, 245, 75, 254, 220, 128, 45, 224,
					186, 240, 246, 1, 3, 156, 88, 36, 157, 158,
					129, 168, 217, 163, 56, 195, 181, 81, 168, 72,
					20, 192, 95, 244, 203, 15, 198, 91, 183, 5,
					55, 221, 223, 253, 54, 86, 52, 226, 232, 235,
					126, 35, 57, 97, 44, 189, 253, 255, 243, 106,
					249, 11, 208, 137, 176, 71, 56, 9, 0, 0
				},
				new byte[1280]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					57, 0, 109, 86, 93, 119, 154, 64, 16, 125,
					223, 95, 177, 79, 61, 205, 67, 242, 31, 8,
					226, 71, 21, 181, 129, 38, 38, 111, 43, 108,
					112, 42, 236, 218, 253, 72, 36, 191, 190, 23,
					172, 10, 150, 227, 209, 131, 236, 48, 115, 231,
					206, 157, 25, 94, 100, 233, 140, 86, 148, 89,
					30, 106, 190, 72, 71, 236, 57, 254, 20, 70,
					242, 153, 202, 216, 84, 126, 150, 210, 57, 190,
					22, 217, 94, 152, 156, 71, 202, 73, 115, 48,
					100, 37, 155, 45, 211, 104, 153, 172, 216, 218,
					215, 130, 39, 178, 162, 76, 171, 220, 103, 78,
					27, 22, 71, 241, 234, 105, 60, 99, 113, 18,
					242, 84, 102, 59, 165, 75, 93, 144, 180, 124,
					82, 109, 167, 44, 61, 26, 79, 44, 161, 49,
					125, 156, 130, 36, 7, 35, 69, 238, 140, 175,
					128, 160, 170, 60, 176, 8, 71, 90, 89, 182,
					73, 55, 87, 7, 53, 95, 80, 69, 78, 230,
					236, 87, 28, 116, 239, 179, 100, 39, 213, 23,
					190, 252, 85, 171, 130, 55, 255, 138, 222, 241,
					114, 21, 196, 171, 21, 255, 126, 177, 155, 11,
					226, 111, 59, 175, 249, 171, 151, 119, 108, 36,
					28, 238, 225, 129, 19, 78, 209, 144, 16, 92,
					157, 110, 72, 61, 121, 122, 21, 138, 71, 165,
					204, 254, 113, 197, 34, 39, 26, 180, 7, 239,
					36, 139, 148, 52, 133, 246, 150, 61, 9, 123,
					216, 74, 99, 106, 190, 38, 158, 26, 145, 19,
					144, 44, 92, 126, 117, 22, 238, 232, 136, 155,
					184, 110, 17, 194, 197, 233, 156, 202, 134, 63,
					30, 235, 45, 174, 92, 205, 102, 63, 239, 3,
					37, 0, 7, 22, 230, 160, 77, 75, 8, 251,
					181, 83, 185, 52, 45, 105, 179, 234, 64, 234,
					55, 27, 69, 235, 213, 25, 135, 177, 108, 41,
					237, 65, 202, 156, 39, 181, 149, 149, 101, 0,
					93, 184, 47, 201, 99, 89, 105, 83, 247, 106,
					113, 142, 140, 163, 227, 218, 232, 214, 103, 42,
					192, 254, 215, 249, 36, 40, 203, 79, 82, 72,
					109, 144, 234, 16, 40, 249, 216, 59, 2, 47,
					35, 178, 206, 80, 230, 248, 207, 38, 223, 141,
					23, 96, 19, 149, 184, 224, 186, 80, 49, 38,
					83, 177, 77, 28, 50, 120, 44, 133, 117, 44,
					22, 71, 235, 21, 155, 10, 42, 60, 176, 2,
					133, 147, 133, 17, 40, 50, 2, 152, 204, 147,
					227, 35, 105, 169, 80, 160, 182, 10, 35, 72,
					238, 169, 139, 102, 189, 35, 171, 123, 117, 233,
					209, 53, 241, 244, 181, 211, 158, 79, 189, 0,
					233, 14, 144, 90, 161, 222, 135, 23, 165, 46,
					165, 251, 212, 102, 223, 198, 45, 75, 42, 164,
					202, 36, 195, 177, 35, 37, 149, 19, 101, 87,
					125, 223, 167, 186, 108, 178, 176, 119, 112, 12,
					176, 103, 215, 53, 210, 77, 188, 194, 73, 7,
					72, 199, 228, 13, 63, 191, 169, 115, 6, 140,
					125, 81, 76, 64, 162, 131, 43, 130, 48, 255,
					73, 247, 63, 155, 87, 42, 117, 75, 144, 210,
					31, 194, 161, 117, 206, 22, 75, 169, 249, 88,
					155, 47, 193, 22, 53, 128, 203, 253, 169, 167,
					118, 240, 180, 19, 240, 232, 145, 58, 143, 41,
					51, 90, 94, 105, 234, 251, 94, 8, 67, 199,
					78, 170, 103, 215, 63, 183, 228, 250, 173, 221,
					222, 158, 169, 166, 34, 100, 59, 79, 244, 104,
					95, 8, 133, 170, 156, 69, 119, 155, 72, 226,
					15, 210, 124, 138, 186, 91, 181, 179, 81, 40,
					148, 16, 234, 62, 52, 178, 151, 226, 99, 137,
					241, 3, 149, 137, 10, 56, 78, 126, 111, 149,
					184, 22, 102, 15, 161, 61, 138, 125, 191, 14,
					246, 98, 135, 51, 218, 129, 194, 129, 60, 39,
					209, 170, 159, 166, 101, 171, 112, 29, 178, 192,
					64, 188, 245, 192, 3, 63, 72, 213, 254, 226,
					249, 21, 33, 155, 198, 238, 26, 142, 228, 135,
					44, 245, 161, 130, 138, 174, 8, 214, 24, 76,
					89, 59, 157, 48, 83, 122, 58, 150, 133, 176,
					222, 222, 112, 253, 253, 92, 196, 59, 196, 101,
					113, 237, 118, 148, 181, 197, 141, 202, 74, 223,
					26, 7, 19, 54, 135, 130, 173, 145, 215, 120,
					47, 164, 96, 0, 174, 7, 59, 24, 24, 138,
					74, 86, 183, 85, 100, 19, 200, 156, 207, 229,
					80, 222, 83, 177, 69, 133, 160, 151, 173, 61,
					253, 215, 58, 39, 187, 31, 42, 37, 208, 89,
					157, 10, 66, 22, 111, 119, 3, 174, 86, 49,
					95, 10, 165, 29, 14, 248, 250, 195, 61, 244,
					68, 130, 158, 161, 247, 134, 167, 23, 73, 181,
					236, 162, 7, 104, 187, 35, 227, 233, 58, 205,
					239, 110, 194, 247, 91, 15, 78, 166, 66, 243,
					148, 122, 243, 187, 163, 125, 88, 216, 218, 14,
					165, 48, 146, 25, 89, 205, 31, 31, 158, 31,
					6, 182, 92, 7, 192, 213, 219, 179, 36, 37,
					115, 49, 144, 111, 160, 144, 234, 89, 187, 224,
					217, 53, 229, 14, 18, 54, 242, 214, 241, 133,
					212, 7, 172, 87, 212, 248, 69, 115, 220, 252,
					241, 45, 64, 161, 223, 181, 169, 218, 142, 194,
					117, 7, 241, 143, 89, 180, 142, 102, 67, 213,
					145, 148, 203, 18, 251, 167, 224, 191, 20, 218,
					199, 216, 102, 147, 140, 75, 121, 60, 106, 136,
					47, 141, 90, 171, 23, 42, 73, 59, 204, 211,
					218, 250, 222, 232, 108, 135, 160, 81, 109, 68,
					81, 158, 64, 255, 241, 194, 16, 144, 98, 67,
					32, 235, 230, 164, 29, 234, 66, 97, 25, 47,
					66, 22, 7, 225, 114, 22, 6, 124, 52, 125,
					57, 45, 205, 118, 142, 18, 84, 197, 222, 254,
					155, 29, 189, 44, 6, 224, 39, 84, 32, 110,
					182, 163, 195, 201, 10, 70, 79, 50, 19, 185,
					112, 96, 30, 207, 139, 162, 167, 132, 105, 237,
					85, 46, 186, 77, 119, 29, 122, 99, 159, 19,
					194, 125, 72, 235, 154, 14, 236, 117, 99, 64,
					199, 222, 67, 169, 108, 23, 111, 202, 86, 112,
					247, 143, 15, 202, 58, 157, 110, 1, 134, 205,
					169, 114, 84, 232, 91, 9, 76, 231, 119, 151,
					183, 146, 217, 44, 229, 177, 200, 141, 56, 205,
					28, 11, 48, 195, 34, 69, 161, 222, 37, 33,
					130, 145, 231, 196, 58, 167, 23, 127, 33, 240,
					153, 119, 223, 221, 65, 45, 164, 103, 178, 82,
					129, 147, 239, 27, 18, 200, 104, 168, 185, 158,
					180, 128, 176, 11, 56, 123, 124, 102, 203, 100,
					150, 70, 155, 232, 244, 62, 215, 244, 195, 188,
					249, 105, 232, 163, 254, 238, 100, 65, 50, 239,
					58, 155, 24, 237, 15, 23, 56, 147, 217, 36,
					184, 127, 124, 77, 163, 129, 120, 169, 52, 98,
					91, 187, 243, 192, 190, 212, 166, 29, 86, 155,
					48, 90, 68, 79, 65, 204, 214, 150, 230, 148,
					237, 155, 165, 43, 178, 1, 47, 235, 48, 92,
					173, 96, 10, 210, 81, 71, 219, 46, 215, 172,
					167, 209, 193, 57, 214, 238, 182, 203, 98, 108,
					187, 37, 71, 83, 161, 205, 30, 37, 253, 166,
					102, 226, 35, 225, 119, 184, 252, 127, 13, 98,
					90, 108, 222, 150, 67, 242, 10, 161, 195, 208,
					136, 119, 136, 248, 240, 192, 191, 30, 52, 62,
					44, 88, 44, 198, 139, 32, 153, 14, 188, 148,
					254, 5, 57, 108, 10, 93, 71, 11, 0, 0
				},
				new byte[1271]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					65, 0, 125, 86, 203, 118, 226, 48, 12, 221,
					251, 43, 188, 154, 105, 23, 51, 255, 16, 222,
					148, 231, 16, 58, 45, 236, 68, 16, 68, 135,
					196, 230, 56, 118, 75, 248, 250, 145, 67, 41,
					78, 201, 116, 209, 146, 56, 138, 116, 125, 239,
					149, 156, 158, 70, 179, 149, 75, 76, 82, 165,
					51, 189, 47, 101, 91, 203, 177, 221, 138, 17,
					169, 125, 124, 196, 68, 180, 245, 22, 10, 58,
					202, 126, 190, 25, 136, 120, 44, 199, 164, 14,
					215, 160, 56, 69, 117, 230, 63, 57, 194, 157,
					251, 154, 132, 114, 178, 24, 196, 172, 227, 165,
					236, 102, 152, 88, 163, 21, 37, 69, 16, 46,
					70, 165, 62, 56, 171, 131, 199, 114, 168, 18,
					241, 2, 198, 144, 54, 97, 228, 114, 49, 156,
					70, 147, 97, 91, 78, 180, 37, 173, 184, 144,
					226, 23, 178, 10, 157, 252, 193, 183, 98, 78,
					39, 204, 58, 84, 28, 51, 40, 125, 150, 27,
					128, 158, 179, 4, 74, 242, 51, 107, 40, 177,
					178, 165, 229, 202, 225, 22, 170, 178, 98, 65,
					73, 106, 241, 32, 231, 250, 29, 205, 237, 165,
					49, 129, 90, 162, 218, 135, 208, 175, 219, 143,
					134, 75, 6, 130, 185, 54, 165, 120, 158, 14,
					175, 215, 1, 222, 59, 162, 6, 14, 118, 62,
					91, 156, 16, 170, 4, 195, 173, 181, 95, 39,
					75, 209, 119, 160, 246, 231, 84, 59, 249, 74,
					170, 36, 57, 224, 96, 78, 146, 31, 157, 69,
					38, 194, 192, 150, 120, 161, 71, 38, 23, 49,
					228, 27, 152, 234, 55, 144, 113, 89, 88, 204,
					11, 241, 247, 87, 191, 59, 21, 79, 46, 63,
					94, 35, 69, 148, 31, 209, 224, 71, 6, 94,
					8, 228, 72, 181, 218, 23, 41, 53, 162, 13,
					99, 42, 210, 90, 250, 236, 175, 107, 82, 24,
					250, 229, 111, 153, 100, 198, 166, 192, 235, 1,
					153, 136, 41, 163, 68, 171, 203, 234, 222, 0,
					123, 224, 10, 144, 243, 155, 163, 54, 85, 100,
					64, 9, 231, 237, 48, 180, 118, 138, 254, 173,
					157, 54, 249, 37, 98, 158, 225, 201, 106, 31,
					144, 249, 189, 20, 159, 150, 138, 38, 177, 124,
					120, 34, 224, 53, 39, 163, 237, 27, 48, 147,
					219, 43, 249, 49, 230, 244, 40, 94, 92, 202,
					168, 159, 200, 67, 38, 248, 64, 195, 176, 20,
					139, 136, 23, 191, 123, 179, 68, 39, 130, 123,
					213, 68, 59, 165, 99, 129, 54, 88, 186, 130,
					184, 183, 53, 235, 196, 44, 110, 225, 59, 30,
					219, 169, 131, 138, 236, 29, 214, 35, 3, 189,
					39, 4, 122, 197, 119, 79, 20, 62, 143, 58,
					127, 163, 105, 221, 198, 127, 8, 84, 10, 36,
					95, 144, 10, 190, 223, 111, 33, 240, 166, 87,
					58, 231, 157, 94, 36, 7, 85, 122, 32, 65,
					149, 234, 138, 49, 243, 115, 255, 238, 167, 79,
					98, 11, 102, 1, 121, 93, 203, 112, 31, 114,
					253, 177, 89, 127, 51, 112, 151, 77, 92, 3,
					158, 51, 107, 224, 66, 99, 133, 117, 138, 239,
					252, 8, 10, 155, 145, 66, 217, 207, 244, 134,
					147, 125, 120, 101, 235, 184, 253, 60, 69, 236,
					20, 117, 64, 35, 58, 4, 185, 86, 91, 49,
					127, 142, 71, 195, 241, 56, 0, 59, 0, 205,
					18, 130, 92, 97, 157, 92, 49, 161, 106, 19,
					234, 83, 141, 22, 24, 220, 105, 109, 229, 20,
					237, 187, 54, 135, 66, 180, 232, 157, 84, 101,
					134, 68, 115, 205, 196, 106, 35, 31, 6, 163,
					199, 79, 200, 29, 57, 156, 246, 102, 53, 75,
					46, 13, 30, 194, 74, 15, 241, 163, 156, 47,
					187, 85, 252, 43, 163, 244, 131, 142, 43, 111,
					50, 56, 227, 119, 114, 143, 53, 87, 109, 50,
					196, 212, 37, 25, 210, 71, 59, 52, 60, 191,
					184, 246, 213, 121, 182, 65, 5, 178, 6, 177,
					183, 50, 67, 181, 135, 68, 231, 245, 77, 86,
					121, 214, 218, 66, 248, 78, 181, 216, 211, 39,
					47, 71, 48, 15, 193, 64, 65, 208, 52, 139,
					186, 59, 82, 116, 170, 180, 244, 90, 79, 129,
					169, 244, 200, 8, 84, 3, 236, 62, 247, 134,
					229, 213, 166, 25, 25, 167, 172, 166, 183, 235,
					128, 95, 93, 248, 17, 50, 161, 196, 104, 172,
					69, 126, 153, 56, 251, 202, 164, 43, 167, 82,
					188, 25, 52, 7, 99, 101, 156, 146, 194, 135,
					63, 188, 208, 1, 253, 120, 151, 74, 44, 83,
					52, 57, 100, 22, 14, 223, 10, 52, 251, 185,
					2, 143, 4, 40, 195, 175, 52, 61, 207, 39,
					221, 137, 224, 142, 85, 220, 97, 89, 246, 221,
					12, 136, 88, 70, 3, 114, 12, 155, 226, 114,
					94, 145, 98, 29, 222, 161, 20, 213, 80, 178,
					65, 135, 180, 171, 78, 117, 178, 199, 63, 9,
					80, 179, 180, 30, 62, 79, 90, 228, 26, 215,
					246, 21, 173, 12, 232, 140, 85, 250, 133, 239,
					19, 95, 242, 191, 206, 153, 130, 86, 204, 95,
					14, 244, 253, 25, 68, 21, 145, 251, 229, 151,
					137, 190, 210, 206, 100, 165, 86, 162, 7, 27,
					215, 104, 185, 21, 29, 176, 26, 58, 77, 230,
					158, 45, 126, 85, 212, 161, 121, 211, 231, 235,
					106, 139, 44, 195, 9, 92, 67, 88, 145, 245,
					155, 139, 36, 184, 209, 250, 80, 159, 110, 99,
					127, 42, 149, 197, 189, 149, 194, 41, 22, 83,
					137, 255, 235, 14, 150, 212, 236, 189, 2, 144,
					123, 37, 230, 235, 254, 45, 185, 239, 222, 203,
					1, 31, 230, 15, 18, 175, 9, 116, 207, 121,
					211, 41, 175, 244, 29, 117, 60, 161, 6, 179,
					101, 180, 168, 247, 156, 152, 187, 172, 192, 5,
					144, 170, 33, 65, 60, 212, 180, 186, 63, 59,
					102, 113, 123, 54, 11, 39, 105, 192, 244, 247,
					35, 230, 137, 88, 7, 167, 155, 38, 191, 232,
					195, 121, 11, 85, 223, 190, 96, 147, 17, 186,
					197, 17, 13, 40, 171, 107, 162, 136, 167, 203,
					108, 223, 135, 57, 31, 174, 5, 31, 239, 169,
					72, 137, 189, 154, 57, 197, 56, 106, 126, 187,
					181, 252, 130, 203, 175, 29, 212, 78, 245, 42,
					80, 244, 12, 112, 119, 235, 29, 243, 51, 28,
					198, 98, 196, 116, 51, 251, 45, 87, 200, 56,
					18, 81, 130, 70, 68, 198, 230, 141, 51, 178,
					95, 88, 48, 117, 254, 111, 216, 72, 97, 135,
					138, 67, 112, 242, 14, 166, 13, 57, 158, 21,
					35, 241, 135, 124, 115, 158, 37, 168, 13, 20,
					69, 122, 75, 179, 36, 80, 165, 243, 172, 151,
					206, 111, 199, 102, 114, 204, 172, 21, 214, 219,
					103, 210, 230, 235, 143, 79, 214, 174, 54, 120,
					170, 29, 42, 145, 169, 237, 226, 129, 203, 42,
					248, 164, 115, 140, 39, 48, 225, 231, 50, 207,
					54, 180, 244, 71, 246, 141, 118, 71, 121, 204,
					56, 229, 9, 138, 4, 213, 86, 12, 124, 187,
					142, 252, 191, 1, 163, 224, 220, 161, 137, 239,
					244, 105, 121, 17, 56, 184, 41, 104, 194, 93,
					41, 95, 200, 96, 134, 69, 113, 153, 242, 140,
					186, 71, 111, 200, 132, 36, 206, 144, 189, 125,
					58, 175, 43, 187, 185, 148, 233, 153, 27, 76,
					168, 224, 194, 205, 150, 93, 32, 146, 82, 250,
					158, 239, 127, 154, 211, 194, 105, 100, 12, 0,
					0
				},
				new byte[1322]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					66, 0, 117, 86, 205, 114, 155, 48, 16, 190,
					235, 41, 116, 107, 123, 104, 47, 125, 2, 130,
					29, 227, 196, 198, 174, 193, 113, 211, 155, 12,
					27, 216, 90, 72, 30, 129, 98, 147, 167, 239,
					2, 54, 63, 129, 102, 198, 51, 25, 177, 250,
					118, 247, 219, 111, 119, 229, 60, 44, 67, 62,
					151, 16, 21, 70, 43, 140, 114, 254, 53, 72,
					65, 125, 208, 239, 27, 119, 53, 95, 21, 49,
					11, 32, 195, 216, 224, 59, 176, 117, 25, 194,
					105, 96, 237, 106, 115, 102, 135, 43, 74, 140,
					180, 226, 33, 68, 169, 210, 82, 39, 101, 123,
					247, 6, 198, 215, 128, 87, 84, 253, 187, 245,
					247, 69, 170, 243, 130, 31, 180, 124, 99, 43,
					12, 176, 143, 129, 144, 243, 165, 138, 216, 86,
					95, 192, 112, 39, 42, 40, 130, 59, 236, 150,
					44, 129, 78, 61, 76, 82, 254, 136, 49, 72,
					44, 74, 30, 10, 188, 8, 69, 54, 63, 106,
					163, 149, 213, 1, 202, 83, 23, 131, 155, 90,
					161, 146, 60, 197, 55, 192, 88, 244, 162, 101,
					15, 82, 68, 39, 30, 64, 46, 50, 24, 71,
					240, 132, 213, 53, 203, 127, 163, 202, 9, 39,
					161, 195, 2, 164, 196, 4, 84, 209, 71, 89,
					175, 54, 27, 127, 193, 126, 89, 17, 27, 81,
					96, 36, 248, 106, 229, 50, 71, 157, 33, 234,
					39, 206, 126, 227, 23, 65, 140, 104, 3, 71,
					168, 221, 102, 21, 123, 177, 141, 10, 109, 106,
					60, 74, 129, 61, 163, 74, 142, 66, 157, 38,
					72, 93, 134, 59, 80, 112, 169, 98, 235, 146,
					155, 139, 4, 10, 58, 82, 250, 93, 20, 119,
					34, 107, 243, 39, 241, 1, 134, 156, 82, 110,
					106, 232, 140, 204, 223, 33, 47, 178, 42, 143,
					133, 209, 246, 204, 22, 21, 67, 31, 169, 182,
					124, 165, 85, 18, 11, 205, 125, 40, 46, 218,
					156, 218, 184, 90, 135, 143, 182, 64, 65, 128,
					115, 183, 151, 28, 95, 11, 115, 130, 130, 57,
					82, 66, 98, 52, 95, 99, 100, 116, 94, 230,
					5, 100, 121, 205, 134, 103, 21, 93, 218, 89,
					69, 202, 129, 65, 184, 29, 143, 238, 119, 250,
					152, 15, 10, 244, 39, 181, 169, 192, 91, 5,
					223, 26, 45, 141, 137, 121, 0, 252, 75, 180,
					213, 176, 107, 200, 6, 149, 188, 219, 188, 106,
					29, 162, 234, 210, 216, 82, 65, 175, 72, 201,
					78, 224, 205, 232, 56, 33, 143, 252, 181, 41,
					70, 66, 31, 178, 12, 76, 132, 66, 242, 208,
					136, 152, 142, 7, 156, 236, 136, 3, 202, 182,
					71, 72, 235, 215, 35, 172, 19, 253, 42, 21,
					73, 130, 172, 153, 62, 181, 138, 118, 206, 112,
					109, 216, 130, 209, 221, 54, 47, 175, 174, 9,
					127, 66, 149, 225, 100, 192, 43, 58, 38, 114,
					177, 255, 169, 107, 232, 81, 40, 94, 105, 21,
					165, 62, 21, 110, 80, 8, 115, 171, 91, 95,
					103, 13, 21, 127, 81, 92, 173, 162, 243, 216,
					230, 133, 33, 50, 70, 132, 185, 70, 95, 20,
					157, 198, 208, 3, 191, 179, 7, 108, 173, 41,
					56, 44, 82, 58, 107, 218, 59, 232, 185, 58,
					88, 175, 210, 21, 40, 95, 224, 131, 230, 243,
					239, 247, 107, 109, 252, 119, 141, 122, 120, 202,
					73, 198, 164, 252, 33, 23, 93, 180, 11, 173,
					99, 188, 78, 48, 229, 96, 162, 251, 161, 141,
					45, 60, 32, 157, 241, 103, 173, 114, 200, 144,
					7, 141, 159, 9, 59, 87, 68, 133, 205, 135,
					90, 91, 97, 134, 5, 16, 31, 193, 210, 167,
					225, 98, 249, 1, 13, 72, 200, 243, 190, 166,
					125, 161, 234, 170, 238, 221, 189, 223, 59, 175,
					57, 112, 34, 65, 34, 171, 9, 179, 20, 94,
					221, 34, 249, 77, 7, 127, 111, 99, 168, 196,
					116, 42, 158, 63, 229, 71, 121, 101, 225, 119,
					111, 238, 204, 134, 237, 62, 26, 202, 30, 177,
					174, 237, 212, 216, 46, 63, 160, 106, 10, 246,
					12, 217, 81, 43, 193, 126, 97, 51, 13, 194,
					212, 170, 24, 140, 62, 234, 98, 226, 218, 154,
					218, 22, 26, 21, 247, 6, 147, 122, 199, 88,
					76, 88, 239, 103, 21, 169, 192, 3, 45, 109,
					149, 95, 203, 90, 64, 78, 36, 66, 7, 65,
					45, 67, 218, 239, 73, 123, 152, 198, 235, 71,
					159, 135, 113, 150, 182, 234, 220, 45, 201, 5,
					106, 164, 206, 180, 25, 138, 205, 12, 56, 11,
					252, 111, 255, 61, 45, 3, 111, 239, 119, 136,
					135, 205, 108, 187, 9, 150, 225, 39, 213, 237,
					21, 230, 212, 54, 108, 239, 47, 221, 205, 110,
					222, 151, 215, 215, 192, 86, 138, 109, 91, 208,
					185, 106, 165, 96, 216, 89, 193, 230, 101, 190,
					155, 187, 206, 39, 212, 25, 26, 104, 54, 228,
					33, 21, 217, 89, 87, 170, 48, 147, 66, 12,
					150, 220, 19, 242, 40, 129, 88, 52, 124, 145,
					29, 61, 182, 241, 231, 67, 13, 176, 0, 179,
					179, 132, 181, 136, 82, 84, 240, 169, 183, 221,
					70, 86, 188, 174, 54, 138, 254, 96, 24, 20,
					67, 210, 183, 43, 170, 110, 55, 116, 140, 190,
					136, 178, 20, 180, 88, 50, 145, 16, 72, 179,
					172, 5, 230, 48, 50, 158, 170, 212, 35, 185,
					191, 128, 202, 113, 178, 205, 206, 18, 7, 235,
					176, 213, 203, 211, 83, 216, 138, 168, 237, 94,
					170, 143, 207, 23, 82, 31, 133, 236, 207, 183,
					145, 207, 106, 207, 206, 48, 63, 113, 23, 84,
					97, 77, 217, 79, 38, 216, 188, 110, 216, 108,
					25, 78, 132, 131, 143, 218, 42, 170, 163, 65,
					89, 117, 233, 217, 18, 229, 245, 63, 66, 149,
					204, 9, 246, 65, 111, 41, 88, 44, 201, 73,
					49, 218, 49, 204, 115, 124, 167, 17, 157, 98,
					59, 199, 15, 54, 59, 18, 6, 138, 88, 215,
					239, 42, 109, 234, 182, 103, 33, 228, 82, 12,
					78, 182, 53, 156, 77, 160, 26, 238, 36, 238,
					84, 224, 183, 81, 171, 179, 224, 231, 86, 126,
					30, 76, 129, 195, 170, 199, 75, 66, 64, 16,
					243, 160, 121, 183, 117, 228, 45, 115, 35, 64,
					54, 47, 51, 3, 160, 14, 226, 29, 134, 8,
					204, 223, 191, 44, 157, 90, 54, 221, 91, 129,
					26, 246, 221, 92, 80, 253, 119, 12, 123, 245,
					187, 203, 86, 10, 195, 115, 159, 225, 90, 113,
					71, 45, 241, 2, 216, 239, 151, 7, 155, 87,
					242, 204, 217, 179, 38, 92, 78, 203, 92, 155,
					114, 162, 12, 115, 75, 246, 80, 124, 30, 159,
					129, 251, 58, 16, 108, 170, 73, 215, 182, 158,
					154, 55, 47, 24, 9, 201, 182, 212, 167, 238,
					102, 221, 153, 134, 154, 254, 18, 61, 246, 199,
					94, 86, 193, 178, 229, 137, 185, 186, 106, 244,
					129, 174, 200, 237, 96, 228, 20, 250, 60, 245,
					116, 83, 249, 217, 246, 47, 210, 189, 55, 109,
					178, 170, 174, 237, 66, 45, 59, 160, 7, 93,
					18, 203, 157, 194, 58, 196, 246, 81, 112, 0,
					41, 99, 18, 240, 164, 202, 109, 93, 159, 249,
					246, 174, 142, 118, 73, 164, 64, 75, 156, 110,
					207, 68, 106, 197, 84, 190, 104, 10, 203, 31,
					81, 9, 21, 33, 81, 53, 163, 132, 10, 56,
					213, 79, 95, 163, 234, 112, 187, 181, 31, 2,
					185, 214, 25, 23, 42, 30, 205, 208, 123, 82,
					149, 118, 230, 81, 170, 47, 61, 47, 205, 40,
					220, 206, 127, 127, 95, 250, 143, 27, 246, 138,
					213, 24, 54, 167, 94, 250, 152, 92, 68, 19,
					215, 205, 215, 63, 195, 226, 218, 44, 176, 12,
					0, 0
				},
				new byte[1254]
				{
					31, 139, 8, 8, 205, 199, 46, 99, 2, 0,
					67, 0, 133, 86, 203, 118, 155, 48, 16, 221,
					235, 43, 180, 234, 227, 156, 54, 155, 174, 186,
					36, 24, 219, 180, 126, 21, 156, 198, 241, 78,
					129, 9, 76, 13, 146, 43, 161, 58, 244, 235,
					59, 130, 98, 131, 67, 218, 69, 114, 0, 105,
					30, 186, 247, 234, 142, 111, 1, 127, 160, 204,
					248, 92, 136, 211, 193, 61, 108, 33, 201, 165,
					42, 84, 86, 115, 95, 241, 69, 149, 178, 245,
					17, 36, 159, 223, 243, 153, 86, 246, 200, 190,
					204, 67, 223, 103, 50, 81, 41, 104, 238, 205,
					216, 54, 71, 121, 112, 65, 60, 148, 79, 74,
					151, 162, 66, 37, 135, 89, 88, 156, 131, 252,
					77, 127, 220, 207, 241, 153, 138, 208, 243, 72,
					157, 91, 20, 138, 71, 162, 28, 89, 58, 39,
					248, 42, 240, 119, 110, 85, 109, 129, 7, 5,
					36, 149, 86, 18, 19, 243, 98, 219, 131, 207,
					227, 74, 105, 145, 141, 212, 249, 127, 55, 236,
					158, 142, 196, 99, 40, 49, 81, 50, 181, 9,
					101, 226, 239, 186, 168, 247, 93, 26, 47, 140,
					183, 235, 136, 109, 68, 81, 10, 238, 3, 138,
					38, 98, 2, 6, 51, 201, 130, 37, 95, 98,
					162, 21, 156, 155, 252, 184, 20, 26, 37, 143,
					189, 75, 253, 165, 146, 66, 19, 112, 75, 40,
					149, 174, 123, 61, 176, 8, 10, 20, 50, 129,
					110, 41, 148, 9, 251, 66, 153, 13, 11, 204,
					81, 131, 49, 248, 196, 227, 218, 84, 80, 26,
					215, 153, 144, 89, 46, 240, 253, 139, 3, 198,
					68, 6, 143, 75, 161, 171, 17, 24, 86, 96,
					41, 123, 247, 182, 192, 39, 40, 148, 204, 88,
					39, 136, 53, 86, 20, 50, 18, 215, 200, 0,
					248, 98, 178, 240, 153, 59, 113, 90, 75, 65,
					72, 153, 254, 214, 24, 244, 47, 76, 192, 240,
					120, 113, 199, 204, 73, 233, 244, 17, 139, 130,
					61, 132, 81, 176, 234, 177, 132, 242, 25, 197,
					168, 230, 54, 234, 4, 223, 123, 20, 143, 108,
					89, 4, 235, 40, 244, 3, 118, 47, 234, 146,
					190, 80, 55, 223, 65, 86, 66, 138, 22, 249,
					14, 31, 54, 135, 39, 64, 62, 179, 84, 136,
					216, 190, 166, 197, 188, 4, 77, 41, 9, 154,
					135, 196, 188, 169, 52, 138, 162, 219, 49, 87,
					26, 127, 147, 182, 35, 245, 168, 42, 138, 100,
					91, 74, 9, 165, 187, 4, 83, 91, 89, 13,
					27, 81, 245, 1, 27, 106, 134, 69, 62, 17,
					158, 218, 2, 216, 22, 75, 32, 112, 169, 68,
					5, 90, 54, 23, 70, 20, 13, 199, 161, 191,
					244, 118, 151, 20, 8, 109, 123, 88, 18, 25,
					116, 226, 154, 240, 26, 174, 82, 95, 46, 55,
					157, 206, 233, 88, 89, 190, 21, 104, 236, 81,
					200, 3, 208, 247, 242, 104, 169, 2, 15, 126,
					90, 60, 150, 4, 14, 243, 65, 59, 65, 29,
					115, 76, 154, 122, 183, 120, 66, 249, 250, 85,
					57, 139, 33, 136, 239, 195, 213, 223, 132, 67,
					147, 96, 247, 48, 85, 58, 129, 23, 48, 78,
					69, 75, 238, 43, 182, 192, 238, 36, 26, 149,
					176, 7, 74, 231, 231, 150, 205, 238, 188, 149,
					127, 183, 98, 225, 198, 139, 215, 43, 230, 213,
					66, 243, 133, 120, 52, 204, 43, 5, 129, 126,
					201, 187, 115, 162, 145, 38, 183, 215, 38, 51,
					19, 133, 66, 211, 28, 235, 142, 212, 70, 136,
					185, 231, 75, 224, 55, 43, 92, 100, 246, 154,
					164, 121, 180, 223, 245, 215, 22, 219, 137, 199,
					30, 84, 85, 137, 100, 64, 234, 46, 124, 235,
					173, 28, 163, 234, 8, 154, 142, 213, 111, 46,
					178, 24, 129, 124, 181, 66, 92, 9, 61, 178,
					24, 221, 123, 252, 221, 92, 81, 107, 95, 233,
					223, 251, 54, 0, 36, 152, 154, 8, 86, 217,
					95, 174, 182, 159, 206, 202, 115, 239, 55, 68,
					158, 50, 148, 176, 85, 180, 187, 172, 87, 130,
					242, 137, 232, 158, 172, 119, 165, 219, 55, 82,
					190, 243, 15, 254, 64, 168, 93, 84, 51, 68,
					119, 223, 120, 229, 95, 83, 36, 79, 84, 164,
					153, 46, 129, 157, 42, 43, 201, 16, 49, 83,
					124, 34, 42, 103, 132, 137, 213, 88, 213, 195,
					28, 55, 205, 230, 155, 217, 78, 105, 24, 168,
					248, 210, 228, 70, 139, 20, 148, 108, 14, 82,
					20, 152, 129, 236, 59, 87, 227, 10, 36, 139,
					56, 100, 155, 40, 92, 6, 151, 184, 47, 182,
					110, 165, 38, 213, 47, 58, 254, 47, 232, 71,
					249, 65, 244, 125, 205, 98, 12, 100, 134, 18,
					174, 122, 250, 224, 154, 186, 57, 11, 157, 84,
					153, 53, 84, 109, 13, 189, 86, 32, 179, 22,
					222, 214, 76, 216, 173, 22, 40, 141, 80, 124,
					86, 62, 206, 153, 175, 33, 29, 0, 218, 176,
					60, 196, 244, 22, 245, 168, 32, 86, 54, 41,
					192, 14, 135, 204, 249, 60, 173, 91, 145, 198,
					213, 216, 148, 219, 231, 138, 22, 209, 85, 24,
					33, 211, 54, 54, 176, 116, 125, 230, 40, 251,
					99, 242, 239, 150, 155, 158, 91, 68, 104, 204,
					184, 203, 246, 166, 100, 161, 108, 122, 161, 180,
					51, 11, 231, 104, 235, 25, 219, 128, 78, 128,
					0, 103, 240, 241, 8, 194, 176, 169, 22, 86,
					230, 234, 201, 57, 232, 102, 25, 95, 210, 76,
					4, 233, 167, 160, 194, 215, 245, 92, 38, 239,
					81, 36, 214, 112, 74, 134, 199, 28, 180, 40,
					232, 89, 35, 81, 9, 103, 243, 91, 47, 214,
					245, 192, 119, 108, 46, 72, 48, 111, 226, 33,
					134, 103, 28, 176, 210, 214, 244, 3, 60, 57,
					183, 78, 222, 82, 130, 238, 78, 113, 190, 129,
					42, 201, 129, 24, 214, 125, 188, 169, 196, 51,
					82, 79, 149, 120, 172, 171, 161, 98, 187, 184,
					230, 66, 183, 51, 154, 121, 25, 22, 221, 192,
					30, 33, 124, 25, 124, 9, 124, 87, 232, 144,
					228, 120, 28, 227, 117, 66, 180, 102, 86, 180,
					236, 151, 130, 195, 199, 68, 149, 37, 244, 204,
					53, 18, 181, 81, 146, 207, 241, 163, 203, 79,
					243, 101, 255, 254, 140, 207, 50, 92, 69, 65,
					60, 232, 178, 85, 233, 28, 75, 241, 60, 248,
					62, 244, 69, 255, 132, 14, 146, 145, 150, 233,
					75, 137, 181, 234, 121, 136, 205, 45, 38, 118,
					76, 207, 223, 49, 57, 92, 225, 215, 93, 249,
					27, 86, 168, 83, 20, 198, 62, 11, 118, 193,
					204, 113, 58, 221, 247, 238, 238, 103, 238, 231,
					226, 72, 177, 215, 8, 51, 47, 77, 11, 148,
					135, 6, 101, 115, 18, 53, 137, 77, 26, 33,
					211, 243, 108, 111, 125, 208, 67, 61, 65, 115,
					232, 181, 121, 4, 72, 75, 69, 67, 224, 250,
					135, 221, 38, 216, 63, 92, 198, 24, 11, 158,
					43, 13, 37, 240, 214, 25, 64, 59, 7, 136,
					85, 97, 157, 129, 118, 40, 181, 63, 27, 254,
					101, 155, 251, 92, 40, 218, 242, 66, 135, 108,
					103, 48, 203, 171, 102, 140, 53, 17, 243, 238,
					218, 205, 241, 96, 198, 102, 46, 155, 144, 235,
					13, 96, 104, 239, 106, 234, 102, 131, 67, 97,
					138, 191, 174, 66, 254, 0, 250, 92, 254, 227,
					192, 11, 0, 0
				},
				new byte[1307]
				{
					31, 139, 8, 8, 155, 227, 108, 99, 2, 0,
					0, 125, 86, 219, 114, 218, 64, 12, 125, 223,
					175, 216, 199, 102, 166, 105, 191, 193, 24, 72,
					104, 32, 16, 156, 52, 151, 55, 197, 86, 176,
					138, 189, 114, 215, 94, 136, 243, 245, 213, 218,
					1, 108, 234, 118, 38, 225, 98, 36, 173, 46,
					231, 28, 237, 253, 36, 92, 222, 47, 111, 85,
					240, 186, 101, 29, 178, 158, 87, 137, 138, 82,
					52, 31, 242, 175, 167, 72, 165, 165, 45, 234,
					123, 140, 83, 195, 25, 111, 234, 191, 108, 34,
					103, 82, 206, 81, 79, 50, 140, 43, 203, 134,
					226, 242, 96, 115, 149, 241, 43, 100, 122, 65,
					239, 152, 92, 230, 156, 244, 226, 204, 76, 124,
					10, 242, 136, 132, 230, 44, 196, 55, 31, 227,
					219, 201, 38, 100, 174, 177, 28, 72, 229, 6,
					235, 156, 203, 115, 239, 175, 122, 78, 57, 85,
					152, 168, 201, 229, 154, 227, 45, 197, 125, 215,
					188, 0, 83, 31, 109, 2, 180, 92, 22, 16,
					163, 142, 98, 66, 35, 239, 11, 204, 217, 214,
					250, 112, 252, 41, 143, 59, 7, 230, 23, 13,
					228, 49, 118, 91, 46, 73, 45, 224, 29, 179,
					76, 30, 218, 130, 45, 84, 196, 70, 243, 155,
					14, 114, 180, 20, 67, 19, 166, 244, 97, 158,
					200, 188, 147, 169, 128, 135, 58, 247, 146, 186,
					20, 72, 71, 32, 54, 160, 35, 204, 41, 102,
					147, 184, 184, 98, 123, 108, 174, 229, 223, 77,
					19, 131, 178, 178, 112, 143, 219, 83, 134, 18,
					186, 118, 31, 67, 83, 11, 36, 177, 17, 85,
					253, 136, 42, 8, 167, 25, 239, 59, 35, 165,
					2, 49, 25, 112, 159, 147, 24, 144, 190, 102,
					179, 209, 55, 254, 37, 228, 99, 7, 35, 87,
					88, 20, 28, 60, 82, 137, 199, 135, 163, 204,
					161, 14, 83, 196, 10, 82, 29, 24, 144, 96,
					122, 140, 37, 109, 76, 147, 250, 53, 190, 33,
					233, 57, 208, 214, 13, 156, 246, 194, 86, 194,
					142, 150, 250, 26, 193, 146, 156, 22, 124, 143,
					212, 26, 55, 104, 164, 128, 20, 108, 33, 49,
					42, 180, 166, 233, 49, 100, 199, 67, 87, 104,
					115, 48, 222, 106, 197, 149, 188, 81, 231, 183,
					208, 162, 152, 239, 36, 77, 182, 89, 242, 143,
					0, 35, 144, 18, 90, 75, 54, 131, 54, 29,
					252, 191, 164, 36, 115, 148, 34, 29, 152, 77,
					70, 96, 58, 149, 168, 149, 149, 12, 98, 177,
					215, 115, 222, 80, 220, 5, 133, 138, 224, 213,
					74, 118, 234, 193, 200, 183, 79, 184, 169, 219,
					201, 67, 120, 61, 91, 69, 61, 203, 153, 145,
					154, 41, 30, 198, 65, 68, 43, 4, 155, 157,
					18, 10, 226, 170, 68, 195, 146, 247, 27, 219,
					220, 71, 232, 165, 180, 158, 253, 12, 102, 167,
					7, 36, 148, 250, 114, 240, 189, 24, 98, 183,
					169, 7, 102, 19, 114, 85, 245, 128, 219, 122,
					73, 11, 26, 220, 214, 166, 68, 249, 235, 31,
					115, 30, 251, 135, 135, 191, 140, 117, 234, 114,
					63, 221, 101, 81, 49, 158, 34, 170, 48, 99,
					151, 140, 38, 193, 90, 207, 231, 161, 154, 228,
					31, 196, 246, 107, 251, 57, 165, 61, 212, 162,
					43, 177, 237, 120, 28, 153, 235, 15, 149, 195,
					212, 195, 237, 108, 33, 93, 48, 188, 59, 111,
					130, 254, 242, 232, 244, 211, 236, 66, 93, 141,
					215, 193, 34, 88, 71, 74, 218, 79, 70, 112,
					153, 86, 93, 133, 241, 60, 252, 72, 217, 53,
					136, 79, 224, 140, 169, 182, 104, 73, 225, 211,
					103, 87, 54, 154, 226, 42, 41, 165, 193, 246,
					106, 189, 124, 90, 76, 22, 106, 108, 161, 64,
					43, 32, 127, 45, 213, 114, 61, 11, 151, 221,
					166, 116, 117, 174, 81, 159, 201, 123, 145, 125,
					142, 253, 220, 206, 22, 42, 88, 222, 142, 39,
					63, 103, 225, 36, 234, 203, 231, 45, 86, 50,
					235, 61, 216, 164, 109, 74, 39, 81, 37, 179,
					168, 32, 102, 139, 103, 131, 197, 56, 199, 188,
					223, 195, 182, 44, 181, 188, 165, 165, 14, 202,
					147, 237, 10, 51, 72, 204, 0, 10, 150, 151,
					161, 123, 149, 228, 142, 131, 63, 15, 167, 130,
					232, 62, 84, 15, 139, 89, 164, 86, 96, 33,
					177, 156, 251, 167, 17, 153, 180, 181, 61, 68,
					90, 160, 64, 26, 54, 216, 199, 120, 255, 72,
					21, 224, 14, 154, 194, 253, 56, 26, 253, 185,
					174, 157, 103, 207, 144, 132, 134, 41, 25, 208,
					211, 12, 202, 244, 136, 60, 103, 138, 204, 13,
					173, 144, 89, 2, 41, 183, 0, 170, 232, 205,
					119, 173, 157, 123, 52, 253, 171, 161, 229, 209,
					103, 49, 121, 210, 33, 20, 58, 184, 82, 83,
					170, 168, 224, 61, 218, 70, 45, 54, 22, 132,
					242, 255, 89, 154, 143, 204, 32, 237, 31, 220,
					101, 88, 140, 161, 2, 189, 180, 180, 33, 47,
					56, 82, 71, 81, 170, 53, 237, 184, 108, 106,
					31, 209, 166, 11, 234, 243, 69, 246, 232, 100,
					22, 250, 217, 201, 14, 24, 214, 139, 7, 227,
					237, 62, 245, 166, 199, 137, 31, 36, 83, 44,
					221, 133, 90, 221, 69, 41, 97, 150, 180, 43,
					195, 198, 41, 201, 122, 233, 107, 151, 20, 241,
					210, 136, 205, 203, 227, 64, 21, 215, 104, 54,
					226, 117, 208, 198, 47, 99, 153, 213, 198, 129,
					185, 232, 114, 107, 178, 217, 124, 18, 119, 98,
					164, 84, 196, 70, 227, 133, 6, 106, 234, 74,
					22, 208, 82, 79, 73, 31, 230, 43, 189, 202,
					192, 3, 61, 87, 55, 44, 69, 108, 59, 83,
					233, 152, 246, 229, 172, 43, 55, 149, 164, 229,
					185, 178, 103, 187, 61, 135, 214, 142, 118, 32,
					77, 52, 219, 182, 203, 247, 150, 50, 50, 8,
					182, 199, 194, 62, 235, 198, 184, 195, 140, 133,
					220, 131, 44, 186, 242, 251, 32, 97, 47, 108,
					171, 213, 82, 47, 248, 149, 50, 153, 184, 183,
					226, 60, 119, 98, 214, 182, 50, 216, 34, 24,
					80, 243, 58, 254, 0, 219, 185, 101, 208, 47,
					250, 15, 130, 90, 202, 125, 164, 192, 181, 235,
					175, 156, 107, 144, 31, 101, 222, 45, 1, 218,
					212, 14, 72, 104, 239, 69, 253, 107, 149, 72,
					220, 111, 26, 34, 247, 145, 212, 183, 50, 23,
					127, 202, 112, 187, 85, 224, 132, 216, 100, 240,
					192, 153, 103, 42, 83, 55, 200, 152, 41, 56,
					139, 49, 129, 14, 51, 176, 103, 180, 85, 17,
					45, 160, 215, 108, 21, 78, 199, 58, 130, 172,
					109, 251, 33, 186, 224, 29, 234, 238, 110, 59,
					4, 127, 70, 35, 144, 80, 119, 108, 119, 220,
					159, 211, 51, 139, 43, 125, 202, 51, 218, 110,
					238, 81, 29, 167, 251, 97, 236, 28, 149, 233,
					70, 79, 217, 153, 4, 237, 80, 143, 40, 243,
					125, 221, 195, 14, 203, 193, 101, 231, 149, 106,
					251, 127, 165, 234, 221, 219, 222, 155, 27, 214,
					249, 173, 112, 228, 74, 50, 88, 118, 36, 89,
					24, 118, 71, 29, 27, 95, 93, 142, 54, 198,
					62, 228, 253, 228, 134, 96, 244, 68, 192, 169,
					59, 187, 89, 14, 220, 185, 199, 144, 185, 127,
					74, 114, 103, 255, 248, 236, 252, 157, 113, 80,
					39, 195, 231, 112, 80, 138, 58, 121, 130, 168,
					195, 39, 141, 202, 186, 172, 48, 63, 122, 143,
					144, 126, 121, 93, 152, 132, 151, 237, 24, 254,
					246, 190, 7, 218, 162, 158, 73, 240, 178, 178,
					4, 153, 14, 92, 197, 71, 112, 252, 1, 85,
					118, 43, 171, 225, 12, 0, 0
				},
				new byte[248]
				{
					31, 139, 8, 8, 234, 228, 108, 99, 2, 0,
					0, 85, 144, 205, 78, 195, 48, 16, 132, 239,
					150, 252, 14, 123, 132, 199, 40, 105, 104, 66,
					225, 66, 138, 196, 117, 235, 172, 98, 131, 127,
					162, 181, 141, 148, 62, 61, 107, 85, 160, 246,
					96, 201, 90, 205, 55, 59, 179, 71, 244, 140,
					27, 76, 59, 173, 38, 139, 113, 177, 232, 96,
					244, 245, 7, 11, 50, 116, 137, 233, 19, 38,
					10, 206, 164, 56, 87, 83, 82, 155, 105, 245,
					92, 227, 226, 206, 158, 96, 140, 70, 184, 20,
					23, 249, 205, 53, 23, 118, 8, 189, 72, 2,
					177, 113, 9, 102, 130, 222, 83, 225, 20, 197,
					32, 107, 181, 103, 194, 240, 228, 150, 123, 203,
					171, 75, 103, 49, 172, 39, 250, 110, 132, 185,
					34, 185, 5, 88, 219, 186, 156, 98, 145, 96,
					39, 50, 54, 38, 159, 150, 77, 171, 62, 206,
					76, 57, 195, 128, 53, 19, 195, 238, 160, 21,
					250, 66, 166, 109, 95, 107, 33, 158, 182, 92,
					40, 16, 28, 194, 121, 208, 234, 195, 23, 198,
					247, 113, 234, 110, 76, 224, 225, 175, 242, 163,
					80, 240, 90, 230, 118, 4, 138, 23, 121, 240,
					226, 164, 213, 30, 225, 40, 138, 27, 230, 95,
					56, 200, 252, 98, 83, 133, 65, 234, 127, 213,
					8, 111, 206, 112, 162, 187, 240, 77, 249, 11,
					164, 49, 193, 0, 97, 1, 0, 0
				}
			};
		}
	}
}
namespace SpdReaderWriterCore.Driver
{
	public class WinRing0 : IDisposable, IDriver
	{
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct m
		{
			public uint Xb;

			public uint QC;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct ub
		{
			public uint Hb;

			public uint eC;

			public byte pC;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct mb
		{
			public uint ob;

			public uint w;

			public ushort yB;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct Kb
		{
			public uint Zb;

			public uint k;

			public uint jB;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct iB
		{
			public uint Sb;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct Vb
		{
			public uint Jc;

			public uint OC;
		}

		private class I
		{
			internal enum JC
			{
				GENERIC_READWRITE = -1073741824
			}

			internal enum IA
			{
				FILE_SHARE_READWRITE = 3
			}

			internal enum Rc
			{
				OPEN_EXISTING = 3
			}

			internal enum oA : uint
			{
				FILE_ATTRIBUTE_NORMAL = 0x80u
			}

			[StructLayout(LayoutKind.Sequential, Size = 1)]
			public struct uC
			{
				public static readonly uint IC = 40000u;

				public static uint j = 2621448192u;

				public static uint tA = 2621448196u;

				public static uint zc = 2621448324u;

				public static uint VB = 2621448328u;

				public static uint wB = 2621448332u;

				public static uint Bc = 2621448336u;

				public static uint o = 2621464772u;

				public static uint nC = 2621481160u;

				public static uint Hc = 2621464780u;

				public static uint xB = 2621464784u;

				public static uint Ac = 2621464788u;

				public static uint lB = 2621481176u;

				public static uint Z = 2621481180u;

				public static uint ib = 2621481184u;

				public static uint t = 2621464836u;

				public static uint CC = 2621481224u;

				public static uint WA = 2621464900u;

				public static uint ZA = 2621481288u;
			}

			[DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "CreateFile", SetLastError = true)]
			internal static extern IntPtr Cb([MarshalAs(UnmanagedType.LPTStr)] string P_0, [MarshalAs(UnmanagedType.U4)] JC P_1, [MarshalAs(UnmanagedType.U4)] IA P_2, [Optional] IntPtr P_3, [MarshalAs(UnmanagedType.U4)] Rc P_4, [MarshalAs(UnmanagedType.U4)] oA P_5, IntPtr P_6);

			[DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "DeviceIoControl", ExactSpelling = true, SetLastError = true)]
			internal static extern bool r(SafeFileHandle P_0, uint P_1, [In][MarshalAs(UnmanagedType.AsAny)] object P_2, uint P_3, [Out][MarshalAs(UnmanagedType.AsAny)] object P_4, uint P_5, out uint P_6, IntPtr P_7);
		}

		private static class lA
		{
			internal enum XA : uint
			{
				SC_MANAGER_ALL_ACCESS = 983103u
			}

			internal enum Jb : uint
			{
				SERVICE_KERNEL_DRIVER = 1u
			}

			internal enum MA : uint
			{
				SERVICE_DEMAND_START = 3u
			}

			internal enum jA : uint
			{
				SERVICE_ERROR_IGNORE,
				SERVICE_ERROR_NORMAL,
				SERVICE_ERROR_SEVERE,
				SERVICE_ERROR_CRITICAL
			}

			internal enum tc : uint
			{
				SERVICE_ALL_ACCESS = 983551u
			}

			internal enum pc : uint
			{
				SERVICE_CONTROL_CONTINUE = 3u,
				SERVICE_CONTROL_INTERROGATE = 4u,
				SERVICE_CONTROL_NETBINDADD = 7u,
				SERVICE_CONTROL_NETBINDDISABLE = 10u,
				SERVICE_CONTROL_NETBINDENABLE = 9u,
				SERVICE_CONTROL_NETBINDREMOVE = 8u,
				SERVICE_CONTROL_PARAMCHANGE = 6u,
				SERVICE_CONTROL_PAUSE = 2u,
				SERVICE_CONTROL_STOP = 1u
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			internal struct Lc
			{
				public QB rC;

				public LC pA;

				public Mb uA;

				public uint mC;

				public uint bC;

				public uint bB;

				public uint bb;
			}

			internal enum QB : uint
			{
				SERVICE_KERNEL_DRIVER = 1u
			}

			internal enum LC : uint
			{
				SERVICE_CONTINUE_PENDING = 5u,
				SERVICE_PAUSE_PENDING = 6u,
				SERVICE_PAUSED = 7u,
				SERVICE_RUNNING = 4u,
				SERVICE_START_PENDING = 2u,
				SERVICE_STOP_PENDING = 3u,
				SERVICE_STOPPED = 1u
			}

			internal enum Mb : uint
			{
				SERVICE_ACCEPT_NETBINDCHANGE = 16u,
				SERVICE_ACCEPT_PARAMCHANGE = 8u,
				SERVICE_ACCEPT_PAUSE_CONTINUE = 2u,
				SERVICE_ACCEPT_PRESHUTDOWN = 256u,
				SERVICE_ACCEPT_SHUTDOWN = 4u,
				SERVICE_ACCEPT_STOP = 1u
			}

			[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW", ExactSpelling = true, SetLastError = true)]
			internal static extern IntPtr h(string P_0, string P_1, XA P_2);

			internal static IntPtr cb(XA P_0)
			{
				return h(null, null, P_0);
			}

			[DllImport("advapi32.dll", CharSet = CharSet.Auto, EntryPoint = "CreateService", SetLastError = true)]
			internal static extern IntPtr y(IntPtr P_0, string P_1, string P_2, XA P_3, Jb P_4, MA P_5, jA P_6, string P_7, [Optional] string P_8, [Optional] string P_9, [Optional] string P_10, [Optional] string P_11, [Optional] string P_12);

			[DllImport("advapi32.dll", EntryPoint = "DeleteService", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool DB(IntPtr P_0);

			[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW", SetLastError = true)]
			internal static extern IntPtr nB(IntPtr P_0, string P_1, tc P_2);

			[DllImport("advapi32.dll", EntryPoint = "ControlService", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool fB(IntPtr P_0, pc P_1, ref Lc P_2);

			[DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool YA(IntPtr P_0);
		}

		private byte TB = byte.MaxValue;

		private const byte iC = 32;

		private const byte kA = 8;

		private const string qC = "WinRing0_1_2_0";

		private const int gc = 1000;

		private static SafeFileHandle mA;

		private static ServiceController HA = new ServiceController("WinRing0_1_2_0");

		private IntPtr wA = lA.cb(lA.XA.SC_MANAGER_ALL_ACCESS);

		private IntPtr Tb;

		private bool xc = true;

		public bool IsInstalled
		{
			get
			{
				return UA();
			}
		}

		public bool IsServiceRunning
		{
			get
			{
				try
				{
					ServiceController hA = HA;
					if (hA != null)
					{
						hA.Refresh();
					}
					int result;
					if (IsInstalled)
					{
						ServiceController hA2 = HA;
						result = ((hA2 != null && hA2.Status == ServiceControllerStatus.Running) ? 1 : 0);
					}
					else
					{
						result = 0;
					}
					return (byte)result != 0;
				}
				catch
				{
					return false;
				}
			}
		}

		private bool Vc
		{
			get
			{
				if (mA != null)
				{
					return !mA.IsClosed;
				}
				return false;
			}
		}

		private bool Lb
		{
			get
			{
				if (IsInstalled && mA != null)
				{
					return !mA.IsInvalid;
				}
				return false;
			}
		}

		public bool IsReady
		{
			get
			{
				if (Lb)
				{
					return Vc;
				}
				return false;
			}
		}

		private static bool Fb
		{
			get
			{
				if (mA != null && !mA.IsInvalid)
				{
					return !mA.IsClosed;
				}
				return false;
			}
		}

		private static string O
		{
			get
			{
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\spdrwdrv.sys";
			}
		}

		public WinRing0()
		{
			if (IsInstalled && IsServiceRunning)
			{
				xc = false;
			}
			else
			{
				if (!IsInstalled && !InstallDriver())
				{
					throw new Exception("Unable to install driver service");
				}
				if (!IsServiceRunning && !StartDriver())
				{
					throw new Exception("Unable to start driver service");
				}
			}
			if (!Vc && !LA())
			{
				throw new Exception("Unable to open driver handle");
			}
		}

		~WinRing0()
		{
			Dispose();
		}

		public void Dispose()
		{
			tb();
			int num = 0;
			rB(I.uC.tA, null, ref num);
			if (xc && num <= 1)
			{
				StopDriver();
				MC(false);
			}
			mA = null;
			lA.YA(Tb);
			lA.YA(wA);
		}

		void IDisposable.Dispose()
		{
			//ILSpy generated this explicit interface implementation from .override directive in Dispose
			this.Dispose();
		}

		private bool OB()
		{
			byte[] array = Data.Gzip(Environment.Is64BitOperatingSystem ? Resources.Driver.WinRing0x64_sys : Resources.Driver.WinRing0_sys, Data.GzipMethod.Decompress);
			if (!File.Exists(O) || !Data.CompareArray(array, File.ReadAllBytes(O)))
			{
				try
				{
					File.WriteAllBytes(O, array);
				}
				catch
				{
					return false;
				}
			}
			if (File.Exists(O))
			{
				return Data.CompareArray(array, File.ReadAllBytes(O));
			}
			return false;
		}

		public bool InstallDriver()
		{
			if (!OB())
			{
				return false;
			}
			if (wA == IntPtr.Zero)
			{
				return false;
			}
			Tb = lA.y(wA, "WinRing0_1_2_0", "WinRing0_1_2_0", lA.XA.SC_MANAGER_ALL_ACCESS, lA.Jb.SERVICE_KERNEL_DRIVER, lA.MA.SERVICE_DEMAND_START, lA.jA.SERVICE_ERROR_NORMAL, O);
			if (Tb == IntPtr.Zero)
			{
				return false;
			}
			lA.YA(Tb);
			return true;
		}

		bool IDriver.InstallDriver()
		{
			//ILSpy generated this explicit interface implementation from .override directive in InstallDriver
			return this.InstallDriver();
		}

		public bool RemoveDriver()
		{
			if (wA == IntPtr.Zero)
			{
				return false;
			}
			Tb = lA.nB(wA, "WinRing0_1_2_0", lA.tc.SERVICE_ALL_ACCESS);
			if (Tb == IntPtr.Zero)
			{
				return false;
			}
			if (lA.DB(Tb))
			{
				return lA.YA(Tb);
			}
			return false;
		}

		bool IDriver.RemoveDriver()
		{
			//ILSpy generated this explicit interface implementation from .override directive in RemoveDriver
			return this.RemoveDriver();
		}

		private bool MC(bool P_0)
		{
			if (!P_0)
			{
				return RemoveDriver();
			}
			if (!RemoveDriver())
			{
				return false;
			}
			try
			{
				File.Delete(O);
				return !File.Exists(O);
			}
			catch
			{
				return false;
			}
		}

		public bool StartDriver()
		{
			if (!IsInstalled)
			{
				return false;
			}
			try
			{
				if (HA.Status == ServiceControllerStatus.Running)
				{
					return true;
				}
				HA.Start();
				HA.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(1000.0));
				return HA.Status == ServiceControllerStatus.Running;
			}
			catch
			{
				try
				{
					return HA.Status == ServiceControllerStatus.Running;
				}
				catch
				{
					return false;
				}
			}
		}

		bool IDriver.StartDriver()
		{
			//ILSpy generated this explicit interface implementation from .override directive in StartDriver
			return this.StartDriver();
		}

		public bool StopDriver()
		{
			try
			{
				HA = new ServiceController("WinRing0_1_2_0");
				if (HA.Status != ServiceControllerStatus.Stopped)
				{
					HA.Stop();
					Stopwatch stopwatch = new Stopwatch();
					stopwatch.Start();
					while (stopwatch.ElapsedMilliseconds < 1000)
					{
						HA.Refresh();
						if (HA.Status == ServiceControllerStatus.Stopped || HA.Status == ServiceControllerStatus.StopPending)
						{
							return true;
						}
					}
				}
				return HA.Status == ServiceControllerStatus.Stopped || HA.Status == ServiceControllerStatus.StopPending;
			}
			catch
			{
				try
				{
					HA = new ServiceController("WinRing0_1_2_0");
					return HA.Status == ServiceControllerStatus.Stopped || HA.Status == ServiceControllerStatus.StopPending;
				}
				catch
				{
					return true;
				}
			}
		}

		bool IDriver.StopDriver()
		{
			//ILSpy generated this explicit interface implementation from .override directive in StopDriver
			return this.StopDriver();
		}

		private static bool UA()
		{
			try
			{
				ServiceController hA = HA;
				int result;
				if (hA != null && hA.ServiceType == ServiceType.KernelDriver)
				{
					ServiceController hA2 = HA;
					result = ((((hA2 != null) ? hA2.DisplayName : null) == "WinRing0_1_2_0") ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			}
			catch
			{
				return false;
			}
		}

		private bool LA()
		{
			mA = new SafeFileHandle(I.Cb("\\\\.\\WinRing0_1_2_0", I.JC.GENERIC_READWRITE, I.IA.FILE_SHARE_READWRITE, IntPtr.Zero, I.Rc.OPEN_EXISTING, I.oA.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), true);
			if (mA.IsInvalid)
			{
				tb();
			}
			return Lb;
		}

		private bool tb()
		{
			if (Vc)
			{
				mA.Close();
				mA.Dispose();
			}
			return !Vc;
		}

		public uint GetDriverVersion(ref byte major, ref byte minor, ref byte revision, ref byte release)
		{
			uint num = 0u;
			rB(I.uC.j, null, ref num);
			major = (byte)(num >> 24);
			minor = (byte)(num >> 16);
			revision = (byte)(num >> 8);
			release = (byte)num;
			return num;
		}

		private static bool rB<_0001>(uint P_0, object P_1, ref _0001 P_2)
		{
			if (!Fb)
			{
				return false;
			}
			uint num = ((P_1 != null) ? ((uint)Marshal.SizeOf(P_1)) : 0u);
			uint num2 = 0u;
			object obj = P_2;
			bool result = I.r(mA, P_0, P_1, num, obj, (uint)Marshal.SizeOf(obj), out num2, IntPtr.Zero);
			P_2 = (_0001)obj;
			return result;
		}

		private static bool lb(uint P_0, object P_1)
		{
			if (!Fb)
			{
				return false;
			}
			uint num = ((P_1 != null) ? ((uint)Marshal.SizeOf(P_1)) : 0u);
			uint num2 = 0u;
			return I.r(mA, P_0, P_1, num, null, 0u, out num2, IntPtr.Zero);
		}

		public static uint PciBusDevFunc(uint bus, uint dev, uint func)
		{
			return ((bus & 0xFF) << 8) | ((dev & 0x1F) << 3) | (func & 7);
		}

		public static byte PciGetBus(uint address)
		{
			return (byte)((address >> 8) & 0xFF);
		}

		public static byte PciGetDev(uint address)
		{
			return (byte)((address >> 3) & 0x1F);
		}

		public static byte PciGetFunc(uint address)
		{
			return (byte)(address & 7);
		}

		public uint FindPciDeviceById(ushort vendorId, ushort deviceId, ushort index)
		{
			if (index > TB * 32 * 8)
			{
				throw new ArgumentOutOfRangeException();
			}
			uint result = uint.MaxValue;
			uint num = 0u;
			if (vendorId == 0 || deviceId == 0 || index == 0)
			{
				return result;
			}
			for (ushort num2 = 0; num2 <= TB; num2++)
			{
				for (byte b = 0; b < 32; b++)
				{
					if (ReadPciConfigWord(PciBusDevFunc(num2, b, 0u), 0u) != ushort.MaxValue)
					{
						for (byte b2 = 0; b2 < 8; b2++)
						{
							if (ReadPciConfigDword(PciBusDevFunc(num2, b, b2), 0u) == (uint)((deviceId << 16) | vendorId))
							{
								result = PciBusDevFunc(num2, b, b2);
								if (++num == index)
								{
									return result;
								}
							}
						}
					}
				}
			}
			return result;
		}

		public uint[] FindPciDeviceByIdArray(ushort vendorId, ushort deviceId)
		{
			return FindPciDeviceByIdArray(vendorId, deviceId, (ushort)(TB * 32 * 8));
		}

		public uint[] FindPciDeviceByIdArray(ushort vendorId, ushort deviceId, ushort maxCount)
		{
			if (maxCount > TB * 32 * 8 || maxCount == 0)
			{
				throw new ArgumentOutOfRangeException();
			}
			uint num = 0u;
			if (vendorId == 0 || deviceId == 0)
			{
				return new uint[0];
			}
			Queue<uint> queue = new Queue<uint>();
			for (ushort num2 = 0; num2 <= TB; num2++)
			{
				if (ReadPciConfigWord(PciBusDevFunc(num2, 0u, 0u), 0u) == vendorId)
				{
					for (byte b = 0; b < 32; b++)
					{
						if (ReadPciConfigWord(PciBusDevFunc(num2, b, 0u), 0u) != ushort.MaxValue)
						{
							for (byte b2 = 0; b2 < 8; b2++)
							{
								if (ReadPciConfigDword(PciBusDevFunc(num2, b, b2), 0u) == (uint)(vendorId | (deviceId << 16)))
								{
									queue.Enqueue(PciBusDevFunc(num2, b, b2));
									if (++num == maxCount)
									{
										return queue.ToArray();
									}
								}
							}
						}
					}
				}
			}
			return queue.ToArray();
		}

		public uint FindPciDeviceByClass(byte baseClass, byte subClass, byte programIf, ushort index)
		{
			if (index > TB * 32 * 8)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			uint result = uint.MaxValue;
			uint num = 0u;
			for (ushort num2 = 0; num2 <= TB; num2++)
			{
				for (byte b = 0; b < 32; b++)
				{
					if (ReadPciConfigWord(PciBusDevFunc(num2, b, 0u), 0u) != ushort.MaxValue)
					{
						for (byte b2 = 0; b2 < 8; b2++)
						{
							if ((ReadPciConfigDword(PciBusDevFunc(num2, b, b2), 8u) & 0xFFFFFF00u) == (uint)((baseClass << 24) | (subClass << 16) | (programIf << 8)))
							{
								result = PciBusDevFunc(num2, b, b2);
								if (++num == index)
								{
									return result;
								}
							}
						}
					}
				}
			}
			return result;
		}

		public uint[] FindPciDeviceByClassArray(byte baseClass, byte subClass, byte programIf)
		{
			return FindPciDeviceByClassArray(baseClass, subClass, programIf, (ushort)(TB * 32 * 8));
		}

		public uint[] FindPciDeviceByClassArray(byte baseClass, byte subClass, byte programIf, ushort maxCount)
		{
			if (maxCount > TB * 32 * 8)
			{
				throw new ArgumentOutOfRangeException("maxCount");
			}
			if (maxCount == 0)
			{
				return new uint[0];
			}
			uint num = 0u;
			Queue<uint> queue = new Queue<uint>();
			for (ushort num2 = 0; num2 <= TB; num2++)
			{
				for (byte b = 0; b < 32; b++)
				{
					if (ReadPciConfigWord(PciBusDevFunc(num2, b, 0u), 0u) != ushort.MaxValue)
					{
						for (byte b2 = 0; b2 < 8; b2++)
						{
							if ((ReadPciConfigDword(PciBusDevFunc(num2, b, b2), 8u) & 0xFFFFFF00u) == (uint)((baseClass << 24) | (subClass << 16) | (programIf << 8)))
							{
								queue.Enqueue(PciBusDevFunc(num2, b, b2));
								if (++num == maxCount)
								{
									return queue.ToArray();
								}
							}
						}
					}
				}
			}
			return queue.ToArray();
		}

		public byte ReadPciConfigByte(uint pciAddress, uint regAddress)
		{
			byte output;
			ReadPciConfigByteEx(pciAddress, regAddress, out output);
			return output;
		}

		public bool ReadPciConfigByteEx(uint pciAddress, uint regAddress, out byte output)
		{
			output = byte.MaxValue;
			m m = new m
			{
				Xb = pciAddress,
				QC = regAddress
			};
			return rB(I.uC.WA, m, ref output);
		}

		public ushort ReadPciConfigWord(uint pciAddress, uint regAddress)
		{
			ushort output;
			ReadPciConfigWordEx(pciAddress, regAddress, out output);
			return output;
		}

		public bool ReadPciConfigWordEx(uint pciAddress, uint regAddress, out ushort output)
		{
			output = ushort.MaxValue;
			m m = new m
			{
				Xb = pciAddress,
				QC = regAddress
			};
			return rB(I.uC.WA, m, ref output);
		}

		public uint ReadPciConfigDword(uint pciAddress, uint regAddress)
		{
			uint output;
			ReadPciConfigDwordEx(pciAddress, regAddress, out output);
			return output;
		}

		public bool ReadPciConfigDwordEx(uint pciAddress, uint regAddress, out uint output)
		{
			output = uint.MaxValue;
			m m = new m
			{
				Xb = pciAddress,
				QC = regAddress
			};
			return rB(I.uC.WA, m, ref output);
		}

		public void WritePciConfigByte(uint pciAddress, uint regAddress, byte value)
		{
			WritePciConfigByteEx(pciAddress, regAddress, value);
		}

		public bool WritePciConfigByteEx(uint pciAddress, uint regAddress, byte value)
		{
			ub ub = new ub
			{
				Hb = pciAddress,
				eC = regAddress,
				pC = value
			};
			return lb(I.uC.ZA, ub);
		}

		public void WritePciConfigWord(uint pciAddress, uint regAddress, ushort value)
		{
			WritePciConfigWordEx(pciAddress, regAddress, value);
		}

		public bool WritePciConfigWordEx(uint pciAddress, uint regAddress, ushort value)
		{
			if ((regAddress & 1) != 0)
			{
				return false;
			}
			mb mb = new mb
			{
				ob = pciAddress,
				w = regAddress,
				yB = value
			};
			return lb(I.uC.ZA, mb);
		}

		public void WritePciConfigDword(uint pciAddress, uint regAddress, uint value)
		{
			WritePciConfigDwordEx(pciAddress, regAddress, value);
		}

		public bool WritePciConfigDwordEx(uint pciAddress, uint regAddress, uint value)
		{
			if ((regAddress & 3) != 0)
			{
				return false;
			}
			Kb kb = new Kb
			{
				Zb = pciAddress,
				k = regAddress,
				jB = value
			};
			return lb(I.uC.ZA, kb);
		}

		public byte ReadIoPortByte(ushort port)
		{
			byte output;
			ReadIoPortByteEx(port, out output);
			return output;
		}

		public bool ReadIoPortByteEx(ushort port, out byte output)
		{
			output = byte.MaxValue;
			iB iB = new iB
			{
				Sb = port
			};
			return rB(I.uC.Hc, iB, ref output);
		}

		public ushort ReadIoPortWord(ushort port)
		{
			ushort output;
			ReadIoPortWordEx(port, out output);
			return output;
		}

		public bool ReadIoPortWordEx(ushort port, out ushort output)
		{
			output = ushort.MaxValue;
			iB iB = new iB
			{
				Sb = port
			};
			return rB(I.uC.xB, iB, ref output);
		}

		public uint ReadIoPortDword(ushort port)
		{
			uint output;
			ReadIoPortDwordEx(port, out output);
			return output;
		}

		public bool ReadIoPortDwordEx(ushort port, out uint output)
		{
			output = uint.MaxValue;
			iB iB = new iB
			{
				Sb = port
			};
			return rB(I.uC.Ac, iB, ref output);
		}

		public void WriteIoPortByte(ushort port, byte value)
		{
			WriteIoPortByteEx(port, value);
		}

		public bool WriteIoPortByteEx(ushort port, byte value)
		{
			Vb vb = new Vb
			{
				Jc = port,
				OC = value
			};
			return lb(I.uC.lB, vb);
		}

		public void WriteIoPortWord(ushort port, ushort value)
		{
			WriteIoPortWordEx(port, value);
		}

		public bool WriteIoPortWordEx(ushort port, ushort value)
		{
			Vb vb = new Vb
			{
				Jc = port,
				OC = value
			};
			return lb(I.uC.Z, vb);
		}

		public void WriteIoPortDword(ushort port, uint value)
		{
			WriteIoPortDwordEx(port, value);
		}

		public bool WriteIoPortDwordEx(ushort port, uint value)
		{
			Vb vb = new Vb
			{
				Jc = port,
				OC = value
			};
			return lb(I.uC.ib, vb);
		}
	}
}
[CompilerGenerated]
internal sealed class mc
{
	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 6)]
	private struct nc
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 9)]
	private struct RC
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 12)]
	private struct vB
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
	private struct GC
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
	private struct QA
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 68)]
	private struct b
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 248)]
	private struct aA
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 487)]
	private struct KC
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 922)]
	private struct cc
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 992)]
	private struct wb
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1007)]
	private struct gb
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1039)]
	private struct Wb
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1045)]
	private struct Ec
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1060)]
	private struct X
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1081)]
	private struct GA
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1146)]
	private struct cB
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1150)]
	private struct OA
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1254)]
	private struct rc
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1271)]
	private struct cC
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1280)]
	private struct hB
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1307)]
	private struct UB
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1322)]
	private struct xb
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 7939)]
	private struct Rb
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 8264)]
	private struct Uc
	{
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 8631)]
	private struct p
	{
	}

	internal static readonly vB _093B3357935904F97823324DCBA1085FBE9B64056BA8741E440615DD426208CA/* Not supported: data(00 00 C0 3F CD CC AC 3F 00 00 A0 3F) */;

	internal static readonly b _0AC7FA37D3AF0482766CED9124EB0E46E9828D1B7003AC97908D3483A195262E/* Not supported: data(2C 01 00 00 58 02 00 00 B0 04 00 00 60 09 00 00 C0 12 00 00 80 25 00 00 40 38 00 00 00 4B 00 00 00 96 00 00 00 E1 00 00 00 C2 01 00 00 84 03 00 90 D0 03 00 00 08 07 00 20 A1 07 00 40 42 0F 00 80 84 1E 00) */;

	internal static readonly UB _14245013C876D67A1C0277841629246FB2011114389E4FB16B17CE9D13031215/* Not supported: data(1F 8B 08 08 9B E3 6C 63 02 00 00 7D 56 DB 72 DA 40 0C 7D DF AF D8 C7 66 A6 69 BF C1 18 48 68 20 10 9C 34 97 37 C5 56 B0 8A BD 72 D7 5E 88 F3 F5 D5 DA 01 6C EA 76 26 E1 62 24 AD 2E E7 1C ED FD 24 5C DE 2F 6F 55 F0 BA 65 1D B2 9E 57 89 8A 52 34 1F F2 AF A7 48 A5 A5 2D EA 7B 8C 53 C3 19 6F EA BF 6C 22 67 52 CE 51 4F 32 8C 2B CB 86 E2 F2 60 73 95 F1 2B 64 7A 41 EF 98 5C E6 9C F4 E2 CC 4C 7C 0A F2 88 84 E6 2C C4 37 1F E3 DB C9 26 64 AE B1 1C 48 E5 06 EB 9C CB 73 EF AF 7A 4E 39 55 98 A8 C9 E5 9A E3 2D C5 7D D7 BC 00 53 1F 6D 02 B4 5C 16 10 A3 8E 62 42 23 EF 0B CC D9 D6 FA 70 FC 29 8F 3B 07 E6 17 0D E4 31 76 5B 2E 49 2D E0 1D B3 4C 1E DA 82 2D 54 C4 46 F3 9B 0E 72 B4 14 43 13 A6 F4 61 9E C8 BC 93 A9 80 87 3A F7 92 BA 14 48 47 20 36 A0 23 CC 29 66 93 B8 B8 62 7B 6C AE E5 DF 4D 13 83 B2 B2 70 8F DB 53 86 12 BA 76 1F 43 53 0B 24 B1 11 55 FD 88 2A 08 A7 19 EF 3B 23 A5 02 31 19 70 9F 93 18 90 BE 66 B3 D1 37 FE 25 E4 63 07 23 57 58 14 1C 3C 52 89 C7 87 A3 CC A1 0E 53 C4 0A 52 1D 18 90 60 7A 8C 25 6D 4C 93 FA 35 BE 21 E9 39 D0 D6 0D 9C F6 C2 56 C2 8E 96 FA 1A C1 92 9C 16 7C 8F D4 1A 37 68 A4 80 14 6C 21 31 2A B4 A6 E9 31 64 C7 43 57 68 73 30 DE 6A C5 95 BC 51 E7 B7 D0 A2 98 EF 24 4D B6 59 F2 8F 00 23 90 12 5A 4B 36 83 36 1D FC BF A4 24 73 94 22 1D 98 4D 46 60 3A 95 A8 95 95 0C 62 B1 D7 73 DE 50 DC 05 85 8A E0 D5 4A 76 EA C1 C8 B7 4F B8 A9 DB C9 43 78 3D 5B 45 3D CB 99 91 9A 29 1E C6 41 44 2B 04 9B 9D 12 0A E2 AA 44 C3 92 F7 1B DB DC 47 E8 A5 B4 9E FD 0C 66 A7 07 24 94 FA 72 F0 BD 18 62 B7 A9 07 66 13 72 55 F5 80 DB 7A 49 0B 1A DC D6 A6 44 F9 EB 1F 73 1E FB 87 87 BF 8C 75 EA 72 3F DD 65 51 31 9E 22 AA 30 63 97 8C 26 C1 5A CF E7 A1 9A E4 1F C4 F6 6B FB 39 A5 3D D4 A2 2B B1 ED 78 1C 99 EB 0F 95 C3 D4 C3 ED 6C 21 5D 30 BC 3B 6F 82 FE F2 E8 F4 D3 EC 42 5D 8D D7 C1 22 58 47 4A DA 4F 46 70 99 56 5D 85 F1 3C FC 48 D9 35 88 4F E0 8C A9 B6 68 49 E1 D3 67 57 36 9A E2 2A 29 A5 C1 F6 6A BD 7C 5A 4C 16 6A 6C A1 40 2B 20 7F 2D D5 72 3D 0B 97 DD A6 74 75 AE 51 9F C9 7B 91 7D 8E FD DC CE 16 2A 58 DE 8E 27 3F 67 E1 24 EA CB E7 2D 56 32 EB 3D D8 A4 6D 4A 27 51 25 B3 A8 20 66 8B 67 83 C5 38 C7 BC DF C3 B6 2C B5 BC A5 A5 0E CA 93 ED 0A 33 48 CC 00 0A 96 97 A1 7B 95 E4 8E 83 3F 0F A7 82 E8 3E 54 0F 8B 59 A4 56 60 21 B1 9C FB A7 11 99 B4 B5 3D 44 5A A0 40 1A 36 D8 C7 78 FF 48 15 E0 0E 9A C2 FD 38 1A FD B9 AE 9D 67 CF 90 84 86 29 19 D0 D3 0C CA F4 88 3C 67 8A CC 0D AD 90 59 02 29 B7 00 AA E8 CD 77 AD 9D 7B 34 FD AB A1 E5 D1 67 31 79 D2 21 14 3A B8 52 53 AA A8 E0 3D DA 46 2D 36 16 84 F2 FF 59 9A 8F CC 20 ED 1F DC 65 58 8C A1 02 BD B4 B4 21 2F 38 52 47 51 AA 35 ED B8 6C 6A 1F D1 A6 0B EA F3 45 F6 E8 64 16 FA D9 C9 0E 18 D6 8B 07 E3 ED 3E F5 A6 C7 89 1F 24 53 2C DD 85 5A DD 45 29 61 96 B4 2B C3 C6 29 C9 7A E9 6B 97 14 F1 D2 88 CD CB E3 40 15 D7 68 36 E2 75 D0 C6 2F 63 99 D5 C6 81 B9 E8 72 6B B2 D9 7C 12 77 62 A4 54 C4 46 E3 85 06 6A EA 4A 16 D0 52 4F 49 1F E6 2B BD CA C0 03 3D 57 37 2C 45 6C 3B 53 E9 98 F6 E5 AC 2B 37 95 A4 E5 B9 B2 67 BB 3D 87 D6 8E 76 20 4D 34 DB B6 CB F7 96 32 32 08 B6 C7 C2 3E EB C6 B8 C3 8C 85 DC 83 2C BA F2 FB 20 61 2F 6C AB D5 52 2F F8 95 32 99 B8 B7 E2 3C 77 62 D6 B6 32 D8 22 18 50 F3 3A FE 00 DB B9 65 D0 2F FA 0F 82 5A CA 7D A4 C0 B5 EB AF 9C 6B 90 1F 65 DE 2D 01 DA D4 0E 48 68 EF 45 FD 6B 95 48 DC 6F 1A 22 F7 91 D4 B7 32 17 7F CA 70 BB 55 E0 84 D8 64 F0 C0 99 67 2A 53 37 C8 98 29 38 8B 31 81 0E 33 B0 67 B4 55 11 2D A0 D7 6C 15 4E C7 3A 82 AC 6D FB 21 BA E0 1D EA EE 6E 3B 04 7F 46 23 90 50 77 6C 77 DC 9F D3 33 8B 2B 7D CA 33 DA 6E EE 51 1D A7 FB 61 EC 1C 95 E9 46 4F D9 99 04 ED 50 8F 28 F3 7D DD C3 0E CB C1 65 E7 95 6A FB 7F A5 EA DD DB DE 9B 1B D6 F9 AD 70 E4 4A 32 58 76 24 59 18 76 47 1D 1B 5F 5D 8E 36 C6 3E E4 FD E4 86 60 F4 44 C0 A9 3B BB 59 0E DC B9 C7 90 B9 7F 4A 72 67 FF F8 EC FC 9D 71 50 27 C3 E7 70 50 8A 3A 79 82 A8 C3 27 8D CA BA AC 30 3F 7A 8F 90 7E 79 5D 98 84 97 ED 18 FE F6 BE 07 DA A2 9E 49 F0 B2 B2 04 99 0E 5C C5 47 70 FC 01 55 76 2B AB E1 0C 00 00) */;

	internal static readonly vB _17809FD22CEC2D886B96788908794D74C349B4061AB6794626ED350329B79F6B/* Not supported: data(33 33 CB 41 00 00 F0 41 00 00 F4 41) */;

	internal static readonly hB _1966574FE2F887C0A53C154B2FFE59930A3356E60CD3BC1D5C6A8330B801CFA5/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 39 00 6D 56 5D 77 9A 40 10 7D DF 5F B1 4F 3D CD 43 F2 1F 08 E2 47 15 B5 81 26 26 6F 2B 6C 70 2A EC DA FD 48 24 BF BE 17 AC 0A 96 E3 D1 83 EC 30 73 E7 CE 9D 19 5E 64 E9 8C 56 94 59 1E 6A BE 48 47 EC 39 FE 14 46 F2 99 CA D8 54 7E 96 D2 39 BE 16 D9 5E 98 9C 47 CA 49 73 30 64 25 9B 2D D3 68 99 AC D8 DA D7 82 27 B2 A2 4C AB DC 67 4E 1B 16 47 F1 EA 69 3C 63 71 12 F2 54 66 3B A5 4B 5D 90 B4 7C 52 6D A7 2C 3D 1A 4F 2C A1 31 7D 9C 82 24 07 23 45 EE 8C AF 80 A0 AA 3C B0 08 47 5A 59 B6 49 37 57 07 35 5F 50 45 4E E6 EC 57 1C 74 EF B3 64 27 D5 17 BE FC 55 AB 82 37 FF 8A DE F1 72 15 C4 AB 15 FF 7E B1 9B 0B E2 6F 3B AF F9 AB 97 77 6C 24 1C EE E1 81 13 4E D1 90 10 5C 9D 6E 48 3D 79 7A 15 8A 47 A5 CC FE 71 C5 22 27 1A B4 07 EF 24 8B 94 34 85 F6 96 3D 09 7B D8 4A 63 6A BE 26 9E 1A 91 13 90 2C 5C 7E 75 16 EE E8 88 9B B8 6E 11 C2 C5 E9 9C CA 86 3F 1E EB 2D AE 5C CD 66 3F EF 03 25 00 07 16 E6 A0 4D 4B 08 FB B5 53 B9 34 2D 69 B3 EA 40 EA 37 1B 45 EB D5 19 87 B1 6C 29 ED 41 CA 9C 27 B5 95 95 65 00 5D B8 2F C9 63 59 69 53 F7 6A 71 8E 8C A3 E3 DA E8 D6 67 2A C0 FE D7 F9 24 28 CB 4F 52 48 6D 90 EA 10 28 F9 D8 3B 02 2F 23 B2 CE 50 E6 F8 CF 26 DF 8D 17 60 13 95 B8 E0 BA 50 31 26 53 B1 4D 1C 32 78 2C 85 75 2C 16 47 EB 15 9B 0A 2A 3C B0 02 85 93 85 11 28 32 02 98 CC 93 E3 23 69 A9 50 A0 B6 0A 23 48 EE A9 8B 66 BD 23 AB 7B 75 E9 D1 35 F1 F4 B5 D3 9E 4F BD 00 E9 0E 90 5A A1 DE 87 17 A5 2E A5 FB D4 66 DF C6 2D 4B 2A A4 CA 24 C3 B1 23 25 95 13 65 57 7D DF A7 BA 6C B2 B0 77 70 0C B0 67 D7 35 D2 4D BC C2 49 07 48 C7 E4 0D 3F BF A9 73 06 8C 7D 51 4C 40 A2 83 2B 82 30 FF 49 F7 3F 9B 57 2A 75 4B 90 D2 1F C2 A1 75 CE 16 4B A9 F9 58 9B 2F C1 16 35 80 CB FD A9 A7 76 F0 B4 13 F0 E8 91 3A 8F 29 33 5A 5E 69 EA FB 5E 08 43 C7 4E AA 67 D7 3F B7 E4 FA AD DD DE 9E A9 A6 22 64 3B 4F F4 68 5F 08 85 AA 9C 45 77 9B 48 E2 0F D2 7C 8A BA 5B B5 B3 51 28 94 10 EA 3E 34 B2 97 E2 63 89 F1 03 95 89 0A 38 4E 7E 6F 95 B8 16 66 0F A1 3D 8A 7D BF 0E F6 62 87 33 DA 81 C2 81 3C 27 D1 AA 9F A6 65 AB 70 1D B2 C0 40 BC F5 C0 03 3F 48 D5 FE E2 F9 15 21 9B C6 EE 1A 8E E4 87 2C F5 A1 82 8A AE 08 D6 18 4C 59 3B 9D 30 53 7A 3A 96 85 B0 DE DE 70 FD FD 5C C4 3B C4 65 71 ED 76 94 B5 C5 8D CA 4A DF 1A 07 13 36 87 82 AD 91 D7 78 2F A4 60 00 AE 07 3B 18 18 8A 4A 56 B7 55 64 13 C8 9C CF E5 50 DE 53 B1 45 85 A0 97 AD 3D FD D7 3A 27 BB 1F 2A 25 D0 59 9D 0A 42 16 6F 77 03 AE 56 31 5F 0A A5 1D 0E F8 FA C3 3D F4 44 82 9E A1 F7 86 A7 17 49 B5 EC A2 07 68 BB 23 E3 E9 3A CD EF 6E C2 F7 5B 0F 4E A6 42 F3 94 7A F3 BB A3 7D 58 D8 DA 0E A5 30 92 19 59 CD 1F 1F 9E 1F 06 B6 5C 07 C0 D5 DB B3 24 25 73 31 90 6F A0 90 EA 59 BB E0 D9 35 E5 0E 12 36 F2 D6 F1 85 D4 07 AC 57 D4 F8 45 73 DC FC F1 2D 40 A1 DF B5 A9 DA 8E C2 75 07 F1 8F 59 B4 8E 66 43 D5 91 94 CB 12 FB A7 E0 BF 14 DA C7 D8 66 93 8C 4B 79 3C 6A 88 2F 8D 5A AB 17 2A 49 3B CC D3 DA FA DE E8 6C 87 A0 51 6D 44 51 9E 40 FF F1 C2 10 90 62 43 20 EB E6 A4 1D EA 42 61 19 2F 42 16 07 E1 72 16 06 7C 34 7D 39 2D CD 76 8E 12 54 C5 DE FE 9B 1D BD 2C 06 E0 27 54 20 6E B6 A3 C3 C9 0A 46 4F 32 13 B9 70 60 1E CF 8B A2 A7 84 69 ED 55 2E BA 4D 77 1D 7A 63 9F 13 C2 7D 48 EB 9A 0E EC 75 63 40 C7 DE 43 A9 6C 17 6F CA 56 70 F7 8F 0F CA 3A 9D 6E 01 86 CD A9 72 54 E8 5B 09 4C E7 77 97 B7 92 D9 2C E5 B1 C8 8D 38 CD 1C 0B 30 C3 22 45 A1 DE 25 21 82 91 E7 C4 3A A7 17 7F 21 F0 99 77 DF DD 41 2D A4 67 B2 52 81 93 EF 1B 12 C8 68 A8 B9 9E B4 80 B0 0B 38 7B 7C 66 CB 64 96 46 9B E8 F4 3E D7 F4 C3 BC F9 69 E8 A3 FE EE 64 41 32 EF 3A 9B 18 ED 0F 17 38 93 D9 24 B8 7F 7C 4D A3 81 78 A9 34 62 5B BB F3 C0 BE D4 A6 1D 56 9B 30 5A 44 4F 41 CC D6 96 E6 94 ED 9B A5 2B B2 01 2F EB 30 5C AD 60 0A D2 51 47 DB 2E D7 AC A7 D1 C1 39 D6 EE B6 CB 62 6C BB 25 47 53 A1 CD 1E 25 FD A6 66 E2 23 E1 77 B8 FC 7F 0D 62 5A 6C DE 96 43 F2 0A A1 C3 D0 88 77 88 F8 F0 C0 BF 1E 34 3E 2C 58 2C C6 8B 20 99 0E BC 94 FE 05 39 6C 0A 5D 47 0B 00 00) */;

	internal static readonly gb _196C29DA8D018B9C00285F7F4984221844C705FE798AADECE15D3C9BDF871B19/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 32 00 65 55 4D 73 DA 30 10 BD EB 57 E8 D4 69 0E 49 A7 1F 7F C0 31 0E D0 42 E2 89 69 69 7B 5B E4 C5 DE 41 96 3C 92 5C 70 7F 7D 57 81 36 C8 BD 80 58 AF 57 EF ED 7B BB E4 D0 91 09 D6 18 99 5B D7 5B 07 81 AC 11 CB EA 29 93 4B A3 2E 21 AC 45 D6 F0 97 AC B0 23 65 4D 3D A8 60 9D C8 66 EB 80 87 34 6F F1 A3 2C 9E 37 45 2E B2 BA B5 4A 6E 50 B5 C6 6A DB 10 7A B1 7E AA B2 E5 2C 8D 65 AE 46 13 D2 58 75 A4 A0 5A 2E 8A 22 27 AF AC AC 46 1F B0 F3 F1 26 91 69 0D 23 BA F4 8D AD 75 87 EF 32 9B CB B7 5B 52 6D 07 E6 46 3C 81 27 3F 01 FC 68 7F 81 C1 30 89 16 B7 6B 59 59 3D 44 E6 5E 94 F6 C8 D5 E7 68 D0 81 66 16 FC 86 C2 5A 2E C0 D5 47 70 28 33 A7 DA 3B B1 34 5C 2A AD E3 E5 BC DB 2D C4 06 B5 55 14 46 31 43 7D 20 23 67 F8 8B 54 24 35 76 D0 A0 1B E5 9A 94 B3 FE CC 48 E4 B7 A5 75 21 E9 7D 45 3B 74 1C C0 49 53 EC 10 5A 0D A6 4E 0B AC 41 6B 84 9D 9E 64 7F 41 53 53 D4 B4 EB 06 43 0A CE DC E6 0E E1 AA D7 17 2C CA 76 FD 10 D0 89 0A 0C 9B 01 12 34 8B 6C 96 3F 25 11 3E 7B 20 56 5F 05 07 C6 5F C4 39 6B B3 2A 17 D9 04 36 E9 D8 22 B9 82 5D 2C 60 1D 07 63 AE 7C 9B 8F 8D 01 7D C3 0E 08 E8 47 33 B1 85 A6 C6 C8 35 98 61 0F 2A 0C 8E 4C 23 4A 74 D8 F0 69 6A C3 BC 85 0E 35 5A 73 81 C2 6F F7 7A F0 F2 41 83 6F AF D8 8A F5 B2 AC D2 6B F2 D6 8D 1E 34 31 A4 4D C5 7E 66 E3 26 5C B9 8D C1 59 33 31 1B 4D 02 1B FC FD 1B 38 6D EA AB 53 70 D8 A1 2C 41 1D 30 FC 33 C2 F3 C3 B9 ED AF CE 20 CE 32 9E ED CB 0A 38 63 F7 FB 74 16 03 B0 3A 4A 56 99 78 86 9A 20 C0 4B AF EF D1 44 A7 BB 83 2C 34 AA 70 27 DF 66 DF 8A EF 37 62 85 0D 6B 2F AA 1E D5 E6 BF E1 A4 77 7B 23 0A 43 07 82 E4 C9 65 E4 9E 8C 7C C4 70 E4 61 E2 1E 66 7C 64 6B A4 79 67 E4 6B EC 2C 3B F9 1E CC 41 14 55 75 DD E3 6F C4 89 A9 85 8A 13 B3 F4 31 BB 3E 52 1D 5A B1 45 1F F8 E7 38 E9 D7 AC 2A E5 DC D9 A1 17 8F 78 E4 B7 C3 D4 BE 79 4B FD 87 F8 91 62 2A 5B BB B3 3E ED 99 09 A8 35 05 C6 95 C4 1F AD AB 49 C9 6F AB 6A 29 33 6E E8 D2 B7 D6 D0 5F D2 AF 6E AD 7A 52 C8 26 54 2D 76 53 98 59 43 FA BF 95 95 73 84 F8 C6 A1 9B 82 DE DE AD EE E4 3C EE B2 05 98 7B 0A 67 B9 9C E5 0C 1E 48 6D 77 58 F5 60 04 47 BB 58 F5 FD 27 51 8E 2A AE 01 A0 FD 30 B1 54 04 B8 1B 03 4E 34 C1 00 2B 32 87 14 D1 03 D2 08 D7 CA 2C E5 1B 99 5F 07 AA 96 0E 70 41 12 EF 3F F0 79 47 81 EB 35 A4 04 D3 B8 DD 10 BA B8 61 60 F4 04 67 D7 5E 6D CA C5 D8 A3 53 2C 87 98 63 E7 83 35 38 E5 9E 19 A8 A9 E9 E4 DB 78 18 0D DE 88 8F 25 B8 19 04 E0 4B B4 06 63 4F D3 49 32 93 C8 02 35 9D E2 68 CC 6C 17 97 9E A8 0E E3 D0 5F D3 58 D0 E3 26 5D 50 2D 81 B3 62 3D DB 24 A5 CE EB B9 38 ED 28 59 81 D9 BB EA C5 2E CD 4B 33 AF 9F 14 A7 DE A1 F7 82 E7 6A 76 B1 FC CB 74 B9 B8 DD 3F 83 EF C8 E0 AB 75 72 F0 3D C1 D5 00 99 FC EB 7D F1 CF 51 99 8A 43 F0 FA F4 61 F6 25 DD B3 D4 B4 AF 23 72 49 17 EB 41 07 4A B5 1D C5 FD 73 91 2F AB F8 9F A7 6B B9 A5 FA EF 86 61 A4 E5 36 B2 57 8C F4 DF 2E FC EE 83 43 E8 E4 CA 46 55 1F C0 87 DB 3C 6A F6 73 50 36 04 CB 05 1C EA 78 D7 33 82 3E AB 09 1A 4E A3 7C F9 23 14 78 21 20 D6 0C 96 4B FB 6B 24 11 A5 46 07 26 FC 3F 42 5B F8 85 A2 62 42 B9 83 7D 60 7F F5 54 C3 1F EF D8 33 64 6E 08 00 00) */;

	internal static readonly QA _1C3E20672997EBFBB5DD01D1B781057C1DCB0BDCFA33C3AC4AD087F8DA83EEF7/* Not supported: data(00 00 00 00 00 00 80 3E C3 F5 A8 3E 00 00 00 3F C3 F5 28 3F 00 00 40 3F) */;

	internal static readonly GA _1E07F093B38374FA893FE1373E5C44D362AB77DCB5E88B9F5215C4C8F89AE1D6/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 35 00 6D 56 C1 72 DA 30 10 BD EB 2B 74 EA B4 87 66 DA CE F4 03 5C 87 06 5A 08 14 D3 90 EB 22 36 66 1B 59 72 57 32 81 1E FA ED 5D 5B 38 C1 A4 87 CC 10 B1 DA 7D 6F DF DB 15 45 8D A6 B1 C0 FA 16 E3 93 E7 C7 A0 16 10 99 7C D4 33 AC 3C 1F F5 DB C5 F5 42 17 C7 10 B1 0A EF D4 CF F7 F9 8E 6A BD 42 B3 73 DE FA F2 A8 73 CF B5 2A C8 92 F1 4E CF EB 48 07 75 C3 88 EE 81 D0 6E 5F B2 E6 BE AA 9B 65 36 D3 37 D5 66 AC 8A 08 5C A2 D3 13 67 94 84 E4 68 6D 97 C8 33 44 F2 4E 8D 0E 06 2C 6D B8 09 2F A5 08 83 9E C6 AD 2A F2 99 9E 91 61 1F 12 28 75 1F A8 F4 3D C4 2E 65 3E 9E 2C 0A FD 66 70 B6 22 64 FD 51 CF 1A 1B C9 B4 1C 0A 6F 9B B6 98 60 5B 2F A7 7A 0A 9B A0 56 C8 B0 25 43 EA 86 4A 60 A8 BA 9B E5 A7 61 BD 85 7F 42 FE 6A 21 EC 74 81 55 CB 7B DB 98 E8 59 2D AE B2 AB EE 28 F1 F2 7B 68 C1 BF 14 D2 85 04 28 33 4C D7 C5 4E 71 8F F6 F3 59 B7 E6 45 DF FF EC 46 4D 9C 93 5C 81 CC 45 BD 0F 9F 26 B9 B4 AD EB CA 0A 36 22 63 62 CF 8D 21 B0 67 22 A9 7C 87 36 90 97 E0 AA 6A 1C 19 48 C4 05 19 F0 83 FC E1 E5 37 F7 50 6D E0 94 6F 94 5D 17 3A 0B 91 A9 A9 DA 0E 31 0C 71 74 41 93 0A 4A 72 A5 5E 0B FE C4 49 6E 34 11 7B 4E 27 11 FE 20 7B 35 AA 1A 8B 87 D4 C7 F7 73 87 6A D1 D8 80 7F A7 93 DB EF 5D D4 58 9C 34 06 D2 0B 46 43 02 5B 7C 22 A5 04 C0 51 AD 77 24 39 97 DE 3C 5E 24 46 8B 62 A8 F8 2C F9 CF 22 4B 28 22 37 30 30 51 3A 36 1B B4 7A E1 ED 31 CA 57 DD 11 2E 69 8B DD A7 9F 53 D2 23 8B 26 B2 77 64 D2 85 19 94 AE A9 FE 43 DC A1 17 0A 67 CD 4E 1A 78 E7 F0 70 79 5A 44 46 A8 84 97 37 18 82 E7 94 FA AB 37 E2 F3 91 DB 81 33 58 09 89 D0 D1 11 EA 7A 4D 2C 9F 42 8A 6B 6E 61 9F AC 83 2F E0 44 78 06 A6 44 AA DA 30 99 D3 50 3D D5 9E DB 21 DE 52 52 F1 6E B6 2A D4 C8 35 C6 D2 FF F4 BB 23 8E A2 EF 2B C4 DE D2 56 17 11 22 9E 5A DB 1B EE 3B 81 EB A2 F5 74 9A AB 4C 6E 57 94 24 D5 3F 1A 1F 49 78 48 86 88 EC 3A 4B 81 55 D9 1E 4A 3F 90 42 65 D7 67 26 2D A8 AC 40 5F 63 A0 B2 F5 26 E5 42 00 0F 09 9D 64 6B 18 CE D1 DD B0 6F 6A 85 59 31 C9 D5 EC 6A 7C 55 C8 E8 65 85 EA FA D3 02 E6 8B EA 4B A8 BB 8E 24 31 1F 9F E0 78 11 F0 85 3D 6C F5 12 41 28 8D 9C 98 19 91 C5 D1 C2 57 0D DB 35 7B 28 DB AB B6 5F 7D 9D D6 26 AA AF F9 E4 D9 76 77 20 7D A3 78 94 4E BB 56 E8 D6 10 78 1C EE CD B4 CD 6A 70 B2 D7 A4 1D DD 26 44 4E 76 37 EA 07 55 52 11 5A 25 F5 37 90 28 BD 84 2D F9 FE DE 1D B6 2D 3C A8 99 77 11 EC FE 79 FF 29 BA A3 3D C5 A4 DE 1A 6C 94 74 F9 0E C8 B5 3E 57 D9 E8 76 34 BF 55 53 CF BE EA 87 8A C0 F6 49 BB 0A A1 26 C6 97 55 D4 12 A0 A1 6C FD DA DE 31 E8 D3 DC AB 31 1E BC 3B E7 B7 88 D8 E6 4C 8B BF 88 DE 1C F4 DB BC B8 7F A7 66 72 D1 6F F1 D5 84 9D DA A8 E7 2E 5D 9C D7 8D 05 37 F4 4B 81 75 44 17 59 00 DD DE A9 1B 6F B7 E8 2A E0 70 69 DB EF 8C 42 7C F0 A4 E4 DE EC 2C 02 77 B9 33 1B 81 78 38 06 2D A3 D1 C3 03 9A CE 23 49 98 5E 8C 15 D0 93 60 79 E5 03 59 65 B5 8C ED 71 F0 D2 64 35 18 5C C3 1E 87 D8 67 7E 43 F6 58 E2 10 D6 0A 4B AF F2 F0 44 D1 EC 06 5F 8C 81 90 F5 DB 2F 48 BF A4 BF EF F4 24 3F 8D 86 44 B5 3D 04 79 4C 55 76 40 3B 68 64 FF 1A 90 45 86 41 BE EC 77 03 2E 12 28 F1 87 E0 BB E0 BE C4 6D 4D 4E 4E A5 00 D8 A0 E6 26 82 25 03 AA 9B 91 6B 2A 49 FE BF 7C 24 B2 BD 64 3C 7F 66 B2 C0 B2 99 53 13 F6 40 96 DC A3 FA D1 00 C7 5E 63 81 5A A1 DC C9 EF 27 A7 57 CD 48 26 0C 97 6B 0D 82 18 9C A9 FF 59 F1 6A E9 AD 5F DE CA 7F BB 15 BF D2 C1 08 00 00) */;

	internal static readonly wb _2098D4DA9DF1E0842C6C012F614F25C2944AA0265CD8DACF35D09203020D098C/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 33 00 65 55 C9 9A DA 30 0C BE FB 29 7C 6B 7B E8 A9 5F 1F 20 C3 32 43 4B 18 4A 28 5D 6E C2 D1 04 15 C7 4E 65 07 9A 79 FA CA 40 21 A4 47 3B B2 FE 45 4B 0A 6F D1 44 F6 4E 3D 37 D1 87 2E E8 35 9A 9D F3 D6 57 84 41 3D B4 2F 2F 60 BD 7E 3B F5 5C 23 DB 4E E7 68 8D 7F A7 D6 4C 39 96 04 F7 E1 A3 0E DC FD CD A3 F5 5B B0 7A EE 0D 44 4C 18 64 2D 09 DA 1A 19 2A AF 47 BE AE 5B 47 F2 51 2E 83 9A ED C1 F9 30 BC 5D 32 39 83 D1 F7 52 77 6A 01 AE 83 FE C5 C4 52 44 3D B5 10 76 BA 88 5E D2 A3 CA BB 10 C9 F8 5A CD A9 DA C5 02 5C 39 CC 9D AD 67 F7 84 B3 0A 19 75 21 0F B1 0E 6A 81 3E 87 8A 8C CA 5A 96 94 0B 94 74 22 CA DB 12 9D 9E D4 0D B1 80 B4 61 B7 27 91 44 BE 1A F8 21 F1 96 42 3C A9 9D FB 94 67 24 C8 25 E8 02 6B E1 E5 CA D6 08 53 35 42 97 D4 4D 2E A5 48 10 EB CE F8 BB 8B 44 23 E8 6F 9E F7 41 FD C4 18 21 C9 1A F9 0A 9D E9 86 E9 76 D4 C8 49 67 85 CA 42 83 7F FA 2E 4D 3F 6A 21 75 94 34 C9 57 5F 31 D4 35 6C AD 28 26 4B E9 91 34 44 7B B6 26 A5 F9 C6 C9 38 71 C5 78 76 B7 97 5F 5A 32 7B 7B 12 F4 99 5C 55 C3 9F 01 85 87 E7 65 A1 52 2D 3A E9 21 8A 0F 52 9B D7 A1 F5 98 63 ED B9 EB B3 13 46 66 8F B1 47 B1 E5 46 C8 AD A0 93 8E C3 2D C1 ED D3 18 6D 84 3B 8B 9E 9D 68 1D D6 B7 96 F7 C3 CB 0B F0 E4 4F 83 1C 83 9E B9 68 C5 A9 D8 46 BC A5 CF 5E A5 5B AE 70 7A 8C 07 32 28 A8 B4 4D C6 AF 71 5F 7B 09 5A CE 52 C4 B9 2A 0F 20 D3 41 86 7D B8 34 CF 94 18 E5 E8 F4 3C 96 6A 85 C1 0B BF 38 E4 B2 06 B3 1B 4E DD E4 77 0B E2 62 DF 99 91 77 06 9B D3 EB 46 78 B2 2A 66 F3 D1 73 AE 3E 8C 2D 6C 83 32 6F A2 96 0E 81 57 72 A8 A4 CF 91 E1 DA C3 FF 2A BB 84 E4 6D 50 1B 82 0B 43 FD C8 BE 6D 24 A0 8E B8 57 97 0A FE 93 AA 0B A9 2C 34 9E 53 C2 98 64 3C 89 23 68 43 85 32 A8 75 C3 FE 70 85 98 2D C6 5F 8B 1F 85 7E AC B7 4F 42 95 59 1A 5A CD DC 0B 83 8B F7 D2 56 24 50 5A 42 1A 85 75 E3 8F C8 6B B4 37 D7 9F BA 54 12 94 39 81 03 B5 75 AF 11 E6 DF FB 76 E4 10 02 38 10 AE 81 2A 27 50 91 C9 05 8A 9D DA 80 45 B1 6A D0 8F 69 00 8F 70 C0 FF 16 8E C1 69 C7 C3 68 59 38 35 B0 04 A8 A5 DC 1A 99 83 0B 50 48 A5 4C 86 1E 90 B7 62 E7 D5 80 4F 50 4A 92 08 7C AF 76 49 BF 1C BA 6D CB 95 20 98 96 11 2C B9 BD 8A B0 C7 BC D0 EF F5 57 7B 32 36 7B 14 BD F5 96 A9 AC 6E 83 B8 82 92 BC 2A 8E 14 C2 96 A2 EC BB 57 5F D3 90 3F 7E 13 51 17 16 6A 25 C3 73 44 6B 25 C8 0A 4E 38 B3 C7 74 4E BC F5 DB 25 C8 A3 77 2A B3 CD 0E A4 7D 81 CC 59 8F D4 55 38 86 28 CA 46 82 BC A1 12 BD 00 3A 2F D3 D3 5B 08 13 26 13 A4 8B EF 27 16 88 09 13 2D 95 53 04 93 A6 09 D9 9D F8 81 55 73 E8 90 F5 E2 56 C8 5C EA BF A1 40 92 E6 6D 16 53 11 13 21 8B 15 FB 5E 10 F0 41 84 DC 97 E5 B4 4E 91 AB CB 94 E1 75 F2 D5 62 33 1B CF 32 75 46 C6 34 09 52 33 87 FF 06 3D 5C 14 9C DB 73 4E D1 EC 5E 08 ED E0 5F 20 3B CE 0C FE 31 A9 6B 4A 02 D7 5B 48 85 01 8B 5E A7 DD 98 1A 3D 92 BB 0D DA 2A CB 4F F3 E9 9D B4 BF 1C E1 77 7F 5F 8D 2C 02 17 0D 62 29 F2 62 90 9F 86 D8 A5 1F 20 0A E9 4E 7D 47 2B 60 11 53 77 A5 95 95 58 A8 AF 91 EF 09 65 02 34 74 38 DB E4 BA 4A C2 56 58 B6 FF ED BF B1 8F FA 89 AC BD 92 4C 9A 12 FB BF A5 39 D5 0F F9 07 00 00) */;

	internal static readonly OA _20D961062C451F0C3DA0827F644298D7D3DF8DBAD582217416167DFEF8F7A417/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 38 00 6D 55 CB 96 9B 30 0C DD FB 2B BC 6B BB E8 E3 B4 5F 40 08 43 D2 3C 9A 13 32 33 6D 77 0E 68 40 8D B1 A9 1F 99 D0 AF AF 0C 61 08 D3 2E 92 10 90 AC 7B 75 AF C4 97 39 DF AD EF 33 36 47 A8 24 8F C0 68 DB 88 1C D8 9D 40 93 57 28 0B B6 01 93 7B D3 F2 AC B5 0E 6A CB 32 AD 30 B7 7C A9 72 96 D4 60 AC 56 3C F2 4E D7 C2 21 5D 66 5A FA 70 41 71 15 A8 3F F4 E1 5F 51 95 40 F1 4F DA 5C 83 62 CD D7 AE 60 59 FC F8 C8 32 94 98 D3 BD 8D 0E 8F BA 63 23 E5 8D 28 D9 8A F2 F8 4A AB 92 DD ED BF 6D BE 7C 1A D2 52 FD 8C 54 08 EA 90 57 F8 DC 69 43 8F 4C C3 EE 0C D4 5A 39 BE C1 DC 68 3E 87 33 E6 60 BB 94 C4 10 64 DB 55 29 02 87 E4 02 12 09 22 C1 B9 3C B5 FD A9 42 8A 4B DB E7 DA 9E 6A 7F 5F EA 67 D1 12 2E 07 46 75 F0 85 1C 90 AC C5 91 65 B5 30 8E 27 0A 4C 19 A2 AC 33 BE 06 E5 2C 8B 9A C6 E8 33 14 7C 43 A8 4C DB 41 D4 A6 3B 81 45 54 5F 4E EE 24 D6 82 CA B5 01 BE C6 1A 1D 14 6C 57 B5 0E 7D CD BE E3 1B A1 F8 BD C2 65 3C 25 6D 07 18 51 7D C4 DF 3D 72 06 67 D8 47 1B 7E 80 BC 52 5A EA 0E 53 CE 42 F3 6B 71 61 33 EF 88 C6 93 6C F9 16 DC B3 36 A7 F0 74 54 2A 46 D7 F2 34 17 C8 13 09 B9 33 9D D2 2C 73 22 3F 0D ED 9C D2 98 AF 88 5D 81 82 A7 46 FB 86 1D B2 1D A7 7E 1D C7 0E 2D B0 AC BE 8F 05 12 D2 FA 64 6F D0 11 3B B5 12 F8 71 70 01 5D 5B 30 24 B1 2E 25 74 E0 E6 C2 62 2D A6 FD 0F 60 40 EA 26 34 9A AD 41 54 27 54 EA 96 F3 D0 C3 C5 72 13 F1 9D F0 92 2F C8 CA 70 34 42 15 8E A7 F5 71 11 00 AE 52 B6 82 D6 12 44 37 26 23 58 16 FE E4 BA 6E 5E 55 7D 7B 27 AC 13 47 09 EF 58 D4 69 35 66 4D E5 DD FA B3 76 F4 BB A2 18 C1 EF AB 23 48 98 9E D5 37 AC 6B D1 F2 04 A5 A8 91 1F AC B7 15 AA 13 0E BD DB 83 8C 2B 6C BA 2E CC 04 9E 84 9C C8 B2 25 5B 59 47 00 BA 80 DE 64 F6 3A 7D 1D C5 61 B4 BA CA 25 41 83 62 18 E2 09 DA EF 08 05 B0 D9 3E 66 77 52 D8 8A 53 51 F6 55 2B 60 69 7C 98 3A AE AB B4 D0 D7 B1 E4 3F C1 39 71 35 C6 AD A6 E4 D5 BA 83 73 73 F3 AD 7D C7 77 0E 3A 62 B1 B7 9A AD E8 2B 44 FE F6 F8 D4 F6 36 3C B5 35 14 38 C1 46 D7 40 80 84 CA 61 68 CB 01 4E 68 EB 97 1D 02 A2 14 6E A2 C4 6E 1D DF 80 4C C5 60 CF 90 32 18 23 C5 52 90 C2 AF E8 AD 29 F3 E1 F3 2B 2F AC 91 5A AD 3B 34 6B 50 FA AC 47 3B FF AC A8 82 03 55 DE 08 D3 C1 EF 69 92 83 B4 57 05 DF 55 DA F5 9A A1 FA BC F8 CC F1 76 E6 76 42 95 7A B2 76 06 6A 0F C2 C2 09 5A 16 0B F9 3E EE DD 58 78 EB 0C F1 29 60 44 4E C0 09 66 D2 82 C2 CB CB D8 81 A6 B0 A2 65 51 9E 83 04 6A E6 B8 87 76 46 07 BA C3 B2 5D 6E 1F 92 38 CA FA C5 BB BB 06 B1 B9 F6 BF F1 56 D3 24 B0 FB 77 AD 14 08 D3 1E B2 4C E7 74 34 5C 5C 6F 96 34 3B B0 E4 21 8D 58 E4 29 96 64 EC 5F 1C 0D E6 29 08 C3 1E D0 81 B5 C0 93 30 1C 8D 41 1B 64 66 77 FA 12 CA A9 E9 CC 4C 8C 31 33 E0 40 5E 39 A4 46 34 55 18 48 96 E8 46 C2 A5 9F 08 71 59 A3 A2 22 7D C5 43 34 AC 30 B6 FE B6 5A B2 E5 26 BB 51 6D 68 3A 11 B7 FD D4 8C 37 64 53 E1 38 44 54 7C 14 8F 64 BB 3D 64 EC 0F 9B E9 56 F4 AA C2 F8 BC 07 0B 0A 45 1E 06 FB ED 5E E7 55 58 26 17 2C 55 F7 BA 33 50 DC 5A 69 6C 77 6F A8 4A 68 FE C3 03 F9 CE 6B 1E 2C E1 1D 18 3E F3 96 78 5A 4B FC 1A F7 81 A5 9E 1C F5 A7 D2 9E 67 C8 B7 5E FF FF C0 0F 2C 36 3A F7 F6 B5 A4 B1 01 E2 78 06 1E 56 8F ED F7 48 9A F0 E8 8C 1D F5 61 81 84 51 F9 C0 22 5B 0A 53 84 6D 5D F0 47 10 D2 55 AF 21 1F 0C 92 6A D3 89 DA EA B3 78 3F 9C D3 15 F8 7A BF F8 B6 67 C4 AA 12 C8 C9 7A 27 08 EC 6A 30 E3 D4 CF B3 F5 E0 CD 48 9D F5 4B FE DC 80 2D E0 BA F0 F6 01 03 9C 58 24 9D 9E 81 A8 D9 A3 38 C3 B5 51 A8 48 14 C0 5F F4 CB 0F C6 5B B7 05 37 DD DF FD 36 56 34 E2 E8 EB 7E 23 39 61 2C BD FD FF F3 6A F9 0B D0 89 B0 47 38 09 00 00) */;

	internal static readonly p _261D4D2B1BBAC02D336BEA948D602F3C8006ADABE5D04CB44939860A06D67A71/* Not supported: data(1F 8B 08 08 8A 2F 0C 58 02 00 57 69 6E 52 69 6E 67 30 2E 73 79 73 00 EC 58 79 54 1B 65 10 9F 0D 0B 04 48 4C 6A 83 82 A2 06 A4 5A 45 71 D3 70 54 A8 08 94 60 50 8E 90 70 A8 05 31 40 30 29 21 89 C9 A6 8A 4A 0B A4 A0 71 45 F1 F6 79 E2 7D E0 AD AF E2 81 52 AA 05 B4 5A EB 89 77 BD 43 A9 5A AF 82 88 AC F3 6D 20 2F 80 F7 F3 A9 7F 38 FB 66 E7 9B F9 66 7E DF CC 77 2D A1 E8 D4 6E 08 01 00 1A 99 E7 01 FA C0 4F D9 F0 FB A4 13 01 EC 73 C8 13 FB C0 A3 11 2F C6 F7 51 85 2F C6 97 99 2D 2E A5 C3 69 3F C3 69 6C 52 D6 19 6D 36 3B AB AC 35 29 9D 6E 9B D2 62 53 E6 95 18 94 4D F6 7A 53 B2 54 1A 99 08 FF D3 7F 81 76 44 77 4F 5E C9 CE F4 CE F1 C3 0F 7B 7A AF 12 DA 7C AF 07 E5 B6 CA 2F 7A BB 05 FB E5 BD 97 0B B2 BD F7 0A 41 76 0A 52 6F A9 33 93 B8 C0 9E D0 00 14 52 61 B0 69 19 A7 9D B3 ED 04 11 15 45 89 A1 55 0A D0 1A EE B7 75 2C C1 36 D9 74 32 A2 51 D0 0A 20 70 D8 2C 87 82 9F 5A 63 FD 7E FB 14 13 2F 42 A8 C8 89 94 CF 4A 41 04 68 18 71 57 91 38 05 C0 00 D9 D8 41 38 2F 1F 8C 32 06 A0 1B F5 D3 71 DC 38 F8 75 6A 95 2D 38 03 32 8C F9 0D FF 64 D6 74 36 8B D2 19 49 F2 45 F6 CB 79 A4 04 30 27 3B EB 8D AC 11 60 23 35 5B BB C8 2F 83 09 C7 D5 26 FB DD 40 42 0C 64 AE 04 B9 C8 6F B8 A0 B8 A0 8C B4 B7 8A D0 87 F4 87 10 B9 68 DC 8F 92 9D 2E 67 1D 90 39 F1 CF 0D D0 44 2E C2 CB 4D 76 9A AC 76 74 7C 0A FC 73 05 14 91 8B FD E0 6F 25 8E 2F E7 76 97 96 72 1A 71 05 E7 A0 CD 17 EF 43 41 97 66 52 C7 47 E7 C9 E6 5A 5A 6C 75 EC 65 C3 2B F8 68 1D 36 4F 1B 19 A4 61 1B A1 B5 62 F3 72 39 05 BE 86 30 00 8F 69 1A 10 84 13 CB 56 70 45 72 2F E5 2D A5 39 4D CC 10 88 01 60 48 33 3D C3 F3 BC 7A 60 3C 56 3D 90 BD 85 33 F9 B0 CB A3 9C B3 F7 51 38 39 02 C8 A0 02 81 67 91 B5 04 59 32 87 5C 2E E6 22 B9 5C 9A D3 8B 65 8C 10 FC C7 40 97 04 81 22 A6 99 60 F6 85 06 67 AB FE 4B D9 22 48 50 B6 C2 1C B6 B5 88 B7 9D 9E 7D 3D A7 91 70 C0 AE 42 ED 65 D4 D8 44 6C BC 46 1A 52 4E 13 87 20 7D 52 8C 1E 57 E3 AC CA 71 2A B9 22 85 97 1A 3F 10 35 85 5F 6B 40 55 82 6A 8C 5F BD 80 C2 28 92 A0 58 3D 50 3D 18 03 F3 46 1B ED F1 8F 26 66 D5 A8 BD 8B 1A 1B 87 8D 9D A4 11 EE 1F 67 39 EF A0 4B F9 68 1A D1 C6 0F 90 3D 96 4D EB 50 13 13 4D 22 DB E4 D7 24 A8 05 81 FB D1 79 B7 02 67 87 77 4B B8 22 7C CB 37 FB 42 3C 1F 86 7B 76 1E B2 F9 C3 D0 A8 CD 3A D9 26 4D 84 6E 2D CD 47 03 C9 72 B8 63 C4 1D 4E 26 65 E7 B8 D2 D3 A2 10 B1 12 CF 0F 22 B4 88 88 65 1F 4E A3 48 DA 3C 31 7A E0 C0 32 1A F5 EA 41 39 FC E9 51 E2 70 94 24 8D 42 00 09 09 80 10 9A DD BB 9E 16 89 9C 8D 14 2A 1E DB 02 00 15 9C 5B 5C C9 AD 17 CB 1E 58 2F 39 B6 45 21 6B BF 13 8D 5C 05 DD F1 0A 17 E6 D5 4C CA 2E BA 1A F5 0D 51 C7 91 23 2E 6B BF 10 00 27 80 DB 1E 52 34 B9 74 D0 33 46 79 46 A1 63 60 C3 59 E7 89 DB 7E E0 79 19 9C D5 64 58 0B 95 7A DE 3D C9 47 33 A4 DE 51 2E 5F AC DD AA 91 01 9B A4 65 63 B5 5A 36 0C 15 6A 3C 89 5F 27 C1 22 0C 7C 74 0A 99 DF B8 80 BA 92 A8 D2 80 BA 0A D5 4A 94 D9 28 5B 5B 64 B0 06 CB D7 C4 E0 5A 93 35 1E 0F 15 AA A8 C1 23 36 6F 6F 61 85 E2 B9 35 3D 74 6E DA E4 B8 B8 3C E3 3B 09 37 25 57 1E C3 0D 4F BC 75 E0 C8 C4 8E 43 11 29 B2 9A 84 07 A2 71 93 78 26 C5 AE D9 F8 03 B9 A2 18 4F 3C 78 06 26 75 08 D3 A5 15 97 FA 71 2C 88 33 3F CE 50 C1 61 72 F9 A7 77 15 C7 61 80 6C 93 DC B3 0B 2A 9F 11 21 88 6C E3 DD 14 C0 89 27 CA 36 76 A3 F4 EC 92 C8 3A 88 81 D3 49 9E 24 9B 3D F3 55 D9 F9 6F 92 CB 75 E3 63 F8 7E F2 22 A5 DF D4 89 0A 7B 66 DB 14 A0 CE 9E D6 36 45 13 99 DD 36 B5 91 C8 84 B6 A9 0B 50 CA 3A 4E 27 40 15 12 92 95 9E 77 88 F5 BE 3D 3F F2 FC D8 08 2C B0 B6 12 EB 23 C4 9A 2F E1 A2 C5 4B 28 F0 CA 87 28 1A 0D 63 71 94 60 1D 82 50 10 51 E3 BB E6 85 4D 60 D8 F8 86 B6 A9 6E 32 26 D3 36 F5 2C 39 9B 8A B6 A9 6D 44 4A DA A6 F0 D0 62 0E EF 12 DC F5 42 58 25 86 55 EA 7D B1 D3 18 77 C2 77 63 03 A4 20 3C 6B 58 D0 59 B5 6C 22 16 61 C4 40 4D DB 54 1E 91 47 B6 4D 0D F7 F8 81 46 7B 08 D0 E5 8B 80 B2 10 88 9B 1C 6F 99 97 D5 FE 33 88 3E 36 CF A4 FC 09 4D 6F B4 4D 09 C7 F9 A9 B6 29 F1 CD 28 0F 6C 9B D2 A2 74 1F BF 38 BA 17 97 E8 19 61 81 13 3C C7 91 E9 E0 D9 FD 79 29 69 8C CB E6 0C 61 7C 28 69 A8 F9 15 AF 70 2F 79 D7 C7 F0 D1 CB C9 8E 1E AA 39 6D 4D F5 A0 18 82 F6 DB 6E 05 B9 21 2B 2A B9 E3 C8 67 C0 FC AC F0 55 F8 4E C7 BF 89 47 AB 4B 33 AD 5B 0B 66 A0 00 CC D9 D7 03 E9 40 1D 3F 0E 85 E4 3B 31 D0 12 E1 11 86 E5 C7 F3 3D CB 48 03 FA AE 17 E3 EA E4 AF F4 E6 67 7B F3 1D B3 9F 98 DD BA A1 FC 94 4E 1A FD DE 24 00 C4 C0 47 27 92 74 F6 74 EC 6D 89 E0 DD D3 FE AF 0D B7 15 4F 83 3F 39 24 73 4F 14 05 F5 7C 2A 20 71 79 89 72 AF 35 51 DE 85 9C B4 D3 50 51 79 33 E0 68 2A CD B4 FA 39 9D D7 E4 C3 13 CB 69 A6 67 2F 6D 3C F6 5D 9A 3D F5 B7 02 D2 16 AE 68 4F BD 57 4A 9A A7 D4 60 E9 DC A7 D5 A5 5B B6 CD A7 E0 99 30 70 D5 92 0A CE 25 56 A7 12 FC 4A 2E CC 33 39 B3 55 C3 E3 F7 60 92 02 2C FF 5C 39 5E EE C5 74 C8 76 B5 64 A5 CF 2E C2 C4 8A F1 D4 88 FD 7A 0D D1 35 E2 BD D9 74 83 AC E3 23 40 A5 48 DE 55 BE DB 6B 98 E6 D6 48 3C 3F CE 78 35 BB BD 45 7B D8 9A AE 02 E8 A2 D7 70 85 9D 8A 8E 91 AE BC 4E B9 57 F3 1D 07 5E 8D 98 55 70 6F FA 1E A1 00 27 16 C7 A4 CE CB DE 70 02 C2 61 06 DC A8 FB A5 D6 16 1E D8 44 21 A1 A0 14 76 53 FE 14 2A 84 14 32 7C EF 12 5D 33 E9 AF 74 0B 66 0D 48 E3 23 5C 91 C4 E7 11 BA 24 C7 9E 2C 61 F7 35 FB AB 7B 95 1B F6 75 FA CD 78 2D 7B B5 92 85 F0 5D 0B E0 5B 88 8E D9 6A C5 DC 9B 63 4A 54 9E 20 13 7E AC 41 C2 5E 38 0B F9 82 2F 15 CD 63 65 68 9E 9B 5B 5C 2D AE 2C 51 8E 4B A8 E0 0A 13 63 CA F5 BA D2 52 F3 06 69 60 6D 85 A5 54 3F EB CD 4B 14 D7 7B 97 CD AE 36 C3 9D 2C 46 EF A3 D4 B1 9C 43 E2 99 99 61 93 11 23 C5 33 35 C3 D2 99 DF AC 8B EF 4A 59 D7 55 F5 88 9C 8B C2 A4 3D E7 D2 E0 DE F6 38 45 02 57 8B 7D 3B 48 C5 FD F5 97 84 02 92 E7 D9 18 9C 8B 2D 88 44 4F E4 D0 61 68 E9 23 7E AC 9A C3 D1 B0 0C F5 B0 EF 6E D4 CB 39 73 0C EF 90 F0 0E BC 67 15 BE 93 30 79 CF B3 92 6A E2 43 32 F7 8A FA 42 C8 5E 2A E7 0A D1 70 04 7F 66 1C 7F 66 0C 7F E6 72 5F C2 AC E3 20 0D E5 15 95 06 6E 97 7A 40 FD 8E FA 15 F5 5E 35 CF EF 58 53 73 5A F5 16 6E 17 F7 35 B7 59 30 8F 60 07 CF 7F 26 DC B5 95 6B 01 4F D5 21 78 38 4A 7D 16 84 26 87 12 E1 AD 89 62 7D 29 CF 26 2A 7C C3 33 02 72 E0 A4 F2 2F FB 77 6D A6 94 4C 96 5B F4 ED 96 31 52 5F D0 97 D1 5B 34 BD 16 F8 54 9A 9C 46 61 F3 E2 A9 32 4F A0 0F 1F 7D B2 8C 42 4F 3F CE B2 32 54 AA 20 0F EC E0 C2 B7 09 D6 81 05 EA 50 BA D0 5A 89 6D 1B E8 85 F7 19 C0 40 0D A8 90 57 20 33 08 54 15 EC FF 3B DE 0B E9 9B A5 00 EF 23 3F 8F BC 09 F9 36 E4 6E E4 F5 C8 91 0A BF CF 4A 6C 27 20 D7 22 EF 8B FC DD BE 00 6F 20 F7 21 DF 81 7C 31 F2 3A 64 1D F6 D5 A3 3C 12 E3 D2 90 67 29 F0 5B 48 84 5C 86 3C 2C F3 33 21 2D FC 75 22 93 A9 94 53 02 AE DE 90 67 78 D2 75 C8 B9 F5 DE 25 F9 F7 E8 AD F7 BC F2 E3 E5 62 0A 30 9F 8C 2A B3 9D C5 9F E4 6B 4D 75 6C D5 59 16 9B D3 62 3B 83 A9 72 D9 DD CE 3A 53 55 BD D5 5A E5 6A 76 55 59 2D B5 55 16 F5 CA B4 AA 4A 8B 4D 4F 1C 92 1D F5 B5 40 A8 27 0A 60 83 14 80 1C 27 A2 8F 0A D2 AF 7F 4F 53 F0 03 32 C0 2F F7 9F 18 4A 41 71 E8 AF F7 DF 8A 7D 77 22 FF 9B 54 FC 59 F6 93 0F C5 3E 93 07 FF 26 09 07 45 B8 6D 3A 06 1E 27 19 E1 5D B2 D9 7D 30 17 7D 14 9E 89 3E 62 DF EC 13 AB 45 B7 0A 47 2C 9C DB 2C 34 26 5E BE 95 46 51 3D B6 7D 12 AF B4 E5 FB 42 80 1C B8 0F 13 65 00 74 90 2D 41 11 F8 9D F9 8F 9F 81 65 54 81 3D CF 64 35 B1 A6 3C D3 3A 4B 9D 09 E0 F0 80 C5 D0 DC 54 6B B7 5A EA 0A 2D B6 46 00 73 88 9E B5 16 D8 2C 6C B9 CD 52 67 AF 37 19 58 B2 5D D1 2E 2A 6A 2A B7 35 19 1D 05 76 83 C3 48 10 F6 88 F4 9A 9C BC 1A BD E6 84 02 43 99 46 5F 93 5B 9E 9F 8F A2 7C B5 36 47 8F 15 FD 4A AF 41 5B A2 2F 83 AF 7F A5 B7 B0 A4 F8 04 80 12 1C AB 28 68 A4 AB 31 D7 86 D5 F6 26 07 49 57 6F 3A D3 6D 72 B1 00 F1 68 5D ED 34 19 17 56 70 E0 9C 3D 50 AB 54 74 92 A9 CC 52 D7 B8 DA EE B6 B1 60 63 ED AE 46 A7 CD 9A 6C 3A 1B FB 2A 40 C8 44 87 59 CD A6 7E 72 B0 C5 9F 2E 54 06 DB 48 92 E4 8E D3 17 94 69 82 03 E1 34 BF 6D 5E 68 75 B0 6D B6 BE 68 D0 1A AD 27 98 D8 5C B7 2B CF C8 1A 73 9B 4B 1A 1A 5C 26 16 96 11 BB 61 B1 5D 9B 53 98 8C F7 04 BC 40 D6 A6 DC 86 57 48 3D DC 41 9D 64 CA 75 9F B1 DA 6C AA 6B D4 9C 0D 7F 1B 51 20 C7 77 0C 40 EB 42 3B 85 6F E6 17 EC 11 B4 FF 0E DD A9 00 A8 0A 59 04 88 B6 14 20 F3 6C 80 1A 7C 6B 40 8F AD 02 28 81 62 D4 0B F0 9D 8F 6D 42 FD F4 57 33 04 4F 84 1C 0A 10 90 C7 83 9F 68 7C 44 0B B0 9F 42 03 85 78 2C 38 67 BF 35 F9 28 AD 60 82 02 D4 1A C0 8E 3E D7 08 3E 0C A4 80 0A 1F 22 6B 49 1D 70 22 C4 A2 7D 35 FA 34 E1 63 42 7F 16 5C C2 F7 C1 8C 9A 12 6D 76 A8 47 B4 06 64 13 B6 94 90 8B 23 E5 A1 B4 A2 A5 4E 88 70 E1 9B 50 16 C8 02 58 0E 30 62 4F 33 14 A3 24 B8 84 4A D0 4A FC 0B 31 B2 16 51 9A 31 32 19 BD 9D 98 31 A1 0C 88 00 2A 90 7B 1E B2 0B EA 48 4D 18 C7 E2 DB 8E B1 48 8B BE AB 48 F8 16 07 C5 56 20 3B C1 15 14 A3 C2 91 56 20 33 C8 A9 C2 58 52 A0 84 F9 61 89 2F 4A 23 58 E7 B2 5D 3C 06 46 B9 84 7C 09 DD 08 C7 61 6C 21 FA 9D 41 A2 84 8A 1D D8 8B 99 A2 C5 8C 88 F0 0B 36 25 2C 47 EB 11 28 57 00 83 4F 3A 1C 3D DB 5A 09 CA DF 9C 9B 64 EC CF 01 2B 3E CA 20 3C 97 A0 99 50 92 FC D7 91 D5 41 4F 42 B9 42 6D 25 B3 BE 16 7F 6D 81 B9 B1 FD A1 1A 57 08 6B A1 03 A7 B0 FE 6E 5C 07 76 6E 76 7E 75 0D 52 40 BC 30 66 C1 4A 2C 5E 87 3C 64 0A BD 8C E0 5C BC 6B 91 12 81 16 76 A3 53 D8 4F 2E EC 37 CE DB 0B 4B E8 07 69 F8 9B C8 81 7C 65 CA 4D 29 77 A7 3C 9A F2 5C CA 21 A9 AE 54 3E 55 9A 16 97 B6 36 ED EC B4 CE B4 C1 B4 E5 E9 0F A7 8F A6 7F 9E BE 37 7D 26 BD F0 D8 35 19 F5 19 D6 0C 77 C6 C6 8C 3B 32 7A 33 76 64 BC 93 F1 49 C6 AE 8C C9 8C 90 CC A5 99 09 99 A9 99 FA CC 87 33 BB 57 7D BC 6A C9 71 07 1D 17 9D 75 62 96 25 CB 9E D5 92 75 61 D6 53 C7 0F 90 C3 2C 07 50 A2 58 C5 64 33 55 CC E9 CC 79 4C 2B C3 A9 6E 54 DD AA BA 4F B5 49 D5 AF 82 7F 98 C8 FF A4 41 24 02 A6 FD E0 FE B0 88 23 3B B5 9D 13 52 2A 5C D4 D3 7E F0 FD 68 BA 47 44 51 AA 28 26 22 2C 34 69 9F 10 D1 01 A1 C0 98 C3 22 93 C2 28 9A 6A 4F 17 51 74 CF A9 CC C9 8C 3A C8 22 63 96 85 50 D0 A3 BC E5 E0 D6 38 58 25 3C 25 50 0B 2E B0 0B 0B CC 22 67 91 87 89 0F C2 A4 15 3D 45 8E D8 2B 9A 6E 78 B5 D4 A8 7B F4 EC 1B C6 8E 79 35 C6 F1 40 4F 7B 6C 2D D3 1E D2 CF B4 8B 6E EF 09 11 51 22 51 14 05 48 54 74 7A 62 FC 5D 8C 34 90 2C 15 8A 69 D5 09 59 86 94 D3 61 4B 45 B9 1A 55 2C B3 3F 51 22 97 CA 4F B0 DA 6B F1 B3 62 39 C3 A6 B4 AD 3B DA 65 54 ED C7 28 48 57 D4 52 69 49 2D F9 9B 54 E8 5A 9D A3 8A 67 0E 21 F6 90 A5 31 41 21 F3 5C 98 83 F7 97 32 E9 CC B1 2B 52 54 4C 2A 93 9A 7A 2A AA 2B 83 54 C6 10 94 C4 89 3A D5 01 4C 8C 1F 71 49 B1 DD 69 69 76 37 5A 94 45 05 A7 E4 9C 9A 73 52 81 6A 39 73 58 A0 80 08 6A BF 58 B3 A5 D9 4E 38 BB CE D9 EC 62 8D D6 26 A3 B3 31 D9 62 6B B0 33 ED 54 42 50 B1 58 2D 84 B4 53 32 40 7B A4 A8 9D A2 60 EB 53 6F C5 6C 37 ED 38 7F D5 85 D3 D7 46 E4 3E 6E 1E D9 D3 6F 0E 7F D0 61 3C C8 F5 C1 BD EF ED 7D FA DD B4 7D D7 57 9D DE FC B4 A9 5E 76 D4 BE 77 54 3D 68 18 39 E7 1A 2A FA 82 A1 F3 FB DB BA 8F BE AF EC A4 42 C9 DB 45 DB CF BF 79 47 F9 4B 23 E7 7E 7C EF F3 05 C7 3C 99 90 10 2F B5 BD B4 31 FE BD C6 C8 E3 37 F4 1C A1 7F 36 47 74 C3 25 FD 87 5E 76 EB 9A 5E C5 4F 3F 56 9D D3 78 FD D0 01 9F 7D F2 C6 C8 F4 EE CF 0F BA D4 76 47 52 5D FA 97 E7 4E D8 FB 8D E9 F7 AD B7 44 1E B3 75 3C E5 C9 AC 08 DF DD AD 4D BB 0F B8 C1 7B CE E5 EA C6 9D 86 F0 C3 2F 7C 69 A0 E8 CE 8F 22 2F 9B AE F9 BA 72 3F 5F DD E7 AF 94 7E 2F BD D8 7B F4 A6 C1 4B 07 34 97 69 5B CF D7 1F BD F1 BB 26 27 F5 E9 F6 EB 46 55 33 49 77 DC 73 FB B1 1B 32 86 2E 3C E7 F5 1F 0F EF 99 BE E8 FD A5 03 DF B4 FE DC 9C 99 C7 43 99 C7 71 7C 86 31 E3 4A 18 14 39 93 B3 F0 8C 7B 64 2B B7 DC 57 72 26 C7 8C E4 18 C6 A8 C4 62 24 4B 8E 90 9B E4 28 67 EE 33 51 1B 22 77 72 86 44 6C 24 77 B9 C3 CE 50 9B DA F6 78 ED BE F6 78 E6 8F 99 EF F3 FB 3D C7 3C BF CF FB F3 7C BF BF DF 66 C4 66 A6 7C E7 DE 93 19 37 2A A9 6D 4A C8 7B 2A 6A 36 48 26 55 CC 2B C7 49 08 82 03 A7 E1 F1 80 37 40 07 A5 38 EB AF 0A F6 5F 91 07 83 21 10 52 12 08 2D 40 43 18 01 B6 BD 60 F0 D6 76 3C 07 70 10 63 6E C8 7E 80 C9 87 E1 99 E9 3B 75 5E F5 3B 05 E3 E6 1B 86 DE 4B F3 C2 D2 5A FA C9 00 92 D8 81 03 22 0A 00 80 70 F2 91 64 41 7F FE 73 38 9C B3 8C 88 88 35 D6 41 D8 76 5B 19 AE 04 39 08 3B A1 70 22 9F D5 21 4C 68 FD 4A 92 A4 C4 C1 51 77 D0 CA 24 2F D5 69 CE DB 1B 53 73 91 37 59 D1 9C E6 18 09 FB F0 30 B9 C7 A6 69 95 00 CD 31 A5 26 09 B1 E3 82 31 EB 17 6C 97 14 78 AD 6E 5D 8E DB 14 99 D2 CC DA 64 98 EB 33 92 8B F3 BD 77 45 E9 92 EA 7A 4F 16 DD 69 BB 01 07 43 DE 92 67 2B DE 0D CE CC 19 A7 B2 66 5F E2 0B A3 43 D6 D7 AC 83 56 3C 9E 99 5C 7B C9 EA 70 B4 52 4A 62 CE DE A0 83 4F 59 B8 6B 4F AD 4D AE 86 B6 40 E3 AA DD 61 81 AD A8 D3 00 A5 E7 1E A4 05 0F 18 3B 4A BF C5 73 D4 73 22 E0 CC CA A3 CD B3 75 AA B6 96 3E 63 4D 14 C5 EA 7A 79 74 F6 E0 85 C3 8A F6 BE 97 71 C1 99 94 33 16 A1 B7 96 B0 67 D8 DA 4C DC FD 6A 9C CA 05 2D 4D 92 16 B0 4B CB 45 C3 C7 1C C5 D1 66 F9 3A F6 37 9A 4D 84 46 F8 F6 79 15 46 C8 2E 56 9D 4C 42 08 39 28 6C 99 F0 D2 6B A5 CB 68 36 A6 89 EF C9 B6 39 59 23 CA 7F 3A 77 15 8F 96 97 5D 27 00 3C 4E 00 B8 FD 23 C0 3B 8E BD EA 9D 2D CC FE 35 C0 A7 FF 34 C0 84 41 DD 01 18 A6 87 C1 E0 88 E8 1E 00 98 77 40 A3 DF D5 99 D8 F8 09 5A 31 84 28 42 12 21 06 10 36 02 B4 08 71 00 21 2A 85 40 6C 87 80 0B 91 C5 9D C3 39 BE 3A 7C FB 87 8E 25 16 E7 84 C2 BA 12 2F C4 06 1C D8 B9 34 C3 B7 9A 7F EF A6 77 FF B9 3F 44 BA 7A 16 98 07 E6 C5 B3 02 51 17 35 95 33 42 C2 36 F2 B4 43 9B F8 F7 55 1A 8F 66 46 50 2A 91 0A 5E BB FE 13 85 CE 29 3D FC 5C 5A 78 E1 73 7A 41 FC 52 0D 29 AB AC EF 8F D8 D6 F9 0C 50 EF A2 41 22 A5 CA 4C 7C 94 5C 44 68 72 2E C5 77 9D 6F 53 71 B9 B7 98 39 29 CA BE 4F 3F 23 71 4B 1B 79 9E B4 F4 5A 2E 47 B4 95 FD BE 7B 39 3F 9C ED 36 77 7C 1E D1 F8 14 9E D7 15 F0 5C 27 53 31 1C 52 30 A9 32 69 51 34 0D 79 D4 0B F6 E4 5A A9 73 E1 FE 60 AF 81 52 14 AB 0D 61 7B FD 0C CD AA 27 A0 5C 07 1D A7 92 98 33 06 A3 41 81 ED 89 A3 1E 97 B2 F5 5F CD DC 95 88 1A DF 30 EF 95 8F 8C EA 64 71 42 9C 52 E9 7D 9B DB A0 71 CB C0 E5 0A CB BE 27 AB E3 6D 46 E0 F7 92 0F F3 8E 1B DF 42 79 9E 80 99 B9 72 8A A7 D2 D7 9E 7C 7F EE 84 1D 7B E0 F0 D5 37 32 E5 31 D6 F7 55 0A 90 F6 72 20 AF 45 F5 0A EF 51 AE 8F 48 C7 01 F8 E8 2F 01 06 43 81 BD C4 18 4E 8C C9 00 52 C2 17 C0 46 DC 41 03 61 82 30 18 FA 4D 95 BA DC 19 B7 EE 1B D0 85 F4 AC BD 9E 53 D5 3B D9 06 88 ED F0 7C 04 10 04 F8 93 79 93 0F F9 1F FC 6D 9E 89 83 4A 24 F9 0B 97 38 EB 81 66 51 DA 13 D3 14 A8 23 F2 3D A4 55 3C 73 EB C3 86 FA B7 50 37 13 11 B6 55 E3 2D 1E 2F 3D 2E 21 FB 22 99 74 DA 30 7E 55 51 4A 8F 09 40 35 74 68 A5 EE 0D 29 2F 2B B3 0D 1D D7 98 0A 89 7A F0 BA 2D E3 E0 25 DC DD DE 15 5C 5C F1 72 5B 10 5C BA 3B BA 25 E4 C2 63 11 E9 22 99 3C CB 07 54 3F C4 8D 72 59 D9 70 23 43 FC 96 AE 86 2C 5F DC 2A 90 86 CE 79 3D 52 B5 28 9D 31 E2 85 A2 C9 0E E2 22 AE 64 0C 94 AE 46 8C 4E D4 32 1F 11 B6 9E BD CF F1 72 75 19 A6 82 DE 03 0B 8C F1 5A 11 3B 92 D4 9C C7 F0 DD 3C EB CB B4 79 F7 FC 27 11 D7 23 5D EA 3A 14 1F 1B 0C B0 97 52 51 66 C9 A7 E5 F7 28 20 D9 66 AE DF 88 35 32 7D E0 94 48 B5 68 3D 75 26 AE 66 D5 60 0D 0C 53 EE DF 53 60 73 A8 3D B4 03 CA A6 2D 9B 6A 7D 51 C9 F4 76 95 45 81 CF 7D C4 30 95 01 D8 8C 89 0D AB D5 2D 68 A7 D2 71 00 ED 65 01 76 9A 97 08 B7 1F 16 DE 2C 50 EA 63 E2 2B 7D 1C 54 D3 AF 4F 40 7D 85 80 FA F0 6E D4 C1 E4 CF 2D 59 7B FE 4D D4 91 48 02 DB D2 08 D1 6F A2 8E C7 FF E9 2B F3 00 DC 3B 57 66 D5 C1 DA 11 5E C6 EE 1F 53 01 4E 1D 37 2B 07 3B D7 73 84 59 01 E2 ED 00 80 F0 CE ED F0 ED 3A C3 EF 1D F1 87 EC A7 26 B8 09 66 C0 2B 29 5C 0E ED 7F 99 02 B5 12 A0 4F 1A 4C 72 73 9D 78 69 78 DA 2E 1A 6E A8 BD F0 A4 0A 6C 34 CD EB 8F A5 3A BB C8 FC F8 14 E9 E9 84 7B B4 8D 29 5D B8 13 5B 0E A0 89 D7 FA 52 9B E5 FD EE C7 2A F4 A4 DF E0 8F DC A2 11 D3 4B 6F 75 6E 53 33 B7 7A DF 61 7B A6 2A 46 0D D6 54 F5 22 44 A5 B0 75 1E 65 7A 15 5C A0 6E 3F D4 1A 2A BC A8 33 91 96 78 93 37 D6 5E D2 5A 8E D4 59 71 CE E2 15 8F 9B BB CE F7 16 29 D0 54 65 EA 53 51 2A 0C 9D E1 CA FE E7 E2 C6 F9 DE 47 4E C5 CB E9 2E 47 77 5D 0E 1E 51 8C 39 E7 42 DA 67 84 A9 2E A1 95 9F D9 83 76 53 0E 52 8C 7E 06 80 E8 61 D6 CB 8A 09 60 E7 26 43 4C A2 51 8C 20 DF BE 20 61 17 3D 47 E5 9C 8C C3 7B BD 99 54 6D 9E E8 AD 41 F7 81 60 B8 5E A5 62 EA C5 4B 51 AE 13 94 33 75 C3 D8 60 76 BA 6C F6 FB 67 DA 56 4B 40 33 AE D5 AD 7F 91 7D 46 5D 77 16 4F 6A E3 D5 FE D6 BE 3A 19 78 7C E3 25 21 6F CD 7F 8F FD 64 79 E1 8A 0B 19 F2 2E FE 27 34 95 35 4D 79 0B 0A 70 AB C7 03 33 3D 3C CB E7 EB 63 86 7D 91 71 37 15 36 CF FA 4A CA 97 9A FE 10 B2 68 FE FE FE 56 16 AD 62 A6 4B 67 C3 FC 41 B9 85 F8 DB 77 A6 AC D7 7E 7A 8B 52 63 06 6E 06 35 94 6D 0C 1E 1F 71 D8 E8 D6 63 80 7D 70 3C D4 52 DC 82 4F FD 51 EF 1D DF 3D C7 24 E8 77 7D 99 27 49 44 ED 1A C8 E4 23 9F DE 1F BD 9F 45 5E 7B 43 E7 C0 80 9F E3 8F 95 EC 7A B1 A8 44 F8 85 EA E2 6E 5B 01 11 0E 8F EC 23 BA C9 1B 57 B5 79 65 55 D3 F0 7C E9 9B 33 DE 97 E2 6A 94 DA 6F 72 E8 79 F5 46 05 6D 8C 3C E5 AC CD F7 D2 4D 15 41 1F CD 11 6A 17 0A 0D D6 FC 7E 3F 92 8B 7F 7D 30 CF 11 85 7F 3A DC BB 5F 7D BC 74 19 D7 EB F7 5A 4F F0 EE D1 73 1B 47 D5 2A C0 06 7E E1 A2 F7 A3 9E 85 5A 03 E9 49 6D DC D1 9B 39 C2 15 E7 83 70 83 43 5C 29 0C 35 EB 0F F5 F3 69 01 5F C8 5E 02 FB CB 5F B0 4F 1B F1 D6 37 EA 6B F6 FF 83 B7 ED 4E D6 2E 0A 48 00 C8 2F 5C E1 63 08 60 10 70 80 6E E7 4C 94 9F CF 84 38 0C 08 EC DC E7 C1 5D A7 37 B0 73 44 71 EA E3 2C 1D 9D 89 38 CB B9 E1 CE 11 32 7B 9C 3B B1 F3 EE 74 9E 15 47 E8 E7 BA D3 0D 8D 39 B1 4B 6A D6 18 C7 3F B4 80 D6 E6 03 AE 6C 19 5B 2F 6B F3 54 7C 1F 81 16 C2 65 69 7D 7A DD 5E A7 0E 17 5C 5B B1 77 AD 51 5D 7D EE E7 3B 80 79 90 F7 D0 50 2F 4E 22 BF A7 66 F4 55 47 57 59 C6 0D 1D 2C C4 E7 7D A3 3D E3 BB 0F 9E 47 8D B4 2F BE 3A 09 B1 0A 8A 0B B3 22 BD 1A B1 49 6D FF 52 AE AE 28 C4 88 3D E0 1E AD 25 A4 A4 E1 4A 62 4D 21 53 4F B5 4D 14 6C 75 35 DD 04 19 AE 27 73 84 6F E5 E9 60 27 4B BC F1 55 DA 52 90 25 CF D1 EF E0 FB 9E D0 69 5C 48 DF D4 B0 6C 31 7E 56 A7 00 70 99 31 9E A0 D0 76 1D 79 14 75 1B 85 BE 3E D0 99 09 D0 AD 3E BE 0B A7 D3 C9 D4 2E 37 E0 98 CA 8F 63 41 26 4F F3 93 D2 79 54 16 14 AD FB 1D 2B 5B C1 A9 07 26 C4 51 C6 8F AF 04 6A 56 04 36 96 DC 01 89 85 A1 EA E4 CF B6 55 29 C8 4B C3 BB 87 4C ED 53 B5 3A 8E AB 21 95 6C 8D C0 15 DC FA B3 52 30 34 D8 FB A1 2D 05 19 D5 F8 C3 8F 16 90 05 E0 D3 BF B4 00 98 CF 2E E2 F3 D9 0C A8 D4 CB ED 0E 07 1F 12 57 C0 9B EF C9 C9 11 35 E1 04 8E EF 10 2F 0D 48 02 E2 C9 A2 C9 80 BF F0 EF 11 BF 23 AA 4F 9A DA A6 1F 4E 3C 01 0F 84 1A A0 84 92 1F 86 12 24 0B 23 25 DF 6D 09 DF 4C 32 BE 65 09 36 09 B0 A6 BB 2D 72 EC 52 B3 18 F1 C4 D2 89 26 D5 D5 FE C9 B5 E5 7D C2 66 B9 CC 47 5D E9 A6 4C 06 36 3C B8 10 96 3F 46 E8 9A 16 A2 0B 3C 6D FA 36 69 4B 5C 32 AB 92 DC A4 12 2D 30 0B FD 10 A9 8A A9 1F 49 5F 19 96 5F 32 8E 32 62 BB EC 79 6C 7F AF 86 68 F5 2A FB 38 B7 92 F7 D0 70 08 CA D7 54 69 C3 A6 1E 53 DB 3B 49 2F C7 46 8D 3C B3 14 BD 52 DB 9A C4 B1 A5 1B 60 77 21 36 28 B4 84 2F C0 97 A5 47 F6 72 4E 81 56 F9 B1 89 2E F1 67 8F 6E 23 F5 65 8B 70 1E 8C 52 80 41 FA DB 7B 87 5A 44 C8 D5 15 6C E6 D5 F2 43 12 27 0C 1C 8A 06 06 8F 16 36 1C F4 BB 75 2A 14 9B 59 94 B9 34 B7 45 DA 18 FD 36 C7 D7 B5 FF 81 92 21 B3 E4 46 A5 43 95 CB 5C 67 43 ED 85 DE B1 AA 57 29 E9 58 50 7B 75 11 68 3A 22 1B 99 43 D1 E1 84 D4 F7 17 EC 3A AA EB 1A 5B 6E F0 26 46 28 EE E2 0C FD 31 49 79 1E 6F 82 25 70 12 52 02 F2 AF D3 81 43 AA 5F 3D AD FF C5 5B 99 E8 11 44 53 10 FD CA 23 10 C0 76 F8 AF 4C 2F FC A1 2F 14 14 2E F8 80 9C DB A6 6A 83 4B 62 A6 EB 29 82 79 31 81 2F 6D 8D F1 ED 43 0C 70 8D 9A 42 A5 57 FA 6E 72 6B 30 49 4C 25 9D 33 7F A4 A7 23 B6 1A C6 C8 83 92 A1 D8 BA 3A B8 E6 71 46 EA 07 E9 B1 06 75 72 9F D8 A4 6B AD 46 14 24 3F D6 82 4F 46 1B 05 04 F2 1A 2F 3A D6 DF 7B B4 BE 9F 67 BA CF 4A AB 89 D2 C1 DC 79 B6 AD 33 41 80 8A A2 7E 69 A6 8C A9 4B D6 20 B4 C5 7C 38 7F E0 05 37 85 A8 87 28 9E C9 8A 12 DA 7C B9 5D 9C AB 0C 23 59 56 D8 D6 1C 2F 20 71 E2 D8 4D 23 CF AD 5C DF D8 B5 8A EE C0 26 89 17 CD AD A7 A1 CD 09 63 0F 23 33 7D 62 2D 2E 46 CF 95 8D E7 E0 DC 4F 70 A5 5B BF 79 0A 0B AF BE 2A 5D 92 17 7D 37 65 8B C1 B5 FF 8E 52 85 A6 7F 9B A0 EC 6C D7 4B 7E 1A B7 AC CC 00 78 CD 98 E7 D9 1B 0B 3C 2D A4 7E 31 AF B5 CA 6E 09 F5 94 06 89 CB E6 74 4B CB 3C F9 AE AE 70 35 F2 53 6A 50 0A E0 8B 7E 95 1A D0 FF 92 1A 90 03 50 C2 17 09 18 F4 D9 2B FE 7E A5 EF 4C 10 0F 66 7B B0 88 2E F1 AB 89 06 10 78 B7 47 7C 33 19 F9 96 47 D0 75 99 5F 7B FF DD 9E 9B 5E DA 19 20 17 4B 2A C3 62 8D 6C E7 91 A2 8E A1 86 00 47 B6 40 CF C3 59 CA 3F 05 71 84 6A D4 A0 A3 C2 39 46 35 91 6A A3 07 9C CB 15 9D 06 A1 D7 34 1D 79 26 62 E9 07 AB 92 5F F8 6B E5 5D C7 C3 C9 4E 84 99 18 F0 08 44 AA 35 CD A3 4A 66 A1 EE FD E3 CD FE 73 90 C6 FD 83 33 D8 62 4E F0 69 D5 97 A3 62 23 47 5A 13 61 DE 91 2A 7C 21 01 21 E2 D4 E9 9E 66 9C 8F BE 87 15 BB DA DB 04 F3 3E 4B E8 9D 70 47 A5 67 86 A7 E0 FC 05 C9 48 7C 1F F9 79 2E B8 B7 CA 0A 1E DE 1B 27 C0 3E 19 7C 8D A5 F0 FC 64 2A 5A 9A 06 35 92 DB 57 DF C8 11 10 82 BC 30 B3 CF B7 0E 1E 12 D3 66 2D 37 7E 79 B1 A4 CC 81 B1 56 1A 5E 7C 83 CA 0A 1A F0 62 44 D1 9B 36 4B E7 50 D0 D9 41 B2 65 D3 3B 0C 45 1E 86 58 AA E8 C9 87 1F C8 9C C3 50 49 64 AC B7 5F C0 F2 D5 4F 0F D5 23 6D 8F 3E C6 01 BE 64 DE 04 8F B0 DD F1 08 4A 4B 2A 6F 7B 10 71 63 FE DA 22 BC 77 E1 77 4A FF D3 FB 9A 1C 4E 79 DA 72 1B 61 1C C6 E9 53 AD 00 23 D4 0A 28 1B 47 8C 93 0D 82 1D 60 DD A1 94 51 D3 CE 1A 8B 71 C5 A0 09 55 02 06 EB 8C C1 5A E2 EC 08 47 08 00 7C 3B 48 72 EE 6E B7 41 71 1A A2 B0 76 68 3B 6B 62 AF 9D D4 64 DB 1F 24 01 09 51 31 84 14 00 48 20 88 FE F0 31 44 10 C3 7F AA A4 F9 63 4F E8 A7 79 1D 17 D2 96 36 AA 7D CD 6B 7D 3E E8 0A 8F FD A4 2A C7 BC 60 C1 BD 28 3A 48 F5 9C 75 C7 2B 5B 74 3B EB 8B F6 03 AA 87 67 42 02 43 E3 F3 05 7C 50 39 6F 1A 84 E8 9B 73 58 35 9C 61 29 DF 51 02 AD 8C 21 DA 2B 2F BA 82 75 18 59 67 75 66 EA 84 97 3F BC D1 5B 08 FF 7E D8 B1 5B 02 08 3D A3 E0 BA 20 F7 A6 FB FC 68 51 A0 8C 21 52 1A 03 95 0D B0 33 15 D4 7C 9C 61 50 E1 10 D8 92 B0 2A DB F4 D3 46 E0 22 BD 6C C4 A5 E8 9E 41 9C D3 0D 45 CB 8E 90 06 E5 22 37 1A FF 09 E6 6B DD 8E 0E DD 97 98 A2 52 DF 3C A1 94 7E BB 70 08 AE ED 6A 70 03 7E 55 E6 40 15 BB 78 69 10 59 F0 BD 8B 41 85 03 1C 9C E1 39 14 FA 4E 61 6D 1E 52 B8 7B CE 2A E1 07 75 AD 99 DC F3 0A F3 E4 78 C9 9F 30 1F EE 50 CE 56 ED B1 11 EC BD 22 BE 75 44 F0 91 03 B3 82 1A EC EA 78 A7 E7 12 D7 B9 D9 C9 05 BD 5B DE 37 C2 DA 77 3C C1 17 CC 4D 78 22 1C DB B2 60 E3 84 50 02 04 13 80 42 08 3F 41 80 24 94 E2 E3 EC 34 23 0C 42 1C 71 8E 4F 21 79 C8 70 47 A0 96 5F 6F 62 2D CC 3F DD 7F 9D F5 7A E7 C3 74 46 30 33 09 D8 89 68 1E 54 DB 06 B3 ED 2E FE BF 53 78 7C BB 44 60 FB E5 A2 0C 84 49 48 76 1A 90 C2 F6 DA 88 2B E1 A3 00 92 FB C2 2B AC D6 29 B9 4C BD 15 9C E8 FA 29 0C 74 EC 17 9F 45 BB CC 27 01 A7 76 AC 49 0B D0 00 D4 92 55 93 95 FD 15 77 59 93 E3 27 B1 12 93 49 11 67 7B 3B E2 5E 82 43 61 6C DC AC 71 AE 22 BF 68 99 28 E5 6D 25 13 B5 F4 CD 09 4A C2 FF 84 3F 76 38 03 F3 96 3D BD 95 80 B0 38 5E 1D 65 C0 53 EF BE 7A 04 41 AA 69 63 A7 6E F8 DC 34 06 E3 F9 6E EE E9 21 94 9B B4 75 2C DC 99 09 BE 9C 01 9E AF E7 1E BE EA EF 40 35 E7 13 69 E2 36 E1 39 C6 1E 51 E2 FA F0 51 71 46 01 55 E7 7E BF E6 16 8A 34 B3 44 ED 63 05 32 89 42 07 EA 20 B3 31 52 D1 98 9F A6 2A EF 9E 2F EE 7A 25 FB 32 88 67 FC 6E 95 95 12 E2 C5 6D 4A EC D8 81 95 4B 0A E0 96 4A 24 C3 90 79 58 5E 9F 49 FB 99 26 65 BC AE 58 77 3A 8D 91 6D 67 1F DA 28 80 29 61 A6 90 A5 29 37 1C EE C7 D5 1F EF C4 39 DB 0F C2 F1 57 36 5A BC D3 34 AF ED E7 B1 E3 31 98 54 AC AA 4D 10 10 63 ED 91 8D E6 F8 01 FF 58 BF 57 90 4F AC 4B 98 D9 93 DC C8 71 40 8B 39 2E 6B D2 E2 5D FC CD 60 5D 19 13 B3 6A 91 2C 72 89 04 83 8C 01 09 16 3D 26 31 71 99 34 1F C8 F9 8D 92 2B C1 B9 61 ED D4 32 A8 E8 29 31 AD FD BC 15 27 EF BF C5 47 6C C1 35 45 D5 B9 1B C4 C6 CB 5C EC 19 97 12 7D 5E 57 95 3B 3C 90 EB 26 95 71 11 63 04 E9 E0 8E D2 98 AF 46 67 4F 87 1D CB 81 6B F8 C4 F1 56 49 DF 79 4B 22 19 C8 56 C6 F9 E0 4A 0E ED 0A 73 52 AF B5 7B 57 5E A1 88 A5 E4 AA 9A 7A A6 58 03 0D 56 6E 75 BE 30 CC 2B 4F 79 76 08 E9 E1 96 2C C7 60 0C 61 93 AF 52 09 A8 80 D3 90 AD B1 CD F9 84 6C 39 D7 5F EC 53 1F 54 A6 E1 66 C1 76 3F 1C DA CC CE B3 33 92 FD 60 A6 3C D6 8D 8F 0F 72 42 19 5F 64 11 BD 13 96 5E 66 73 C3 66 03 A6 DF 98 3C C4 9F 2B E8 50 2F DD 81 6F 8B 5D 3C C1 FC E6 C8 3E 85 DB BC 16 9D 8A 80 FE 18 1B 49 E5 6D 4A 11 A6 17 97 CF F2 E9 C9 DF CF 8D 95 E2 8F 19 AB 78 E6 4E 37 4A 1B AC DA 0D 98 53 52 EB 0D 2D 06 4C 86 B0 08 D9 36 BE F7 61 F0 2B BF 4A DD 81 E6 BF 53 8F 39 64 89 F0 85 D4 13 52 B5 1A 12 30 18 C0 FE 2B 69 CE 17 CB 38 BB 56 81 92 F5 00 DA 5D 2B 49 D4 08 12 00 04 30 7F AE AA 48 11 D4 BB 9A 21 00 F7 E7 26 08 82 00 E2 A2 B7 C3 A8 E5 5A 2F E3 74 A5 2C 24 39 F1 7C 72 D3 E5 01 FC 57 D6 09 F1 05 83 18 F6 E2 B5 AD D9 FA CE 79 B4 A2 46 7F 98 31 99 B1 44 84 AC C5 8D A4 44 3D BD A9 7B 9E 2D F6 F0 8C E7 F4 48 13 D9 6D B6 E5 20 FB 3E 1A 25 19 9B FB 1E 9C 15 07 B8 97 82 C6 5E EB 53 0F 2E 85 35 18 26 31 0E 05 26 84 D3 6E 18 E0 56 4A AA 92 C9 A6 AC 1C A3 85 BC AF A4 D7 A6 AE EE B5 2F AF E5 42 9D FC F0 E6 A1 B1 7E 62 7F 6A A6 88 BD 75 D8 15 2D 97 92 F6 F4 A6 08 33 E6 D0 9A 93 63 C6 7C D1 E8 11 B7 F5 43 AC 72 DC 9B 45 33 49 C5 C6 7B B4 9B DC 8F 3B 06 CA 0B E6 F0 1E 5C FB 40 DD F3 1E AA 5C 1A 7C C0 EB DA 31 F2 7D D1 F9 EE 43 6C 1A 19 2D 50 8B 6C CA 08 6F 51 2A 1E 2C FB BB 78 2C 08 74 EE 9E F3 2D 83 71 23 9D EC A3 C7 C2 82 59 2C 6E BB 4A BD B1 7B 1C 20 CB B7 0E 27 11 43 1C B4 BA AD 49 6A B3 28 C9 11 99 E8 F3 96 1F E9 8B 64 34 73 E1 36 2D 97 AC 7C DF BA 42 E5 D5 33 06 4F BC 92 E2 4B D2 4D 28 D5 3B 3E 3F 3D 28 C2 97 A4 86 B0 EB 01 51 03 78 9F FF A0 44 FF 62 C6 60 B7 24 7C C1 4C C0 FE DD 12 A0 FA 25 80 81 01 D6 CF 2D 64 88 BD DB 8B 71 52 A2 84 69 7E 31 40 0C 61 F2 2B 7D 08 AA F6 F6 8D 2A C1 B0 73 A1 0B E8 F6 4A 2F F5 13 C9 5C F4 00 BE 0C 4A F5 A9 17 2D 09 35 82 50 71 E2 D3 00 7C 32 80 BF 09 61 08 D6 2A 70 0E 75 6A 30 78 EF 21 74 C9 27 10 2E DC DC 92 40 78 40 7E 80 DB 6D D7 FF FC 21 FD 5A D8 25 7D C1 2D B1 52 81 EE DC 19 97 53 74 9D 85 10 55 5C A6 59 C1 E5 82 73 A8 F3 CF 6B D9 FD C8 52 1E D8 B9 5C 3F 44 75 D3 96 7F 7E FF 35 B3 7D B7 92 56 B0 AD BC C6 02 D3 D2 CE 2B 68 8A F5 E5 AC C3 CA F9 01 13 E5 FC 1C E7 9F E3 A8 E9 B2 53 C5 2E DC 97 DC 54 30 78 86 FD 31 73 0D 5F 3D 4B 82 29 43 B2 D1 2E FE C4 1D A9 D0 25 17 4B 05 72 6D 16 B9 30 AD 61 B4 27 F5 80 F6 DE F3 5C A7 DC F4 C4 53 8E CF 38 DB 41 F6 5B 03 33 94 68 17 FE 8B FB 8B 8B 44 29 98 6F 02 DC 76 10 58 C5 DB 10 F4 72 2A 63 C6 93 D0 3A 52 04 BF A1 F1 95 51 77 B8 F4 34 BF F5 94 42 5A 02 B4 8D AC 41 4D 30 E9 7B 24 9E E5 A7 08 DC C8 73 25 B7 64 72 7A 21 28 A8 44 F6 C5 09 4D AB 78 A5 DA 73 F9 37 C0 65 9D 71 D7 55 0D 23 D1 69 56 17 32 3C F8 E4 CB 2E BC E1 78 32 3F 61 73 53 66 73 E3 A4 76 D1 DB 53 07 75 54 65 34 40 3B DB CF F2 AF 43 9E 50 38 00 00) */;

	internal static readonly vB _296B6E51BA5AACB7425E4FD72B2794E9F4B16399985A5C64822DD8B0AD8E472F/* Not supported: data(00 00 90 3F 00 00 A0 3F 9A 99 D9 3F) */;

	internal static readonly Ec _2E3AB1CD50733E680CBFB545B9B18313EAF76136EB5056F319334C41D0CF75C7/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 31 00 65 95 CF 72 DA 30 10 C6 EF FB 14 3A 75 C8 A1 4C 67 FA 04 8E 70 28 0D 06 12 13 FA E7 B6 D8 1B A3 89 2C B9 92 0C A1 4F DF 95 0C 81 A4 37 63 76 57 AB EF FB ED 5A 2A E7 7A 2F E6 B6 51 15 2C 30 28 6B 50 8B 99 F1 C1 F5 2D 99 E0 61 36 97 62 82 01 C5 84 F6 AA 22 C8 74 85 81 B4 28 14 05 AA A0 50 95 B3 62 AE 0C A1 83 27 A3 F6 63 61 9F C5 42 C2 F7 75 36 15 6B AA 76 C6 6A AE 4E 1E 6E B3 5C 94 47 1F A8 F5 62 34 B7 0E F5 0D 2C AA 9D EA 60 8A 5A 69 B2 29 1C 6E C9 07 AD CC CB 39 16 A6 0E 8F 43 58 BE 58 3C 15 B0 51 35 D9 A1 E3 47 BB 25 17 C4 AD F5 9C 28 39 46 E4 AF 9D 23 EF 61 92 AD B3 C7 AC 88 2D 05 AA 45 6A 93 34 55 C1 59 A3 2A 2F A4 75 1D AC 65 39 83 B2 45 2E 51 D8 BA D7 E8 E0 5B DF EC C8 8B 4C B9 CA E1 73 80 39 B2 16 E8 44 49 AD AA AC A9 FB 2A 58 07 0F 3A 1D 7F AF 4C E3 83 35 50 F4 5E 55 29 06 72 C7 E5 BD 35 7C 42 DB 59 93 34 2C 3B 2C 73 C8 F9 65 A9 74 2C 73 12 93 D5 35 81 1A 87 DC E2 DB 5F A5 D5 7D F4 41 8C 66 65 39 BB 81 89 9D 0C 61 63 51 50 6B 1D 4B 99 84 1A 03 DF C1 6A 6E FA C8 86 55 30 41 7E F6 1F 1A 5D B6 6C 89 F2 5C 0E F2 D9 66 54 1E 54 F8 4B 4E A3 A9 59 7B BB 4F 4E FE 50 8E 74 D4 EC 37 BA 24 FC A8 60 CD D8 1C A9 09 5D 67 95 09 20 71 AB 29 6A 07 E5 3A 97 62 74 6A 36 35 72 03 1B 34 4D 8F AE 86 6F D8 A8 03 3A 8C DE 7D 66 01 E2 3F 41 79 90 F1 80 A0 2A E4 27 13 7A 77 E4 48 9D 14 EA 03 39 CF 3E EE DA F4 13 CD 51 CC 43 0D DF 7B A3 3A 72 62 41 E1 60 DD 8B 87 B9 DA AA C0 1A 35 91 CF 95 B3 2C 9E 67 F5 A3 F2 BB 17 65 44 6E 76 68 2A AA 07 8D 8E B0 EE 4D ED F0 83 1A 59 8D 1D 43 9B E4 9A AB 66 17 CA 8E A8 4E 41 63 F8 5D AE 06 2A B2 62 26 2F E4 1E 39 8B 29 7B A3 71 72 34 98 68 5C 2D 7E BD E3 3B 55 5D D0 A1 B3 8C D3 44 35 2A A0 86 A2 90 97 3B AC 45 F9 87 65 22 28 49 BD 58 91 77 8C 09 DC 3A 8B 75 15 A5 52 7C 8F E6 1A 9B CD D7 0F FD DF 69 7A 3D 03 3C 5A 3A 56 E4 7D C0 0D 94 FD 01 45 7E E1 1C D6 8E 01 6E 29 60 9A D4 88 65 51 42 D6 12 53 8A E6 CD 01 F1 E9 DC F1 E5 FC E1 42 27 5D C5 D7 2F 5F BE A4 17 6B 7B A0 8F E3 20 57 4F 4C B4 67 73 60 E5 14 47 AF 12 34 05 BE AA 56 5C 10 8F BE C5 0C 58 13 A3 BA F5 09 06 EC DD B5 D8 3C AF 0D 99 64 84 75 69 1F 0D 57 A8 C8 D4 5C EA D9 BA 76 78 3B F8 2C 24 63 77 9D 2F EF 27 D7 C9 09 26 89 9D 0A EF D7 5A BA 4A C6 1A 88 7B CE B2 29 8C 97 D8 9E 5E AF 8B 15 A5 14 1B 72 C1 29 DA 7A 31 6D B7 DF 20 BB 2F AE 41 4D 38 B4 D4 0E E6 E7 8F 99 C8 CA 0C A6 E5 EC BA CC 04 0D 7D 8E A6 88 91 3C F1 79 03 59 65 DD C5 00 06 1C 1D 5E 27 2D F1 E5 EA 67 AA 3F 63 76 DF F8 46 A3 FC D1 5F 67 AC 5D 4F A7 59 FF A1 CC C0 79 DD FB D8 BD 67 30 E2 38 AD E4 39 BF 98 6E CE 8F 53 D4 7B 8C F1 3B 7E AC FF 5A FB 7A 41 B6 E8 75 50 CC 06 8B 16 0B 27 3E 8C 1F 73 60 A0 3B 45 BA BE DE 60 43 BD D3 A4 70 3B 2A B9 06 3F F1 21 28 98 5A 5D 93 71 D8 0E 6B E5 F4 C9 91 AA 45 37 2C CB B6 67 60 93 69 1E 16 AA EB E2 2A 0C 44 7A 18 CF D3 68 D6 B1 53 6C E8 DC 7B 56 48 09 73 92 CE 1E E1 17 B6 B8 C3 77 E8 9C A1 4E EC 1F 70 4F C0 37 4B 27 0F AF FC 69 A8 8B 59 B1 2C FF 5B 16 FB 61 A3 DC A9 AD 23 B8 BD 13 53 6B 6B C6 7B 97 BE 87 63 C8 3B D5 C4 FB 64 D5 96 34 23 AF 8F 51 C3 81 AC 0E 2B 72 EF 97 08 F7 7B EE FA 6E F9 53 2E 17 0B 78 E8 B1 E6 4E E3 38 27 7F 2E 87 7F 95 CB E2 1F 7A FC 70 9E 9C 07 00 00) */;

	internal static readonly nc _3E3832818FB6518593CAC6CFA47269EA6235BF627895A21B44F479E8EEF0CCC5/* Not supported: data(10 14 18 1C 20 24) */;

	internal static readonly int _3FABD35D949BD295BC396B880A2EDC95F2EAAC6B954A3E5A7366C8B9599CDDD0/* Not supported: data(63 69 6B 61) */;

	internal static readonly GC _50F930A7CEBDB4D1E8374D6912E147A58F9892F59D568B58FB8C480256858F97/* Not supported: data(00 00 80 3E C3 F5 A8 3E C3 F5 28 3F 00 00 40 3F) */;

	internal static readonly GC _7DEE4AE07B5B54979358E5EE3BBB41CDE8D69432CCE04C4840706AC495818C6E/* Not supported: data(00 00 80 3F 00 00 A0 3F 00 00 C0 3F 00 00 00 40) */;

	internal static readonly cB _81E3F2B51857395070E962B4408B058DE0C613F0981FB4111F22A1CC915C9126/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 37 00 6D 54 DD 76 D3 30 0C BE F7 53 98 1B 0E 5C 6C EF 90 FE 90 16 D2 B5 34 61 1B DC A9 8E 9A 88 3A 76 B1 9D 6E E5 E9 91 5D 3A 92 B1 8B 9D D3 D5 AA F4 FD 49 25 1D 74 2F A7 B6 EB 7A 43 0A 02 59 23 8B 50 8B 4C 7E B2 4E A1 5C 81 E9 F7 A0 42 EF C8 34 E9 A5 0C CE 9A 40 7D 27 B2 82 F8 97 EE 28 3F 64 3B D2 E4 65 79 F6 01 3B FF 51 94 D4 68 84 5A 2E 8D 12 DF 76 A4 6C 77 F9 68 68 6F 3D A4 1F 59 97 86 C5 76 18 54 9B DE 0B E0 C6 BF E4 0C FB E0 55 AB C1 D4 32 EF 76 0B 71 4F 9E 8E CE DE 8A F9 97 15 76 D6 9D C5 8A 94 B3 A8 51 45 30 A4 3C FF DC 07 0A 7D 40 F9 A3 9A 8B FE 66 A7 ED B3 CC 72 31 05 E7 CE B2 42 D5 1A AB 6D 73 E6 D9 89 C5 9D 3D 10 88 2F 91 54 45 0D BA 41 09 A3 47 E7 40 3E 90 E3 09 DE 8B 45 25 D3 3C 23 32 BD 83 38 F1 8D 86 05 92 02 99 A3 F5 17 11 E2 F0 89 B3 50 17 D4 B4 41 64 F3 C7 79 26 A6 1A 1C 6D DA F3 58 70 9F C8 E7 0E D1 C8 8D EE 1B 31 43 4F 8D 91 99 0B F2 0E C3 93 75 07 2F 56 C0 22 3D B2 58 1D 0E A7 27 AF AA B5 2C AD EE 53 AF 2B 9E 2D 74 3E C0 A5 AB 26 16 F6 EA 4E 7A AD 90 0E 91 8F 09 A8 E4 02 EA 48 EE 0E 4E 0C 6B D0 3C C1 2A 5B 30 4D 0B 24 3F F5 35 18 F9 5A 78 16 58 3F 63 0D A9 F6 73 39 95 F3 19 FF 0D DE BF B0 8B 14 06 5D 07 00 AD 1F CA 9E 5B 5D A3 E9 C0 8D BE 7D C4 E2 35 A4 3B 7C FA 6D 0D 8E 63 D4 A2 F9 D1 22 03 44 A7 CE 1B FB F4 D7 52 66 65 7E 46 97 BF 53 DB DB 61 63 56 B6 75 8C BB 83 86 DF 2F 5C A9 42 2D 4B EC 48 59 53 F7 2A 58 27 27 F7 82 B5 A5 3A 47 70 A3 89 95 3D A6 29 AC D9 91 73 E7 B8 43 7D E5 F6 40 DA 2A 0A 67 B1 71 76 4F AA A5 E3 25 C8 39 3A A8 FF 81 20 F4 62 4B 01 0F A3 C6 B9 1D 0B 23 0B EA B8 A8 16 29 FB 31 4D A3 F2 D9 4D 8A 59 22 B0 A0 92 74 04 3F 9E 51 9E 4D 9D 22 C0 35 BC 44 E6 84 EE 94 EA A7 B6 31 F8 2C 1E C9 18 7B 82 D7 3A 7F D3 D1 C4 B4 45 D6 28 EB 78 46 8D E3 ED CD 96 AB D1 FF 05 ED 31 10 47 F4 B2 A7 92 F9 47 1D 7D 8A E3 13 9C C5 16 B9 11 BE 64 71 72 7B 7F 2B 16 60 03 C3 93 9F C9 F8 96 76 56 96 8A D0 28 BC 18 38 C1 68 4F 56 9F 80 BF AA FF 36 16 59 8D 3E D8 31 CD 3C 72 44 35 36 F0 A2 CB 6A 36 0C 65 A4 94 6B CB 78 F8 95 AD 33 09 3C 68 F9 61 F1 E5 63 3A 0A 11 24 6F 47 AF 50 EB A1 2C 0B CE 28 3F 8B 4C 25 3D 4A BB 0F 4F C0 7C DE BF 30 DA 9C C2 ED 65 2B 55 A0 13 DE 44 30 09 C3 0C 8D B7 E3 04 15 E5 32 3D 7D A5 1A 8C 58 F5 3E 00 F7 5E 3B 05 D7 76 62 03 DE D3 7E 4C 89 89 36 30 C3 13 29 7C C5 F5 03 8B 15 D3 FE 31 B5 65 A9 8E 2D F9 01 73 16 53 1D 5A BB DF CB AC 0F B6 4B 30 2E C9 5C 80 EB AC 39 8F FB 25 B4 22 A3 7F 21 F7 B2 DC 16 A2 5A 55 57 1F E6 2C D4 F3 88 D5 23 CF E7 4A 88 0B E6 AD E6 91 3E D4 6F 9E CC 09 9C 3D 98 FA 92 B5 98 E1 41 CD 60 93 0E 91 C0 21 11 38 C8 F2 36 BB 15 59 B6 4C 80 C1 70 77 D7 FC 77 94 52 C7 AC 2C 97 59 FA 74 4F 9E 81 F1 9A BD C4 51 16 C5 54 AC A7 AB F9 6A BD FD 2E 1E 50 93 39 BC 9C D0 EB D9 73 07 99 43 97 DC 3E 81 06 A3 5A 1C DE 8F ED FB 99 9C 62 8C 8F 9C 17 F7 F3 79 29 D7 7C FF 52 7A 76 76 05 CE BF C1 66 C1 DB 2B 27 8E EA 06 07 17 9B 0F 07 5B EF 08 22 D1 0B 05 10 95 03 E3 15 9A B7 44 99 9F D8 89 23 BD DA F2 85 35 37 0B 20 26 89 2A 31 16 65 07 2E C8 92 9D 84 06 5F 22 55 D9 BE 83 DF 32 77 B6 3F 8A 1F 91 81 1E 2E C7 C8 CC 0D 18 07 DD 78 4B 46 05 D7 7B 53 1E 41 8D E4 29 96 D5 FC 66 7D 27 97 D5 A8 7E 69 7A 8A 7B 21 16 C9 34 31 A1 10 1E C0 61 12 3D 2F D6 93 AC F8 B4 FE 76 37 DB 2E E7 A5 C8 A6 9B A5 9C 51 43 01 F4 95 7B 66 0C 1C 7B 06 23 0B D8 79 DE B2 92 36 83 B9 E3 61 35 C2 BB 01 B7 17 09 72 BE DC 6F D9 83 AE C3 E8 9B 0F EC E2 C8 21 25 36 D6 47 E4 56 4E F2 85 88 82 68 4D 4D 3A 53 FF DD DC F3 1F 17 9C 54 1F 56 09 00 00) */;

	internal static readonly Rb _82A472223635658F009A55A82D8D076AAAC4E7819AEC2440D8CB6A8873BD7C0A/* Not supported: data(1F 8B 08 08 8D 2F 0C 58 02 00 57 69 6E 52 69 6E 67 30 78 36 34 2E 73 79 73 00 EC 59 79 50 1B 55 18 FF 76 93 40 C2 95 54 89 02 2A 2E 35 D6 6A 95 2E 47 6D 2D A2 04 82 6E 34 2D 08 D4 A3 8D 42 48 36 4D 24 24 31 59 B4 D4 AA 95 80 35 AE 1D EB 35 5E D5 E2 7D 55 EB 38 1E 20 52 89 ED B4 45 ED E9 85 56 47 D4 8E 16 A9 5A AD 23 56 AB F1 7B 9B 35 8D A4 DE FF E8 D8 6F E7 7B BF EF 7E DF 7B 6F 5F 42 86 59 73 97 81 02 00 94 C8 D1 28 40 0F C4 A8 02 FE 98 86 91 B3 8E 7E 21 0B 9E D1 6C 2C E8 A1 2C 1B 0B 1A 5C EE 20 E3 0F F8 E6 07 6C AD 8C DD E6 F5 FA 04 A6 99 67 02 6D 5E C6 ED 65 4C 35 F5 4C AB CF C1 17 66 66 A6 19 E4 1A D3 66 7A F7 5D 32 0F 56 ED 67 6A D5 15 88 E7 DE 7D F9 2A 41 C2 2B 56 F9 25 6C 5D 15 90 F0 12 59 5F 24 E3 02 09 EB DC 76 17 C9 87 71 54 5B 0D E0 B8 26 05 22 93 44 0E 64 DA 0D 13 21 9D 56 03 64 A0 92 16 B3 A9 6B 71 D0 11 89 42 96 65 1A 20 05 E2 1C 23 7F 6C B3 D4 B9 94 1C A9 8C C7 CB 42 02 24 89 E0 C0 79 4E 25 42 13 C0 80 02 B1 82 88 48 79 00 5B F2 21 4E 7E 06 E0 48 F8 0B C4 60 CE EF B8 0B 05 7E 81 80 B8 2E 45 6E 48 4D 5A 4F 2A E1 2A 0C 38 6C 82 0D 60 11 15 33 00 8D 9C 01 89 44 5A E6 0A 63 61 90 4D E2 58 39 2E 2B 29 6E 43 A1 3F 16 D8 24 19 E4 38 5D 72 3D F3 6C 73 03 91 27 12 7F AD DC DB 21 49 FD 7D 54 18 08 06 EC 20 EF 5D 93 1C 77 58 52 BD 4A 38 48 7F 89 38 71 6D 3D 17 DA D5 C4 85 9A 72 50 79 87 5B AA 37 A6 A0 79 29 37 14 D5 8F 69 01 2C 4B 97 1B 16 03 41 93 A1 C2 12 B6 1A 58 63 6F C5 72 80 92 6D 9C F8 EA 3A 93 61 32 AC 37 19 18 72 27 A2 FA B7 31 BE 6B A0 5D 13 CA 5C C9 A0 3E 6A 0F 1D 7B 3F 0A 58 4D E5 90 40 AF 27 B5 C3 55 7E 2E 1C 22 45 11 86 63 FE 65 0A 02 16 43 2D 7A 5D 51 FD 15 5A A2 36 18 2A 24 5B 54 7F AB 54 59 1C 6A CF E2 C4 EB B1 1F 32 9B 0D 6D E2 1A 2E B4 B6 69 DE 9A D7 7E 21 0E 3B 54 73 61 8F 41 C7 85 05 43 CE 79 B8 36 96 5B BA 70 06 27 7E CD 89 37 F5 90 C9 42 C7 C1 E2 99 D0 47 03 0C 68 3B DF A4 01 35 5A DB D9 27 09 59 DA AE ED 28 88 95 39 C6 DE 37 9A 2A 96 1B CB 22 DA 25 78 E5 C1 D8 F7 1A 51 5F D8 82 A3 70 76 39 30 15 CB B5 9D 1F 01 40 B9 52 12 5F 22 62 A7 24 DE 46 C4 6B 51 14 82 E5 D7 13 A8 29 5F 46 A0 B2 7C 2D 49 CE 32 96 AD 11 34 C6 B2 97 B5 5D 7E 9C 0A 35 4E 9C 9D 63 12 2B 75 E2 04 6D E7 7D 98 4C 7C 9D 37 A1 40 66 EF 9A 83 B3 7F 1E A6 46 44 34 7C 33 52 86 29 B1 F8 B3 D4 62 9D CE 22 46 B8 F0 22 03 B3 73 1F DA 47 F6 50 07 76 3E 4E 9C 6F FD 86 73 2E 71 F6 CA 4E 51 B5 27 1F 20 4C 71 EB 53 95 80 F6 D7 24 FB 19 39 EB 41 05 34 35 BA D3 F9 B9 33 4C 8D 2A 76 5D 4B 99 C2 A9 23 4F 52 64 FD 36 B2 E8 26 22 9A 24 F1 63 40 71 43 37 D9 AD 21 32 F6 BE 47 C6 9E E1 6E E2 BC E5 97 F5 89 F2 FA 3A 3B 24 61 40 B8 A2 5C 7D 2F 6E 8F BB 9C 43 D0 76 35 60 41 71 AE 2E F4 83 3A 70 81 05 3B C3 53 0B AD F9 CE 28 6A AA C5 73 94 E6 50 44 6D 12 23 62 24 32 A2 36 46 76 2A 42 C3 A9 DA E7 B6 1A 43 C3 47 47 86 55 BD A4 73 53 FA 00 BE 09 93 4D E1 06 03 13 D5 67 65 02 4C 59 33 36 74 C4 F6 AB 3F 56 00 0C 8F 7C 8B 21 58 B1 0F ED 03 23 23 B1 9E A4 F5 4F 10 F2 B1 2B 72 48 03 6D 1F 8A 46 E5 97 23 83 E8 D5 3E 6F 54 3A BF 1C 59 8D E2 75 46 E5 17 23 CF 92 F4 85 3A B5 78 B1 9A F4 D6 F6 BC DC 17 BE 77 93 FF B0 2F B9 A7 8E 0C F2 4A B7 A5 F6 51 D8 D0 68 5E E8 47 5A C8 08 7D 47 A3 81 26 06 4D D9 4E 21 AD 4F 89 22 F6 39 6A 0E 4F 1D AD 3A D0 E9 CD 20 DB 34 34 7A BC A8 9A 79 14 F6 34 18 C5 E6 F3 14 91 51 9D A8 3A 3E 6E D0 84 06 A8 B0 2A 17 F5 92 ED E4 DE 6E 0A 5F C4 46 F5 E7 64 90 B3 F5 18 38 4E 14 0C B5 78 93 44 AB A1 02 AF 13 DB 28 5F A7 8A 7A E9 F6 88 17 A8 F1 E6 CE A3 A5 0B 4A 3A 1F 4E 8F 8B 1B 89 D8 B5 5D D0 E0 87 41 54 BF 19 35 52 20 E1 3E 8A 1A 6D 31 17 F9 88 E1 D2 5F 36 87 71 3A FC C0 58 0F 6A C0 4E 06 46 33 25 35 C4 40 0F 85 C7 90 70 87 71 71 B8 99 5C E4 33 46 D4 88 15 4A 2D 2B 07 FE 4E 12 CE 53 F2 D7 E7 59 CB 85 CF C7 4F 0B 17 7E 58 F8 73 B8 F0 02 C6 D8 80 4B 66 42 DF EB 8C E2 3B 66 71 37 27 EE D5 76 91 9B 28 1A 33 B4 AB 8C EA B2 21 6D 47 08 C8 B6 69 B8 AB F7 02 64 C0 A2 CB C4 9D DC 52 13 15 E5 CA A3 51 2D 5C E5 AA 2E 19 E0 C4 37 A3 FA D7 D3 30 AB 46 5D 5D BC 23 34 42 09 27 90 E1 F0 D0 5E 5A 50 19 9F A1 46 27 89 35 19 9C F8 13 27 EE DE F3 E0 68 6E 5C 71 A2 96 BE DF F5 00 16 E2 C4 0D 51 FD 0A AC 55 DD B9 A3 2D 9D 2C A4 36 9C 83 6B 52 F5 90 57 97 1C 19 4B CE 70 06 39 C3 0A 4E 5C 64 E0 F0 00 18 A3 95 AC 32 99 9C 4E E7 32 89 97 71 65 99 7B F3 00 DA 26 70 91 88 CE 39 16 89 46 DB E8 3D 6B B8 C8 A0 6E 44 03 00 F1 0C DC 8E 19 16 31 F3 4D 8C B5 88 AA 75 08 5C C8 61 60 70 07 B6 F6 8E 49 1F C3 5F 6B F6 87 47 8F 55 A6 C5 34 2B 98 80 87 4B C1 0D 76 44 2B 9C 87 92 17 EA A4 71 3E B0 D0 08 45 C8 C5 C8 2C 00 C4 E2 7D 10 FC 55 56 F0 0F F2 0E D2 DF A7 6D E7 C4 F0 33 C4 44 AA 95 75 97 8C 8B 65 3C 4D C6 07 64 7C 46 C6 49 32 DE 24 A3 4E C6 B4 3A 18 4F F1 BF C5 69 E4 39 C8 D7 33 C8 19 00 75 F5 A6 7A E7 C3 9F 98 9F B7 EC AD BA AB 39 EB B3 AD 53 DB 5F A1 D0 EF 98 69 75 F9 04 FC 59 71 31 6F 17 AC 97 B9 BD 01 B7 77 3E 6B 0D FA DA 02 76 DE EA F0 78 AC C1 F6 A0 D5 E3 6E B6 DA 5A 1D 27 97 5A CF 73 7B EB 48 04 FE ED D9 0C 48 94 92 02 25 4A 1A 94 CF 3D 0C 75 C4 2D D9 00 9F 23 A3 2C 61 A2 6F 05 EA 2F 48 3E C4 71 3E 17 EA 5D 92 4F C6 14 1A 52 EA 68 96 CA 4D 83 5C 41 03 B9 0E 35 E4 36 A4 42 6E 69 0A E4 16 EB 07 A8 6C 35 64 3B D2 20 BB 41 03 D9 A5 28 D7 E9 FC 94 9A 06 F5 D3 CA 7F CF 8B 4B C1 FF 96 8A EF FB E0 A4 3B A6 00 6C BC 90 D9 E6 7C 23 1A 85 FF 19 A9 75 00 9B 91 85 02 80 21 C4 FC 6C 80 26 94 0D 88 8D C8 15 28 BB 10 6F 46 36 A0 BC 1C 71 03 B2 1A E5 2D 88 3F 20 EF 62 00 94 7A 80 0F 91 39 B4 4F C6 BB 52 83 BC 83 81 83 F4 1F 21 4E 54 7D 35 1C 8D 9A 7B 7F B9 10 5C D7 80 A0 32 97 45 DA A6 5A 96 AA DE 42 17 D7 C3 28 00 16 8F A1 24 82 B9 64 C0 DC 13 95 08 C0 7C CC 80 59 6B 8A 70 61 D5 93 24 6E 6C 0B 4A 2B 51 1A 79 F8 A5 68 14 FF 06 E8 A9 85 38 AD 3D 07 20 87 01 E8 4E B0 D1 75 38 30 FF 8E EF A7 16 DA EC 73 56 F9 5A FD 1E 5E E0 EB F8 4B DA F8 A0 00 F0 34 65 F6 55 05 78 9B C0 D7 B7 B7 36 FB 3C 6E BB C5 ED 6D 01 78 24 6E 37 F1 97 BA ED 3C C0 6A B4 98 78 4C 1E 17 A9 57 D5 09 1E B3 D7 2D CC F1 BA ED 3E 07 5F 2F 90 2F 31 80 17 E5 F8 FD 15 2E 57 CC 6A 9D E3 6D B5 F9 CD BE 7A BF 8D 58 E6 A1 65 56 82 7E 1D 7D 36 5F D9 36 BF CA C5 DB 5B AA 17 00 78 05 5F B0 25 E0 F5 14 F2 0B D0 3B 15 38 9B A7 9E 17 2A DB 82 26 9B 60 AB 6C AF 71 3A 83 BC 00 7A 62 3F 33 D9 CE 19 2D 85 F8 1D 0A 7B 53 1A 1B AB 1A 83 7E DE EE 76 BA ED 8D 2E 9B D7 E1 E1 03 70 90 FE 47 44 81 0E C7 1C BC 4E E3 ED 14 8E EC 01 EC 1A 25 00 87 52 53 13 80 55 91 54 10 6D A5 38 9E 0B F5 D0 88 63 35 D4 A1 64 86 1A 98 8D BA 19 C7 33 50 26 D4 AF FC F2 27 52 8F 46 56 01 C4 F1 74 B9 8E 12 1F 7A 5C ED 17 D1 40 61 3D 01 02 F2 EF 82 33 10 3D C0 83 19 35 27 F8 30 E6 76 29 86 85 52 28 C2 87 60 33 8E 00 67 41 2E DA AB 30 A6 15 1F 1E E3 05 08 A2 BD 01 5C A8 31 68 F3 81 03 AB 39 91 79 94 18 A8 C4 99 4C 88 1E B4 D8 A5 8C 20 8E 84 4E 03 6D BC 96 1F 6C E8 69 87 D9 88 AD B2 BF 06 AD 24 DE 82 99 CD 58 A5 1D 33 0B 31 3A 80 1D 13 9A 09 1A A0 E2 BD 9B 90 83 60 27 6B C2 3C 01 47 1F E6 22 25 FD 06 42 C2 51 9D 90 7B 2E 72 00 82 09 39 45 38 53 31 32 8B 3C 4D 9A 2B 13 28 69 7F 04 12 8B 68 C3 4C B9 DB E4 39 30 2B 28 F5 4B E8 1E 28 C7 5C 0B F0 E8 C1 2C 69 C5 7E F4 62 A7 68 71 61 45 38 80 8D 81 C9 68 3D 1E B1 18 58 7C A6 C3 49 B2 34 03 98 DF DB 1B 44 06 8C E0 C1 87 49 A8 17 94 34 1E 91 47 BC 94 9C 0E 46 12 AA 94 D6 56 23 C7 BA 63 6B 8B EF 8D F7 4F AD B1 58 3A 8B 5A 08 48 E7 DF 06 76 10 E2 BB F3 5B 67 50 0A EA A4 9C 71 27 91 74 0E 26 64 0A A3 6C 10 48 7E 6B 91 0C A0 C4 B1 01 BD 36 E9 4D F3 20 26 BE 0B 13 94 4F 29 E1 1F 93 FC 3F 07 9A 06 B6 23 BF 3F 45 73 C2 35 DC 35 63 99 54 2A DD DD 91 FF 24 9A 1E A3 29 AA 28 9D D5 A4 A8 A6 64 29 E8 3C 15 B0 AE 94 B4 29 29 94 92 EA 98 4E 53 CA EE B9 EC F9 6C 49 82 45 CB 1E AB A0 A0 9B B9 2F 7F F1 91 70 AA F4 D4 40 33 B6 EF 93 16 28 20 9F 46 1E B6 20 A1 A6 32 7B 92 AB E0 FE 1B 85 6D 2B 7C 4B 6E DC BC C2 F4 D5 BD 7B AA EE 7E B7 BB 23 B7 99 ED 50 F4 B3 1D F4 83 DD 0A 9A A2 E9 74 0A 90 28 FD 74 43 C1 23 6C 66 BC 59 4A 85 6D D9 A5 2E 15 73 94 29 87 D2 95 D5 45 B9 EC E1 44 49 3B 54 77 A6 C7 D7 8C 5F 87 EE F9 5E C6 7B E9 49 41 5B D1 61 6C 36 71 A5 1F 9A 59 D3 4C 7E 4F 4A AE 2A 63 51 01 7B 34 B1 2B 0E CD 49 48 F9 55 08 9B 7F 78 26 3B 9D 3D A5 B8 B4 88 9D C6 4E 9B 36 17 D5 19 09 2A 5B 9F D0 C4 59 B5 45 79 6C 4E AC E2 84 D9 BE 80 BB BD AD C5 CD CC 32 5F 60 9C 6B 3C DB 5C 34 99 9D 14 5F 80 86 3A 2C D7 E5 6E F7 11 AE B0 07 DA 83 82 CD D3 6A 0B B4 14 BA BD 4E 1F DB 41 4D 4C 5C 2C A5 02 45 07 A5 05 B4 A7 D1 1D 14 05 EB 5E 7C 27 67 33 BF 75 C9 A9 D7 ED BB 53 53 D9 EB 1A DC DD EF 4A 7D CA 6F 3B 2A F8 C1 CA F7 BF 5D FD DE C9 87 5C 69 6D 6A 5F CD 3B B4 27 1E F2 90 F5 A9 FA C1 85 B7 53 FA 6B D7 2F E9 BF 7A D9 49 4F 34 9C 6D C9 78 77 D6 E6 25 F7 6E 9D B3 69 F0 F2 8F 57 BE 62 9E DA 37 71 62 41 A6 77 53 67 C1 FB 2D 69 A7 5F D5 7D 7C DD 5A 23 7D F7 0D FD C7 DC 74 FF BC C7 B3 7F FC C1 BA B0 65 F9 FA BC 4F 76 BC 35 B8 6F D7 A7 47 DD E8 7D 68 8A 7D FA 17 97 8F F9 FA 6D D3 9F B8 D2 9D 36 75 DD 68 69 DF 69 9A 9D 8F 2E 6E DD 95 77 77 78 E1 CD 25 2D C3 F5 A9 C7 5D B7 E9 E7 E6 CC 3C 1E CA 3C 8E E3 86 61 DC 61 50 CA B9 72 87 67 DC 83 0E B7 DC 86 24 72 CF 90 75 0C 63 84 D8 18 D7 BA 17 E5 96 50 AE 44 6E 42 97 33 72 E4 B6 EE 52 91 E4 2A 77 D8 19 B6 4D 6D 7B BC 76 5F 7B 3C F3 C7 CC F7 F9 FD 9E 63 9E DF E7 FD 79 BE DF DF EF A1 76 CE 73 CA D8 0F 16 8B 67 99 A7 6D A6 BA F5 97 69 A2 42 85 2B EA 62 1E AA C4 AA FB 7D 8F 10 0E 7C EF 84 01 BD EC 4C 19 84 6D 0B 65 DF CA 82 FB CA 36 87 5D EA DB E4 4B FF 10 31 06 7D B8 E4 B7 1D BB 9D AB D8 73 E0 74 CE 95 6A 6A 64 19 79 7F 55 DD 16 F1 8C 9A 59 F5 14 31 5E 70 A0 1B 38 1C E0 0B D0 93 51 58 06 AB 83 82 D7 14 41 20 30 98 84 18 4C 07 D0 E2 47 80 ED 00 08 B4 B3 1B 2F 00 1C 84 98 1B 7C 08 60 F6 63 EC 36 7D A7 C9 AB 99 55 34 65 B6 65 E4 BB B2 28 22 A3 63 90 0E C0 09 1D 38 C0 62 00 00 88 A4 1F 4B 17 0C E6 BF 80 C5 BA C8 8A 8A DA 60 1C 45 EC 76 95 E1 86 97 83 88 33 0A 2B FA 49 1D 22 F8 D6 2F 24 49 42 18 1C 4D 47 9D 5C F2 72 BD 27 85 07 12 EA 3C 78 D3 95 CD 68 4F 10 B3 4F 4C 90 7B 6F 9B D6 0A D0 9E 50 69 95 14 3F 29 98 B0 79 D1 6E 45 89 D7 FA FA A5 A4 6D D1 59 ED BC 6D C6 85 41 63 85 24 FF BB 01 2A 9E EA 9B FD 79 F4 67 ED 87 1D 8D 78 CB BA D7 7C 9B 5D 58 72 CE E4 CD 8F E3 8A E3 23 37 37 6C C2 D7 BC BB 4D C2 C6 59 1D E5 AA A5 25 17 1C 0C BB F8 54 45 7A 69 EA 91 05 5A BA 02 2D EB F6 42 02 3B 71 67 01 4A 1F 1A B8 05 0F 08 33 C9 B0 C3 23 E7 33 1D 62 BE F6 68 DB B2 41 DD CE CA EF 45 2B 45 A9 26 A2 90 DE 01 B4 24 A4 EC E0 7F 09 1B 91 4B 39 67 11 75 7D 05 63 CE D6 61 E2 15 58 E7 5C 29 68 65 92 B6 84 59 59 2D 99 38 E1 24 61 7B FE 8E 9E C3 95 27 26 C2 CF F8 0E 5E 2E 8E 95 5F AE 3D 9D 06 13 76 54 DA 31 E1 65 D0 C9 96 D5 6E B9 21 41 73 0B 79 BA 4E 8C FF 6C C1 3A CE 56 51 7E 13 0F F0 14 1E E0 CE 9F 01 DE 73 AC 75 DF 5B 22 EC 5F 02 7C F6 4F 03 8C 1F D4 3D 80 21 08 34 1A 4B 40 F7 08 C0 B2 07 1A C3 BE CE 84 C6 8F D0 8A C3 C4 60 52 30 71 00 BF E1 A1 85 49 00 30 31 69 18 6C 37 04 5C 09 2C EE 1D CE F1 C5 E1 BB 3F F4 AC 30 58 67 14 C6 8D 70 21 36 E0 C8 DE A5 19 BF D6 FC 7B 37 BD FF CF FD 21 D2 F7 E6 81 45 60 51 22 2F 14 E5 A1 AD 9A 13 19 BD 55 A8 1B D5 CA 7F B0 FA DC 64 6E 2C A5 0A 89 60 D8 0F 2F 29 F4 CE 20 70 0B 37 62 8A 7F 64 10 C4 AD D4 91 B0 CA FB 3F C4 B4 2F E6 10 0D 2C 1B A6 52 AA CD 25 C7 29 C4 46 A5 17 50 1C EF 79 93 89 2D B8 CE C2 49 51 F1 5D B6 B9 E4 75 5D F8 B7 24 E5 61 05 1C F1 D6 0E 07 EF E6 7F 6F D9 67 E6 F4 63 6C CB 53 68 61 6F C8 8F 7A B9 CA 31 E0 A2 19 B5 19 8B 92 B7 E0 47 03 20 1F AE B5 06 57 EE 0F 0E 5A 28 65 F1 FA 48 B6 57 DD B6 AC 08 01 D5 06 B2 29 2A C9 85 73 20 5B A2 D0 CE D4 49 6F CF 5B 06 CF E7 6E 4B C6 4D 6D 99 0D 28 5E 8D EB 39 EC 0C 3B A3 36 F0 A6 A0 59 EB BA A1 6B C0 E1 83 8F D7 A7 3A 8C 41 EF A5 1E 14 9E 3C 77 1D E5 73 0A 72 DE 8D 53 22 93 A1 FE F4 FB 0B A7 EC D9 43 27 82 5E CB 56 26 D8 D4 A8 15 C1 1D 14 88 2E 2F 6B 56 F9 4E 72 FD 8C 74 12 80 8B FF 1C 60 10 19 70 80 10 43 09 31 29 40 82 FF 02 D8 08 3B 68 C1 CC 60 46 A3 C0 D9 72 D7 AC 29 9B C1 61 7D 70 FF C6 AB 05 75 C4 E9 0E 40 7C 8F E7 63 80 20 C0 9F CE 9B 7E 34 F8 9B DF E6 99 30 A8 04 92 3F 73 09 4B 6F DB C3 2A 34 09 AD A1 7A A2 DF 81 DB 25 72 77 3E 6C 69 7E 0D F5 F3 A2 22 76 1A BC A5 53 E5 27 25 E5 47 D3 49 DE 1A 25 AF 2B 4B 23 98 01 54 73 97 4E E6 81 C8 CA 8A 0A BB A8 29 AD D9 C8 B8 FB AF 3A 72 BE F1 C4 DE 1E 58 C3 26 95 AE 76 84 43 65 FA E2 DB 22 2F 36 8A CA 94 C8 16 5A DD A7 FA 3E 69 92 CB 1A C9 0D 8F 0C 5C 09 8A 5C F5 D8 29 92 21 5B B8 FC 48 DD A2 7C CE 98 97 CC 96 F4 1B 6C 6C 40 CE 70 F9 7A EC E4 74 3D CB 31 11 9B F9 1A 8E F1 F5 55 88 9A 2D 0D 24 34 E1 F2 9A F8 B1 B4 27 85 8C C7 17 59 C7 6F 2C 7A DD 79 1C FB C3 55 D7 86 2E E5 46 C3 61 F6 72 2A CA 3C C5 1B 77 FA 95 E0 6C 73 3F 5C 49 34 36 BD EF 9C 4A B5 6C 33 6B 9E 54 B7 6E B8 01 82 A8 0E D1 14 21 8F 76 46 75 91 B1 E9 CA 67 DA 78 A8 98 DE AC B5 28 F2 AB 81 4D 50 19 82 CE 33 B3 61 74 FA 04 ED D5 BA 8E D8 5E B6 00 39 2F 4A C6 38 4C 88 6C 17 A9 0C 32 F3 95 37 86 D7 0D 19 E0 51 5F C3 A3 3E B1 1F 75 10 F9 8F 56 AC FD FF 26 EA 70 38 9E 6D 19 98 D8 57 51 C7 E1 FE F4 95 79 00 EE BD 2B B3 EA 61 EC F1 2F 63 AF 9F 53 01 4E 3D 77 6B 47 7B B7 0B F8 DA 9D 70 3B 00 20 B2 77 3B 7C FB CE F0 7B 47 FC 21 FB 99 29 EE 82 39 D0 6A 0A D7 A3 87 C6 33 C8 AC 05 18 D2 46 D2 DC DD A6 C7 8D CE DA C7 43 8D 74 97 1E D7 82 8C DF F2 06 63 A8 2C 97 59 1A CF 90 9C 4D B9 4B D7 92 D1 8B 3D B5 E3 48 34 FD CA 40 7A BB 72 C8 EB 44 15 42 E6 35 EE D8 75 5A 71 44 76 BB 4B 87 86 99 F5 FB 2E 3B F3 DA 04 0D 48 6B ED 68 A4 5A 71 FB 22 CA 34 08 54 A4 E9 30 D6 1E 25 B2 AC 37 7D 23 F5 1A 6F A2 83 94 8D 02 89 8B F2 82 C5 73 1E 77 2F BD EF 2C 32 C8 32 55 A9 CF C4 A9 31 F6 C4 A8 06 5F 48 9A E2 7B 7F 75 36 59 41 7F 35 BE F7 52 C4 33 E5 84 0B AE 24 83 C6 E8 7B 65 74 8A 73 34 B6 EE AA E1 CA F1 DD 00 11 03 C4 66 55 39 05 E4 D2 6A 84 4E 35 4E 10 E4 3B 18 2E E2 8A 70 52 CD CF 11 3A E0 CB AC 8E 7C 8C D8 20 3B 48 04 C1 0E A8 94 52 2F 7B C6 B9 4D 53 CE 35 4C 60 22 D8 E9 6F B1 D7 98 77 AC 97 11 CD B9 DD 6B FF 8B EC 33 E9 7B 1D F6 A1 3E B7 3E D4 3E D8 20 0B 4D 6E F1 14 F6 D5 FE F7 D8 4F 57 14 A9 BA 98 A3 E8 1A 7C 4A 5B 55 DB 94 B7 A8 08 BB 7E 32 34 D7 DB A7 72 B1 29 61 C2 1F 9E 74 4D 69 DB D2 5F 4A B1 DC F4 FB C8 65 B3 F7 35 3B 79 74 CA B9 AE 3D CD 8B DF 28 2C 25 DF CC 9A B5 D9 78 F9 06 A5 C1 02 5C 0B 6F AE D8 1A 39 F9 CC 71 AB 0F C1 08 F9 E0 74 B4 AD B4 0D 97 F9 10 F1 8E EF AE 53 1A D9 F1 C1 DC D3 C4 62 F6 CD A4 8A 57 9F D6 4C D6 E4 91 D7 5F D1 3B 32 1C E8 F4 B0 9A 1D 91 88 4A 85 5E BC 57 DA 67 27 20 CA E1 7D EB 98 7E FA 56 90 2E AF BC FA 0D 1C 5F F6 F6 9C AF 67 52 9D 4A E7 35 0E C4 E5 81 B8 F0 AD 67 4F 39 EB EF 5C D6 CF 14 B5 95 CB 17 EE 14 8E 8A D0 FE EE 10 9C 8B 7F 73 A4 D0 09 85 7B 3A 31 70 48 73 AA 7C 15 3B 10 F8 0A 21 78 5B EE C2 96 9C 46 15 C8 30 30 46 AC 26 AE 3B CA 06 C8 4E EB E0 8E DF CE 17 A9 FA 36 1C 3B 32 C6 95 C1 58 B7 F9 C0 E0 0E 1D E0 0F 3E 80 67 7F F5 33 F6 E9 62 DF F8 C7 7D C9 FE 7F F0 B6 DD CB DA C5 00 49 00 FE 99 2B FC 1C 02 68 18 14 A0 DF 3B 13 E5 A7 33 C1 84 00 81 BD FB FC 66 DF E9 0D ED 9D 50 9C 06 58 2B 27 17 02 CE 0A EE D8 0B F8 CC 1E EB 45 E8 BC 3F 9D 67 C5 E2 FB B9 ED 75 B3 45 9F DA 27 35 1B B4 D3 1F 5A 40 FB 93 23 6E 6C 39 3B E3 F5 85 6A FE 8F 88 96 62 E4 E9 FC 06 DC 5F 65 4E 14 85 AD 39 B8 D5 A9 AF FF 18 E8 3F 8C BE 5F F8 C0 08 91 24 79 A7 BF 6E F2 79 57 6F 45 CE 15 3D 0C D8 EF 7D 8B 03 D3 BB 0F 3E 72 C6 BA 1E CF 4F 83 AD C3 93 A2 AD 49 82 62 B7 A9 1D C6 15 1A 4A 22 8D D9 43 EE D2 59 81 CB 9A 03 52 EB 8A 99 FB EF 21 E3 20 EB EB D9 26 F0 18 84 EC 31 BE B5 A7 23 3D 87 93 CF 05 D1 95 13 59 F1 C8 1D 87 1E 7C 4C AF 75 31 7B 5B CB AA ED 5C 77 83 12 C0 75 9E E9 14 85 AE DB B3 47 71 37 51 B6 3F 0C F7 E4 02 F4 EB 8D B7 A1 F4 7A B9 BA 95 86 1C B3 77 92 0E C3 D3 DF F2 93 D0 7B 57 17 95 6C 06 9E A8 58 C3 6A 86 A6 24 51 26 4F AD 85 6A 57 85 B6 94 65 11 89 47 A3 1A 14 2D 3B 6A 95 14 65 A0 7D 63 A6 0E 99 3A 5D 27 35 E0 2A 76 C6 A0 2A 6E 83 79 69 88 2D C8 F7 81 1D 05 29 D5 D4 83 9F 2D 20 0F C0 65 7F 6E 01 10 BF 7D C4 DF 61 33 A4 D2 AC B4 17 8A 38 2A A1 84 33 A3 C9 CF 17 33 E1 04 4E EE 11 2F 03 48 01 12 E9 62 E9 40 B0 C8 EF 11 BF 27 AA 8F 9A DA A5 1F 4A 38 01 0F 98 1A A0 24 23 17 22 C3 4B 16 42 42 BE DF 12 BE 9A 64 7C CD 12 90 29 90 D6 DB 6D 0A EC D2 F3 68 89 D4 F2 E9 56 F5 F5 A1 99 8D D5 83 22 E7 0B 58 E4 DC E8 67 4D 86 B7 BC B9 60 56 0F 63 F5 4D 8B 6D 8B 7C 90 83 DB 74 65 AE B9 B5 69 EE D2 A9 16 E8 A5 21 B0 74 D5 EC 43 92 E7 46 95 9E E7 E2 8C D9 2E F9 9C 38 34 A0 25 76 6F 9D 7D 8A 5B C5 77 6C 22 12 E5 6F AA B2 85 6C 42 D7 0F CC 30 28 B0 51 C3 CD 57 E2 D7 EA DB D3 38 76 F4 43 EC 2F 26 86 47 95 F1 85 F8 1F EE 97 BF 94 5F A4 53 79 62 BA 57 A2 FB D1 4D B8 81 7C 09 D6 9B 49 1A 30 CC 7E 73 F7 68 9B 28 B9 A6 12 72 51 E3 4E 64 EA B4 A1 63 C9 F0 88 5C 71 F3 37 81 D7 CF 44 61 72 4B 72 57 16 76 48 5A E2 DF E4 FB BB 0D DD 57 31 62 91 DA AA 76 AC 75 5D E8 69 AE BF 38 F0 A2 F6 79 46 36 86 A8 F3 5E 09 D1 DB D8 5B F0 7C 8A 2E 67 B8 41 B0 60 AF 9C BE 5B 62 A5 E1 EB 04 E1 24 8F 39 86 13 52 8A 3C BE 78 4B E0 C4 A7 04 E4 5F A6 03 47 D5 BF 78 5A FF 8B B7 32 C1 23 08 A6 20 F6 85 47 C0 80 DD F0 5F 99 5E F8 43 5F 28 2A 5E F2 23 72 E9 98 AD 8F 28 4B 78 DB 44 11 C1 8B 0E 1D B7 3B 87 EB 1C 63 84 6A D5 15 AB 3C 37 70 57 D8 80 48 A1 AB E9 5D F8 AF FA 38 61 EE 41 98 78 50 B2 14 3B 41 23 1B DE E6 D2 DF CB BC 68 D6 24 F7 4B 4C 0B 6B 37 A6 20 7E 58 0F 3A 1D 6F 1C 12 CA 7B 6E D9 A9 E9 EE A3 CD 43 3C 6F 07 AD 75 5A 29 1D CD 5C E6 3B 7A 52 04 A8 28 9A 56 E6 2A 98 7B E5 0D A3 DA CC 26 EE 0C 8F 72 53 88 79 8B E1 98 AD 29 C9 9E 5C EA 94 E0 AA 40 4B 55 14 77 3C 49 16 90 3C 75 E2 9A B1 CF 4E 81 7F E2 46 55 5F 68 AB E4 E8 93 F6 B3 64 4F 52 5E 3C B8 9A EB 97 68 E1 11 BF 50 31 95 8F F5 3A C5 95 6D F3 FA 29 24 E6 5E 90 4C 59 61 FC ED 8C 1D 46 B7 A1 2C 95 2A ED E0 0E 41 F9 F9 DE 71 7E 5A F7 BC DC 10 68 DD 0B 1F CB 2B 4B 3C 6D 24 81 09 AF 74 2A AE 0B F7 97 87 4B C8 E7 F7 C9 C8 3E 3E DE 50 BC 7E F5 63 6A 50 0E E0 4A 7E 95 1A 30 FC 92 1A 90 03 64 F8 2F 62 10 D1 27 AF F8 FB 95 BE 0B 5E 3C E8 DD C1 22 B8 C4 AF 26 1A 88 40 FB 3D E2 AB C9 C8 D7 3C 82 BE D7 2C EC FD 71 9A 6B 97 75 73 88 5C AD A8 8C 4A B5 6E B9 3C 2B E9 1A 6B 0E 71 62 0B F5 11 CA 53 7D 19 CE 11 A5 55 67 1B 17 C3 31 A9 0D D7 98 3C E2 52 A9 EC 3C 42 16 A6 ED C4 33 9D C8 30 52 9B 3E 1A AC 53 F8 03 0E 4A 7A 2A DA C4 90 47 E0 AA 46 EB 22 AA 6C 9E CC 6B 68 EA 49 F0 02 B8 E5 D0 C8 1C A6 94 13 74 56 7D 7C 52 FC D9 B1 F6 54 88 EF 55 35 BE C8 90 48 09 EA 6C 9F F3 9C 8F BE 83 94 BA 39 20 23 78 BB 53 06 A6 BD 50 D9 B9 31 19 D8 60 41 52 62 FF 47 81 3E 4B 5E ED F2 82 42 07 92 04 D8 67 22 C2 0E 17 7F 3B 93 69 2B 43 8B 7A 56 30 D8 D4 C2 11 12 09 BF 38 77 D0 BF 01 1A 99 D0 61 A3 30 75 69 B9 AC C2 91 A9 5E 06 5A 7A 85 CA 9A 2C 64 F4 99 B2 2F 5D 9E DE D1 70 CB 11 D2 55 D3 2C C6 12 6F 23 0C 55 FC CC 83 0F A4 2E D1 A8 34 52 D6 9B A3 90 3B 9A 67 C7 9A E0 76 72 8D 58 C0 9F D4 17 EF 11 76 7B 1E 41 69 45 E5 EB 40 44 D8 58 BE B4 08 DF 7D F8 9D 31 F8 F8 BE 26 87 52 9E B5 DA 45 18 8B 76 FE 58 2B 40 F0 B5 02 0A E9 84 76 46 C2 D8 01 D6 3D 4A 99 B4 ED 6D 30 68 37 B4 2D BE 4A 40 63 5C D0 18 2B AC 3D FE 08 01 80 6F 0F 49 CE FD ED 48 14 A7 11 0A 43 58 02 23 F4 DA 4B 4D 76 FD 41 0A 90 14 13 87 49 03 80 24 8C E0 0F 3F 87 30 42 F8 4F 95 34 7F EC 09 43 B4 AF 92 22 3B 6E 4C EA 86 5D DE 5C 0C 0F E0 71 98 51 E7 58 14 2C BA 1B 47 0F BE B7 60 D3 F5 DC CE B6 93 75 B4 F3 88 BA D0 5C 64 68 54 F2 1D 01 3F 54 FE EB 66 61 86 27 F9 AC 5A 2E 90 8C E3 94 40 3B 53 A4 EE DA 68 6F 84 1E 13 EB BC DE 5C 83 C8 EA 87 D7 88 A5 98 EF 26 9C FA 24 81 28 73 25 B7 25 85 D7 7D DF 4E 96 84 CA 1A C1 65 D0 64 F2 21 F6 A6 82 DA 8D 39 86 55 8E A1 6D 29 EB F2 AD 2F B7 42 97 19 E4 63 3D E3 FB 47 B0 CE 57 94 AD BA 22 9B 55 4B DC 69 83 A7 59 C2 FA 9C 1C FB 3C 99 E3 32 5F 3F A6 94 79 B3 74 14 AA EB 66 78 05 1A 24 7B A4 96 5D A2 3C 9C 34 E2 AE 47 78 F1 30 07 67 4C 3E 85 81 73 74 87 B7 34 F6 AE 8B 5A CC 37 FA 36 CC 5E 85 C5 85 0A BC E4 8F 59 84 BA 54 6F A9 F7 23 05 07 02 24 76 8E 09 3E 72 64 51 D2 80 04 4D F5 F8 AC 70 5D 98 9F 59 42 5C F7 BD 12 DD B9 E7 09 FE 20 6E FC 13 E1 D8 95 05 1B 27 98 12 C0 9B 00 19 18 FF 93 08 90 22 A3 F8 79 76 9A 09 02 26 8C 38 C7 C7 90 3C 72 A2 2B 54 27 70 20 B5 1E 12 9C 1D BC C9 FA 43 CF 83 6C 26 10 0B 31 C8 99 60 1E 54 BB 06 B3 EB 2E C1 BF 53 78 7C BD 44 60 FB E5 A2 8C F8 49 48 76 5A 22 A5 DD B5 01 37 FC 47 89 48 E1 33 AF B0 DE A4 E4 32 F5 55 72 A6 1F A2 30 D4 73 58 EE 8E 77 5D 4C 03 CE EC 59 93 0E A0 05 68 A4 AB A7 AB 06 2B EF B3 26 A7 8F 62 25 24 93 A2 2E 0E F6 84 BD 78 87 42 23 DD 6D B0 6E A2 BF 68 99 20 E5 5D 25 13 B4 F4 D5 09 4A FC FF 84 36 3A 9A 43 7C E5 CF EE A4 C0 2C 4E DE 8B 33 E4 69 F2 5A 3F 06 23 D1 46 DA 6B 1A FD 68 9A 80 F6 79 B7 F0 F4 28 CA 5D C6 26 11 EA C2 0C 5D CD 01 2D 36 71 4F 04 05 3B 52 2D F8 5D 35 71 9F F6 79 C1 1E 5B E6 F6 E0 51 69 4E 11 55 CF A1 C0 27 6D 14 37 CE A7 EA 9E 28 92 4D 15 3E D2 00 9E 4F 90 8E 47 BF 9C AD BE FD 6D 69 EF 73 F9 F1 70 9E A9 DB B5 D6 2A B0 D1 9B 94 98 17 47 D6 3C 95 40 6D D5 70 C6 31 B3 E8 C2 41 93 4E F3 56 55 9C BE 78 5F 36 AD B1 5D CF A0 AD 71 08 73 CA 5C F1 E1 D6 82 18 68 20 D7 50 B2 33 E7 FC 10 11 96 BF BA C5 E2 9D B6 59 FD 10 8F 3D 8F E1 8C 72 6D 7D 8A 80 38 6B BF 7C 3C C7 F7 B8 46 83 01 41 3E F1 5E 11 16 1F 72 63 A7 61 1D 96 A4 BC 19 8B 77 C9 D7 22 F4 65 4D CE DF 13 CD 23 97 4C 31 CC 19 96 3C 8C 60 16 97 90 BD E1 07 FE 76 AB 2C 20 A2 20 BA 93 5A 16 15 3F 2B AE 73 88 B7 EA 74 CD 1B 5C EC 0E 54 5B 4C 93 BB 59 7C AA C2 D5 81 69 25 D5 EF 55 6D A5 E3 7D 85 3E 12 59 57 71 26 22 3D AC 1C AD D9 7A FC AD B7 D1 27 F2 A1 5A 7E 49 BC B5 32 59 6F 88 A5 42 D9 2A 38 EF 07 E4 D3 AD B1 A4 0D D8 78 F5 16 16 8B 5A 49 AD 6B 68 E6 8A 37 D3 62 14 D6 17 8B A3 2F 17 AA CE 8F C1 BD DD D3 15 18 CF 81 D9 14 6B D5 42 AA A0 B4 A4 1B 6C 0B 7E 91 3B 2E 4D 1E 83 9A 23 AA B4 DC 87 31 7D 0F C6 B6 6F 15 DA 1B CB 7F 38 AF FA A2 0F 97 1C EE 8C 3A E7 71 58 2C 2B 3A BB 02 79 05 B9 05 31 68 49 1F E3 2F 10 74 6C 92 E9 C2 75 24 2E 9F 62 79 7D EC A0 D2 4D 5E 8B 1E 65 C0 E0 05 1B 71 F5 4D 4A 51 E6 D1 4B 96 7C 08 C5 9A 82 44 69 FE 84 17 55 DD 5E F4 93 74 11 EA 7D 80 19 25 35 62 6C 39 64 26 F2 B0 B0 5D CB 7B 3F C6 C0 CA 20 EA 2E 5B FE AC 26 F4 51 2B 98 3F B8 09 9F AA D5 11 83 40 00 E6 5F 49 73 3E 5B C6 D9 B7 0A 94 8E 00 E8 F6 AD 24 51 C3 88 01 22 80 E5 53 55 45 02 A3 DE D7 0C 06 B8 3F 35 81 61 78 10 4B E2 7B 28 91 E8 F0 E7 4E 15 22 D2 BA EE A1 F5 55 49 09 2A 5F 58 27 D8 1F 44 94 C1 30 4A 8A 7C 70 31 50 0F E3 F5 2C 53 96 64 F0 34 A7 DA A8 7A 25 B1 6E 23 79 4E 2B A7 B1 53 9B 6B 40 A9 FC 86 57 17 5B 99 C9 11 40 74 5A AF 4E ED EE 7D F3 2A 69 ED 5A 6E 5E 65 A2 04 C7 0C 57 F9 46 CF D5 D0 72 55 8E AA B7 62 96 D1 0A 26 6A EC 97 33 1A 64 1A A7 B9 67 05 6E B6 7B 78 34 3C A4 BA 1F A8 18 C2 C0 22 14 30 40 EF 47 BA 26 55 E9 93 9A 81 D1 B5 E5 78 8A E5 2A 4C 3D 6E 42 D9 53 AD A1 7F 10 AC D3 B9 60 59 6E 26 5F D3 E1 D5 C3 62 E4 F3 3E C4 BD 2B CA D2 28 B6 85 AC 83 76 BB A4 B1 3A AC 10 33 10 40 43 FB FD 49 4F E7 2E C6 DB A7 72 71 17 8A 26 BA EF CB BF B6 F7 9B 45 20 BB 56 02 7A 54 8D A8 4D B2 14 E7 56 86 78 25 F5 15 75 6F 6E 9C CC 5D 95 AC 14 CA AF BB 6A EF 78 91 BD EB C3 16 B9 9E 10 B5 2E E3 8C BF 6B 65 B5 E6 36 B1 9A 74 11 AC D1 E7 EE 78 0B 4C 56 49 E5 68 86 3F 71 1F BE 54 EF FA F4 F4 C8 60 FE C4 75 F8 5D F7 09 1A C0 F9 FD 07 25 FA 67 33 06 FB 25 E1 0F 62 06 0E ED 97 00 D5 2F 01 04 04 B0 7E 6A 21 85 1D D8 5D 8C 93 16 C3 4F F3 8B 03 E2 32 26 BF D2 47 0D 7A 83 F7 D9 1B E5 D8 DA 33 AE F6 CD BD 54 19 CD FA DE 52 00 AE 82 8C EA 63 2F 3A 62 6A 18 BE E2 C4 DD 00 70 E9 00 EE 1A 98 31 42 A7 C8 25 CA B9 D9 F0 BD B7 B0 A7 5F 28 54 E4 49 5B 0A FE 01 05 02 EE 37 DD FE F3 87 F4 6B 61 37 5C D9 A8 11 81 DA A7 2D 6E 2E 95 5F E6 E9 30 A9 94 3C 43 57 E7 18 86 65 0F 12 50 31 B7 F8 21 06 1B 8C 68 AD 4F 7C A7 A7 ED 34 B3 C5 3A 58 6D 62 03 F1 A8 32 70 57 4A 62 D4 B7 65 A8 E5 B7 1E CB 29 31 71 15 90 06 E1 0E 39 07 9D 8C A8 7E 4B AC 9A 3B 13 5B B3 81 08 88 30 C8 8A 69 44 C6 F9 CB ED 20 4B 22 B6 18 8D 96 4D 47 7D 71 61 12 34 16 92 AA 53 2B E7 97 E3 0C FD DB 77 F2 B4 D5 A7 A4 99 46 DF 73 1A E7 D9 8F 85 8B 66 BE A9 42 8E 35 66 4E 5D 6F 2E 4A F9 B1 B6 BE 3C EB B6 51 D7 EA 12 6B D7 E3 97 D4 35 9B 7D 99 AC 83 9C 75 63 7A A3 ED 34 47 A2 71 F2 E2 F9 46 EF D4 7C 1F AA F3 66 C4 D3 F6 E5 C2 B5 5A B2 C7 75 72 3C 2A 9F BA 0A B8 D8 66 C3 CB 8E 55 C0 63 95 EC 97 85 57 22 B9 73 5E 3D 7B 9A 19 82 A4 B9 95 07 09 7E 81 4A 76 BA C6 DD FF E2 65 C9 45 86 48 16 96 D1 E3 1A 14 44 7B DB 4F EB 23 03 6B D0 38 00 00) */;

	internal static readonly RC _85A1F369383DD0F1C93101D993A70BAE2AC462E8411E675E31F6915EFF250B3D/* Not supported: data(00 04 08 0C 10 18 20 30 40) */;

	internal static readonly aA _8747A7D7E8451DC10DF8684BABFBBA1A6D3CCA8046B69584D86B951B0F82630C/* Not supported: data(1F 8B 08 08 EA E4 6C 63 02 00 00 55 90 CD 4E C3 30 10 84 EF 96 FC 0E 7B 84 C7 28 69 68 42 E1 42 8A C4 75 EB AC 62 83 7F A2 B5 8D 94 3E 3D 6B 55 A0 F6 60 C9 5A CD 37 3B B3 47 F4 8C 1B 4C 3B AD 26 8B 71 B1 E8 60 F4 F5 07 0B 32 74 89 E9 13 26 0A CE A4 38 57 53 52 9B 69 F5 5C E3 E2 CE 9E 60 8C 46 B8 14 17 F9 CD 35 17 76 08 BD 48 02 B1 71 09 66 82 DE 53 E1 14 C5 20 6B B5 67 C2 F0 E4 96 7B CB AB 4B 67 31 AC 27 FA 6E 84 B9 22 B9 05 58 DB BA 9C 62 91 60 27 32 36 26 9F 96 4D AB 3E CE 4C 39 C3 80 35 13 C3 EE A0 15 FA 42 A6 6D 5F 6B 21 9E B6 5C 28 10 1C C2 79 D0 EA C3 17 C6 F7 71 EA 6E 4C E0 E1 AF F2 A3 50 F0 5A E6 76 04 8A 17 79 F0 E2 A4 D5 1E E1 28 8A 1B E6 5F 38 C8 FC 62 53 85 41 EA 7F D5 08 6F CE 70 A2 BB F0 4D F9 0B A4 31 C1 00 61 01 00 00) */;

	internal static readonly xb A92B767E8BC70041241B2D1354BCDD4A95B9776DB3236F9EDD6DA504896B0D15/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 42 00 75 56 CD 72 9B 30 10 BE EB 29 74 6B 7B 68 2F 7D 02 82 1D E3 C4 C6 AE C1 71 D3 9B 0C 1B D8 5A 48 1E 81 62 93 A7 EF 02 36 3F 81 66 C6 33 19 B1 FA 76 F7 DB 6F 77 E5 3C 2C 43 3E 97 10 15 46 2B 8C 72 FE 35 48 41 7D D0 EF 1B 77 35 5F 15 31 0B 20 C3 D8 E0 3B B0 75 19 C2 69 60 ED 6A 73 66 87 2B 4A 8C B4 E2 21 44 A9 D2 52 27 65 7B F7 06 C6 D7 80 57 54 FD BB F5 F7 45 AA F3 82 1F B4 7C 63 2B 0C B0 8F 81 90 F3 A5 8A D8 56 5F C0 70 27 2A 28 82 3B EC 96 2C 81 4E 3D 4C 52 FE 88 31 48 2C 4A 1E 0A BC 08 45 36 3F 6A A3 95 D5 01 CA 53 17 83 9B 5A A1 92 3C C5 37 C0 58 F4 A2 65 0F 52 44 27 1E 40 2E 32 18 47 F0 84 D5 35 CB 7F A3 CA 09 27 A1 C3 02 A4 C4 04 54 D1 47 59 AF 36 1B 7F C1 7E 59 11 1B 51 60 24 F8 6A E5 32 47 9D 21 EA 27 CE 7E E3 17 41 8C 68 03 47 A8 DD 66 15 7B B1 8D 0A 6D 6A 3C 4A 81 3D A3 4A 8E 42 9D 26 48 5D 86 3B 50 70 A9 62 EB 92 9B 8B 04 0A 3A 52 FA 5D 14 77 22 6B F3 27 F1 01 86 9C 52 6E 6A E8 8C CC DF 21 2F B2 2A 8F 85 D1 F6 CC 16 15 43 1F A9 B6 7C A5 55 12 0B CD 7D 28 2E DA 9C DA B8 5A 87 8F B6 40 41 80 73 B7 97 1C 5F 0B 73 82 82 39 52 42 62 34 5F 63 64 74 5E E6 05 64 79 CD 86 67 15 5D DA 59 45 CA 81 41 B8 1D 8F EE 77 FA 98 0F 0A F4 27 B5 A9 C0 5B 05 DF 1A 2D 8D 89 79 00 FC 4B B4 D5 B0 6B C8 06 95 BC DB BC 6A 1D A2 EA D2 D8 52 41 AF 48 C9 4E E0 CD E8 38 21 8F FC B5 29 46 42 1F B2 0C 4C 84 42 F2 D0 88 98 8E 07 9C EC 88 03 CA B6 47 48 EB D7 23 AC 13 FD 2A 15 49 82 AC 99 3E B5 8A 76 CE 70 6D D8 82 D1 DD 36 2F AF AE 09 7F 42 95 E1 64 C0 2B 3A 26 72 B1 FF A9 6B E8 51 28 5E 69 15 A5 3E 15 6E 50 08 73 AB 5B 5F 67 0D 15 7F 51 5C AD A2 F3 D8 E6 85 21 32 46 84 B9 46 5F 14 9D C6 D0 03 BF B3 07 6C AD 29 38 2C 52 3A 6B DA 3B E8 B9 3A 58 AF D2 15 28 5F E0 83 E6 F3 EF F7 6B 6D FC 77 8D 7A 78 CA 49 C6 A4 FC 21 17 5D B4 0B AD 63 BC 4E 30 E5 60 A2 FB A1 8D 2D 3C 20 9D F1 67 AD 72 C8 90 07 8D 9F 09 3B 57 44 85 CD 87 5A 5B 61 86 05 10 1F C1 D2 A7 E1 62 F9 01 0D 48 C8 F3 BE A6 7D A1 EA AA EE DD BD DF 3B AF 39 70 22 41 22 AB 09 B3 14 5E DD 22 F9 4D 07 7F 6F 63 A8 C4 74 2A 9E 3F E5 47 79 65 E1 77 6F EE CC 86 ED 3E 1A CA 1E B1 AE ED D4 D8 2E 3F A0 6A 0A F6 0C D9 51 2B C1 7E 61 33 0D C2 D4 AA 18 8C 3E EA 62 E2 DA 9A DA 16 1A 15 F7 06 93 7A C7 58 4C 58 EF 67 15 A9 C0 03 2D 6D 95 5F CB 5A 40 4E 24 42 07 41 2D 43 DA EF 49 7B 98 C6 EB 47 9F 87 71 96 B6 EA DC 2D C9 05 6A A4 CE B4 19 8A CD 0C 38 0B FC 6F FF 3D 2D 03 6F EF 77 88 87 CD 6C BB 09 96 E1 27 D5 ED 15 E6 D4 36 6C EF 2F DD CD 6E DE 97 D7 D7 C0 56 8A 6D 5B D0 B9 6A A5 60 D8 59 C1 E6 65 BE 9B BB CE 27 D4 19 1A 68 36 E4 21 15 D9 59 57 AA 30 93 42 0C 96 DC 13 F2 28 81 58 34 7C 91 1D 3D B6 F1 E7 43 0D B0 00 B3 B3 84 B5 88 52 54 F0 A9 B7 DD 46 56 BC AE 36 8A FE 60 18 14 43 D2 B7 2B AA 6E 37 74 8C BE 88 B2 14 B4 58 32 91 10 48 B3 AC 05 E6 30 32 9E AA D4 23 B9 BF 80 CA 71 B2 CD CE 12 07 EB B0 D5 CB D3 53 D8 8A A8 ED 5E AA 8F CF 17 52 1F 85 EC CF B7 91 CF 6A CF CE 30 3F 71 17 54 61 4D D9 4F 26 D8 BC 6E D8 6C 19 4E 84 83 8F DA 2A AA A3 41 59 75 E9 D9 12 E5 F5 3F 42 95 CC 09 F6 41 6F 29 58 2C C9 49 31 DA 31 CC 73 7C A7 11 9D 62 3B C7 0F 36 3B 12 06 8A 58 D7 EF 2A 6D EA B6 67 21 E4 52 0C 4E B6 35 9C 4D A0 1A EE 24 EE 54 E0 B7 51 AB B3 E0 E7 56 7E 1E 4C 81 C3 AA C7 4B 42 40 10 F3 A0 79 B7 75 E4 2D 73 23 40 36 2F 33 03 A0 0E E2 1D 86 08 CC DF BF 2C 9D 5A 36 DD 5B 81 1A F6 DD 5C 50 FD 77 0C 7B F5 BB CB 56 0A C3 73 9F E1 5A 71 47 2D F1 02 D8 EF 97 07 9B 57 F2 CC D9 B3 26 5C 4E CB 5C 9B 72 A2 0C 73 4B F6 50 7C 1E 9F 81 FB 3A 10 6C AA 49 D7 B6 9E 9A 37 2F 18 09 C9 B6 D4 A7 EE 66 DD 99 86 9A FE 12 3D F6 C7 5E 56 C1 B2 E5 89 B9 BA 6A F4 81 AE C8 ED 60 E4 14 FA 3C F5 74 53 F9 D9 F6 2F D2 BD 37 6D B2 AA AE ED 42 2D 3B A0 07 5D 12 CB 9D C2 3A C4 F6 51 70 00 29 63 12 F0 A4 CA 6D 5D 9F F9 F6 AE 8E 76 49 A4 40 4B 9C 6E CF 44 6A C5 54 BE 68 0A CB 1F 51 09 15 21 51 35 A3 84 0A 38 D5 4F 5F A3 EA 70 BB B5 1F 02 B9 D6 19 17 2A 1E CD D0 7B 52 95 76 E6 51 AA 2F 3D 2F CD 28 DC CE 7F 7F 5F FA 8F 1B F6 8A D5 18 36 A7 5E FA 98 5C 44 13 D7 CD D7 3F C3 E2 DA 2C B0 0C 00 00) */;

	internal static readonly cc B4E03C2EC6582F59F6C196055A1A60A56D84E7346436DA129A4318A32ADCF292/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 30 00 5D 54 CD 72 DA 30 10 BE EF 53 E8 D4 81 43 D3 43 9F C0 35 A4 78 82 29 C5 6E 92 C9 4D 31 5B B3 45 96 1C 49 2E F8 ED FB C9 49 1A 92 61 60 F0 CA 5A 7D 7F AB AC 5C 50 56 16 74 AD C5 37 07 31 7B BA 1E FE 48 0C 03 7D AF 97 B4 D2 DE 4B A0 95 44 8D 45 2A 6C E7 02 7E 23 1B 2A AE 6A 7C A6 07 1F C4 50 E9 AC 33 12 0F D2 A8 92 3B E7 85 03 6A 21 F2 91 AE 3D 73 68 B4 61 35 2B 5D 74 DE 19 3D A7 8D 8E E2 AC 36 B4 59 E6 B4 CB 33 DA E9 31 1E D8 59 CA 9D E5 B3 B6 51 CD 76 AE 39 9E D8 98 39 55 CC 4F B4 B9 DF AA D9 16 20 A5 0F 28 8D 96 7D 6A 5F E3 ED A0 0A 1B A2 1F 3A B6 31 D0 8D B8 B3 68 95 3B DF 3B 3F 9D 43 F7 D2 38 4F 0F 62 5C 4B CB C1 BB C8 CD C1 CA D3 C0 54 26 B6 8F 12 C0 6F 3D 34 9C CE CD EA 4F F5 9C 96 67 D0 CC 62 87 DF AA 2E A5 F1 8E 0D 37 D1 3B 2B 4D A0 B5 8E 51 1A 56 15 77 72 45 9B 7C 47 77 FA 37 7B 55 4D 44 93 2C ED CB D1 C5 B7 92 6A C8 18 B5 A7 5B 09 D2 24 D1 CC 95 CA CB 1F 95 AA 13 0E 07 54 23 55 55 55 D0 74 0E B4 EE 2F 57 76 00 7F 50 EB B8 A7 DB F5 EB 3B F6 DD D6 1B B5 1A AD 9C E9 C7 4D 31 21 6A 9C DD 0F 4D 04 E5 2C AF 97 6B AA 0E DA F7 94 EB A8 CD 18 22 6D B5 D5 C1 D9 04 65 51 53 3E F6 9E 43 A0 05 9C 40 7F B5 76 2D 56 1E B4 37 62 8F 50 DC 60 91 C7 39 FD AA CB 9C EA 03 8A 62 5B 55 A6 48 58 46 C1 75 01 70 F2 EA 9A 5E 69 F3 FE 99 DD EC 36 59 74 9E D3 0A 9E 8E C9 4A B8 75 84 84 13 56 AF 1B C3 EF 5C AA C4 24 E8 AA 8A A8 B4 7C 49 71 EB 1D E2 F4 05 5F 36 EA 56 10 C1 49 C8 DF 80 80 0D B3 4A 18 DE 23 16 80 F5 DC FE 9E BD 3B D3 D6 0C E1 85 D0 1D 07 64 D5 AA 85 B4 02 1D DE 9A 0B A7 F8 34 B4 34 1A 34 30 07 83 C4 69 F5 6A 8A 4A CF 28 BF 21 F3 57 94 F5 FD 04 BC EB 07 74 44 B6 20 D4 99 D2 B3 7E 4A 40 A3 6B 9C 51 4B DB 02 5C A0 2A 2F 90 5F 39 BA 77 19 AD 74 17 06 DB 52 F1 55 2D 38 48 8B D6 70 86 3B BA 49 C4 72 EF 42 E8 9D 20 8D 95 33 43 12 27 50 66 8C 68 DB F0 F3 80 8D 13 E6 5A DB 3D 36 AD F8 64 38 C6 CF 5B DD 1C B5 DF 5F 18 F1 8A FC A2 CF 37 EF 1C 4C 60 A6 0D 9F D0 6C 2F 9A CA 55 A5 96 FF E3 4D 5B F6 BF 9D EF B4 FD 9F F0 3B B1 8F 08 D5 E5 4B 37 FA A4 83 3E 0A 44 61 36 68 2B ED 21 AA 29 9D 54 2F F3 32 DB 61 86 10 FA 6D 5E E6 45 46 EB EF 53 2B 35 FB EE CC 3E 4D 03 2E 01 E7 31 F3 3E 65 19 6D 5D 07 55 EC E8 28 F3 5E 8F CF 8D C2 24 4A 80 20 F8 A3 CD C7 70 5B 0D FF 20 E0 5F 69 A0 F4 B6 CC 3F 23 08 D8 4D 59 E8 B5 97 64 8A FD CB E7 37 AF 7E 0E DA 48 1C 3F F4 D9 48 F7 38 84 CB B8 D5 5E DB 70 92 D8 1C 5E C6 4D 07 35 2B EA 7A 9A 6C DF 31 B0 98 39 06 CA 5A 67 61 0C 6A 1A 37 D9 7D FE A3 9C 8E 00 17 90 71 76 A4 3C 51 D9 71 60 ED D1 2B 2B AB 59 36 20 05 A2 9F F9 CD 29 65 39 04 06 62 34 B9 90 17 3E 61 E7 86 E3 C9 F9 23 0E C7 7D E7 FA 28 CD 9C 1E D8 A6 18 7D 79 C0 DD 5D EF EE 30 81 0C 99 08 06 3F BA C1 DB 8B 64 A6 C4 F0 1E 9A B4 10 8A 16 92 D4 FA 40 7D B2 5F A5 7B 09 44 36 43 07 CC E7 CB 99 FC 07 A1 D0 7D 8A 21 06 00 00) */;

	internal static readonly Uc B5AA1A37E56FAF776CA4D868FD74074723BCB913C381345116CC70F1D9C8EA1B/* Not supported: data(1F 8B 08 08 00 00 00 00 00 00 00 94 59 6D 73 DB 36 0C FE BE BB FD 07 6C BB 5B 9C 2D 89 DF E2 2E 4B D7 6D 7E 51 12 DF 12 DB B3 94 E4 BA AE 97 A3 25 DA D6 2A 4B 9E 44 27 CD 7A DD 6F 1F 00 4A A6 18 DB 69 E7 6B E3 13 F5 E0 01 08 02 20 09 57 BF FB F2 0B C0 4F 3B 0D 56 61 9C C0 44 64 32 00 C7 19 8D 87 57 E0 8E 7A 90 4A 11 C8 14 44 1C C0 43 1A 2A 99 32 FE DF FF F1 61 81 B3 24 85 E4 5E A6 7E 94 F8 EF 64 9A 31 E1 A8 0B 73 91 06 0F 22 95 20 63 35 5F 65 A1 C8 54 F6 E5 17 2C 32 96 CB 24 3B 05 80 B9 52 CB EC B4 5A 9D 85 08 99 1C F9 C9 A2 5A 17 8D 45 B3 8A F6 1D 8E D9 BE C3 5B 63 9B BB 5A 2E 93 54 9D AE E5 A6 49 BA 5A 64 47 F2 7E 26 58 F8 2C 8C 83 51 92 A9 6A B3 D6 6A B6 8E 8F 59 AA 97 C4 42 C9 53 A3 6D 29 1E 97 22 3A 5A C8 EA 22 7C 77 9C AA E6 22 B7 6B E4 9E 42 6F 08 83 A1 07 4E AF EF 81 77 D1 77 E1 AC 7F E9 C0 F5 E0 D2 71 5D 78 3D BC 86 DF 06 C3 5B B8 BD 68 7B FC D4 1E 3B 28 D2 1F 9C 7F C5 0C DD E1 E0 AC 7F 7E 3D 6E 77 50 C8 75 3C 0F DF B8 0C EA 0F E0 6B 77 19 E8 49 E9 39 B9 52 A9 30 9E 65 47 F3 AF 59 0B 59 F1 5D 95 FE 7E 13 C6 7E B4 0A 24 FC 74 1B A6 F2 68 FE 73 79 48 2F A1 35 F8 2C 35 54 AB 50 3C 33 79 20 A7 61 2C E1 EC F6 EE C6 19 43 A3 D6 68 D6 F0 0F E3 CE C2 74 C1 8B 86 0B 9A 85 49 0C F1 6A 31 91 29 54 5E E3 E7 EA AA D7 DB 27 06 04 8E DB 57 30 76 6F 47 90 E9 35 81 49 A8 16 22 7B 97 19 FE 5E 6F DC 82 BB CE 4D A5 B5 CF DC C3 E9 34 A2 F1 45 12 48 0B 75 CC A8 63 8D BA B9 B8 01 3F 89 55 9A 44 16 A8 C9 A0 E6 1A F4 BD DB AE 17 C0 4C 1B 45 51 DD 02 0C 24 48 E5 2C CC D0 0D 25 6B AE C6 35 80 DA FB 5A 8D 09 7A F2 3E F4 25 78 8F 4B F9 12 AE 30 62 C0 0D 67 71 38 0D 7D 11 2B E8 3C 2A 59 96 AC B3 64 7D 53 F2 52 8A 4F 88 BE 60 D1 17 65 51 5E 21 CC 01 9F B2 E6 11 BC 70 21 A1 2B 96 62 12 46 A1 7A B4 F4 D6 49 B8 C3 C2 FD 46 17 D5 CD 84 FF 88 F6 06 B2 20 EB 26 F1 34 9C AD 52 A1 70 B5 2C D9 06 C9 76 59 56 6B 1C A5 89 92 3E E1 38 67 07 37 57 D0 A1 9C CD E0 CD 0F A7 B5 B7 96 70 93 84 7B DB 85 A7 4F 84 EB AD D3 13 5B FA 98 A4 1D 92 DE 6A 26 1C C2 05 BA 9C 0B C5 65 E2 8B 08 FA 31 2E D6 54 20 B0 3F B4 98 4E 90 A9 CE A1 F9 C9 09 37 6A 84 3D 66 6C 37 92 22 85 71 1E 05 F8 B2 D5 00 27 4D D1 70 57 09 B5 CA 90 65 B1 40 F5 65 F9 63 D2 D5 B4 C2 43 83 0D 88 78 08 A4 95 5C AC 26 07 E0 CD 65 BA 10 11 CD 85 9C 52 D6 62 25 DB E8 AA DF 85 DA A4 5E C3 30 FA E9 27 68 32 03 0F 46 EC 81 40 6B 54 18 56 D0 EF 3D 89 67 EB 6D 20 94 30 BC 04 B9 1B 0C D1 AA 56 BD 76 02 90 4B 91 71 F9 2C 9E 60 3D 97 B1 F5 AD 58 78 A8 82 27 17 4B 70 65 9C 25 A9 36 83 53 B4 D8 38 96 62 26 C1 D7 DE CB CA D4 6D 72 FF 8B 6E 51 6D 60 44 C0 76 10 A4 32 CB A0 66 21 EB 84 74 B6 23 EB 06 39 1E B5 81 90 3A 0C A9 BA 59 50 6D 5C 61 17 D7 22 63 97 E1 70 D9 AE A6 E1 40 60 0D 32 1D 06 FC A9 24 D3 69 26 15 3D D5 0E EB 8D 1F F6 A1 42 33 AE 52 D1 A1 3F 8D 7D 8B 8F AD FF D1 E2 AB 6F E7 AB 37 4E 0E 1B AD 56 CE 67 B3 34 88 A5 63 B1 34 B6 B3 34 5A 2F 0E 9B 27 CD AD 2C 4D 62 A9 5B 2C CD ED 2C CD 93 E3 43 5C 75 C3 62 78 50 88 7D D4 28 56 84 9D C9 69 3E A1 14 87 DA E7 F8 08 BE B3 18 D9 4B 27 BB 18 EB BB BD 64 B1 B0 97 DA BB 58 1A 3B BD 64 B3 B0 97 6A BB 58 9A BB BD 04 E6 F3 5D D9 63 5D 24 20 CE 17 A6 D8 68 D6 67 3F 9B 1E B3 43 78 B4 3D 84 47 B7 A3 0E 56 8E 5A BD CE 53 60 58 79 17 82 7E 20 63 85 FB 8F 4C A1 AB F7 43 FC 0E 24 54 26 21 4E E9 87 43 9C 47 C5 68 B5 75 DA C5 A4 37 E8 9A 2D F2 EB 5E 02 71 A2 C0 17 A9 FC 1A 26 BC AF B1 A8 D6 BD 2D D9 9C 76 AF F3 DA 73 78 B6 7B E9 DE 3A 2E 0D E4 76 DC F7 9C 02 B3 F7 B0 67 76 18 AD C1 06 8E DA E7 1A 38 2B 01 B9 04 19 A0 DB 6D 0F 3A D7 2E 68 A5 99 06 BA BE 88 79 C7 9C 94 CA 37 0D 74 2F 87 DD DF 34 D4 67 68 81 02 3E BE 6E 9E 3C D0 4B 1D A7 8D 9E A3 E3 DF 9E 60 99 75 B1 52 32 53 25 68 7F 80 C7 3F 6F 3C BC 24 FA A5 86 8E C2 B8 20 B5 90 C8 E7 78 DA 90 A0 70 14 86 20 2C 11 4F 09 2C 91 3D 01 C4 8B 55 A4 CC 1C EC 30 DB 9B 14 A2 7C 5C 9B 44 58 C0 B5 8B CC 6E BD A9 DC 62 88 72 33 69 17 8B 31 8E 3E 83 60 88 A7 46 5E 1E 26 48 34 C1 90 33 68 53 DA 76 D1 A0 7D E5 18 DD B1 16 1D 88 85 DC D4 82 4A DC FE 70 90 43 EF 35 F4 5C 2A 3C B6 16 87 53 03 F6 1C D7 33 BC 4A 83 3D 54 CD 51 BA 8A 43 9F 8F 0B B6 1B C7 CE 68 38 F6 48 60 CA 02 F9 1E 52 9C C4 42 99 D9 A7 D4 9E E3 39 5D C6 1F 33 9E 07 71 AE B8 6E 71 26 79 A6 F6 E1 D7 08 B4 D6 02 2D 5B C0 DE 9C 2F AE 3B 63 E7 9C 04 E6 79 A0 F9 3E C5 99 DE A5 B7 9F 6B DD FE 1F 25 97 FE 63 FC 94 67 78 16 FE 23 0D FA AC DD F5 86 E3 D7 3A FA F6 0E 35 FA 4C F8 2A 49 1F 81 0C 53 C5 59 23 2B DD 18 10 93 1F 98 30 F9 52 5C 2E DB 04 67 C0 D7 1D 3E 23 9B D1 22 BE ED D1 5E DF 25 30 97 19 33 5A 64 83 3D 7A 5E 64 C8 2F 79 56 AF A6 D3 F0 3D 88 20 90 01 A8 A4 A8 40 9C 29 A9 54 AB 14 83 75 95 A6 18 C5 9C 42 76 BD E2 CC 8A D1 F6 AD 53 B8 B8 B9 73 6F FB 5E F7 02 6D 28 F2 96 68 55 32 9B 45 92 2F 25 18 CC 6E BB 46 34 46 0C EF 20 85 5C 7D 43 8C 5F 6F B1 84 FC BC 4C 30 02 32 CB 01 A3 E1 C0 75 60 EF DB 3D 33 DA BE 74 C6 1E 7B E0 D7 D2 E8 F5 80 AE A0 03 F6 0B 13 EB 33 5B 24 54 99 11 2D BB 6A BB BF D1 99 13 0F 9D F4 7F 5D 19 3B AB AC B2 5F 5C D8 D6 D6 C0 C3 5C C6 6C 72 98 C1 3C 9C CD 4F E1 A4 71 78 D2 3C 80 93 17 87 27 3F D8 53 10 91 4C 55 39 0A 2F DB 37 0E D6 3F D8 FB 7E EF C9 68 CF C1 D1 C3 D2 28 D7 60 C6 56 9F 8E 32 F6 CF 3F F7 2C 65 B4 6A 26 1E ED 62 72 E9 0C CE 3D 74 FE 8B 2F BF F0 E7 22 CD 83 97 2A CA 1B F3 FA ED 4B 9B 6F 93 AA E7 DC F4 BB 4E 71 5F C7 30 6C B0 B7 8A 1C 8A 12 AE 1F BC B4 19 A6 8A DC 48 12 7B 1A 57 C3 5E 9E 93 48 03 CC D4 09 15 60 D3 23 2C 6E 50 BC EF E8 3D C7 90 98 2C 75 3D C3 01 2A 5D 95 37 3C AF 57 7A 07 53 11 65 45 7C 19 CE 69 2A FF 5E C9 D8 E7 22 16 C6 AA D9 B8 53 FA D5 9B B7 F0 0A 3E 40 BD 46 9F 03 38 E6 6F F8 58 B8 E8 3C 4A 26 22 82 7B 91 86 62 12 91 F4 AA 10 0F 1B 7E 97 C9 5F E5 4C B5 B7 2F F5 16 1A E3 B4 44 64 D4 6B A1 13 94 91 12 F7 82 05 1D DB F3 3D 13 25 A0 2C 64 DD 2B 84 C6 18 F1 2C 12 F7 B2 9B AC 62 D5 D5 89 BD 16 CF 9F 8B FE 44 32 65 EC 9A 42 66 90 98 13 C0 16 BE 4B 91 31 59 C1 87 CF 9F 4B 36 49 92 68 ED 0C CB AE 27 C6 19 87 E8 9E 87 2D 69 2C B0 CD D8 21 E6 2F 02 E7 BD F4 57 14 2A B6 18 3B 33 A0 2D 4E 52 F3 0D C2 78 B9 52 45 71 A4 64 9E 48 94 01 C9 D2 32 30 DE 28 32 BF 83 75 55 A6 6F 9A 0D 5A 4F BD 13 16 35 61 92 04 8F 30 E1 F7 9B 72 97 32 9E A9 79 D9 8B C3 95 22 DD A9 25 1E 31 0C D8 98 38 90 EF 39 D2 B8 54 F2 F1 13 32 8C 6F 1F 77 42 BA DD 62 8C E7 CF F0 81 1A 6A 3E D2 28 40 BD 5C 04 5E D2 10 79 A3 38 18 D1 3D 5B 8F E6 B6 91 C7 68 E0 23 D5 E9 1E B2 E7 71 6D B7 0D E8 25 BA 2A 4D 05 B6 5B 72 60 8E 40 AB 38 43 58 FB 07 DA 17 9C C1 81 4E B2 03 18 5E 7B A3 6B 0F 3E 1E D0 5C 4D 9F 6A 0D 3D EB 1C C0 E0 FA F2 F2 00 FA 83 1C 08 39 74 2A 65 30 11 FE 3B C6 52 95 25 5E CE 6C 9B 96 5E 19 5E 9D 96 B4 85 DF F1 D1 90 43 17 AD A3 91 64 5A 59 9B BC 0F D5 8D 31 4C CF 7D 16 BF 4F C2 80 8A CC 6A 89 55 FF 03 8D AC AF 42 AB 25 F8 8C 67 8F E0 0B 2E 4D 95 C2 99 21 AA AA BD C4 AF 9F D6 CA F1 E9 FB EF 99 06 00 68 94 9A 51 25 A5 E1 DB 23 5A A9 03 B0 86 68 55 C8 18 80 8F AC 5F 1F 35 F0 65 C6 9D 9F 58 44 15 6D 6B A9 34 28 C9 21 F3 57 42 5B EA 5C AE 0F EA 02 FF 01 EE 5C 79 53 98 1B A4 13 39 0B 63 A6 30 93 63 01 15 2E 64 B2 52 54 B8 6B 47 B5 3A BA C1 3F 5A 4B A1 09 F4 ED 69 4C 85 4B A2 5E 13 9B 69 B5 34 29 59 16 E6 34 AE E8 5A 38 93 AA 9F 27 36 7B 64 9F 9D 0F 76 9D E0 B5 DB 85 B7 E0 5C 07 5E 3D 95 36 56 D9 17 1C D8 28 94 24 8C 2A 7E 5F 89 58 61 53 11 5D 63 A3 34 BF 25 66 34 98 EB C8 F6 EA 0C 14 4D A5 9A 5E A9 95 FD A5 44 AA 10 90 52 71 E7 FC 56 A9 88 B3 45 98 E9 23 3B 00 9D BA F3 15 EB B4 AF 7B 77 E3 B6 E7 20 41 F1 06 B9 CD 8A 20 B3 0E 09 AE 69 C5 6A 62 44 2C B2 B5 C6 5B 11 2A 8A DB 42 29 B5 A3 29 AC E3 FC 0A 82 6F 42 BD D9 84 FF 08 95 1B F1 30 0F 23 09 95 AF 48 25 86 F3 C7 35 5B 77 2E FD 77 EB 1F 2F 68 F0 9B 70 1A 53 5D BA BB 6B DF 8C EF EE D6 86 F2 5D A7 92 9F C6 70 02 86 95 42 88 48 59 5A 62 6D 9E 1A F7 E0 23 08 20 C4 93 93 97 9A AF 8F 15 61 C6 3F CE 3C 92 8C 2E C5 01 73 B2 92 BC C8 16 25 9A 97 F6 E3 3A CF A3 24 E1 34 7F 3E CD 6C BF F1 86 C1 6B C5 21 38 85 0A CF 4F DC 8B 30 12 93 08 55 98 84 17 69 26 F3 BB 40 1E 54 C6 75 57 09 BA 39 49 4D 5C 72 3C E7 A3 C6 4E AA FD 69 42 17 1B 7B AB CA 38 DF D9 0C 9E 8B AD 8C 4D 28 EC FB 6A 97 81 D6 36 09 AF 74 E1 66 33 21 BF 22 18 9B 37 C0 E4 E2 DC 3D D9 43 A8 FC 39 54 2A 74 AA DC 67 6D B4 22 AC 48 23 4C D3 8D 1B 17 5A B9 C8 4C 17 E4 54 8F B1 16 C2 69 77 F1 67 82 5C EF 72 4D A6 B1 61 13 99 5E 89 C5 C4 C8 4F 52 51 C2 1A 2A D3 4D D9 A4 A2 34 DE 4D 67 17 1A 0A 16 73 26 32 F4 45 0F A6 4C 9E DF 33 76 53 73 10 4C 24 2E 27 D1 17 B4 14 FA FA 82 AC 0C 7F B9 09 63 29 61 06 D4 A2 EB D0 73 CA 8A 29 98 C3 76 C1 6E DA 42 65 66 1A E3 EA BE 9B 72 DD 6C 0B C2 59 A8 44 A4 B7 50 43 6B DA 41 96 C9 61 9C CB ED A0 66 69 D3 22 7A 22 CB 55 79 97 A4 E9 61 68 0D 25 6B 78 BC CC 45 CF CF 2E CE 0E 9E D1 13 1E 7A 7E 76 26 A6 5D 54 16 1B 62 0B E7 93 A1 7C 2E D5 C6 0F 92 86 B8 E8 10 95 69 6F 34 68 37 A9 F9 19 A9 DC 17 E2 56 91 61 E6 76 52 99 16 5F 3F EF 74 49 7B 4E F1 53 A8 0C B6 74 92 0C B9 69 3D 59 EB 91 3D 2C F3 52 BF 43 93 69 39 05 B2 DC 5D 33 D4 A6 4B 65 51 F7 82 F4 B8 C7 22 9F 60 6E 3D CB DC DA CA DC FA 2C 66 BB 7B 65 78 4B 5D 2F AB 78 2C 83 16 0A 3C 1B 57 DC F8 B2 84 FE 23 DF 4A 7A 95 08 82 F0 5F 29 2F 06 64 50 86 A7 C6 83 C3 C1 B8 5C 3C 98 18 8D 89 7A 40 44 25 0E 60 44 34 26 FA DF AD A5 7B BE A9 69 1A 19 F7 C5 83 4F A7 BB D6 AE A5 AB BA 1E DF 7F B3 10 BE 99 90 1A B6 76 13 1C 3A E9 21 E4 D1 A1 69 16 5B B4 2C 9D B6 06 80 D2 35 DB DA A8 03 68 C6 97 91 9E D2 82 2A 93 FE F9 73 2E F1 31 2A CD B9 17 88 C6 15 A1 B0 7B C5 39 B5 96 69 0D AA C6 44 17 2E C5 C4 CC C4 76 8C A1 B6 6C 24 95 D7 16 D4 2D 2D 07 33 45 95 20 49 5C 4A 28 4B C2 DD B2 D2 57 8B 5C 5B 01 40 85 F5 EB A3 91 30 0D 6E 94 0D 2B D5 4E 61 E6 02 90 17 64 75 53 A8 3F 95 B9 7C 7D 63 9B 7C 75 13 71 37 28 B9 94 09 77 1E C7 A3 54 AC AC B3 A3 FC DD D7 2D E0 EE 38 3B D8 77 D1 18 1B 0C 7B B3 96 56 E3 AF 74 40 C4 F4 52 A8 0F 70 9A DD 15 7A ED 5A C8 B5 77 B7 5F 1B DB 89 91 09 5D BB 7A F9 E3 A2 19 4D 74 11 59 71 5E 2F F6 D2 8B 04 4A 5D 0A 42 DA D7 FB FB B5 CA 2A 4B 79 25 80 54 7A 36 04 44 A3 AA 6B 72 2B 2B A6 1A 47 72 17 F6 D8 6A 55 75 F9 25 4F 30 B3 01 74 0A FA 2A 40 64 32 7A A9 BF 78 6F 55 CB A1 95 FD A6 E6 48 DA 42 F0 A2 DE EF 5E 35 EE 8D 9A AC 21 6A D5 95 35 67 62 67 85 A8 EB 50 AA 4E C3 81 D7 4A E0 50 CF B2 C5 F5 72 CD E8 13 19 A5 27 5F C4 EE 82 5F 1C C2 E4 7C 98 89 AF 05 69 94 31 FB C2 9D 58 CF 13 15 9E 49 83 86 4E FC F2 F8 B2 B5 2D AD 53 49 D4 DC C8 65 DE 66 37 B0 4D 0D 97 CF C0 1D 6F 46 23 17 25 2C F0 87 4F 54 45 4A 13 35 1E BC 69 75 81 CA AB 0C B5 D5 25 00 95 4F 65 AA E3 1A 7D 8A 1F A6 0D 16 E1 90 16 52 63 83 6C 0C 4A 04 0C 67 02 E0 A3 7E AA 8C E7 E2 E9 06 DA E0 BF BD AA 6B 29 1E 75 B1 05 13 B5 33 08 FC 17 81 E9 22 D0 2E 14 C0 74 84 80 22 DF 1A 3D EA 06 57 5F A2 04 F9 D7 4F 0E 82 C9 F9 BD 9F D7 FB 65 F7 24 88 D2 F3 83 26 35 00 1C 3E 80 8C 5E 51 8F FD 1F 5E B1 EB E9 16 0F B9 83 C3 AA 5B 6A 76 8C 7B 63 5E 0A 90 15 87 BB 24 4B 4E 86 B9 16 00 74 0C EF C9 7B 5C AA E3 8C BF 74 98 9A 51 79 35 61 4A 6F 68 79 C6 60 46 66 15 59 3F CE 98 13 2A 70 21 0C 7C BB F0 D9 03 A0 CA C1 6E 74 9D FC 46 5F AB F8 FD 6F 79 29 4C E9 1A BA 04 18 D5 C8 E9 66 5E 3E ED 6B DE C7 2D D9 68 A2 EB E0 02 20 F8 0B AB 07 65 B8 F2 87 CB 70 E5 A8 0C A8 B1 4E 17 E0 EC 67 0B 80 FD 72 15 E1 E1 4D C2 FE D2 F6 A3 84 C4 E6 D8 3E C4 E6 A9 6D 8E 0B AD B6 E1 01 C5 E4 FC 12 AE 88 AE DA BB 2D 18 08 28 1B E2 95 0C 3C 30 32 D4 9B 3E A0 F8 90 D2 08 74 C3 65 97 13 C3 0B 51 27 3E B0 B2 10 1E 4C 79 16 16 58 D1 B8 A4 A1 65 F9 82 1F 65 9D 28 4B 96 B9 2B CF 1D C8 03 FF 66 E8 03 C4 3A 34 1E 6C DE F2 74 F3 CB 0D B3 FD 1C A7 60 54 8E AB DB 9B 28 C3 FF 41 0E 06 33 7A 1F 12 51 EC 3B 46 5B A2 4F 9F E8 DC 1B 34 25 DD 62 BF 04 80 AC AA AC 5B 51 24 1C 4D 74 D5 5E 41 C5 9C 79 25 B5 EA 76 0E 92 D5 48 3B E2 2A 27 D3 CB 86 EE 4C 29 FA C3 31 38 F7 7C 24 F6 50 36 58 22 62 44 72 2C 81 C8 95 72 6A 34 A6 B6 14 E8 10 81 96 87 90 21 4E 83 28 1D 84 FD 88 60 29 6F B3 84 B5 73 8E B7 DE 94 3E B7 CE 5A 00 18 43 DA F2 97 C2 68 EA 3D FB F5 F2 A3 58 DD E3 D2 5F CB 0E DC C5 15 B8 A0 B2 88 30 66 07 46 53 E7 F6 AF 57 71 89 8D 91 CE 9F 6F FD 8F D7 18 BE 6C 84 46 6B B0 D0 BF 6F 15 74 F7 9E 4C 7C 86 9F 97 AD C4 B7 7F 3F 52 90 FC 29 41 01 26 AE CD 55 3D 6B 8F AF AC E7 FC 36 22 DD 8C CE 2F 9D DC AC EB 8B FC E9 A2 44 3E 2B 3E 2F DA 0F AB 32 51 A3 66 0A FE 8A CE 50 E4 9B 32 64 86 55 0B 45 19 48 52 0D EA 79 14 B4 8A C7 EA BA 21 34 A2 32 68 12 3E 95 CA 85 7B A1 CF 8A 68 20 33 72 17 21 A4 FD 6C B2 E0 F5 DD 7E 7F 46 C9 F9 D8 DE EC 95 88 D4 C8 0B 71 81 4C C4 9E A9 F4 E3 71 94 C7 70 AC 68 4C 6A 46 46 85 66 33 1A 5C A3 0B 34 D0 05 C4 DB A4 76 03 72 27 1C 5A 9A 46 C6 27 28 E5 D0 02 B5 19 F7 A1 F8 FA 7D B5 C6 9D A5 CD 70 44 C7 6A 02 6D 26 D5 88 8F 1B C3 3E B7 DC 07 1E C4 03 20 63 6D 8A B3 E0 03 5B 15 26 C1 5A 8E 2C CF 4C FC C0 68 0D 61 97 A7 E5 C9 4F BF 3E 06 96 11 59 7A 31 50 11 DB E0 44 5F 92 95 56 EF AC FB 07 3C 89 86 64 7F 01 C6 86 40 27 35 53 3D DF 85 52 93 B1 3C 99 C8 93 30 F3 C6 65 7D 6C 2E 2E F9 81 D4 20 3C 6B 4F DB 0D 1E DC F2 4D 79 4A 14 0A 0C 69 54 D2 D7 37 E7 E5 F4 D9 2D 6F 52 48 9D 47 AC E9 D7 5D 8B 73 99 39 71 14 BC FE 65 25 C3 7C D6 0F 16 0E C3 2B 46 E3 90 BF 60 40 90 2F 1C 6E 21 0C 07 22 DF 77 C6 4C 12 F3 83 B7 B9 8D 96 F1 3B BB 3F B7 5C 79 D1 1D 71 83 3F E6 9D 3B 4F EE E7 5C 20 FD 43 8B 02 02 CC AD 39 13 C0 FB A8 3F FE 7C 69 F4 5D E7 2D 3A EC D7 83 B1 5F BD B3 71 45 CF 8F 7E 77 75 93 03 89 D3 C8 16 E4 EC 7F 24 7F DA C5 13 18 5B 6E 64 BA 82 44 17 D1 0C 0D A4 AA C2 D0 37 0E 16 81 47 DA 03 B8 E8 28 4B 9D E3 5D E0 37 66 06 F3 BA B6 3D BB 61 3C 61 4F C8 26 C6 53 4A 8A A4 4D AB 43 84 4D CC 48 D8 EF 43 A5 C8 B3 B6 79 8A 08 FB 96 85 2A 99 EF 89 A7 7D 6C 6D FA 1B 5A 7E C2 E0 09 A6 53 A6 A6 73 AF 9F E9 DC CB 1F A7 C8 A8 E8 7A 1C A4 47 98 9C E0 EB CD F6 C3 06 72 F5 3B 35 8C 30 F4 08 13 7F 61 B7 16 74 D0 B9 68 C7 D1 FC 33 43 19 3A 93 72 1F CB 36 C3 5D 9B 02 0A F6 A3 32 BF CA 39 98 62 12 54 75 EE D6 69 16 9B 0F F9 44 C6 1F FC 6F AB 9B 43 7C A9 EE C8 96 9C 86 61 BF 12 1E 80 16 CA D0 2E F7 42 61 B8 EF 63 38 1F 18 86 29 24 85 30 A5 29 CD 96 1B BE 1D 49 96 22 DB 8A 9B 96 E3 01 86 81 DD C4 96 65 59 96 65 5D 21 C0 63 CD 3D F1 B5 D3 C7 26 83 24 CB DA B6 11 A9 00 09 59 19 6D AE 26 22 B7 27 E1 C7 D4 AF B1 28 FD F0 03 6F C2 91 3B 76 9C BF E7 EC 38 2D 03 3C 89 F7 9E BF FB EC FE 8B AE 70 4D 9D 80 33 4D 08 B6 8A 77 25 2C 4E 21 4D 59 E8 FE B7 08 7B 03 EE A6 4F AB D9 1E 1A E9 D7 91 94 86 DC 9E A2 E1 00 7F 42 CD A1 A5 A4 76 47 3E FF 9D C3 AB 9C B7 EA 57 12 7A 2A 9E 86 30 0A 65 92 1F 75 26 D5 E9 6A 4E 01 47 51 20 0A 35 A9 E9 42 54 93 B5 92 A5 1F DD B2 42 5F 0D 5A C2 D4 C0 12 89 C4 41 2C EE 06 91 AB A9 F9 FD 10 41 95 7B BF 76 7B 29 62 92 01 04 96 B5 B4 35 59 BA 7D 47 53 CD E9 A1 7F 5D 9F E4 EF 56 B5 1F 3F 1D CB 44 A4 57 18 DA FE D8 0B A1 56 1B 9F B4 E1 58 03 99 7B BF C7 83 07 AE 28 6A 08 97 48 1F 14 AF 6B B6 CF 73 9A 49 26 1D BD 62 B4 15 1B 4A 2B AA F7 AE C1 8A 28 D6 B3 C6 DC A0 31 D0 34 9E 1F A1 DB C4 D8 70 B4 B5 3A AF FD E5 DC 2E 44 47 C3 B8 E3 D1 DC 20 6A 4D 81 73 41 26 A0 91 B8 F4 44 E2 2C 74 AA 42 02 89 07 A6 87 E2 03 A8 B3 49 10 20 B5 57 31 67 32 23 D2 52 6C C7 89 86 F1 34 AA 8A 6F 92 D4 02 CF 34 C5 6D 8D 5B 70 34 70 9D FB 6D B8 53 24 7F EF 3D 44 AC 95 0B 9E 43 DD 37 B3 10 E8 7F 6B 3F 69 64 3D 77 43 C3 09 F7 C0 C1 27 24 4D 08 B7 26 2F FF 15 F8 86 F3 C9 F2 0B 15 17 9A 4D 16 0B A0 BA 30 73 8F A1 EC 07 C7 6A 76 58 18 4B FD AC 69 3E 56 4C 00 10 DA 57 78 B4 B2 0E 73 97 8B 3C BD CB 49 A3 C1 C7 20 D4 B9 A8 0D 5F 62 79 EC E8 3E 36 96 49 1F CD 4E 1E 57 3B 6F EA A2 21 50 EC 1C 54 74 77 09 A9 2D 25 CD F6 D2 CC 06 33 11 F9 38 90 09 25 BA BF 41 D9 8C 6B FD 47 54 77 05 CC B9 C7 9A 1D FB E3 2F C9 3E D3 04 D9 D1 97 55 BA 4A 5C 30 63 9C B5 CA 48 A1 51 5E CC 26 5F 20 25 85 7F 97 B5 91 CE CE 0A F7 23 38 BE 70 48 3A BD 6C 65 22 CD A6 63 92 98 9D 16 39 43 BD FC 07 25 A7 B8 FA 4E F5 3B D8 7E 5B 82 32 E4 EC 00 AC F7 A9 6B DB 90 8B 06 F6 F9 8A 9B FF E1 19 D3 2C 30 DA F7 D3 A7 47 D7 89 E0 CD 83 A3 EF CC C1 60 8E 05 5D 6C EE 62 E4 2A E7 39 55 76 99 7D 79 BA C1 22 EB EF E4 12 06 9C 53 CE 6B 75 38 6E C7 0B 0A AE 87 F5 A5 D0 3C 2E 4B 7D 80 7F 82 67 54 02 EA DF F2 94 79 4B 33 16 7A 33 69 41 E4 03 59 A9 9A D5 51 44 49 0B 20 64 EF 57 68 30 2F 40 F4 CC 66 D5 A7 02 DD 16 90 7B 56 2D B2 6A 51 2C 9B 6C ED 09 BE C4 65 C1 55 C1 07 AB 05 3A 6E 37 66 67 C1 79 3C 26 24 90 DE C1 93 63 8A 30 96 82 D2 4A 0D 9C C9 95 57 45 4D 55 54 A0 50 DB 0A 6B ED 7D 71 72 04 50 91 1C 23 92 0F D5 DC E5 EC 10 CE D0 1E AC FF F4 BB 0A 9E 60 D8 51 76 21 1B 66 BB 59 B7 34 F2 D4 6E 32 53 25 15 EE 47 05 C5 B0 A7 0B 89 C0 DF 7A 51 BC C6 42 33 B9 3B A9 98 B9 D9 E4 97 E6 6D 6A 1C F0 32 3D C1 E3 FA 44 17 8F 49 02 01 21 BF C9 51 45 53 99 C1 AB 20 BE C1 84 96 F0 7A 1E 96 ED E3 50 C2 83 48 FD 32 57 3F 97 35 B1 20 11 8E F8 33 80 25 46 F0 A7 F8 06 20 A6 82 23 14 E2 13 E2 BE 3C 09 70 E5 DE 0B 40 2C 15 26 C4 3A 40 95 1C 44 FA 29 C1 92 F1 1F 21 76 DF 03 D8 7D 4B E1 E3 30 42 35 03 5F 2E 53 DA C4 D7 D4 5C 39 00 DE 0E 07 54 E2 89 FE DD A1 7F 8F 39 ED 54 FB BC CF B3 B1 F6 EC 0D 41 7F E7 79 1C E0 1F D0 DD DB CF 2E F0 6F BB D9 50 AC 25 7C A1 AB 41 47 D5 44 06 61 9D F8 BE 8D 3A BA 2E BE F2 96 C6 01 D2 9F 56 F3 07 1F 55 EC E7 FE B0 82 FD 9F A3 2A 83 AE C1 23 48 12 D6 B8 22 D8 E2 63 4C 04 19 18 D4 4D 04 83 6D 49 9E 29 97 A0 82 25 9D 6E E6 3D 20 60 78 D1 B7 B9 9C 6D 77 16 06 69 34 91 F4 C6 16 99 C1 9B F9 CD F6 9B 39 BD 1B CD 66 EF D0 6C 03 CE 36 DB 29 B9 63 FB C0 BA BD B6 BD A2 04 4A B2 33 55 88 1B 50 5D 37 FA 77 87 FE 6D 67 E7 35 5C AC 50 E9 F1 8B 6C 57 9F 0C 3D 33 E0 AD 22 2F 5E C3 5D 0B 32 9B 77 7A 17 41 F1 7D FC E8 EA 0E FC 3C F9 E8 34 E7 72 32 C7 8A 7D 31 27 A7 D9 CD 6E 07 D9 DC BA 99 78 85 D3 7C 46 A8 61 D7 A3 13 1C 10 0F 2C BD 07 75 64 12 CB 6A ED 73 83 28 AC 6C AC 30 F0 56 87 A1 A1 08 59 D3 E4 CE D2 F2 63 C3 D5 DC 1B 8E 30 97 4C 85 80 75 EB 6A BA 07 C9 80 96 87 99 79 D9 25 64 D9 77 23 7E 0D 45 31 B6 BA FC 69 71 F9 7D 1E 5A 95 95 AF F7 25 AE 80 DD 2C BE 99 E6 14 68 47 C8 FB 2D 8F 15 4B 31 6B 7B 8F 98 FF B7 BD E8 58 65 82 18 B0 7D 3F F6 D7 BE 3E E6 5E 1B DD 53 59 D9 8A 8C B4 30 4E 0A 72 C3 F5 9D 07 05 B2 CB 3A F1 0B 15 FE 84 78 69 CE 37 42 D7 48 E3 20 0B 8F 4B 92 05 65 73 A3 24 51 59 28 13 46 CD FC A9 49 40 5A B6 C3 A5 CD AB 6D 39 8D B1 AD 1C 21 EB 21 41 E1 46 E5 E7 A5 D3 DE B2 77 A9 00 B0 66 45 58 BC 35 2C 45 D2 92 49 8F A3 C4 52 19 77 DD 06 0A 60 7D 1F 53 67 45 46 81 82 1F 40 60 76 2F BB 85 79 DC 24 4F 03 53 35 00 D9 DA 94 66 3A 60 FC 75 BA 85 8C DC 3E F6 31 63 D2 E9 96 B3 3E 18 AB E1 E3 CC 33 9E 7A D6 3B F3 B4 9F 54 F7 D9 B3 56 13 E3 54 AB E5 EB 82 48 47 4C 44 B7 90 FB F7 F0 5F 77 EB 9D 0F 50 80 DF BF 76 4D 1F 4D A7 7D 16 BA 11 C1 9B 03 47 6F AF 9C 20 EF 9C A5 5C 51 87 5A F8 01 3D 27 8F BF D4 02 1F 70 29 85 AB 7D 89 A1 45 87 21 56 33 B4 2C 37 AF CE 49 7B 5F DD 30 FE 12 90 40 3C 9A B4 8A 0D BC 76 0D EC E6 6D 42 21 6B D9 6A 2B 74 4F 90 D4 C3 2B 02 55 FF 61 8A 18 0C BE 79 50 03 07 19 F5 EA DB 75 7C D0 79 53 5B A4 0B 36 56 73 3D 62 D9 AC 5B EB 5A 3D D8 E0 78 3C DE 62 66 48 9C 9A 5D 77 B7 DB 45 B1 90 61 32 2A 48 0A 5B 64 78 E4 D1 45 38 39 00 09 08 C2 E1 F4 D3 C9 1C 58 56 2A 96 22 9F 1D C4 A2 75 07 AD 16 26 C3 8B 96 07 D8 65 3D 2C 92 4A 75 95 3B 7C 27 A2 56 6B 4C FF 0E 55 3D 25 3B 38 8E 3A AD 70 2B DC 79 74 89 6E E8 85 AB 1C 14 59 2A A0 03 00 49 3C 2B E7 9D E7 6C FA CA 2E 64 EB 98 31 CE 74 E4 4F FB B8 4C 8A 61 B6 EA 79 D4 AB 1F 5D 0D 94 91 BA 2E 06 49 D6 F9 7F D6 7A D4 AC 75 6A A9 47 FF 64 59 11 05 7E 5E 3B DD B8 9C BA 3D 5E 92 75 08 47 FF A7 4B 2F BD 46 56 D6 B0 84 A0 D2 DA 29 91 43 DE EB 09 BC F9 E8 6A 4C 24 AA F5 BD 09 AB 42 11 DB 80 F9 ED 0E 28 F0 33 BE 0E 47 26 4D 79 1B 49 12 5B 36 D0 97 2A B1 32 7B 64 74 36 28 AF 84 6F 5C 05 A8 BC 14 47 C4 E3 67 97 1F 66 63 FA 84 C0 E3 67 57 EF 01 1F F1 CF 37 EF 3D D6 5F AE 5E D4 9F 1F 3D BE D8 56 2B 89 7A 64 D3 D9 E4 8D 5B 34 35 38 F7 70 0C BC 89 2A E0 3E 59 9D 0D 10 42 D0 EF 8B C3 3D 24 BB E8 B5 D3 CE 1B FB 79 78 3A E8 7A A7 9A E4 58 10 5D 6E 95 EA 07 57 33 17 4D F3 0A 4C 13 1B 2A EA CC A4 CD 8E 4D 91 43 29 A0 A4 89 28 40 C7 BF 01 D7 4D 82 C8 F3 C6 4B 87 89 0E C7 87 80 C6 45 28 C9 3C 26 9E C2 BD 02 0F 4F C3 AF F7 BC A7 23 0C 0D D4 05 F7 C9 25 18 AA 3C FF 8C 7B 7C 32 05 8A 48 70 08 ED 78 E1 39 DD 97 38 BA 30 9E 84 E8 07 0E E7 9D D0 D7 DC 90 17 06 60 AD 61 0B 4A 4A 8F 6E 62 59 85 44 19 BB 5A FC 39 5F DF 57 9A B1 4E 83 7E 50 BE C4 68 F1 2B 21 15 A0 A4 35 4D 88 68 BB 7A 3B 08 DF 9C 6E DE 8C F8 0D 57 47 D9 0D A5 A2 06 92 A8 46 13 C8 14 32 83 BB DA C7 68 DC 5F A2 FA 09 2A 4D B1 C4 C4 1D 3E AC AB 29 C9 22 34 B9 4B 8D C3 50 FE B0 58 22 C0 F7 28 AA 8B A7 62 45 64 4F DB B0 E5 F0 82 FB A4 C1 2E FE C7 29 25 46 28 65 63 0F B6 77 AE 5E 44 B7 6B 34 23 9C 4E B5 CC D9 A1 81 33 60 17 AC 1A D5 79 16 C6 69 DB E9 83 87 69 6D 96 9C 26 43 9E 77 19 60 91 D4 B5 32 96 66 70 D6 B8 CD E9 2E A6 95 BA F0 54 17 B4 A9 07 C7 D0 E1 63 C1 19 E6 CD C1 F2 DD 36 43 02 31 6E 70 3D 9F 9D 3A AB 6E 00 7C E9 1B 61 DA AC 0E 23 D4 F9 A9 25 C3 B4 C9 40 A6 60 5A C4 7D 04 E6 F9 0E 7C 16 E5 F7 ED 35 23 FB 98 F8 4C 1F 5B 2D 82 0D 78 52 55 AA 80 75 94 F3 6C C9 46 11 C4 0E 67 4D F4 90 02 4A 4D F8 C6 9A 3C F7 8B 35 14 63 E1 03 5C 17 4F 56 84 C9 7D 0E 13 C2 E2 A5 10 8B AB 84 7C 9C 76 18 AA 2E A0 EB 11 E9 00 70 9A 99 95 08 B7 69 40 92 E4 26 0B AF 69 46 87 89 0A 3B AB 1E E3 AB 31 44 81 F2 CD 3C 9B 64 F3 E2 13 A5 D1 B0 6E CB D9 32 5C A4 87 DE 10 BE 6B A2 91 A8 51 A2 38 0F 7F A0 C9 39 5C 7A E5 80 DA FA 05 7A E2 26 01 AC 41 16 39 F5 A8 3B 72 B4 64 44 F9 3A FC F5 A6 62 FB C1 9A A7 E4 66 A1 AD 65 26 51 81 EC CB F0 AB 57 24 9B 53 9C D6 CE 59 5B 07 13 8E 80 52 B4 95 CC 90 DC EA A5 CE BC 35 DF E9 6B B1 AC E0 5D 1A 39 49 72 52 A2 44 6D A3 5B 8D AD CF 2D 77 98 47 FC 44 A6 27 8B AD 90 1B 6E F7 67 10 16 08 EF BB 25 F5 D7 E1 11 14 52 4D 8C 5A C3 AB D6 61 DB BD FD C6 BD 28 7D 91 A8 69 94 CE 1A B6 8A 5A 0C 70 62 CE 06 13 01 1E 38 6C 18 8B 90 F9 02 9A 61 33 92 AD D4 D2 5C 25 C4 AC B8 CE 72 21 6D 4C 81 6B 93 AA 84 0F F1 3D AF 4E 48 C6 A6 CC FA 80 9A 64 17 34 25 6A B7 49 82 3A 9B 2A D6 8B 5D 5E 98 79 DA 04 28 82 1D 6E B6 9A F0 C8 C3 1A DD 5C F2 75 3C 05 A6 A6 DF D9 FE 37 AE F7 72 37 05 9E 63 34 88 35 0D D9 09 2A 93 39 DB 6D DD 50 70 49 D7 E8 D3 43 2A CE 2F 3C C3 96 DC 08 B2 3C 3D 3D 04 E1 7D 2A 09 52 75 11 07 DC 82 95 DF E9 48 BA 18 FB 06 E1 30 E3 67 86 97 F7 2A B0 00 F2 CB 02 59 59 DB 66 47 02 70 9E AF 37 EA 04 9A C0 5A FB 74 5C 9F 5C 85 46 32 E3 38 C2 2B 4E 3F CE 4C CE 20 A4 18 07 D8 7A 16 5E 1D F7 3B BB EE CB A4 71 51 DA 1A EE 42 4D 5C CB D0 93 40 49 D4 B2 F7 73 E0 8D 00 A3 9A CE A6 D6 4B 6A 18 32 C5 94 73 F9 24 44 33 86 42 F2 1E DC 85 16 5E 3D 38 7E 22 0E F7 EE D5 F9 C0 48 6F B2 3A A7 EC 82 B0 74 96 71 A3 4C 70 01 7E F8 70 9A F4 D2 46 69 42 C4 7D EF EA 14 B3 16 EE 15 2E 0E D4 6B BF CC E2 1A FD 59 2A FD 3F 98 AC EA 22 8A 62 C0 5D E7 7F 5E 40 46 DE AC 8E B7 A2 62 DB EE 1B 47 25 BE 19 43 93 27 43 0E 4D F7 79 11 2F B4 AE 05 E2 B9 08 60 76 41 3F EA B1 DB 7C F5 A3 9F FD B0 A5 EF D2 D9 36 59 B6 49 29 72 6E A8 44 54 73 BD EA B4 2A 86 95 88 B6 E6 BA 91 BB 3E 19 A5 B5 12 B1 01 B0 3D 09 0D B4 73 0A 8C C8 A7 DF 39 D9 75 3F FE 0E F9 BA EA C4 47 44 BA 81 EE FC D9 EA CD 9B 22 97 AB DB FC 20 AA 8D EC 9C 87 EB FE 5B 2A 92 FF 16 AF 05 48 EC 01 F7 7C 55 BC C6 5F E9 43 85 F0 0B 14 7D C8 35 C6 11 C4 E0 17 14 1B 1F 4B F4 CE 93 22 17 4B CB BE 10 59 56 51 A8 A9 D1 B4 8A AB A9 7B F8 23 F4 55 F9 3E 4F D5 19 D4 99 E2 E5 6B 0D B2 C0 0D 65 9C 50 D8 44 5D 50 51 3E 16 67 2D C9 AE EF 72 4F 8D 44 8A 75 3B A8 44 86 E9 21 7D 40 D2 7F 2E 34 1F EC D9 CD 7E F2 8F 22 D8 AC CF CA 16 2D 49 3B 62 12 BE 27 9C AD FA C6 A2 1B 85 52 DA BD 16 F5 25 41 EE F0 F8 09 C8 08 13 F3 66 AB 93 94 F5 20 59 8E 20 3C 0C AC 87 FB D3 51 FD 80 86 43 45 D3 8B 82 C1 63 6F 24 52 A2 CB 4B DB 35 74 A5 63 D3 D9 60 7D B4 88 C3 76 1F CF 10 4E 30 5F C9 00 CA F1 F7 40 9A 76 01 EF 76 7F 6B C3 FB 34 8A C9 9C F3 D7 98 24 41 A5 DF E4 95 45 B6 4E F9 88 C4 6A BB 01 7D 92 68 1B 45 F7 C3 E3 26 B0 7F A7 1F 85 3F 80 C4 10 BD A6 A5 E8 BB F9 D0 8E 11 29 D6 FF B4 51 24 B2 A2 9F 8E 5A E7 60 95 34 BE B5 0B 2C 7D 0D 34 11 77 0E 19 AF 9D D5 86 9C E4 64 B6 66 70 3C 03 63 6E 94 39 30 58 17 AE 7A 59 6E 3A AD B1 C9 C3 C8 0A 07 B2 9A 5C 57 64 05 7D 74 79 80 48 96 7B 48 C8 61 46 25 C1 00 1D 6C 03 F1 A1 A0 E8 15 CE 6A 44 CF C9 05 26 84 3F 4C BF C9 97 1F BC 04 A6 12 85 DC 3E 59 D7 18 4F 32 51 08 42 44 D0 6C 56 BE 5A 62 B2 CE 0A 15 F5 53 C8 11 6A AE AC 61 68 57 C7 64 21 78 53 D4 33 FE 02 D8 A3 47 8E 54 D0 B7 E5 94 C2 6D 97 B0 81 F7 F0 E1 C8 78 04 0D C9 D0 3E 38 DA C0 0F 28 32 5E A6 C7 7C 63 9D 7D A9 A7 BC 89 BA 7D 80 C9 91 AC 63 D0 8A 68 93 01 81 D8 07 D9 0F 18 7D AA 41 76 F4 E9 10 E2 41 FA 66 C8 EB A2 C4 CB A9 F5 7E 30 4F B7 57 90 6F BC 8C C2 B1 1A 02 90 D8 6E 64 68 E6 47 F1 FC B4 73 5B 80 AA D9 CF 61 4E 95 76 E5 90 47 5B D9 AC 63 2A 6F 8A 79 B1 2C 5F DB A9 78 49 E4 F7 E7 B3 2F CE BE 5F D6 D4 AD C8 07 E4 5D 93 4D 4D BB 98 A8 48 F6 7F F4 B1 4D 96 F9 AC A8 D1 ED 80 A1 4F F4 29 E6 D4 97 70 64 AE A1 E6 CE 61 C3 B1 97 14 56 D1 0A 1E 92 CD DD 8B 74 22 BD 48 7F 5E AC AE 3B 85 24 EE DC E2 AD 27 CD B1 9F 08 0F 1C D2 BC 9B CA 5E FC 79 6A 9B 45 F7 7E 39 CA C6 AD F1 85 61 F5 3C D7 70 8C 2E 23 1F 20 CE B4 F5 D5 BD FB FD CE 98 3D AD 3A 5B 9B 02 0C B4 0C 31 79 ED 5A 28 6C 79 88 42 EB 18 A0 83 64 1A 8E C2 BC 31 FA 88 40 AD 36 5D 3E CE 63 A3 E2 D4 94 B6 59 57 56 6D A7 C3 40 2D 6A F8 36 50 88 05 42 2B AF 24 79 75 A7 2F DA 24 DB 5F D3 33 30 C7 AF 82 00 00) */;

	internal static readonly X CD8FF59513E7C3110B4980107C854691C1634833DA9347CB411612EFF2B4204D/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 36 00 6D 55 C1 92 DA 30 0C BD FB 2B 7C EA 8D 99 4E FB 05 01 B2 94 2E 01 4A E8 B6 D3 9B 48 44 D0 AC 63 67 6C 87 0D FD FA CA 4E 4A 93 A5 A7 78 6C E9 E9 49 7A 52 B2 DD 4B FA BC DE AE 44 06 57 4B 9D 3C 62 71 D1 46 99 EA 26 D7 BA 10 0B 54 6A D5 52 89 72 E3 4B F1 04 16 4A B8 8D 8C C4 92 E0 A4 CC BF 1B 42 17 1D BF A2 D6 54 88 5D E1 C1 F1 37 33 0A BB F0 60 6C 63 2C 78 2C C5 E7 0D C2 59 6E D1 BF 19 FB EA C4 DC 52 75 F1 32 A3 C2 1A 3D 8E C0 16 1D 6A FE 74 FE 07 5C 51 CE AD 81 F2 04 BA 8C 71 96 E4 1A 05 B7 0D E9 57 F1 2B DB E5 63 4F 3E CE BE 90 52 22 6B 95 A7 8A AA E8 91 D4 54 1B 2D D2 B6 B9 18 A6 F8 48 7D 7E D8 CA FD C5 A0 A6 4E AC 75 4E 8A 0A 10 69 7D 42 2B 17 03 7D 62 80 E4 8A 1D D9 89 FB E4 3D 2D 2E A8 8C 9E DE 95 15 BE 81 8F 48 75 D3 86 43 7E 73 1E 6B 27 7E 06 F2 39 D6 54 18 5D B6 85 37 36 96 7C 95 6E BF E7 EB DD 36 32 CB B0 36 F6 16 11 E5 F6 45 44 6A 46 CF 55 8B 13 1A E2 00 F5 A9 75 7D B6 BA 44 37 2A CA 84 0E 9F 8D 26 77 E7 90 14 97 70 D1 4D 79 70 1C D0 46 66 E6 44 0A E5 10 34 92 63 33 CF C8 13 CC 3D 75 A8 62 4B 63 FC 15 90 53 9C E6 01 1D 82 65 DB 64 CE 7D B1 A0 D1 53 E1 C4 D1 34 0D 68 B9 B7 A4 3D E9 8A 91 22 F0 33 9F 3B 8E F2 37 45 86 F2 58 45 DD DC C9 AE 67 3B B9 04 0F 72 89 57 2A 30 46 DB 2E 73 99 D4 68 A9 80 3E 7C CE 49 73 B7 83 53 64 BA A1 9A 82 F8 76 9A DD 6A 16 51 2F 38 54 58 78 4E 3D 50 4A 38 75 2E DE 0F B2 7C EB 06 98 2C 39 2C 8F 2F 32 DF 2E 04 77 CE DD 5C 68 60 DD B2 43 CC 3A 92 CE CC 95 4A 82 78 FE 0A 57 28 E5 6A 9B E7 7D E3 8C F6 50 E1 B8 0F 2B 6B DA 46 1C 2D 95 A8 07 D9 BB 21 B3 BC 6D 90 85 05 8A 5F C4 AE F1 14 DB 12 81 9E 5A DF 5A 94 61 10 9C FC FE DC 77 81 E6 69 92 C5 E7 35 13 32 16 C3 F1 85 7A E8 7B BD B2 4F 1F 3F 7E 8C 56 BF D0 9A D5 34 C1 15 17 5C E1 54 27 3D 78 03 05 F6 F4 A2 E1 0F 52 A6 20 CF 63 69 AE 70 36 16 E2 2D 3D 1B 98 0E 47 4E DA 8F E0 82 24 BD 0D F2 50 E0 CD 7D EA 7B D2 FB A1 F4 32 C9 39 83 33 69 F2 38 8B 5A 0F 62 DE 87 A5 83 8F 63 BA 6C 35 DE 81 38 81 0A 06 21 4C C5 9B 99 B2 8D 99 2C D2 F5 31 5D 30 6F EB B9 9C 0D 37 A0 AD 59 01 E2 E7 61 B7 4D 8F 13 F6 79 F0 9F E2 04 DD 26 5E 81 4C FF 49 E5 6F 8D 8E BB FD 21 C9 C6 D9 B2 6C B1 32 EF 96 69 54 B5 A2 33 8A 84 27 89 3C 5F 32 B8 F3 21 CB 80 73 17 FB DE BC 8D 37 C4 87 A9 D6 58 A1 6D C5 6E F0 1E 7E 0B 7A 20 36 A5 EE 04 2F 41 57 5C 18 6A 85 0E 55 38 C3 D9 8B 6F 2D 75 EC 12 43 EF D1 16 FC 12 F3 89 AB 76 BD EB 85 5F 80 A6 1A BD 65 D4 5E 80 F9 2C 80 47 76 A0 6F D1 3E F6 0C 2D 4C 6B 08 BA 7C 32 76 18 CC 0D 76 60 65 86 25 41 1C FF F2 A6 71 BC D2 D2 8E 39 E9 0A 23 04 0F C0 EF 8B 69 65 5E 83 F5 F8 3A 2E 39 6F 5D A6 4C 6D 3D 55 DB 71 3F E9 4B D4 7F 18 1F F7 9F 8D 9A 54 E0 51 6E 58 48 45 5F 36 0C 6E A6 46 9E 0B 7D 45 5B 61 7F 3D E3 A4 AB BE 2F A0 33 E8 1E 15 B8 30 DA 9B D6 4E 63 C4 97 E3 1B 69 DE E7 F7 8E F6 33 38 10 9B 2D 18 C4 3E FE 69 AD 67 DB 69 62 5F F3 85 5C 2F 16 32 23 C5 E5 B4 82 7F 4B DE 3C 81 F3 72 A5 CC 09 D4 30 F4 DA 2C C9 BD 4E 3C B3 D6 15 0A 7B 29 89 54 73 5A B7 7E 8A A3 F9 99 4A 24 E6 DF 34 68 57 E0 F1 BD C2 BE 18 15 0A FF 98 58 76 73 E1 97 86 DA B7 B6 E7 FD B4 5E A6 9B F5 4F 71 C0 52 2E A9 22 CF AC 16 A4 B1 06 B1 44 ED 4E E4 C7 BF 1F AE 71 DD 30 89 CC 70 49 C4 DE 9A 2B 55 0C 16 F6 20 94 8F F1 FE 00 4E A6 21 87 9E 08 00 00) */;

	internal static readonly cC D31E857A99829708A88D6A92C9734D59D0F32BE1BE84F07DB63F396A7900DCEA/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 41 00 7D 56 CB 76 E2 30 0C DD FB 2B BC 9A 69 17 33 FF 10 DE 94 E7 10 3A 2D EC 44 10 44 87 C4 E6 38 76 4B F8 FA 91 43 29 4E C9 74 D1 92 38 8A 74 7D EF 95 9C 9E 46 B3 95 4B 4C 52 A5 33 BD 2F 65 5B CB B1 DD 8A 11 A9 7D 7C C4 44 B4 F5 16 0A 3A CA 7E BE 19 88 78 2C C7 A4 0E D7 A0 38 45 75 E6 3F 39 C2 9D FB 9A 84 72 B2 18 C4 AC E3 A5 EC 66 98 58 A3 15 25 45 10 2E 46 A5 3E 38 AB 83 C7 72 A8 12 F1 02 C6 90 36 61 E4 72 31 9C 46 93 61 5B 4E B4 25 AD B8 90 E2 17 B2 0A 9D FC C1 B7 62 4E 27 CC 3A 54 1C 33 28 7D 96 1B 80 9E B3 04 4A F2 33 6B 28 B1 B2 A5 E5 CA E1 16 AA B2 62 41 49 6A F1 20 E7 FA 1D CD ED A5 31 81 5A A2 DA 87 D0 AF DB 8F 86 4B 06 82 B9 36 A5 78 9E 0E AF D7 01 DE 3B A2 06 0E 76 3E 5B 9C 10 AA 04 C3 AD B5 5F 27 4B D1 77 A0 F6 E7 54 3B F9 4A AA 24 39 E0 60 4E 92 1F 9D 45 26 C2 C0 96 78 A1 47 26 17 31 E4 1B 98 EA 37 90 71 59 58 CC 0B F1 F7 57 BF 3B 15 4F 2E 3F 5E 23 45 94 1F D1 E0 47 06 5E 08 E4 48 B5 DA 17 29 35 A2 0D 63 2A D2 5A FA EC AF 6B 52 18 FA E5 6F 99 64 C6 A6 C0 EB 01 99 88 29 A3 44 AB CB EA DE 00 7B E0 0A 90 F3 9B A3 36 55 64 40 09 E7 ED 30 B4 76 8A FE AD 9D 36 F9 25 62 9E E1 C9 6A 1F 90 F9 BD 14 9F 96 8A 26 B1 7C 78 22 E0 35 27 A3 ED 1B 30 93 DB 2B F9 31 E6 F4 28 5E 5C CA A8 9F C8 43 26 F8 40 C3 B0 14 8B 88 17 BF 7B B3 44 27 82 7B D5 44 3B A5 63 81 36 58 BA 82 B8 B7 35 EB C4 2C 6E E1 3B 1E DB A9 83 8A EC 1D D6 23 03 BD 27 04 7A C5 77 4F 14 3E 8F 3A 7F A3 69 DD C6 7F 08 54 0A 24 5F 90 0A BE DF 6F 21 F0 A6 57 3A E7 9D 5E 24 07 55 7A 20 41 95 EA 8A 31 F3 73 FF EE A7 4F 62 0B 66 01 79 5D CB 70 1F 72 FD B1 59 7F 33 70 97 4D 5C 03 9E 33 6B E0 42 63 85 75 8A EF FC 08 0A 9B 91 42 D9 CF F4 86 93 7D 78 65 EB B8 FD 3C 45 EC 14 75 40 23 3A 04 B9 56 5B 31 7F 8E 47 C3 F1 38 00 3B 00 CD 12 82 5C 61 9D 5C 31 A1 6A 13 EA 53 8D 16 18 DC 69 6D E5 14 ED BB 36 87 42 B4 E8 9D 54 65 86 44 73 CD C4 6A 23 1F 06 A3 C7 4F C8 1D 39 9C F6 66 35 4B 2E 0D 1E C2 4A 0F F1 A3 9C 2F BB 55 FC 2B A3 F4 83 8E 2B 6F 32 38 E3 77 72 8F 35 57 6D 32 C4 D4 25 19 D2 47 3B 34 3C BF B8 F6 D5 79 B6 41 05 B2 06 B1 B7 32 43 B5 87 44 E7 F5 4D 56 79 D6 DA 42 F8 4E B5 D8 D3 27 2F 47 30 0F C1 40 41 D0 34 8B BA 3B 52 74 AA B4 F4 5A 4F 81 A9 F4 C8 08 54 03 EC 3E F7 86 E5 D5 A6 19 19 A7 AC A6 B7 EB 80 5F 5D F8 11 32 A1 C4 68 AC 45 7E 99 38 FB CA A4 2B A7 52 BC 19 34 07 63 65 9C 92 C2 87 3F BC D0 01 FD 78 97 4A 2C 53 34 39 64 16 0E DF 0A 34 FB B9 02 8F 04 28 C3 AF 34 3D CF 27 DD 89 E0 8E 55 DC 61 59 F6 DD 0C 88 58 46 03 72 0C 9B E2 72 5E 91 62 1D DE A1 14 D5 50 B2 41 87 B4 AB 4E 75 B2 C7 3F 09 50 B3 B4 1E 3E 4F 5A E4 1A D7 F6 15 AD 0C E8 8C 55 FA 85 EF 13 5F F2 BF CE 99 82 56 CC 5F 0E F4 FD 19 44 15 91 FB E5 97 89 BE D2 CE 64 A5 56 A2 07 1B D7 68 B9 15 1D B0 1A 3A 4D E6 9E 2D 7E 55 D4 A1 79 D3 E7 EB 6A 8B 2C C3 09 5C 43 58 91 F5 9B 8B 24 B8 D1 FA 50 9F 6E 63 7F 2A 95 C5 BD 95 C2 29 16 53 89 FF EB 0E 96 D4 EC BD 02 90 7B 25 E6 EB FE 2D B9 EF DE CB 01 1F E6 0F 12 AF 09 74 CF 79 D3 29 AF F4 1D 75 3C A1 06 B3 65 B4 A8 F7 9C 98 BB AC C0 05 90 AA 21 41 3C D4 B4 BA 3F 3B 66 71 7B 36 0B 27 69 C0 F4 F7 23 E6 89 58 07 A7 9B 26 BF E8 C3 79 0B 55 DF BE 60 93 11 BA C5 11 0D 28 AB 6B A2 88 A7 CB 6C DF 87 39 1F AE 05 1F EF A9 48 89 BD 9A 39 C5 38 6A 7E BB B5 FC 82 CB AF 1D D4 4E F5 2A 50 F4 0C 70 77 EB 1D F3 33 1C C6 62 C4 74 33 FB 2D 57 C8 38 12 51 82 46 44 C6 E6 8D 33 B2 5F 58 30 75 FE 6F D8 48 61 87 8A 43 70 F2 0E A6 0D 39 9E 15 23 F1 87 7C 73 9E 25 A8 0D 14 45 7A 4B B3 24 50 A5 F3 AC 97 CE 6F C7 66 72 CC AC 15 D6 DB 67 D2 E6 EB 8F 4F D6 AE 36 78 AA 1D 2A 91 A9 ED E2 81 CB 2A F8 A4 73 8C 27 30 E1 E7 32 CF 36 B4 F4 47 F6 8D 76 47 79 CC 38 E5 09 8A 04 D5 56 0C 7C BB 8E FC BF 01 A3 E0 DC A1 89 EF F4 69 79 11 38 B8 29 68 C2 5D 29 5F C8 60 86 45 71 99 F2 8C BA 47 6F C8 84 24 CE 90 BD 7D 3A AF 2B BB B9 94 E9 99 1B 4C A8 E0 C2 CD 96 5D 20 92 52 FA 9E EF 7F 9A D3 C2 69 64 0C 00 00) */;

	internal static readonly int sB/* Not supported: data(62 68 6A 60) */;

	internal static readonly rc E5F958B792F049DF5D2A7D5067A17AF948C7CFE2089357F80DAC771F0E3069F3/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 43 00 85 56 CB 76 9B 30 10 DD EB 2B B4 EA E3 9C 36 9B AE BA 24 18 DB B4 7E 15 9C C6 F1 4E 81 09 4C 0D 92 2B A1 3A F4 EB 3B 82 62 83 43 DA 45 72 00 69 1E BA F7 EA 8E 6F 01 7F A0 CC F8 5C 88 D3 C1 3D 6C 21 C9 A5 2A 54 56 73 5F F1 45 95 B2 F5 11 24 9F DF F3 99 56 F6 C8 BE CC 43 DF 67 32 51 29 68 EE CD D8 36 47 79 70 41 3C 94 4F 4A 97 A2 42 25 87 59 58 9C 83 FC 4D 7F DC CF F1 99 8A D0 F3 48 9D 5B 14 8A 47 A2 1C 59 3A 27 F8 2A F0 77 6E 55 6D 81 07 05 24 95 56 12 13 F3 62 DB 83 CF E3 4A 69 91 8D D4 F9 7F 37 EC 9E 8E C4 63 28 31 51 32 B5 09 65 E2 EF BA A8 F7 5D 1A 2F 8C B7 EB 88 6D 44 51 0A EE 03 8A 26 62 02 06 33 C9 82 25 5F 62 A2 15 9C 9B FC B8 14 1A 25 8F BD 4B FD A5 92 42 13 70 4B 28 95 AE 7B 3D B0 08 0A 14 32 81 6E 29 94 09 FB 42 99 0D 0B CC 51 83 31 F8 C4 E3 DA 54 50 1A D7 99 90 59 2E F0 FD 8B 03 C6 44 06 8F 4B A1 AB 11 18 56 60 29 7B F7 B6 C0 27 28 94 CC 58 27 88 35 56 14 32 12 D7 C8 00 F8 62 B2 F0 99 3B 71 5A 4B 41 48 99 FE D6 18 F4 2F 4C C0 F0 78 71 C7 CC 49 E9 F4 11 8B 82 3D 84 51 B0 EA B1 84 F2 19 C5 A8 E6 36 EA 04 DF 7B 14 8F 6C 59 04 EB 28 F4 03 76 2F EA 92 BE 50 37 DF 41 56 42 8A 16 F9 0E 1F 36 87 27 40 3E B3 54 88 D8 BE A6 C5 BC 04 4D 29 09 9A 87 C4 BC A9 34 8A A2 DB 31 57 1A 7F 93 B6 23 F5 A8 2A 8A 64 5B 4A 09 A5 BB 04 53 5B 59 0D 1B 51 F5 01 1B 6A 86 45 3E 11 9E DA 02 D8 16 4B 20 70 A9 44 05 5A 36 17 46 14 0D C7 A1 BF F4 76 97 14 08 6D 7B 58 12 19 74 E2 9A F0 1A AE 52 5F 2E 37 9D CE E9 58 59 BE 15 68 EC 51 C8 03 D0 F7 F2 68 A9 02 0F 7E 5A 3C 96 04 0E F3 41 3B 41 1D 73 4C 9A 7A B7 78 42 F9 FA 55 39 8B 21 88 EF C3 D5 DF 84 43 93 60 F7 30 55 3A 81 17 30 4E 45 4B EE 2B B6 C0 EE 24 1A 95 B0 07 4A E7 E7 96 CD EE BC 95 7F B7 62 E1 C6 8B D7 2B E6 D5 42 F3 85 78 34 CC 2B 05 81 7E C9 BB 73 A2 91 26 B7 D7 26 33 13 85 42 D3 1C EB 8E D4 46 88 B9 E7 4B E0 37 2B 5C 64 F6 9A A4 79 B4 DF F5 D7 16 DB 89 C7 1E 54 55 89 64 40 EA 2E 7C EB AD 1C A3 EA 08 9A 8E D5 6F 2E B2 18 81 7C B5 42 5C 09 3D B2 18 DD 7B FC DD 5C 51 6B 5F E9 DF FB 36 00 24 98 9A 08 56 D9 5F AE B6 9F CE CA 73 EF 37 44 9E 32 94 B0 55 B4 BB AC 57 82 F2 89 E8 9E AC 77 A5 DB 37 52 BE F3 0F FE 40 A8 5D 54 33 44 77 DF 78 E5 5F 53 24 4F 54 A4 99 2E 81 9D 2A 2B C9 10 31 53 7C 22 2A 67 84 89 D5 58 D5 C3 1C 37 CD E6 9B D9 4E 69 18 A8 F8 D2 E4 46 8B 14 94 6C 0E 52 14 98 81 EC 3B 57 E3 0A 24 8B 38 64 9B 28 5C 06 97 B8 2F B6 6E A5 26 D5 2F 3A FE 2F E8 47 F9 41 F4 7D CD 62 0C 64 86 12 AE 7A FA E0 9A BA 39 0B 9D 54 99 35 54 6D 0D BD 56 20 B3 16 DE D6 4C D8 AD 16 28 8D 50 7C 56 3E CE 99 AF 21 1D 00 DA B0 3C C4 F4 16 F5 A8 20 56 36 29 C0 0E 87 CC F9 3C AD 5B 91 C6 D5 D8 94 DB E7 8A 16 D1 55 18 21 D3 36 36 B0 74 7D E6 28 FB 63 F2 EF 96 9B 9E 5B 44 68 CC B8 CB F6 A6 64 A1 6C 7A A1 B4 33 0B E7 68 EB 19 DB 80 4E 80 00 67 F0 F1 08 C2 B0 A9 16 56 E6 EA C9 39 E8 66 19 5F D2 4C 04 E9 A7 A0 C2 D7 F5 5C 26 EF 51 24 D6 70 4A 86 C7 1C B4 28 E8 59 23 51 09 67 F3 5B 2F D6 F5 C0 77 6C 2E 48 30 6F E2 21 86 67 1C B0 D2 D6 F4 03 3C 39 B7 4E DE 52 82 EE 4E 71 BE 81 2A C9 81 18 D6 7D BC A9 C4 33 52 4F 95 78 AC AB A1 62 BB B8 E6 42 B7 33 9A 79 19 16 DD C0 1E 21 7C 19 7C 09 7C 57 E8 90 E4 78 1C E3 75 42 B4 66 56 B4 EC 97 82 C3 C7 44 95 25 F4 CC 35 12 B5 51 92 CF F1 A3 CB 4F F3 65 FF FE 8C CF 32 5C 45 41 3C E8 B2 55 E9 1C 4B F1 3C F8 3E F4 45 FF 84 0E 92 91 96 E9 4B 89 B5 EA 79 88 CD 2D 26 76 4C CF DF 31 39 5C E1 D7 5D F9 1B 56 A8 53 14 C6 3E 0B 76 C1 CC 71 3A DD F7 EE EE 67 EE E7 E2 48 B1 D7 08 33 2F 4D 0B 94 87 06 65 73 12 35 89 4D 1A 21 D3 F3 6C 6F 7D D0 43 3D 41 73 E8 B5 79 04 48 4B 45 43 E0 FA 87 DD 26 D8 3F 5C C6 18 0B 9E 2B 0D 25 F0 D6 19 40 3B 07 88 55 61 9D 81 76 28 B5 3F 1B FE 65 9B FB 5C 28 DA F2 42 87 6C 67 30 CB AB 66 8C 35 11 F3 EE DA CD F1 60 C6 66 2E 9B 90 EB 0D 60 68 EF 6A EA 66 83 43 61 8A BF AE 42 FE 00 FA 5C FE E3 C0 0B 00 00) */;

	internal static readonly Wb E8B44B9FBDE62220289FDC5B28ECB89DB18E061087FB99CE13C45E7C6B8D1E2C/* Not supported: data(1F 8B 08 08 CD C7 2E 63 02 00 34 00 65 55 CB 76 D3 30 10 DD EB 2B B4 03 16 81 05 5F E0 3A 69 08 8D 9B 10 85 16 D8 4D E5 A9 3D 27 7A F8 48 72 1B F3 F5 8C ED 14 1C B3 C9 71 E4 19 CD 9D 7B EF 8C 8F 8B 43 56 C8 8D D3 3E 34 3E 40 C2 52 6C 9C F3 2F A4 A3 7C A4 80 06 63 14 47 3C F1 51 1B C5 1D 76 AB 0E 65 EE AD 6D 1D 69 48 E4 5D 14 87 96 D3 AD 3C A2 AE 9D 37 BE 22 E4 33 2C D5 2B 25 5D 8B A5 4F 1A 62 12 8A 0C 69 EF 64 E1 5B 97 80 F8 01 AD 0F 1D 9F 57 8E E0 3A 7B 4F 67 B2 62 0D 06 7E 43 90 F7 98 5E 7D 38 45 F1 58 53 42 B9 32 A8 53 F0 5C 5F 2E 31 72 36 C7 43 0A E4 93 54 9A D0 25 7A 26 2D EE D1 C3 99 18 56 7E E9 8C A1 8A CF 3F E5 DE BF 62 F8 57 8D EB 6B 30 E8 65 5E 53 23 F6 3E F5 F9 70 89 52 5D 4C 68 A3 C8 17 55 DB C5 6B 96 96 54 51 02 33 E3 62 72 71 1F FE D6 F4 E2 06 22 96 D3 AA B7 AD D1 A1 B5 B2 20 1D 7C 1C EB 70 F9 48 89 5E 3C A7 3E FB 60 21 91 06 B9 4D A5 F8 B1 D9 25 CE 9D F6 C2 C1 81 EB EF 0D 74 18 C4 AF 9A 3A 72 95 54 FE 39 BD 42 40 66 24 9C 30 3C 50 E4 D0 01 C9 BE F6 0E CF F2 26 78 28 9F C0 95 42 9D BA 81 55 CE 31 ED 28 E4 CA 31 AF 0D E9 B9 C0 9B 77 BD 45 12 1A 43 15 F3 73 51 6E 40 F6 0B 1D A3 97 D9 27 25 B6 C8 2F 4B 79 69 79 C0 CA E4 D2 82 AB 9C E5 DA 3E 7D 11 AA 50 B9 7C AF 12 97 87 50 5E F5 FE 81 1D E3 30 42 9C C8 CB 2E 62 FD C7 30 9C 9C EE EB 11 B9 28 B0 24 60 73 8A 7B A6 58 33 B2 28 F7 C1 97 AD 1E 61 7F 57 B2 E0 7F 06 82 78 24 86 5F 85 91 CD 02 52 1D 13 9F 2A FE 61 98 28 76 8D 69 E3 B5 05 0B 72 65 6C 90 95 FE DA C6 24 7F B2 6B AB 9E 97 A6 4D 18 18 18 57 9E D3 B4 CB 7F 89 95 6D 7F 73 CE 76 B7 DE E4 6C 4F 9E 23 64 FE 5C 53 D3 95 78 DF 5A 38 E1 75 C1 07 3A A3 11 BD 18 B1 6F E9 CE BB 2A 3E 61 A8 64 01 81 12 59 14 B7 10 A0 84 6E 6A A3 CC 24 6A ED D0 D5 86 85 48 28 B2 43 C1 7F 47 7F 8E F2 8B 07 60 53 5D 17 5B B9 92 45 4A 69 90 35 B0 5C 8E E9 9D CD 20 06 0E B1 E2 06 DE 88 D8 E2 23 FD 9E 37 9D EF BF 4F 01 1D C0 02 F7 31 3D 5A AA BD 5C 07 DF 36 22 3B 53 9C E7 B3 6B 40 77 57 AA E7 35 3F 24 26 63 98 42 CD 83 29 15 DA DE 54 BD B4 3E 88 C2 3F 91 E1 45 34 EB E9 AC D1 CC 22 B3 C5 32 3B 66 53 34 0F 14 52 0B 86 E9 E1 05 F3 51 9D C8 98 9E 04 D3 4B E2 12 FC 93 F8 27 A1 29 FF 37 5F F6 3C DB 55 77 9B FB F5 CD EE C7 A4 06 DF 31 48 90 E3 0B 08 52 C9 4F 56 58 56 BE 80 D3 38 1A 93 B3 C7 19 E0 A9 65 0F A2 5B 5C 86 47 AC 3D FC D7 CA 21 1F D5 72 03 73 C0 5E 21 6B 13 6A 51 60 02 E7 09 FA F5 2C 55 0A 08 56 6C FD 6B 18 0A AD A6 D8 4B B2 3E 72 DE 1A 67 97 DF 32 9C F8 9F B4 01 4B E4 3B DE F6 E0 38 D2 EB 7E 4E 29 8E CC 0C E2 F0 7A 61 5A 6B 70 30 3C 3E 6E D4 35 43 6B 48 98 F3 EB 6A 26 D8 17 AA 6A 9E 11 17 29 75 6F B3 22 33 25 54 E7 7C 13 3B 4E A4 0A 02 58 76 2B AB 05 D7 90 87 52 39 BA D4 86 6E C4 32 9C 6C 34 86 59 E4 B8 26 06 FA 07 02 AB 61 81 FF 5D EE BB 77 F7 48 E6 6D 71 F4 38 D0 F8 C6 F2 CD 42 B5 4D 40 CB A8 7D 33 95 97 B5 1D 75 5B 52 6C 0C 74 57 93 7D 44 B0 A3 DD 2F 1F 00 87 29 5D 47 F8 58 D3 13 CC B2 5C EC A5 07 4E 38 DC 4E FD 7A 03 DA EB F1 AA 02 B6 32 07 8B 61 E6 C0 63 ED 6D F4 4E AA 5C 64 14 2A 3F F9 60 52 04 0A 03 60 D5 53 78 44 23 B2 70 82 92 5D D0 1B 1D 13 CA CD 51 56 BC 9C 7B D3 DE AD C5 AA 2D 21 CC BF 62 B7 5E B3 3B 56 AE EE 1D D5 33 13 C5 8F AE 67 F1 FC 07 96 66 5F A1 3F 08 00 00) */;

	internal static readonly KC F4938BA045AE9AEE5D5EAC9592A12B1A2446BAA0C3EBE637E13E6F912EE07115/* Not supported: data(1F 8B 08 08 00 00 00 00 00 00 00 95 92 CD 6A DB 40 14 85 F7 81 BC C3 C1 5D B4 35 B5 65 C7 76 21 86 2C FC 4B 36 69 84 9D B8 74 15 AE 47 57 D2 60 69 46 CC 8F 43 36 7D F6 4A 13 3B C2 5D 14 7A 17 03 1A DD EF 9C A3 83 A2 EE F5 15 EA 99 99 C4 4B A5 B1 27 CB 09 56 AB 78 F3 F8 80 6D BC 84 61 4A D8 80 54 82 57 23 1D 9B B0 FF FB 3F 26 00 6B 6D A0 8F 6C 44 A1 C5 81 8D 0D 82 F1 02 39 99 E4 95 0C 83 95 CB BD 95 64 9D BD BE 0A C8 86 2B 6D A7 00 72 E7 2A 3B 8D A2 4C D6 2B FB BE D0 65 34 A4 9B 72 14 D5 F9 7A 9B 90 AF F7 B3 CD B6 F5 55 A5 8D 9B 7E 70 A9 36 BE B4 7D 3E 66 14 E0 B5 54 49 AC AD 8B 46 83 C9 68 32 1E 07 6A A9 15 39 9E B6 6E 15 BD 55 54 F4 4B 8E 4A 79 18 1B 37 2A 4F B9 7E 69 0F 41 0A 9C 48 87 23 15 9E 2D A4 82 CB A5 45 2A 0B 06 09 A1 4D 22 55 06 A7 F1 A6 BD 81 AD 58 C8 54 8A F6 73 85 56 A9 CC BC 21 27 B5 6A 84 BB 51 73 46 5D F4 EE B0 D0 65 E9 95 14 E1 25 2C 3B 57 8B 59 DC F5 D0 6C 7D 4A 38 95 8A 11 3F 6E 9E 70 9A 2D 1B 49 05 10 45 97 B0 45 5C 57 F1 0D 35 42 BE 70 90 16 9D F7 DD 4E 1F 8B 9C 54 C6 70 FA 7C F7 BC 9D 77 50 B7 05 55 C3 47 46 FD 7C FE 31 6C BF 35 9E CF 9E 97 2F 9B D9 D3 0A C0 70 38 B9 19 0C 1A E3 73 86 A6 7B EC C9 27 30 E4 B8 B5 0E 4D 85 00 EF 4C 1D E0 C1 5B 87 92 9C C8 51 19 9D 19 2A 3F 5B D8 20 D3 2A F4 DB 5E 62 A9 EC A9 B9 BF CB B8 DF BD AC 7E E0 34 B7 CD 11 32 DD CB 2C C7 4E 17 8E 32 C6 97 DB DD 57 38 9D 65 05 A3 92 EA 02 5E CF CF F0 F7 7F C0 29 73 B2 27 71 B8 C4 B7 B3 E1 87 F9 6C 78 C6 9B 6B B4 6E 7F 00 55 E8 39 6C 69 03 00 00) */;

	internal static readonly long F576435A0FC9B4DAB8314C9CE853F08B0C9A64BC9B8D8D0661E02A658DDC2971/* Not supported: data(00 00 00 01 00 02 00 04) */;
}
