using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;

[Table("MessageQueue", Schema = "mq")]
public partial class MsgQueue
{
    [Key]
    [Column("BufferId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long BufferId { get; set; }

    [Column("SessionId")]
    public long SessionId { get; set; }

    [Column("MessageId")]
    public Guid? MsgId { get; set; }

    [Column("MessageBody")]
    public string? Msg { get; set; }

    [Column("MessageKey")]
    [MaxLength(128)]
    public string? MsgKey { get; set; }

    [Column("CreatedAt")]
    public DateTime UpdateDate { get; set; }
}
