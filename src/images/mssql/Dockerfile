FROM mcr.microsoft.com/dotnet/sdk:8.0 AS sdk
RUN dotnet tool install dotnet-script -g
RUN dotnet tool install --tool-path /opt/sqlpackage/ microsoft.sqlpackage
USER root
RUN chmod +x /opt/sqlpackage/sqlpackage
