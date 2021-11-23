#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
ARG BuildConfiguration=Release
RUN --mount=type=secret,id=nuget,dst=/pat cp /pat ~/.nuget/NuGet/NuGet.Config
RUN dotnet nuget remove source 'Microsoft Visual Studio Offline Packages' || true
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["TGS.NotificationReceiverService/TGS.NotificationReceiverService.csproj", "TGS.NotificationReceiverService/"]
RUN dotnet restore "TGS.NotificationReceiverService/TGS.NotificationReceiverService.csproj"
COPY . .
WORKDIR "/src/TGS.NotificationReceiverService"
RUN dotnet build "TGS.NotificationReceiverService.csproj" -c ${BuildConfiguration} -o /app/build

FROM build AS publish
ARG BuildConfiguration
RUN dotnet publish "TGS.NotificationReceiverService.csproj" -c ${BuildConfiguration} -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TGS.NotificationReceiverService.dll"]