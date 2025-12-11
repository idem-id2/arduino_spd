using System.Runtime.CompilerServices;

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
