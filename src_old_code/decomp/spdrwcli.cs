
// F:\bios\SPD\soft\SPD_RW\20230205_RSWP\spdrwcli.exe
// spdrwcli, Version=2.23.2.5, Culture=neutral, PublicKeyToken=null
// Global type: <Module>
// Entry point: b.g
// Architecture: x86
// Runtime: v4.0.30319
// Hash algorithm: SHA1

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using SpdReaderWriterCore;
using SpdReaderWriterCore.Properties;

[assembly: AssemblyTitle("SPD EEPROM Rreader & Writer")]
[assembly: AssemblyDescription("CLI for SPD EEPROM Reader & Writer")]
[assembly: AssemblyCompany("A213M")]
[assembly: AssemblyProduct("SPD EEPROM Rreader & Writer")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: Guid("fa956090-8022-49d8-b40d-1ec7c9a36d8d")]
[assembly: AssemblyFileVersion("2.23.02.05")]
[assembly: TargetFramework(".NETFramework,Version=v4.0,Profile=Client", FrameworkDisplayName = ".NET Framework 4 Client Profile")]
[assembly: AssemblyVersion("2.23.2.5")]
internal class _003CModule_003E
{
	static _003CModule_003E()
	{
	}
}
internal class b
{
	public static Arduino.SerialPortSettings l = new Arduino.SerialPortSettings(115200, true, true, 10);

	public static Smbus n;

	public static Arduino A = new Arduino(l);

	public static string[] B;

	public static bool i;

	public static bool j;

	public static string E = "";

	private static void g(string[] P_0)
	{
		B = P_0;
		j = Data.ArrayContains(B, "/silent");
		i = !Data.ArrayContains(B, "/nocolor");
		E = ((B.Length >= 4 && !B[3].Contains("/")) ? B[3] : "");
		if (K())
		{
			n = new Smbus();
		}
		I();
		if (P_0.Length != 0)
		{
			F();
		}
		else
		{
			e();
		}
		if (Debugger.IsAttached || P_0.Length == 0)
		{
			Console.WriteLine("\nPress [enter] to quit.\n");
			Console.ReadLine();
		}
	}

	private static void I()
	{
		string[] array = new string[5] { "   SPD-RW - EEPROM SPD reader and writer", "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", "Version {0}", "(C) 2021-2023 A213M", "" };
		for (int i = 0; i < array.Length; i++)
		{
			Console.WriteLine(array[i], FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion);
		}
	}

	private static void e()
	{
		string[] array = new string[33]
		{
			"", "Command line parameters:", "", "{0} /?", "{0} /find", "{0} /find <all|arduino|smbus>", "{0} /scan <PORTNAME>", "{0} /scan <SMBUS#>", "{0} /read <PORTNAME> <ADDRESS#> <filepath> /silent /nocolor", "{0} /read <SMBUS#> <ADDRESS#> <filepath> /silent /nocolor",
			"{0} /write <PORTNAME> <ADDRESS#> <FILEPATH> /silent /nocolor", "{0} /writeforce <PORTNAME> <ADDRESS#> <FILEPATH> /silent /nocolor", "{0} /firmware <FILEPATH>", "{0} /enablewriteprotection <PORTNAME> <ADDRESS#>", "{0} /enablewriteprotection <PORTNAME> <ADDRESS#> <block#>", "{0} /disablewriteprotection <PORTNAME> <ADDRESS#>", "{0} /enablepermanentwriteprotection <PORTNAME> <ADDRESS#>", "", "Parameters in CAPS are mandatory!", "All numbers must be specified in decimal format",
			"Parameter <filepath> is optional when /read switch is used, output will be printed to console only.", "Switch /silent is optional, progress won't be shown with this switch.", "Switch /nocolor is optional, use to show SPD contents in monochrome", "", "For additional help, visit: https://github.com/1a2m3/SPD-Reader-Writer", "                         or https://forums.evga.com/FindPost/3053544", "", "This program is free to use, but if you like it and wish to support me,", "I am accepting donations via systems listed below:", "",
			"Paypal:  https://paypal.me/mik4rt3m", "Bitcoin: 3Pe9VhVaUygyMFGT3pFuQ3dAghS36NPJTz", ""
		};
		for (int i = 0; i < array.Length; i++)
		{
			Console.WriteLine(array[i], AppDomain.CurrentDomain.FriendlyName);
		}
	}

