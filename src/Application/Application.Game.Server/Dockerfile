#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Application/Application.Game.Server/Application.Game.Server.csproj", "Application/Application.Game.Server/"]
COPY ["Domain/Domain.Game/Domain.Game.csproj", "Domain/Domain.Game/"]
COPY ["Library/Library.Commons.Eventbus.RabbitMq/Library.Commons.Eventbus.RabbitMq.csproj", "Library/Library.Commons.Eventbus.RabbitMq/"]
COPY ["Library/Library.Commons.Game.Domain/Library.Commons.Game.Domain.csproj", "Library/Library.Commons.Game.Domain/"]
COPY ["Library/Library.Commons.Game.Server/Library.Commons.Game.Server.csproj", "Library/Library.Commons.Game.Server/"]
COPY ["Library/Library.Commons.Api/Library.Commons.Api.csproj", "Library/Library.Commons.Api/"]
COPY ["Library/Library.Commons/Library.Commons.csproj", "Library/Library.Commons/"]
COPY ["Library/Library.Extensions.DependencyInjection/Library.Extensions.DependencyInjection.csproj", "Library/Library.Extensions.DependencyInjection/"]
RUN dotnet restore "./Application/Application.Game.Server/./Application.Game.Server.csproj"
COPY . .
WORKDIR "/src/Application/Application.Game.Server"
RUN dotnet build "./Application.Game.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM --platform=$BUILDPLATFORM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Application.Game.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM --platform=$BUILDPLATFORM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Application.Game.Server.dll"]