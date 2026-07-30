using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;


[Table("OrdersLogBuffer", Schema = "crs")]
public partial class OrdersLogBuffer : MessageBuffer
{
}