	private static void F()
	{
		string text = B[0];
		try
		{
			uint num = J.C(text);
			if (num <= 2595342849u)
			{
				if (num <= 2071891562)
				{
					if (num != 502227275)
					{
						if (num != 2065612854)
						{
							if (num == 2071891562 && text == "/savefirmware")
							{
								goto IL_0223;
							}
						}
						else if (text == "/read")
						{
							D();
							return;
						}
					}
					else if (text == "/scan")
					{
						m();
						return;
					}
				}
				else if (num <= 2101218422)
				{
					if (num != 2095012991)
					{
						if (num == 2101218422 && text == "/setwriteprotection")
						{
							goto IL_0200;
						}
					}
					else if (text == "/enablepermanentwriteprotection")
					{
						goto IL_021c;
					}
				}
				else if (num != 2463255987u)
				{
					if (num == 2595342849u && text == "/help")
					{
						goto IL_01eb;
					}
				}
				else if (text == "/?")
				{
					goto IL_01eb;
				}
			}
			else if (num <= 2720384985u)
			{
				if (num != 2650693849u)
				{
					if (num != 2701673873u)
					{
						if (num == 2720384985u && text == "/write")
						{
							M();
							return;
						}
					}
					else if (text == "/clearwriteprotection")
					{
						goto IL_0207;
					}
				}
				else if (text == "/find")
				{
					c();
					return;
				}
			}
			else if (num <= 3236012092u)
			{
				if (num != 2741207375u)
				{
					if (num == 3236012092u && text == "/setpermanentwriteprotection")
					{
						goto IL_021c;
					}
				}
				else if (text == "/firmware")
				{
					goto IL_0223;
				}
			}
			else if (num != 3252750815u)
			{
				if (num == 3261403270u && text == "/disablewriteprotection")
				{
					goto IL_0207;
				}
			}
			else if (text == "/enablewriteprotection")
			{
				goto IL_0200;
			}
			Console.WriteLine("Unknown command line parameters.\n");
			e();
			return;
			IL_0207:
			k();
			return;
			IL_0200:
			d();
			return;
			IL_01eb:
			e();
			return;
			IL_0223:
			L();
			return;
			IL_021c:
			H();
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(ex.Message + "\n");
			Console.ResetColor();
		}
		finally
		{
			A.Disconnect();
		}
	}

	private static void L()
	{
		if (B.Length < 2 || B[1].Length < 1)
		{
			throw new ArgumentException("No destination path specified");
		}
		string text = B[1] + "\\SpdReaderWriter";
		Directory.CreateDirectory(text);
		File.WriteAllText(text + "\\SpdReaderWriter.ino", Data.BytesToString(Data.Gzip(Resources.Firmware.SpdReaderWriter_ino, Data.GzipMethod.Decompress)));
		File.WriteAllText(text + "\\SpdReaderWriterSettings.h", Data.BytesToString(Data.Gzip(Resources.Firmware.SpdReaderWriterSettings_h, Data.GzipMethod.Decompress)));
		File.SetAttributes(text + "\\SpdReaderWriter.ino", FileAttributes.ReadOnly);
		Console.WriteLine("Firmware files saved to " + text);
	}

	private static void H()
	{
		G();
		if (Eeprom.SetPswp(A))
		{
			Console.WriteLine(string.Format("Permanent write protection enabled on {0}:{1}", A.PortName, A.I2CAddress));
			return;
		}
		throw new Exception(string.Format("Unable to set permanent write protection on {0}:{1}", A.PortName, A.I2CAddress));
	}

	private static void M()
	{
		string text = B[0];
		byte i2CAddress = (byte)int.Parse(B[2]);
		if (E.Length < 1)
		{
			throw new ArgumentException("File path is mandatory for write mode.");
		}
		if (!File.Exists(E))
		{
			throw new FileNotFoundException("File \"" + E + "\" not found.");
		}
		byte[] array;
		try
		{
			array = File.ReadAllBytes(E);
		}
		catch
		{
			throw new FileLoadException("Unable to read " + E);
		}
		G();
		A.I2CAddress = i2CAddress;
		Console.WriteLine("Writing \"{0}\" ({1} {2}) to EEPROM at address {3}\n", E, array.Length, (array.Length > 1) ? "bytes" : "byte", A.I2CAddress);
		if (array.Length > A.DataLength)
		{
			throw new Exception(string.Format("File \"{0}\" is larger than {1} bytes.", E, A.DataLength));
		}
		int num = 0;
		int tickCount = Environment.TickCount;
		if (!Spd.ValidateSpd(array))
		{
			throw new Exception("Incorrect SPD file");
		}
		for (ushort num2 = 0; num2 < array.Length; num2++)
		{
			byte b2 = array[num2];
			if (!((text == "/writeforce") ? Eeprom.Write(A, num2, b2) : Eeprom.Update(A, num2, b2)))
			{
				throw new Exception(string.Format("Could not write byte {0} to EEPROM at address {1} on port {2}.", num2, A.I2CAddress, A.PortName));
			}
			num++;
			if (!j)
			{
				h(num2, b2, 16, true, i);
			}
		}
		A.Disconnect();
		Console.WriteLine("\n\nWritten {0} {1} to EEPROM at address {2} on port {3} in {4} ms", num, (num > 1) ? "bytes" : "byte", A.I2CAddress, A.PortName, Environment.TickCount - tickCount);
	}

