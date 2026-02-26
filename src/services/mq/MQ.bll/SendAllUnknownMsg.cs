using Serilog;
using MQ.bll.Common;
using Microsoft.Extensions.Configuration;
using MQ.dal;
using static MQ.dal.DBHelper;
using Microsoft.IdentityModel.Tokens;
using MQ.bll.Kafka;
using MQ.bll.RabbitMQ;

namespace MQ.bll
{
    public class SendAllUnknownMsg
    {

        BllOption bo;
        DBHelper dbHelper;
        CancellationToken _cancellationToken;
        //RabbitMQSettings _MQSettings;

        public SendAllUnknownMsg(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            bo = bllOption;
            _cancellationToken = cancellationToken;
            //_MQSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Нет конфига");
            dbHelper = new DBHelper(bo.DataBaseServSettings?.ServerName ?? "", bo.DataBaseServSettings?.DataBase ?? "", bo.DataBaseServSettings?.Port ?? 0, bo.ServerType, bo.DataBaseServSettings?.User ?? "", bo.DataBaseServSettings?.Password ?? "");
        }
        public async Task ProcessLauncher()
        {

            if (bo.Iteration > 0)
                for (int i = 0; i < bo.Iteration; i++)
                {
                    await MQProcess();
                }
            else
                await MQProcess();

        }

        public async Task MQProcess()
        {
            List<MsgQueueItem> mq = dbHelper.GetMsgqueueItems();
            string ErrorMsg = "";
            IQueueChannel channel;
            try
            {
                channel = bo.IsKafka ? new KafkaChannel(bo, _cancellationToken) : new RabbitMQChannel(bo, _cancellationToken);
                Random rnd = new Random();

                int iCount = 0;
                
                await channel.InitSetup();

                Log.Information(@$"We are starting to send {mq.Count} messages to the MQ.");
                foreach (var item in mq)
                {
                    //Log.Information("MsgOrder={0}, MsgKey={1} MsgLen={2}.", item.MsgOrder, item.MsgKey, item.Msg!.Length);
                    if (item.Msg.IsNullOrEmpty())
                    {
                        Log.Warning("Null MsgOrder={0}, MsgKey={1}.", item.MsgOrder, item.MsgKey);
                        continue;
                    }
                    await channel.PublishMessageAsync(item.MsgKey!, item.Msg!);
                    iCount++;
                    if (iCount % 10000 == 0)
                    {
                        Log.Information(@$"Send {iCount} messages.");
                    }
                    if (bo.PauseMs != 0)
                    {
                        int ps = rnd.Next(bo.PauseMs / 2, bo.PauseMs);
                        Log.Information(@$"Send {iCount} message. Pause {ps} ms");
                        _cancellationToken.WaitHandle.WaitOne(ps);
                    }
                }
                Log.Information(@$"Send {iCount} messages.");
                var count = await channel.MessageCountAsync();
                Log.Information(@$"MQ total mesages {count} .");
                
                await channel.CloseAsync();
            }
            catch (Exception ex)
            {
                Log.Error("MQProcess:" + ex.Message);
                ErrorMsg = ex.Message;
            }

        }

    }
}
