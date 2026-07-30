using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MQ.dal.Models;

public partial class MessageBuffer
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

    [Column("MessageTypeId")]
    public int? MsgTypeId { get; set; }

    [Column("IsError")]
    public bool IsError { get; set; }

    [Column("CreatedAt")]
    public DateTime CreateDate { get; set; }

    [Column("UpdatedAt")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateDate { get; set; }

}