	private static void D()
	{
		byte b2 = (byte)int.Parse(B[2]);
		byte[] array = new byte[0];
		Console.Write(string.Format("Reading EEPROM at address {0}", b2));
		if (E.Length > 0)
		{
			Console.WriteLine(" to " + E);
		}
		Console.WriteLine("\n");
		int tickCount = Environment.TickCount;
		string text;
		if (B[1].StartsWith("COM"))
		{
			G();
			text = A.ToString();
			A.I2CAddress = b2;
			for (ushort num = 0; num < A.DataLength; num += 32)
			{
				array = Data.MergeArray(array, Eeprom.Read(A, num, 32));
			}
			A.Disconnect();
		}
		else
		{
			if (!K())
			{
				throw new AccessViolationException("Administrative privileges required");
			}
			n.BusNumber = (byte)int.Parse(B[1]);
			n.I2CAddress = b2;
			text = string.Format("{0} ({1})", n, n.BusNumber);
			for (ushort num2 = 0; num2 < n.MaxSpdSize; num2 += 32)
			{
				array = Data.MergeArray(array, Eeprom.Read(n, num2, 32));
			}
		}
		int tickCount2 = Environment.TickCount;
		if (!j)
		{
			for (int i = 0; i < array.Length; i++)
			{
				h(i, array[i], 16, true, b.i);
			}
		}
		Console.Write("\n\nRead {0} {1} from EEPROM at address {2} on {3} in {4} ms", array.Length, (array.Length > 1) ? "bytes" : "byte", b2, text, tickCount2 - tickCount);
		if (E.Length > 0)
		{
			try
			{
				File.WriteAllBytes(E, array);
			}
			catch
			{
				throw new Exception("Unable to write to " + E);
			}
			Console.Write(" to file \"" + E + "\"");
		}
	}

	private static void k()
	{
		G();
		if (Eeprom.ClearRswp(A))
		{
			Console.WriteLine("Write protection successfully disabled.");
			return;
		}
		throw new Exception("Unable to clear write protection");
	}

	private static void d()
	{
		byte i2CAddress = (byte)int.Parse(B[2]);
		G();
		A.I2CAddress = i2CAddress;
		Spd.RamType ramType = Spd.GetRamType(A);
		int[] array;
		if (B.Length == 4)
		{
			try
			{
				array = new int[1] { int.Parse(B[3]) };
			}
			catch
			{
				throw new ArgumentException("Block number should be specified in decimal notation.");
			}
			if (array[0] > 15 || array[0] < 0 || (array[0] > 3 && ramType == Spd.RamType.DDR4) || (array[0] > 0 && ramType != Spd.RamType.DDR4 && ramType != Spd.RamType.DDR5))
			{
				throw new ArgumentOutOfRangeException("Incorrect block number specified");
			}
		}
		else
		{
			int num;
			switch (ramType)
			{
			case Spd.RamType.DDR5:
				num = 16;
				break;
			case Spd.RamType.DDR4:
				num = 4;
				break;
			default:
				num = 1;
				break;
			}
			array = Data.ConsecutiveArray<int>(0, num - 1, 1);
		}
		for (byte b2 = 0; b2 < array.Length; b2++)
		{
			Console.WriteLine(Eeprom.SetRswp(A, b2) ? string.Format("Block {0} is now read-only", b2) : string.Format("Unable to set write protection for block {0}. Either SA0 is not connected to HV, or the block is already read-only.", b2));
		}
		A.Disconnect();
	}

