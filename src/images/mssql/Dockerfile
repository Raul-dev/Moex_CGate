FROM mcr.microsoft.com/dotnet/sdk:8.0 AS sdk
RUN dotnet tool install dotnet-script -g
RUN dotnet tool install --tool-path /opt/sqlpackage/ microsoft.sqlpackage
USER root
RUN chmod +x /opt/sqlpackage/sqlpackage
# 1. Устанавливаем зависимости для работы с репозиториями и сетевыми запросами
#RUN apt-get update && apt-get install -y \
#    curl \
#    gnupg2 \
#    apt-transport-https \
#    && rm -rf /var/lib/apt/lists/*
# 1. Устанавливаем зависимости для работы сетевого стека и библиотек SQL
#RUN apt-get update && apt-get install -y \
#    unzip libicu-dev libssl-dev libunwind8 \
#    && rm -rf /var/lib/apt/lists/*

RUN apt-get update && apt-get install -y bzip2 curl && rm -rf /var/lib/apt/lists/*

# 3. Установка go-sqlcmd (Новый официальный sqlcmd от Microsoft)
# Он быстрее, не требует ODBC драйверов и не зависит от репозиториев apt
RUN curl -L https://github.com/microsoft/go-sqlcmd/releases/download/v1.10.0/sqlcmd-linux-amd64.tar.bz2 -o /tmp/sqlcmd.tar.bz2 && \
    tar -xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin && \
    rm /tmp/sqlcmd.tar.bz2

# 4. Прописываем пути
ENV PATH="$PATH:/opt/sqlpackage"

ENV PATH="$PATH:/opt/mssql-tools18/bin"
# Теперь у вас доступны команды 'sqlcmd' и 'sqlpackage'
# 4. Добавляем путь к sqlcmd в системный PATH, чтобы вызывать его просто как 'sqlcmd'

COPY ./dacpac/RabbitMQ.Client.dll /localdacpac/RabbitMQ.Client.dll
COPY ./dacpac/RabbitMQSqlClr4.dll /localdacpac/RabbitMQSqlClr4.dll
RUN chmod -R 777 /localdacpac/RabbitMQ.Client.dll
RUN chmod -R 777 /localdacpac/RabbitMQSqlClr4.dll



