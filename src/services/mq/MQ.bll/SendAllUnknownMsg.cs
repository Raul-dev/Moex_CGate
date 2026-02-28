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

        BllOption _option;
        DBHelper dbHelper;
        CancellationToken _cancellationToken;
        //RabbitMQSettings _MQSettings;

        public SendAllUnknownMsg(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            _option = bllOption;
            _cancellationToken = cancellationToken;
            //_MQSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Нет конфига");
            dbHelper = new DBHelper(_option.DataBaseServSettings?.ServerName ?? "", _option.DataBaseServSettings?.DataBase ?? "", _option.DataBaseServSettings?.Port ?? 0, _option.ServerType, _option.DataBaseServSettings?.User ?? "", _option.DataBaseServSettings?.Password ?? "");
        }
        public async Task ProcessLauncher()
        {

            if (_option.Iteration > 0)
                for (int i = 0; i < _option.Iteration; i++)
                {
                    await MQProcess();
                }
            else
                await MQProcess();

        }

        public async Task MQProcess()
        {
            string ErrorMsg = "";
            try
            {
                List<MsgQueueItem> mq = dbHelper.GetMsgqueueItems();
                
                IQueueChannel channel;
    
                channel = _option.IsKafka ? new KafkaChannel(_option, _cancellationToken) : new RabbitMQChannel(_option, _cancellationToken);
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
                    if (_option.PauseMs != 0)
                    {
                        int ps = rnd.Next(_option.PauseMs / 2, _option.PauseMs);
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
