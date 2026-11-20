FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KaraokeEtniaApi.csproj ./
RUN dotnet restore
COPY . .
ARG SOURCE_REVISION
ARG RENDER_GIT_COMMIT
RUN revision="${RENDER_GIT_COMMIT:-$SOURCE_REVISION}" && test ${#revision} -eq 40 && dotnet publish -c Release --no-restore -o /app/publish /p:SourceRevisionId=$revision
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p wwwroot/images/productos wwwroot/images/categorias && chown -R $APP_UID:$APP_UID /app
ENV ASPNETCORE_URLS=http://+:10000
USER $APP_UID
EXPOSE 10000
ENTRYPOINT ["dotnet", "KaraokeEtniaApi.dll"]
