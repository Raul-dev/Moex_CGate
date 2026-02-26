using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MQ.bll;
using MQ.bll.Common;
using MQ.OptionModels;

namespace MQ.OptionModels
{
    [Verb("GetMsg", isDefault: false, HelpText = "Get Message.")]
    public class GetMsgOptions : BaseOptions
    {

        [Option('r', "Confirm Msg and remove it from RabbitMQ Queue.", Required = false, Default = true, HelpText = "IsRemoveFromQueue.")]
        public bool? IsConfirmMsgAndRemoveFromQueue { get; set; }

        [Option('g', "Get session mode.", Required = false, Default = "FullMode", HelpText = "Get session mode. bufferonly, whileget, etlonly")]
        public string SessionMode { get; set; } = "FullMode";

        public override void InitBllOption(BllOption blloption, IConfiguration configuration)
        {
            base.InitBllOption(blloption, configuration);
            
            
            blloption.IsConfirmMsgAndRemoveFromQueue = IsConfirmMsgAndRemoveFromQueue ?? false;
            switch (SessionMode.ToLower()) {
                case "bufferonly":
                    blloption.DataBaseServSettings.SessionMode = SessionModeEnum.BufferOnly;
                    break;
                case "whileget":
                    blloption.DataBaseServSettings.SessionMode = SessionModeEnum.WhileGet;
                    break;
                case "etlonly":
                    blloption.DataBaseServSettings.SessionMode = SessionModeEnum.EtlOnly;
                    break;

                default:
                    blloption.DataBaseServSettings.SessionMode = SessionModeEnum.FullMode;
                    break;
            }
            blloption.RabbitMQServSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Have not config RabbitMQSettings");
            if (IsKafka)
            blloption.KafkaServSettings = configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>() ?? throw new Exception("Have not config KafkaSettings");
        }
    }
}
