using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MQ.dal.Models;
[Table("metadata")]
public partial class Metadata
{
    [Key]
    public Guid Nkey { get; set; }

    [Column("namespace")]
    [MaxLength(256)]
    public string? Namespace { get; set; }

    [Column("namespace_ver")]
    [MaxLength(256)]
    public string? NamespaceVer { get; set; }

    [Column("msg")]
    public string? Msg { get; set; }

    [Column("type")]
    [MaxLength(128)]
    public string? Type { get; set; }
    
    [Column("dt_create")]
    public DateTime UpdateDate { get; set; }
}
