using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SpdReaderWriterCore
{
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
}
