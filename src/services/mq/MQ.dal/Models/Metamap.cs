using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MQ.dal.Models;

[Table("metamap")]
public partial class Metamap
{
    [Key]
    [Column("metamap_id")]
    public short MetamapId { get; set; }

    [Column("msg_key")]
    [MaxLength(128)]
    public string MsgKey { get; set; } = null!;

    [Column("table_name")]
    [MaxLength(128)]
    public string TableName { get; set; } = null!;

    [Column("metaadapter_id")]
    public byte MetaAdapterId { get; set; }

    [Column("namespace")]
    [MaxLength(256)]
    public string? Namespace { get; set; }

    [Column("namespace_ver")]
    [MaxLength(256)]
    public string? NamespaceVer { get; set; }

    [Column("etl_query")]
    [MaxLength(256)]
    public string? EtlQuery { get; set; }


    [Column("import_query")]
    [MaxLength(256)]
    public string? ImportQuery { get; set; }

    [Column("is_enable")]
    public bool IsEnable { get; set; }
}
