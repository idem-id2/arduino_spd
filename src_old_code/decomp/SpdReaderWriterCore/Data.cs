using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace SpdReaderWriterCore
{
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
}
