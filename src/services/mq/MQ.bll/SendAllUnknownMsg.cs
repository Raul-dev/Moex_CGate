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

        BllOption option;
        DBHelper dbHelper;
        CancellationToken _cancellationToken;
        //RabbitMQSettings _MQSettings;

        public SendAllUnknownMsg(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            option = bllOption;
            _cancellationToken = cancellationToken;
            //_MQSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Нет конфига");
            dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);
        }
        public async Task ProcessLauncher()
        {

            if (option.Iteration > 0)
                for (int i = 0; i < option.Iteration; i++)
                {
                    await MQProcess();
                }
            else
                await MQProcess();

        }

        public async Task MQProcess()
        {
            List<MsgQueueItem> mq = dbHelper.GetMsgqueueItems();
            Log.Warning($"{mq.Count}");
           string ErrorMsg = "";
            IQueueChannel channel;
            try
            {
                channel = option.IsKafka ? new KafkaChannel(option) : new RabbitMQChannel(option);
                Random rnd = new Random();

                int iCount = 0;
                
                await channel.InitSetup(_cancellationToken);

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
                    if (option.PauseMs != 0)
                    {
                        Thread.SpinWait(rnd.Next(option.PauseMs / 2, option.PauseMs));
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
