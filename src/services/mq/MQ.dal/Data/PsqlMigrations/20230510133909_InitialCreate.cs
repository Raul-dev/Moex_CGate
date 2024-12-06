using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MQ.dal.Data.PsqlMigrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "codegen",
                columns: table => new
                {
                    codegen_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(256)", maxLength: 256, nullable: false),
                    schema = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    table_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    enable_type = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codegen", x => x.codegen_id);
                });

            migrationBuilder.CreateTable(
                name: "metadata",
                columns: table => new
                {
                    Nkey = table.Column<Guid>(type: "uuid", nullable: false),
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(256)", maxLength: 256, nullable: true),
                    namespace_ver = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    msg = table.Column<string>(type: "text", nullable: true),
                    meta = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata", x => x.Nkey);
                });

            migrationBuilder.CreateTable(
                name: "metamap",
                columns: table => new
                {
                    metamap_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    msg_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    table_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    message_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(256)", maxLength: 256, nullable: true),
                    namespace_ver = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    etl_query = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    import_query = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_enable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metamap", x => x.metamap_id);
                });

            migrationBuilder.CreateTable(
                name: "msgqueue",
                columns: table => new
                {
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    msg_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buffer_id = table.Column<int>(type: "integer", nullable: false),
                    msg = table.Column<string>(type: "text", nullable: true),
                    msg_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    dt_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_msgqueue", x => new { x.session_id, x.msg_id, x.buffer_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codegen");

            migrationBuilder.DropTable(
                name: "metadata");

            migrationBuilder.DropTable(
                name: "metamap");

            migrationBuilder.DropTable(
                name: "msgqueue");
        }
    }
}
