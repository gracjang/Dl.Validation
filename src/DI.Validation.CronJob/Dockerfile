FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/DI.Validation.CronJob/DI.Validation.CronJob.csproj", "src/DI.Validation.CronJob/"]
RUN dotnet restore "src/DI.Validation.CronJob/DI.Validation.CronJob.csproj"
COPY . .
WORKDIR "/src/src/DI.Validation.CronJob"
RUN dotnet build "DI.Validation.CronJob.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DI.Validation.CronJob.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DI.Validation.CronJob.dll"]
