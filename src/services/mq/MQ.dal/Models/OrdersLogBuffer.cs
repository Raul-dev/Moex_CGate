using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;


[Table("orders_log_buffer", Schema = "crs")]
public partial class OrdersLogBuffer : MessageBuffer
{
/*
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

    [Column("msgtype_id")]
    public int? MsgTypeId { get; set; }

    [Column("is_error")]
    public bool IsError { get; set; }

    [Column("dt_create")]
    public DateTime CreateDate { get; set; }

    [Column("dt_update")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateDate { get; set; }
*/
}
