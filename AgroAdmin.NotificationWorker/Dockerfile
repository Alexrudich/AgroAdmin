# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgroAdmin.NotificationWorker/AgroAdmin.NotificationWorker.csproj", "AgroAdmin.NotificationWorker/"]
COPY ["AgroAdmin.Domain/AgroAdmin.Domain.csproj", "AgroAdmin.Domain/"]
COPY ["AgroAdmin.Shared/AgroAdmin.Shared.csproj", "AgroAdmin.Shared/"]
COPY ["AgroAdmin.Infrastructure/AgroAdmin.Infrastructure.csproj", "AgroAdmin.Infrastructure/"]
RUN dotnet restore "./AgroAdmin.NotificationWorker/AgroAdmin.NotificationWorker.csproj"
COPY . .
WORKDIR "/src/AgroAdmin.NotificationWorker"
RUN dotnet build "./AgroAdmin.NotificationWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AgroAdmin.NotificationWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgroAdmin.NotificationWorker.dll"]