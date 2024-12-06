using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;

[Table("msgqueue")]
public partial class Msgqueue
{
    [Key]
    [Column("buffer_id")]
    public long BufferId { get; set; }

    [Column("session_id")]
    public long SessionId { get; set; }

    [Column("msg_id")]
    public Guid? MsgId { get; set; }

    [Column("msg")]
    public string? Msg { get; set; }

    [Column("msg_key")]
    [MaxLength(128)]
    public string? MsgKey { get; set; }

    [Column("dt_create")]
    public DateTime UpdateDate { get; set; }
}
