namespace SpdReaderWriterCore
{
	public interface ISpd
	{
		int Length { get; }

		int SpdBytesUsed { get; }

		Spd.RamType DramDeviceType { get; }

		Spd.ManufacturerIdCodeData ManufacturerIdCode { get; }

		string PartNumber { get; }

		Spd.DateCodeData ModuleManufacturingDate { get; }

		ulong TotalModuleCapacity { get; }

		bool CrcStatus { get; }

		bool FixCrc();
	}
}
