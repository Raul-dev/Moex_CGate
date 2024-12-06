using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MQ.dal.Models
{
    [Keyless]
    public partial class SessionId
    {
        [Column("session_id", Order = 0)]
        public long Session_Id { get; set; }
    }
}
