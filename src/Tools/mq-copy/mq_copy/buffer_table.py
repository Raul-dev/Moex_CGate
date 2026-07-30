from __future__ import annotations

import re

_IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def parse_qualified_name(qualified_name: str) -> tuple[str, str]:
    trimmed = qualified_name.strip().strip("[]")
    if "." in trimmed:
        schema, table = trimmed.split(".", 1)
        return _validate(schema), _validate(table)
    return "dbo", _validate(trimmed)


def append_buffer_suffix(qualified_name: str) -> str:
    schema, table = parse_qualified_name(qualified_name)
    if table.lower().endswith("_buffer"):
        return f"{schema}.{table}"
    return f"{schema}.{table}_buffer"


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
        [buffer_id] BIGINT IDENTITY(1,1) NOT NULL,
        [session_id] BIGINT NOT NULL,
        [msg_key] NVARCHAR(256) NOT NULL,
        [msg_id] UNIQUEIDENTIFIER NOT NULL,
        [msg] VARCHAR(MAX) NULL,
        [msgtype_id] TINYINT NULL,
        [is_error] BIT NOT NULL CONSTRAINT [DF_{schema}_{table}_is_error] DEFAULT (0),
        [dt_create] DATETIME2(4) NOT NULL CONSTRAINT [DF_{schema}_{table}_dt_create] DEFAULT (SYSDATETIME()),
        [dt_update] DATETIME2(4) NOT NULL CONSTRAINT [DF_{schema}_{table}_dt_update] DEFAULT ('1900-01-01'),
        CONSTRAINT [{pk}] PRIMARY KEY CLUSTERED ([buffer_id] ASC)
    );
END
"""


def _validate(value: str) -> str:
    if not _IDENTIFIER.match(value):
        raise ValueError(f"Invalid SQL identifier: {value}")
    return value
