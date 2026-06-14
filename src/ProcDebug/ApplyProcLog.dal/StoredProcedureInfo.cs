using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ApplyProcLog.dal
{
    public class StoredProcedureInfo
    {
        public int ObjectId { get; set; }
        public string SchemaName { get; set; }
        public string ProcedureName { get; set; }
        public System.Int16 ProcedureNumber { get; set; }
        public string ProcedureBody { get; set; }
        public string ProcedureParams { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime ModifyDate { get; set; }

        [NotMapped]
        public string AuditEnabledCode { get; set; } = "FullAuditEnabled";
    }
}
