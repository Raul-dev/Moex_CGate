using System.Text;

namespace ImportFilePerformance.Readers;

/// <summary>
/// Streaming semicolon-CSV reader (SPB TradeResult). Header defines columns.
/// Values stored as strings for flexible staging.
/// </summary>
public sealed class DelimitedCsvDataReader : StreamingDataReaderBase
{
    private readonly StreamReader _reader;
    private readonly bool _ownsReader;
    private readonly char _delimiter;

    private DelimitedCsvDataReader(StreamReader reader, string[] names, char delimiter, bool ownsReader)
        : base(names, names.Select(_ => typeof(string)).ToArray())
    {
        _reader = reader;
        _ownsReader = ownsReader;
        _delimiter = delimiter;
    }

    public static DelimitedCsvDataReader Create(string filePath, char delimiter = ';')
    {
        var reader = new StreamReader(
            File.OpenRead(filePath),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1 << 20,
            leaveOpen: false);

        var header = reader.ReadLine()
            ?? throw new InvalidDataException("CSV file is empty");

        var names = Split(header.AsSpan(), delimiter)
            .Select(SanitizeColumnName)
            .ToArray();

        return new DelimitedCsvDataReader(reader, names, delimiter, ownsReader: true);
    }

    public string[] ColumnNames => Names;

    protected override bool TryReadNext()
    {
        string? line;
        do
        {
            line = _reader.ReadLine();
            if (line is null)
                return false;
        } while (line.Length == 0);

        Array.Clear(Values, 0, Values.Length);
        var fields = Split(line.AsSpan(), _delimiter);
        var n = Math.Min(fields.Count, Values.Length);
        for (var i = 0; i < n; i++)
            Values[i] = fields[i].Length == 0 ? null : fields[i];

        return true;
    }

    private static List<string> Split(ReadOnlySpan<char> line, char delimiter)
    {
        var result = new List<string>(64);
        var start = 0;
        for (var i = 0; i <= line.Length; i++)
        {
            if (i < line.Length && line[i] != delimiter)
                continue;
            result.Add(line[start..i].ToString());
            start = i + 1;
        }
        return result;
    }

    private static string SanitizeColumnName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "col";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var s = sb.ToString();
        if (s.Length == 0 || char.IsDigit(s[0]))
            s = "c_" + s;
        return s.ToLowerInvariant();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsReader)
            _reader.Dispose();
        base.Dispose(disposing);
    }
}
