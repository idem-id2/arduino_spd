using System;
using System.IO;
using System.Runtime.InteropServices;
using SpdReaderWriterCore.Driver;

namespace SpdReaderWriterCore
{
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
