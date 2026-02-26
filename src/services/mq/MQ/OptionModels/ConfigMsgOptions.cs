using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MQ.bll.Common;
using MQ.OptionModels;

namespace MQ.OptionModels
{
    [Verb("Config", HelpText = "Get settings from config.")]
    public class ConfigMsgOptions: BaseOptions
    {
        [Option('c', "Config name.", Required = false, Default = "ServiceGetMsgSettings", HelpText = "Config name.")]
        public string ConfigName { get; set; } = "ServiceGetMsgSettings";
    }
}
