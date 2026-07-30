from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import pyodbc

SQL_SERVER_ODBC_DRIVERS = (
    "ODBC Driver 18 for SQL Server",
    "ODBC Driver 17 for SQL Server",
    "ODBC Driver 13 for SQL Server",
    "SQL Server Native Client 11.0",
    "SQL Server",
)


def resolve_sql_server_driver(preferred: str = "") -> str:
    available = pyodbc.drivers()
    if preferred:
        if preferred in available:
            return preferred
        print(
            f"Warning: ODBC driver '{preferred}' is not installed, trying another SQL Server driver.",
            file=sys.stderr,
        )

    for driver in SQL_SERVER_ODBC_DRIVERS:
        if driver in available:
            return driver

    available_text = ", ".join(available) if available else "(none)"
    raise RuntimeError(
        "No SQL Server ODBC driver found. Install one of: "
        + ", ".join(SQL_SERVER_ODBC_DRIVERS)
        + f". Installed drivers: {available_text}"
    )


@dataclass
class RabbitMqConfig:
    host: str = "localhost"
    port: int = 5672
    virtual_host: str = "/"
    username: str = "guest"
    password: str = "guest"
    queue: str = ""


@dataclass
class DatabaseConfig:
    server: str = "localhost"
    database: str = "CGate"
    user: str = ""
    password: str = ""
    driver: str = ""

    def resolved_driver(self) -> str:
        return resolve_sql_server_driver(self.driver)

    def connection_string(self) -> str:
        driver = self.resolved_driver()
        return (
            f"DRIVER={{{driver}}};"
            f"SERVER={self.server};"
            f"DATABASE={self.database};"
            f"UID={self.user};"
            f"PWD={self.password};"
            "TrustServerCertificate=yes;"
        )


@dataclass
class CopyConfig:
    max_messages: int = 0
    target_table: str = ""
    use_metamap_routing: bool = True
    meta_adapter_id: int = 1
    data_source_id: int = 1
    clear_queue: bool = True
    pause_ms_when_empty: int = 100
    empty_poll_attempts: int = 3
    message_type_id: int = 2
    ensure_buffer_table: bool = True
    append_buffer_suffix: bool = True
    truncate_before_copy: bool = False


@dataclass
class AppConfig:
    rabbitmq: RabbitMqConfig
    database: DatabaseConfig
    copy: CopyConfig

    @classmethod
    def load(cls, path: Path) -> AppConfig:
        raw = json.loads(path.read_text(encoding="utf-8"))
        return cls.from_dict(raw)

    @classmethod
    def from_dict(cls, raw: dict[str, Any]) -> AppConfig:
        rabbit = raw.get("rabbitmq", {})
        db = raw.get("database", {})
        copy = dict(raw.get("copy", {}))
        if "clear_queue" not in copy and "ack" in copy:
            copy["clear_queue"] = copy["ack"]
        return cls(
            rabbitmq=RabbitMqConfig(**{k: v for k, v in rabbit.items() if k in RabbitMqConfig.__dataclass_fields__}),
            database=DatabaseConfig(**{k: v for k, v in db.items() if k in DatabaseConfig.__dataclass_fields__}),
            copy=CopyConfig(**{k: v for k, v in copy.items() if k in CopyConfig.__dataclass_fields__}),
        )
