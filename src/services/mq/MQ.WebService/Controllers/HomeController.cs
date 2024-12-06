using Microsoft.AspNetCore.Mvc;
using MQ.bll;
using MQ.bll.Common;
using MQ.dal.Models;
using Serilog;

namespace MQ.WebService.Controllers
{
    public class MqConfig
    {
        public MqConfig() { }
        public string SessionMode { get; set; }
        public string SqlServerType { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        //ConsumeScopedServiceHostedService ConsumeService;
        IConfiguration _config;

        public HomeController(IConfiguration config)
        {
           
            _config = config;
        }

        
        [HttpGet, Route("Reset", Name = "Reset")]
        public async Task<IActionResult> Reset(string SessionMode ="FullMode")
        {
            //"BufferOnly"
            Log.Logger.Debug($"Reset {SessionMode}");
            SingletonProcessingService.Instance.Stop();

            //_ = SingletonProcessingService.Instance.Start(_config);
            await SingletonProcessingService.Instance.Start(_config, SessionMode);
            
            return NoContent();
        }
        
        [HttpGet, Route("Start", Name = "Start")]
        public async Task<IActionResult> Start()
        {
            Log.Logger.Debug($"Start");
            if(!SingletonProcessingService.Instance.GetStatus())
                await SingletonProcessingService.Instance.Start(_config);

            return NoContent();
        }
        [HttpGet, Route("Stop", Name = "Stop")]
        public async Task<IActionResult> Stop()
        {
            Log.Logger.Debug($"Stop");
            SingletonProcessingService.Instance.Stop();


            return NoContent();
        }
        [HttpGet, Route("Status", Name = "Status")]
        public async Task<IActionResult> Status()
        {
            Log.Logger.Debug($"Status");
            if (SingletonProcessingService.Instance.GetStatus())
                return Ok();
            return NoContent();
        }

        [HttpGet, Route("Config", Name = "Config")]
        public async Task<ActionResult<MqConfig>> GetConfig()
        {
            Log.Logger.Debug($"GetConfig");
            DataBaseSettings databaseSettings = _config.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
            var mqConfig = new MqConfig();
            mqConfig.SessionMode = databaseSettings.SessionMode;
            if (databaseSettings.SessionMode == "FullMode")
                mqConfig.SessionMode ="FullMode";
            else if (databaseSettings.SessionMode == "BufferOnly")
                mqConfig.SessionMode = "BufferOnly";
            else
                mqConfig.SessionMode = "InvalidMode";

            if (databaseSettings.ServerType == "psql")
            {
                mqConfig.SqlServerType = "psql";
            }
            else
            {
                mqConfig.SqlServerType = "mssql";
            }
            return mqConfig;
        }
    }
}
