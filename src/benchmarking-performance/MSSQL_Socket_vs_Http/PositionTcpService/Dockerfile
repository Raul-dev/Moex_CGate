FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Share/ Share/
COPY PositionTcpService/ PositionTcpService/
RUN dotnet publish PositionTcpService -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PositionTcpService.dll"]
