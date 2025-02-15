﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;

[Table("msgqueue")]
public partial class MsgQueue
{
    [Key]
    [Column("buffer_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
