namespace TcpTool;

public class DeviceDefinition
{
    public string Name { get; set; } = "New Device";
    public PayloadFormat PayloadFormat { get; set; } = PayloadFormat.HexDump;
    public Endianness Endian { get; set; } = Endianness.Little;
    public bool InputIsHexDump { get; set; } = false;
    public List<FieldDef> Fields { get; set; } = new();
    public List<ParsedReport> Reports { get; set; } = new();
}

public class FieldDef
{
    public string Name { get; set; } = "";
    public string Meaning { get; set; } = "";
    public int Offset { get; set; }
    public int Length { get; set; }
    public FieldType Type { get; set; } = FieldType.UInt8;
    public Endianness Endian { get; set; } = Endianness.Little;
    public string ExpectedHex { get; set; } = "";
}

public class ParsedReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string RawDataHex { get; set; } = "";
    public List<ParsedField> ParsedFields { get; set; } = new();
    public string DeviceName { get; set; } = "";
}

public class ParsedField
{
    public string FieldName { get; set; } = "";
    public string Meaning { get; set; } = "";
    public object? ParsedValue { get; set; }
    public string DisplayValue { get; set; } = "";
    public int Offset { get; set; }
    public int Length { get; set; }
    public FieldType Type { get; set; }
}

public enum FieldType
{
    UInt8,
    Int8,
    UInt16,
    Int16,
    UInt32,
    Int32,
    Float32,
    AsciiString,
    Fixed
}

public enum Endianness
{
    Little,
    Big
}

public enum PayloadFormat
{
    HexDump,
    Binary
}
