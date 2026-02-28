using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Extensions
{
    public class CustomPropertyEnricher : ILogEventEnricher
    {
        private readonly string _propertyValue;
        private readonly string _propertyName;
        //public const string PropertyName ;

        public CustomPropertyEnricher(string propertyName,string propertyValue)
        {
            _propertyValue = propertyValue;
            _propertyName = propertyName;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var property = propertyFactory.CreateProperty(_propertyName, _propertyValue);
            logEvent.AddPropertyIfAbsent(property);
        }


    }
}
