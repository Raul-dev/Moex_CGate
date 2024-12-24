using CommandLine;
using Microsoft.Extensions.Configuration;
using MQ.bll.Common;
using MQ.OptionModels;

namespace MQ.OptionModels
{
    [Verb("SendMsg",  HelpText = "Send message from DB to RabbitMQ.")]
    public class SendMsgOptions : BaseOptions
    {

        [Option('i', "Iteration.", Required = false, Default = 0, HelpText = "Iteration.")]
        public int Iteration { get; set; }

        [Option('a', "Pause ms.", Required = false, Default = 0, HelpText = "Pause ms")]
        public int PauseMs { get; set; }

        public override void InitBllOption(BllOption blloption, IConfiguration configuration)
        {
            base.InitBllOption(blloption, configuration);
            blloption.Verb = BllOptionVerb.Json;
            blloption.Iteration = Iteration;
            blloption.PauseMs = PauseMs;
            blloption.RabbitMQServSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Have not config RabbitMQSettings");
            blloption.KafkaServSettings = configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>() ?? throw new Exception("Have not config KafkaSettings");
        }
    }
}
