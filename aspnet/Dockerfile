FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY aspnet/ aspnet/
COPY flower.com/ flower.com/
RUN dotnet publish aspnet/src/FlowerFlow.Api/FlowerFlow.Api.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 Reservations__WorkerEnabled=true
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlowerFlow.Api.dll"]
