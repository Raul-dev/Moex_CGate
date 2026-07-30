using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MQ.dal.Models;

[Table("MetaMap", Schema = "mq")]
public partial class Metamap
{
    [Key]
    [Column("MetaMapId")]
    public short MetamapId { get; set; }

    [Column("MessageKey")]
    [MaxLength(128)]
    public string MsgKey { get; set; } = null!;

    [Column("TableName")]
    [MaxLength(128)]
    public string TableName { get; set; } = null!;

    [Column("MetaAdapterId")]
    public byte MetaAdapterId { get; set; }

    [Column("Namespace")]
    [MaxLength(256)]
    public string? Namespace { get; set; }

    [Column("NamespaceVersion")]
    [MaxLength(256)]
    public string? NamespaceVer { get; set; }

    [Column("EtlProcedure")]
    [MaxLength(256)]
    public string? EtlQuery { get; set; }


    [Column("ImportQuery")]
    [MaxLength(256)]
    public string? ImportQuery { get; set; }

    [Column("IsEnabled")]
    public bool IsEnable { get; set; }
}
