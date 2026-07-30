using System.Text;

using MQ.bll.Common;

using MQ.bll.RabbitMQ;

using MQ.dal;

using Serilog;



namespace MQ.bll;



public sealed class CopyMessagesResult

{

    public int CopiedCount { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

}



public class CopyMessages

{

    private readonly BllOption _option;

    private readonly CopyMsgSettings _settings;

    private readonly CancellationToken _cancellationToken;



    public CopyMessages(BllOption option, CopyMsgSettings settings, CancellationToken cancellationToken)

    {

        _option = option;

        _settings = settings;

        _cancellationToken = cancellationToken;

    }



    public async Task<CopyMessagesResult> ProcessAsync()

    {

        if (_option.ServerType != SqlServerType.mssql)

        {

            return new CopyMessagesResult

            {

                ErrorMessage = $"CopyMsg supports MS SQL Server only. Current type: {_option.ServerType}."

            };

        }



        MQSession? mqSession = null;

        RabbitMQChannel? channel = null;

        var copied = 0;

        string? errorMessage = null;

        var useExplicitBufferTable = !string.IsNullOrWhiteSpace(_settings.TargetTable);



        try

        {

            mqSession = new MQSession(_option, _cancellationToken);

            var sessionId = mqSession.StartSessionProcessing();

            if (sessionId == -1)

            {

                return new CopyMessagesResult { ErrorMessage = "Failed to start MQ session (metamap setup)." };

            }



            channel = new RabbitMQChannel(_option, _cancellationToken);

            await channel.InitSetup(mqSession, isSend: false, isSubscription: false);



            var dbHelper = new DBHelper(

                _option.DataBaseServSettings?.ServerName ?? "",

                _option.DataBaseServSettings?.DataBase ?? "",

                _option.DataBaseServSettings?.Port ?? 0,

                _option.ServerType,

                _option.DataBaseServSettings?.User ?? "",

                _option.DataBaseServSettings?.Password ?? "");



            var targetTable = useExplicitBufferTable

                ? ResolveExplicitTargetTable(_settings.TargetTable)

                : null;



            if (useExplicitBufferTable && targetTable is not null)

            {

                if (_settings.EnsureBufferTable)

                {

                    await dbHelper.EnsureCopyBufferTableAsync(targetTable, _cancellationToken);

                    Log.Information("CopyMsg ensured buffer table {Table} exists.", targetTable);

                }



                if (_settings.TruncateBeforeCopy)

                {

                    await dbHelper.TruncateTableAsync(targetTable, _cancellationToken);

                    Log.Information("CopyMsg truncated table {Table} before copy.", targetTable);

                }

            }



            var queueDepth = await channel.MessageCountAsync();

            Log.Information(

                "CopyMsg started. Queue={Queue}, depth={Depth}, maxMessages={Max}, targetTable={Table}, clearQueue={ClearQueue}",

                _option.RabbitMQServSettings?.DefaultQueue,

                queueDepth,

                _settings.MaxMessages == 0 ? "all" : _settings.MaxMessages.ToString(),

                targetTable ?? "metamap",

                _option.IsConfirmMsgAndRemoveFromQueue);



            var emptyPolls = 0;

            while (!_cancellationToken.IsCancellationRequested)

            {

                if (_settings.MaxMessages > 0 && copied >= _settings.MaxMessages)

                    break;



                var message = await channel.GetMessageAsync();

                if (message is null)

                {

                    emptyPolls++;

                    if (emptyPolls >= _settings.EmptyPollAttempts)

                        break;



                    await Task.Delay(_settings.PauseMsWhenEmpty, _cancellationToken);

                    continue;

                }



                emptyPolls = 0;



                var body = Encoding.UTF8.GetString(message.Body.ToArray());
                // BasicGetResult.BasicProperties — same fields as BasicDeliverEventArgs.BasicProperties
                var msgKey = message.BasicProperties.Type ?? "Unknown";       // -> [msg_key]
                var msgId = message.BasicProperties.MessageId ?? Guid.NewGuid().ToString(); // -> [msg_id]

                Log.Debug("CopyMsg message: msg_id={MsgId}, msg_key={MsgKey}", msgId, msgKey);



                if (useExplicitBufferTable && targetTable is not null)

                {

                    await dbHelper.SaveMsgToBufferAsync(

                        sessionId,

                        targetTable,

                        msgId,

                        body,

                        msgKey,

                        _settings.MessageTypeId,

                        _cancellationToken);

                }

                else

                {

                    var tableName = ResolveMetamapTargetTable(mqSession, msgKey);

                    await dbHelper.SaveMsgToDataBaseAsync(

                        sessionId,

                        tableName,

                        msgId,

                        body,

                        msgKey,

                        _settings.MessageTypeId,

                        _cancellationToken);

                }



                if (_option.IsConfirmMsgAndRemoveFromQueue)

                    await channel.AcknowledgeMessageAsync(message.DeliveryTag);



                copied++;

                if (copied % 100 == 0)

                    Log.Information("CopyMsg progress: {Count} messages copied.", copied);

            }



            if (_settings.RunEtlAfterCopy)

            {

                Log.Information("CopyMsg running ETL after copy.");

                mqSession.RunEtlLoadProcedure("All");

            }



            Log.Information("CopyMsg finished. Copied {Count} messages.", copied);

        }

        catch (Exception ex)

        {

            errorMessage = ex.Message;

            Log.Error(ex, "CopyMsg failed.");

        }

        finally

        {

            if (channel is not null)

                await channel.CloseAsync();



            if (mqSession is not null)

                mqSession.FinishSessionProcessing(errorMessage ?? "", errorMessage is null);

        }



        return new CopyMessagesResult

        {

            CopiedCount = copied,

            ErrorMessage = errorMessage

        };

    }



    private string ResolveExplicitTargetTable(string targetTable)

    {

        if (_settings.AppendBufferSuffix)

            return BufferTableSqlHelper.AppendBufferSuffix(targetTable);



        return targetTable.Trim();

    }



    private string ResolveMetamapTargetTable(MQSession mqSession, string msgKey)

    {

        if (_settings.UseMetamapRouting)

            return mqSession.GetTableName(msgKey);



        return "msgqueue";

    }

}


