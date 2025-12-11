using System;
using System.Runtime.CompilerServices;

namespace SpdReaderWriterCore
{
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
}
