from __future__ import annotations

import re

_IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def parse_qualified_name(qualified_name: str) -> tuple[str, str]:
    trimmed = qualified_name.strip().strip("[]")
    if "." in trimmed:
        schema, table = trimmed.split(".", 1)
        return _validate(schema), _validate(table)
    return "mq", _validate(trimmed)


def append_buffer_suffix(qualified_name: str) -> str:
    schema, table = parse_qualified_name(qualified_name)
    if table.lower().endswith("buffer"):
        return f"{schema}.{table}"
    return f"{schema}.{table}Buffer"


def build_create_buffer_table_sql(schema: str, table: str) -> str:
    pk = f"PK_{schema}_{table}"
    return f"""
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'{schema}' AND t.name = N'{table}'
)
BEGIN
    CREATE TABLE [{schema}].[{table}] (
        [BufferId] BIGINT IDENTITY(1,1) NOT NULL,
        [SessionId] BIGINT NOT NULL,
        [MessageKey] NVARCHAR(256) NOT NULL,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [MessageBody] VARCHAR(MAX) NULL,
        [MessageTypeId] TINYINT NULL,
        [IsError] BIT NOT NULL CONSTRAINT [DF_{schema}_{table}_is_error] DEFAULT (0),
        [CreatedAt] DATETIME2(4) NOT NULL CONSTRAINT [DF_{schema}_{table}_dt_create] DEFAULT (SYSDATETIME()),
        [UpdatedAt] DATETIME2(4) NOT NULL CONSTRAINT [DF_{schema}_{table}_dt_update] DEFAULT ('1900-01-01'),
        CONSTRAINT [{pk}] PRIMARY KEY CLUSTERED ([BufferId] ASC)
    );
END
"""


def _validate(value: str) -> str:
    if not _IDENTIFIER.match(value):
        raise ValueError(f"Invalid SQL identifier: {value}")
    return value
