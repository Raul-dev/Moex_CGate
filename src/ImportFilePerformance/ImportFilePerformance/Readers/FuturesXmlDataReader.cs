using System.Globalization;
using System.Text;
using System.Xml;

namespace ImportFilePerformance.Readers;

/// <summary>
/// Streaming XmlReader that flattens FUTURES (+ parent BASEASSET / RESULT) into one row.
/// Target element: FUTURES. Compatible with SPB23M-style reports.
/// </summary>
public sealed class FuturesXmlDataReader : StreamingDataReaderBase
{
    private static readonly string[] ColumnNames =
    [
        "report_date", "board_id", "base_asset_type", "base_asset_code", "base_asset_isin",
        "futures_code", "futures_name", "delivery_type", "currency_id", "lot",
        "min_step", "step_price", "trade_lot", "point_rate",
        "total_amount", "total_volume", "total_deal_count",
        "max_deal_price", "min_deal_price", "last_deal_price", "clearing_price", "current_price"
    ];

    private static readonly Type[] ColumnTypes =
    [
        typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
        typeof(string), typeof(string), typeof(string), typeof(string), typeof(decimal),
        typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal),
        typeof(decimal), typeof(decimal), typeof(long),
        typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal)
    ];

    private readonly XmlReader _xml;
    private readonly bool _ownsReader;

    private string? _reportDate;
    private string? _boardId;
    private string? _baseAssetType;
    private string? _baseAssetCode;
    private string? _baseAssetIsin;

    public FuturesXmlDataReader(string filePath)
        : this(XmlReader.Create(filePath, new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Ignore,
            Async = false
        }), ownsReader: true)
    {
    }

    public FuturesXmlDataReader(XmlReader xml, bool ownsReader = false)
        : base(ColumnNames, ColumnTypes)
    {
        _xml = xml;
        _ownsReader = ownsReader;
    }

    public static string[] SchemaNames => ColumnNames;
    public static Type[] SchemaTypes => ColumnTypes;

    protected override bool TryReadNext()
    {
        while (_xml.Read())
        {
            if (_xml.NodeType != XmlNodeType.Element)
                continue;

            switch (_xml.LocalName)
            {
                case "DOC_INFO":
                    _reportDate = _xml.GetAttribute("ReportDate") ?? _xml.GetAttribute("TradeDate");
                    break;
                case "BOARD":
                    _boardId = _xml.GetAttribute("BoardId");
                    break;
                case "BASEASSETTYPE":
                    _baseAssetType = _xml.GetAttribute("BaseAssetType");
                    break;
                case "BASEASSET":
                    _baseAssetCode = _xml.GetAttribute("BaseAssetCode");
                    _baseAssetIsin = _xml.GetAttribute("BaseAssetDetails");
                    break;
                case "FUTURES":
                    FillFuturesRow();
                    return true;
            }
        }

        return false;
    }

    private void FillFuturesRow()
    {
        Array.Clear(Values, 0, Values.Length);

        Values[0] = _reportDate;
        Values[1] = _boardId;
        Values[2] = _baseAssetType;
        Values[3] = _baseAssetCode;
        Values[4] = _baseAssetIsin;
        Values[5] = _xml.GetAttribute("FuturesCode");
        Values[6] = _xml.GetAttribute("FuturesName");
        Values[7] = _xml.GetAttribute("DeliveryType");
        Values[8] = _xml.GetAttribute("CurrencyId");
        Values[9] = ParseDecimalAttr(_xml.GetAttribute("Lot"));
        Values[10] = ParseDecimalAttr(_xml.GetAttribute("MinStep"));
        Values[11] = ParseDecimalAttr(_xml.GetAttribute("StepPrice"));
        Values[12] = ParseDecimalAttr(_xml.GetAttribute("TradeLot"));
        Values[13] = ParseDecimalAttr(_xml.GetAttribute("PointRate"));

        // Peek nested RESULT inside this FUTURES without loading DOM.
        if (_xml.IsEmptyElement)
            return;

        var depth = _xml.Depth;
        while (_xml.Read())
        {
            if (_xml.NodeType == XmlNodeType.EndElement && _xml.Depth == depth)
                break;

            if (_xml.NodeType == XmlNodeType.Element && _xml.LocalName == "RESULT")
            {
                Values[14] = ParseDecimalAttr(_xml.GetAttribute("TotalAmount"));
                Values[15] = ParseDecimalAttr(_xml.GetAttribute("TotalVolume"));
                Values[16] = ParseLongAttr(_xml.GetAttribute("TotalDealCount"));
                Values[17] = ParseDecimalAttr(_xml.GetAttribute("MaxDealPrice"));
                Values[18] = ParseDecimalAttr(_xml.GetAttribute("MinDealPrice"));
                Values[19] = ParseDecimalAttr(_xml.GetAttribute("LastDealPrice"));
                Values[20] = ParseDecimalAttr(_xml.GetAttribute("ClearingPrice"));
                Values[21] = ParseDecimalAttr(_xml.GetAttribute("CurrentPrice"));
            }
        }
    }

    private static object? ParseDecimalAttr(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static object? ParseLongAttr(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsReader)
            _xml.Dispose();
        base.Dispose(disposing);
    }
}
