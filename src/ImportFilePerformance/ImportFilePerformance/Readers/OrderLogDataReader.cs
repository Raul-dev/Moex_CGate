using System.Text;

namespace ImportFilePerformance.Readers;

/// <summary>
/// Streaming reader for PUBLIC_ORDER_LOG CSV:
/// NO,TICKER,BUYSELL,TIME,ORDERNO,ACTION,PRICE,VOLUME,TRADENO,TRADEPRICE
/// Keeps only the current row — no List/DataTable accumulation.
/// </summary>
public sealed class OrderLogDataReader : StreamingDataReaderBase
{
    private static readonly string[] ColumnNames =
    [
        "sess_id", "ticker", "buysell", "time_str", "orderno",
        "action", "price", "volume", "tradeno", "tradeprice"
    ];

    private static readonly Type[] ColumnTypes =
    [
        typeof(long), typeof(string), typeof(string), typeof(string), typeof(long),
        typeof(int), typeof(decimal), typeof(long), typeof(long), typeof(decimal)
    ];

    private readonly StreamReader _reader;
    private readonly bool _ownsReader;

    public OrderLogDataReader(string filePath)
        : this(new StreamReader(File.OpenRead(filePath), Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 1 << 20, leaveOpen: false), ownsReader: true)
    {
    }

    public OrderLogDataReader(StreamReader reader, bool ownsReader = false)
        : base(ColumnNames, ColumnTypes)
    {
        _reader = reader;
        _ownsReader = ownsReader;
    }

    public static string[] SchemaNames => ColumnNames;
    public static Type[] SchemaTypes => ColumnTypes;

    protected override bool TryReadNext()
    {
        string? line;
        do
        {
            line = _reader.ReadLine();
            if (line is null)
                return false;
        } while (line.Length == 0);

        ParseLine(line.AsSpan());
        return true;
    }

    private void ParseLine(ReadOnlySpan<char> line)
    {
        Array.Clear(Values, 0, Values.Length);

        var col = 0;
        var start = 0;
        for (var i = 0; i <= line.Length && col < Values.Length; i++)
        {
            if (i < line.Length && line[i] != ',')
                continue;

            var field = line[start..i];
            Values[col] = col switch
            {
                0 => ParseLong(field),
                1 => AsString(field),
                2 => AsString(field),
                3 => AsString(field),
                4 => ParseLong(field),
                5 => ParseInt(field),
                6 => ParseDecimal(field),
                7 => ParseLong(field),
                8 => ParseLong(field),
                9 => ParseDecimal(field),
                _ => null
            };
            col++;
            start = i + 1;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsReader)
            _reader.Dispose();
        base.Dispose(disposing);
    }
}