	private static void m()
	{
		if (B.Length == 2)
		{
			byte[] array;
			string arg;
			if (B[1].StartsWith("COM"))
			{
				G();
				array = A.Scan();
				arg = "port " + A.PortName;
				A.Disconnect();
			}
			else
			{
				if (!K())
				{
					throw new AccessViolationException("Administrative privileges required");
				}
				int result = -1;
				if (!int.TryParse(B[1], out result) || result == -1)
				{
					throw new Exception("SMBus number should be specified in decimal notation.");
				}
				if (result > n.FindBus().Length - 1)
				{
					throw new ArgumentOutOfRangeException("SMBus number not available");
				}
				n.BusNumber = (byte)result;
				array = n.Scan();
				arg = string.Format("SMBus {0}", n.BusNumber);
			}
			if (array.Length == 0)
			{
				throw new Exception("No EEPROM devices found.");
			}
			byte[] array2 = array;
			foreach (int num in array2)
			{
				Console.WriteLine(string.Format("Found EEPROM on {0} at address: {1}", arg, num));
			}
			return;
		}
		throw new ArgumentException("Incorrect use of arguments");
	}

	private static void G()
	{
		string text = B[1];
		if (!text.StartsWith("COM", StringComparison.CurrentCulture))
		{
			throw new ArgumentException("Port name should start with \"COM\" followed by a number.");
		}
		A = new Arduino(l, text);
		if (!A.Connect())
		{
			throw new Exception("Could not connect to the device on port " + text + ".");
		}
		if (A.FirmwareVersion < Arduino.RequiredFirmwareVersion)
		{
			throw new Exception("The device on port " + text + " requires its firmware to be updated.");
		}
		if (!A.Test())
		{
			throw new Exception("The device on port " + text + " does not respond.");
		}
	}

	private static void c()
	{
		if (B.Length == 1 || (B.Length == 2 && B[1] == "all"))
		{
			a();
			if (K())
			{
				f();
			}
		}
		else
		{
			if (B.Length != 2)
			{
				return;
			}
			string text = B[1];
			if (!(text == "arduino"))
			{
				if (text == "smbus")
				{
					f();
				}
			}
			else
			{
				a();
			}
		}
	}

	private static void a()
	{
		Arduino[] array = Arduino.Find(l);
		if (array.Length != 0)
		{
			Arduino[] array2 = array;
			foreach (Arduino arg in array2)
			{
				Console.WriteLine(string.Format("Found Arduino on Serial Port: {0}\n", arg));
			}
		}
		else
		{
			Console.WriteLine("No Arduinos found");
		}
	}

	private static void f()
	{
		if (!K())
		{
			throw new AccessViolationException("Administrative privileges required");
		}
		try
		{
			byte[] array = n.FindBus();
			foreach (byte b2 in array)
			{
				Console.WriteLine(string.Format("Found SMBus # {0} ({1})", b2, n));
			}
		}
		catch
		{
			Console.WriteLine("No SMBus found");
		}
	}

	private static void h(int P_0, byte P_1, int P_2 = 16, bool P_3 = true, bool P_4 = true)
	{
		ConsoleColor[] array = new ConsoleColor[16]
		{
			ConsoleColor.DarkGray,
			ConsoleColor.Gray,
			ConsoleColor.DarkRed,
			ConsoleColor.Red,
			ConsoleColor.Yellow,
			ConsoleColor.DarkYellow,
			ConsoleColor.Green,
			ConsoleColor.DarkGreen,
			ConsoleColor.DarkCyan,
			ConsoleColor.Cyan,
			ConsoleColor.Blue,
			ConsoleColor.DarkBlue,
			ConsoleColor.DarkMagenta,
			ConsoleColor.Magenta,
			ConsoleColor.White,
			ConsoleColor.Gray
		};
		if (P_0 == 0 && P_3)
		{
			Console.Write("      ");
			for (int i = 0; i < P_2; i++)
			{
				Console.Write(string.Format("{0:X2} ", i));
			}
		}
		if (P_0 % P_2 == 0)
		{
			Console.Write(Environment.NewLine);
			if (P_3)
			{
				Console.Write("{0:X4}: ", P_0);
			}
		}
		if (P_4)
		{
			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = array[P_1 >> 4];
		}
		Console.Write(string.Format("{0:X2}", P_1));
		if (P_0 % P_2 != P_2 - 1)
		{
			Console.Write(" ");
		}
		Console.ResetColor();
	}

	private static bool K()
	{
		try
		{
			using (WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent())
			{
				return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
			}
		}
		catch
		{
			return false;
		}
	}
}
[CompilerGenerated]
internal sealed class J
{
	internal static uint C(string P_0)
	{
		uint num = default(uint);
		if (P_0 != null)
		{
			num = 2166136261u;
			for (int i = 0; i < P_0.Length; i++)
			{
				num = (P_0[i] ^ num) * 16777619;
			}
		}
		return num;
	}
}
