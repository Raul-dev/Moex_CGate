from __future__ import annotations

import time
import uuid

import pika
import pyodbc

from mq_copy.buffer_table import append_buffer_suffix, build_create_buffer_table_sql, parse_qualified_name
from mq_copy.config import AppConfig


class MqCopier:
    def __init__(self, config: AppConfig):
        self._config = config
        self._metamap: dict[str, str] = {}

    def run(self) -> int:
        cfg = self._config
        copied = 0
        empty_polls = 0
        use_explicit_buffer = bool(cfg.copy.target_table)

        parameters = pika.ConnectionParameters(
            host=cfg.rabbitmq.host,
            port=cfg.rabbitmq.port,
            virtual_host=cfg.rabbitmq.virtual_host,
            credentials=pika.PlainCredentials(cfg.rabbitmq.username, cfg.rabbitmq.password),
        )

        with pyodbc.connect(cfg.database.connection_string(), autocommit=False) as conn:
            target_table = None
            if use_explicit_buffer:
                target_table = (
                    append_buffer_suffix(cfg.copy.target_table)
                    if cfg.copy.append_buffer_suffix
                    else cfg.copy.target_table
                )
                if cfg.copy.ensure_buffer_table:
                    self._ensure_buffer_table(conn, target_table)
                    print(f"CopyMsg ensured buffer table {target_table} exists.")
                if cfg.copy.truncate_before_copy:
                    schema, table = parse_qualified_name(target_table)
                    conn.cursor().execute(f"TRUNCATE TABLE [{schema}].[{table}]")
                    conn.commit()
            elif cfg.copy.use_metamap_routing:
                self._metamap = self._load_metamap(conn, cfg.copy.meta_adapter_id)

            session_id = self._start_session(conn, cfg.copy.data_source_id)
            print(f"Start mq Session Id = {session_id}")

            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()

            queue_depth = channel.queue_declare(queue=cfg.rabbitmq.queue, passive=True).method.message_count
            target_label = target_table if use_explicit_buffer and target_table else "metamap"
            max_label = "all" if cfg.copy.max_messages == 0 else str(cfg.copy.max_messages)
            print(
                f"CopyMsg started. Queue={cfg.rabbitmq.queue}, depth={queue_depth}, "
                f"maxMessages={max_label}, targetTable={target_label}, clearQueue={cfg.copy.clear_queue}"
            )

            try:
                while True:
                    if cfg.copy.max_messages > 0 and copied >= cfg.copy.max_messages:
                        break

                    method, properties, body = channel.basic_get(
                        queue=cfg.rabbitmq.queue,
                        auto_ack=False,
                    )

                    if method is None:
                        empty_polls += 1
                        if empty_polls >= cfg.copy.empty_poll_attempts:
                            break
                        time.sleep(cfg.copy.pause_ms_when_empty / 1000.0)
                        continue

                    empty_polls = 0
                    # properties.message_id -> [msg_id], properties.type -> [msg_key]
                    msg_key = (properties.type if properties else None) or "Unknown"
                    msg_id = (properties.message_id if properties else None) or str(uuid.uuid4())
                    text = body.decode("utf-8")
                    print(f"CopyMsg message: msg_id={msg_id}, msg_key={msg_key}")

                    if use_explicit_buffer and target_table:
                        self._insert_buffer_message(
                            conn, session_id, target_table, msg_id, text, msg_key, cfg.copy.message_type_id
                        )
                    else:
                        table_name = self._resolve_metamap_table(msg_key)
                        self._insert_legacy_message(
                            conn, session_id, table_name, msg_id, text, msg_key, cfg.copy.message_type_id
                        )

                    conn.commit()

                    if cfg.copy.clear_queue:
                        channel.basic_ack(method.delivery_tag)

                    copied += 1
                    if copied % 100 == 0:
                        print(f"CopyMsg progress: {copied} messages copied.")

                self._finish_session(conn, session_id, cfg.copy.data_source_id)
                conn.commit()
            finally:
                channel.close()
                connection.close()

        print(f"CopyMsg finished. Copied {copied} messages.")
        return copied

    @staticmethod
    def _ensure_buffer_table(conn: pyodbc.Connection, qualified_name: str) -> None:
        schema, table = parse_qualified_name(qualified_name)
        conn.cursor().execute(build_create_buffer_table_sql(schema, table))
        conn.commit()

    def _resolve_metamap_table(self, msg_key: str) -> str:
        if self._config.copy.use_metamap_routing:
            return self._metamap.get(msg_key, self._metamap.get("Unknown", "mq.MessageQueue"))
        return "mq.MessageQueue"

    @staticmethod
    def _load_metamap(conn: pyodbc.Connection, meta_adapter_id: int) -> dict[str, str]:
        cursor = conn.cursor()
        cursor.execute(
            """
            SELECT [MessageKey], [TableName]
            FROM [mq].[MetaMap]
            WHERE [IsEnabled] = 1 AND [MetaMapId] = ?
            """,
            meta_adapter_id,
        )
        rows = cursor.fetchall()
        mapping = {row.MessageKey: row.TableName for row in rows}
        if "Unknown" not in mapping:
            raise RuntimeError(
                f"metamap for adapter {meta_adapter_id} has no 'Unknown' key; configure routing first."
            )
        return mapping

    @staticmethod
    def _fetch_first_row(cursor: pyodbc.Cursor) -> tuple | None:
        while True:
            if cursor.description:
                return cursor.fetchone()
            if not cursor.nextset():
                return None

    @staticmethod
    def _start_session(conn: pyodbc.Connection, data_source_id: int) -> int:
        cursor = conn.cursor()
        cursor.execute(
            """
            EXEC [mq].[sp_SaveSessionState]
                @SessionId = NULL,
                @DataSourceId = ?,
                @SessionStateId = 1,
                @ErrorMessage = NULL
            """,
            data_source_id,
        )
        row = MqCopier._fetch_first_row(cursor)
        if row is None or row[0] is None:
            raise RuntimeError("Failed to start session via sp_SaveSessionState.")
        return int(row[0])

    @staticmethod
    def _finish_session(conn: pyodbc.Connection, session_id: int, data_source_id: int) -> None:
        cursor = conn.cursor()
        cursor.execute(
            """
            EXEC [mq].[sp_SaveSessionState]
                @SessionId = ?,
                @DataSourceId = ?,
                @SessionStateId = 2,
                @ErrorMessage = NULL
            """,
            session_id,
            data_source_id,
        )

    @staticmethod
    def _insert_buffer_message(
        conn: pyodbc.Connection,
        session_id: int,
        qualified_name: str,
        msg_id: str,
        body: str,
        msg_key: str,
        message_type_id: int,
    ) -> None:
        schema, table = parse_qualified_name(qualified_name)
        cursor = conn.cursor()
        cursor.execute(
            f"""
            INSERT INTO [{schema}].[{table}]
                ([SessionId], [MessageKey], [MessageId], [MessageBody], [MessageTypeId])
            VALUES (?, ?, ?, ?, ?)
            """,
            session_id,
            msg_key,
            uuid.UUID(msg_id),
            body,
            message_type_id,
        )

    @staticmethod
    def _insert_legacy_message(
        conn: pyodbc.Connection,
        session_id: int,
        table_name: str,
        msg_id: str,
        body: str,
        msg_key: str,
        message_type_id: int,
    ) -> None:
        cursor = conn.cursor()
        if "messagequeue" in table_name.lower():
            cursor.execute(
                f"INSERT INTO {table_name} ([SessionId], [MessageId], [MessageBody], [MessageKey]) VALUES (?, ?, ?, ?)",
                session_id,
                uuid.UUID(msg_id),
                body,
                msg_key,
            )
        else:
            cursor.execute(
                f"INSERT INTO {table_name} ([SessionId], [MessageId], [MessageBody], [MessageTypeId]) VALUES (?, ?, ?, ?)",
                session_id,
                uuid.UUID(msg_id),
                body,
                message_type_id,
            )
