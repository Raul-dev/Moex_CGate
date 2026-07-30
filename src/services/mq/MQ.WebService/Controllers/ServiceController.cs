using Microsoft.AspNetCore.Mvc;
using MQ.bll;
using MQ.bll.Common;
using Serilog;

namespace MQ.WebService.Controllers
{
    public class MqConfig
    {
        public MqConfig() { }
        public string SessionMode { get; set; } = "";
        public string SqlServerType { get; set; } = "";
    }

    [ApiController]
    [Route("v1/mq/service")]
    public class ServiceController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ServiceController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset([FromQuery] string sessionMode = "FullMode")
        {
            Log.Logger.Debug("Reset {SessionMode}", sessionMode);
            SingletonProcessingService.Instance.Stop();
            await SingletonProcessingService.Instance.Start(_config, sessionMode);

            return NoContent();
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start()
        {
            Log.Logger.Debug("Start");
            if (!SingletonProcessingService.Instance.GetStatus())
                await SingletonProcessingService.Instance.Start(_config);

            return Ok();
        }

        [HttpPost("stop")]
        public async Task<IActionResult> Stop()
        {
            Log.Logger.Debug("Stop");
            SingletonProcessingService.Instance.Stop();

            return Ok();
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            Log.Logger.Debug("Status");
            if (SingletonProcessingService.Instance.GetStatus())
                return Ok();

            var customResponse = new
            {
                Code = 503,
                Message = "Service is not running."
            };
            return StatusCode(503, customResponse);
        }

        [HttpGet("config")]
        public ActionResult<MqConfig> GetConfig()
        {
            Log.Logger.Debug("GetConfig");
            DataBaseSettings databaseSettings = _config.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
            var mqConfig = new MqConfig
            {
                SessionMode = databaseSettings.SessionMode.ToString(),
                SqlServerType = databaseSettings.ServerType.ToString()
            };
            if (string.IsNullOrEmpty(mqConfig.SessionMode))
                mqConfig.SessionMode = "InvalidMode";

            return mqConfig;
        }
    }
}
