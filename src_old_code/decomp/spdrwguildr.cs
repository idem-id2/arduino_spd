
// F:\bios\SPD\soft\SPD_RW\20230205_RSWP\spdrwguildr.exe
// spdrwguildr, Version=2.23.2.5, Culture=neutral, PublicKeyToken=null
// Global type: <Module>
// Entry point: G.f
// Architecture: x86
// Runtime: v4.0.30319
// Hash algorithm: SHA1

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using SpdReaderWriterCore;

[assembly: AssemblyTitle("SpdReaderWriterGuiLoader")]
[assembly: AssemblyDescription("SPD-RW GUI Loader")]
[assembly: AssemblyCompany("A213M")]
[assembly: AssemblyProduct("SPD-RW")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: Guid("c7c30ec8-ec48-4a78-8cbc-7783481c4d55")]
[assembly: AssemblyFileVersion("2.23.02.05")]
[assembly: TargetFramework(".NETFramework,Version=v4.0", FrameworkDisplayName = ".NET Framework 4")]
[assembly: AssemblyVersion("2.23.2.5")]
internal class _003CModule_003E
{
	static _003CModule_003E()
	{
	}
}
internal static class G
{
	[STAThread]
	private static void f()
	{
		object[] parameters = new object[1] { Environment.GetCommandLineArgs() };
		byte[] array = g.A;
		Type type = Assembly.Load(Data.Gzip(Data.MergeArray(Data.MergeArray(Data.ConsecutiveArray<byte>(31, 241, 108), Data.RepetitiveArray((byte)8, 2)), Data.TrimArray(array, array.Length - 4, Data.TrimPosition.Start)), Data.GzipMethod.Decompress)).GetType(Data.BytesToString(new byte[2] { 66, 68 }));
		MethodInfo method = type.GetMethod(Data.BytesToString(new byte[2] { 104, 98 }));
		if (method != null)
		{
			method.Invoke(Activator.CreateInstance(type), parameters);
		}
		Application.Exit();
	}
}
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
public class g
{
	private static ResourceManager e;

	private static CultureInfo a;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static ResourceManager B
	{
		get
		{
			if (e == null)
			{
				e = new ResourceManager("g", typeof(g).Assembly);
			}
			return e;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public static CultureInfo E
	{
		get
		{
			return a;
		}
		set
		{
			a = cultureInfo;
		}
	}

	public static byte[] A
	{
		get
		{
			return (byte[])B.GetObject("p", a);
		}
	}

	internal g()
	{
	}
}
