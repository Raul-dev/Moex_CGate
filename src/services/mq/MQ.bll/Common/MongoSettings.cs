using System;
using System.Collections.Generic;
using System.Text;

namespace MQ.bll.Common
{
#pragma warning disable CS8618
    public class MongoSettings
    {
        public string Url { get; set; } = "localhost:27017/";
        public string User { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public string DataBase { get; set; } = "rbbt";
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
