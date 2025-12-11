using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SpdReaderWriterCore.Properties;

namespace SpdReaderWriterCore
{
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
						throw new TimeoutException(PortName + " response timeout");
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
}
