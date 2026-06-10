FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["applications/OpenGitBase.Api/OpenGitBase.Api.csproj", "applications/OpenGitBase.Api/"]
COPY ["common/OpenGitBase.Common/OpenGitBase.Common.csproj", "common/OpenGitBase.Common/"]
COPY ["common/OpenGitBase.Cqrs/OpenGitBase.Cqrs.csproj", "common/OpenGitBase.Cqrs/"]
COPY ["common/OpenGitBase.Cqrs.EfCore/OpenGitBase.Cqrs.EfCore.csproj", "common/OpenGitBase.Cqrs.EfCore/"]

RUN dotnet restore "applications/OpenGitBase.Api/OpenGitBase.Api.csproj"

COPY . .
WORKDIR "/src/applications/OpenGitBase.Api"
RUN dotnet build "OpenGitBase.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenGitBase.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenGitBase.Api.dll"]
