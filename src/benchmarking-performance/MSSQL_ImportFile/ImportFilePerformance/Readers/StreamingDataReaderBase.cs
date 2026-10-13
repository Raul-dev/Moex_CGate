using System.Collections;
using System.Data;
using System.Data.Common;

namespace ImportFilePerformance.Readers;

/// <summary>
/// Minimal forward-only IDataReader for SqlBulkCopy / Npgsql COPY consumers.
/// Keeps only the current row in fields — no List/DataTable accumulation.
/// </summary>
public abstract class StreamingDataReaderBase : DbDataReader
{
    private bool _closed;
    protected readonly object?[] Values;
    protected readonly string[] Names;
    protected readonly Type[] Types;
    private int _fieldCount;

    protected StreamingDataReaderBase(string[] names, Type[] types)
    {
        if (names.Length != types.Length)
            throw new ArgumentException("names/types length mismatch");

        Names = names;
        Types = types;
        Values = new object?[names.Length];
        _fieldCount = names.Length;
    }

    public long RowsRead { get; private set; }

    public override int FieldCount => _fieldCount;
    public override bool HasRows => true;
    public override bool IsClosed => _closed;
    public override int Depth => 0;
    public override int RecordsAffected => -1;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override string GetName(int ordinal) => Names[ordinal];
    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < Names.Length; i++)
        {
            if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new IndexOutOfRangeException(name);
    }

    public override Type GetFieldType(int ordinal) => Types[ordinal];
    public override string GetDataTypeName(int ordinal) => Types[ordinal].Name;

    public override object GetValue(int ordinal)
    {
        var v = Values[ordinal];
        return v ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var n = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < n; i++)
            values[i] = GetValue(i);
        return n;
    }

    public override bool IsDBNull(int ordinal) => Values[ordinal] is null or DBNull;

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();
    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    public override bool NextResult() => false;

    public override bool Read()
    {
        if (_closed)
            return false;

        if (!TryReadNext())
            return false;

        RowsRead++;
        return true;
    }

    protected abstract bool TryReadNext();

    public override void Close()
    {
        _closed = true;
        base.Close();
    }

    protected static object? ParseLong(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty)
            return null;
        return long.TryParse(s, out var v) ? v : null;
    }

    protected static object? ParseDecimal(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty)
            return null;
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    protected static object? ParseInt(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty)
            return null;
        return int.TryParse(s, out var v) ? v : null;
    }

    protected static string? AsString(ReadOnlySpan<char> s)
        => s.IsEmpty ? null : s.ToString();
}
