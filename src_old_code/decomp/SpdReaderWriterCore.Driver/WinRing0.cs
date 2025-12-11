using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32.SafeHandles;
using SpdReaderWriterCore.Properties;

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
