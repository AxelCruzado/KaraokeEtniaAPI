FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KaraokeEtniaApi.csproj ./
RUN dotnet restore
COPY . .
ARG SOURCE_REVISION=unknown
RUN dotnet publish -c Release --no-restore -o /app/publish /p:SourceRevisionId=${SOURCE_REVISION}
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p wwwroot/images/productos wwwroot/images/categorias && chown -R $APP_UID:$APP_UID /app
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "KaraokeEtniaApi.dll"]
