using CommandLine;
using Microsoft.Extensions.Configuration;
using MQ.bll.Common;

namespace MQ.OptionModels;

[Verb("CopyMsg", HelpText = "Copy messages from RabbitMQ queue to MS SQL table.")]
public class CopyMsgOptions : BaseOptions
{
    [Option('n', "max-messages", Required = false, Default = null, HelpText = "Max messages to copy. 0 = until queue empty.")]
    public int? MaxMessages { get; set; }

    [Option('q', "target-table", Required = false, Default = null, HelpText = "Target SQL table (e.g. msgqueue, crs.orders_log_buffer). Empty = metamap routing.")]
    public string? TargetTable { get; set; }

    [Option('r', "ack", Required = false, Default = null, HelpText = "Same as --clear-queue: ACK and remove message from RabbitMQ after save.")]
    public bool? IsConfirmMsgAndRemoveFromQueue { get; set; }

    [Option('g', "clear-queue", Required = false, Default = null, HelpText = "Clear RabbitMQ queue: remove each message after save (ACK). true=remove, false=keep in queue.")]
    public bool? ClearQueue { get; set; }

    [Option('e', "run-etl", Required = false, Default = null, HelpText = "Run ETL stored procedures after copy.")]
    public bool? RunEtlAfterCopy { get; set; }

    [Option('m', "meta-adapter-id", Required = false, Default = null, HelpText = "MetaAdapterId for metamap routing.")]
    public int? MetaAdapterId { get; set; }

    [Option('a', "pause-ms", Required = false, Default = null, HelpText = "Pause between empty queue polls (ms).")]
    public int? PauseMsWhenEmpty { get; set; }

    [Option('y', "empty-polls", Required = false, Default = null, HelpText = "Stop after N consecutive empty polls.")]
    public int? EmptyPollAttempts { get; set; }

    [Option('x', "no-metamap", Required = false, Default = false, HelpText = "Disable metamap routing; use msgqueue table.")]
    public bool DisableMetamapRouting { get; set; }

    [Option('f', "truncate", Required = false, Default = null, HelpText = "Truncate target buffer table before copy.")]
    public bool? TruncateBeforeCopy { get; set; }

    [Option('b', "no-create-table", Required = false, Default = false, HelpText = "Do not auto-create buffer table.")]
    public bool SkipEnsureBufferTable { get; set; }

    [Option('z', "no-buffer-suffix", Required = false, Default = false, HelpText = "Do not append _buffer to target table name.")]
    public bool SkipBufferSuffix { get; set; }

    public override void InitBllOption(BllOption blloption, IConfiguration configuration)
    {
        base.InitBllOption(blloption, configuration);

        var settings = configuration.GetSection(nameof(CopyMsgSettings)).Get<CopyMsgSettings>() ?? new CopyMsgSettings();

        if (MaxMessages.HasValue)
            settings.MaxMessages = MaxMessages.Value;
        if (!string.IsNullOrWhiteSpace(TargetTable))
            settings.TargetTable = TargetTable;
        if (IsConfirmMsgAndRemoveFromQueue.HasValue)
            settings.IsConfirmMsgAndRemoveFromQueue = IsConfirmMsgAndRemoveFromQueue.Value;
        if (ClearQueue.HasValue)
            settings.IsConfirmMsgAndRemoveFromQueue = ClearQueue.Value;
        if (RunEtlAfterCopy.HasValue)
            settings.RunEtlAfterCopy = RunEtlAfterCopy.Value;
        if (MetaAdapterId.HasValue)
            settings.MetaAdapterId = MetaAdapterId.Value;
        if (PauseMsWhenEmpty.HasValue)
            settings.PauseMsWhenEmpty = PauseMsWhenEmpty.Value;
        if (EmptyPollAttempts.HasValue)
            settings.EmptyPollAttempts = EmptyPollAttempts.Value;
        if (TruncateBeforeCopy.HasValue)
            settings.TruncateBeforeCopy = TruncateBeforeCopy.Value;
        if (SkipEnsureBufferTable)
            settings.EnsureBufferTable = false;
        if (SkipBufferSuffix)
            settings.AppendBufferSuffix = false;
        if (DisableMetamapRouting)
        {
            settings.UseMetamapRouting = false;
            if (string.IsNullOrWhiteSpace(settings.TargetTable))
                settings.TargetTable = "msgqueue";
        }

        blloption.RabbitMQServSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>()
            ?? throw new InvalidOperationException("RabbitMQSettings section is missing in configuration.");

        blloption.DataBaseServSettings.MetaAdapterId = settings.MetaAdapterId;
        blloption.DataBaseServSettings.DataSourceID = settings.DataSourceId;
        blloption.IsConfirmMsgAndRemoveFromQueue = settings.IsConfirmMsgAndRemoveFromQueue;
        blloption.CopyMsgSettings = settings;
    }
}
