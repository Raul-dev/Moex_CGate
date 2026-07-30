from __future__ import annotations

import argparse
import sys
from pathlib import Path

from mq_copy.config import AppConfig
from mq_copy.copy import MqCopier


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Copy messages from RabbitMQ queue to MS SQL Server buffer table.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )

    parser.add_argument(
        "-c",
        "--config",
        default=None,
        help="Path to JSON config. Omit to pass all settings via CLI.",
    )

    rabbit = parser.add_argument_group("RabbitMQ")
    rabbit.add_argument("--rabbit-host", help="RabbitMQ host.")
    rabbit.add_argument("--rabbit-port", type=int, help="RabbitMQ port.")
    rabbit.add_argument("--rabbit-vhost", help="RabbitMQ virtual host.")
    rabbit.add_argument("--rabbit-user", help="RabbitMQ username.")
    rabbit.add_argument(
        "--rabbit-password",
        help="RabbitMQ password (can also be set in copy-config.json -> rabbitmq.password).",
    )
    rabbit.add_argument("--rabbit-queue", help="RabbitMQ queue name.")

    db = parser.add_argument_group("MS SQL Server")
    db.add_argument("-s", "--db-server", help="SQL Server host, e.g. localhost,54321.")
    db.add_argument("-d", "--db-name", help="Database name, e.g. CGate.")
    db.add_argument("-u", "--db-user", help="SQL login.")
    db.add_argument("-w", "--db-password", help="SQL password.")
    db.add_argument("--db-driver", help="ODBC driver name.")

    copy = parser.add_argument_group("Copy options")
    copy.add_argument("-n", "--max-messages", type=int, help="Max messages to copy (0 = all).")
    copy.add_argument("-q", "--target-table", help="Target table base name (dbo.Upload -> dbo.Upload_buffer).")
    copy.add_argument(
        "-r",
        "--ack",
        choices=["true", "false"],
        help="Deprecated alias for --clear-queue.",
    )
    copy.add_argument(
        "-g",
        "--clear-queue",
        choices=["true", "false"],
        help="Clear RabbitMQ queue: remove message after save (ACK). false = keep in queue.",
    )
    copy.add_argument(
        "-f",
        "--truncate",
        choices=["true", "false"],
        help="Truncate target buffer table before copy.",
    )
    copy.add_argument(
        "-x",
        "--no-metamap",
        action="store_true",
        help="Disable metamap routing; use msgqueue table.",
    )
    copy.add_argument(
        "-b",
        "--no-create-table",
        action="store_true",
        help="Do not auto-create buffer table.",
    )
    copy.add_argument(
        "-z",
        "--no-buffer-suffix",
        action="store_true",
        help="Do not append _buffer suffix to target table name.",
    )
    return parser


def load_config(config_path: Path | None) -> AppConfig:
    if config_path is None:
        return AppConfig.from_dict({})
    if not config_path.exists():
        raise FileNotFoundError(
            f"Config file not found: {config_path.resolve()}\n"
            "Create it: copy copy-config.example.json copy-config.json"
        )
    return AppConfig.load(config_path)


def apply_cli_overrides(config: AppConfig, args: argparse.Namespace) -> None:
    if args.rabbit_host is not None:
        config.rabbitmq.host = args.rabbit_host
    if args.rabbit_port is not None:
        config.rabbitmq.port = args.rabbit_port
    if args.rabbit_vhost is not None:
        config.rabbitmq.virtual_host = args.rabbit_vhost
    if args.rabbit_user is not None:
        config.rabbitmq.username = args.rabbit_user
    if args.rabbit_password is not None:
        config.rabbitmq.password = args.rabbit_password
    if args.rabbit_queue is not None:
        config.rabbitmq.queue = args.rabbit_queue

    if args.db_server is not None:
        config.database.server = args.db_server
    if args.db_name is not None:
        config.database.database = args.db_name
    if args.db_user is not None:
        config.database.user = args.db_user
    if args.db_password is not None:
        config.database.password = args.db_password
    if args.db_driver is not None:
        config.database.driver = args.db_driver

    if args.max_messages is not None:
        config.copy.max_messages = args.max_messages
    if args.target_table:
        config.copy.target_table = args.target_table
    if args.ack is not None:
        config.copy.clear_queue = args.ack == "true"
    if args.clear_queue is not None:
        config.copy.clear_queue = args.clear_queue == "true"
    if args.no_metamap:
        config.copy.use_metamap_routing = False
        if not config.copy.target_table:
            config.copy.target_table = "msgqueue"
    if args.truncate is not None:
        config.copy.truncate_before_copy = args.truncate == "true"
    if args.no_create_table:
        config.copy.ensure_buffer_table = False
    if args.no_buffer_suffix:
        config.copy.append_buffer_suffix = False


def validate_config(config: AppConfig, config_path: Path | None) -> None:
    if not config.rabbitmq.queue:
        hint = (
            f"Set rabbitmq.queue in {config_path.resolve()} or pass --rabbit-queue."
            if config_path
            else "Pass --rabbit-queue or create copy-config.json from copy-config.example.json."
        )
        raise ValueError(f"RabbitMQ queue is not set. {hint}")
    if not config.database.server:
        raise ValueError("SQL Server is not set. Use -s/--db-server or database.server in config.")
    if not config.database.database:
        raise ValueError("Database name is not set. Use -d/--db-name or database.database in config.")


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    config_path = Path(args.config) if args.config else None
    config = load_config(config_path)
    apply_cli_overrides(config, args)

    try:
        validate_config(config, config_path)
        driver = config.database.resolved_driver()
        print(f"Using ODBC driver: {driver}")
        print(f"SQL Server: {config.database.server}, database: {config.database.database}")
        print(f"RabbitMQ queue: {config.rabbitmq.queue}")
        MqCopier(config).run()
    except Exception as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
