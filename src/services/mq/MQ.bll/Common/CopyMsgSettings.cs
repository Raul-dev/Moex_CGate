namespace MQ.bll.Common;

public class CopyMsgSettings
{
    /// <summary>0 = copy until queue is empty.</summary>
    public int MaxMessages { get; set; } = 0;

    /// <summary>Fixed target table. Empty = use metamap routing.</summary>
    public string TargetTable { get; set; } = "";

    public bool UseMetamapRouting { get; set; } = true;

    /// <summary>Remove message from RabbitMQ after save (ACK). false = keep messages in queue.</summary>
    public bool IsConfirmMsgAndRemoveFromQueue { get; set; } = true;

    public int PauseMsWhenEmpty { get; set; } = 100;

    /// <summary>Stop after N consecutive empty polls.</summary>
    public int EmptyPollAttempts { get; set; } = 3;

    public bool RunEtlAfterCopy { get; set; } = false;

    public int MessageTypeId { get; set; } = 2;

    public int MetaAdapterId { get; set; } = 1;

    public int DataSourceId { get; set; } = 1;

    /// <summary>Create target buffer table if it does not exist.</summary>
    public bool EnsureBufferTable { get; set; } = true;

    /// <summary>Append _buffer suffix to TargetTable (dbo.Upload -> dbo.Upload_buffer).</summary>
    public bool AppendBufferSuffix { get; set; } = true;

    /// <summary>TRUNCATE target table before copy.</summary>
    public bool TruncateBeforeCopy { get; set; } = false;
}
