using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SpdReaderWriterCore.Driver;

namespace SpdReaderWriterCore
{
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
}
