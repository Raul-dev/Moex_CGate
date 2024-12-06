using CommandLine;
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

        public override void InitBllOption(BllOption blloption)
        {
            base.InitBllOption(blloption);
            blloption.Verb = BllOptionVerb.Json;
            blloption.Iteration = Iteration;
            blloption.PauseMs = PauseMs;


    }
}
}
