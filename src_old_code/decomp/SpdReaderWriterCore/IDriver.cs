namespace SpdReaderWriterCore
{
	public interface IDriver
	{
		bool IsInstalled { get; }

		bool IsServiceRunning { get; }

		bool IsReady { get; }

		bool InstallDriver();

		bool RemoveDriver();

		bool StartDriver();

		bool StopDriver();
	}
}
