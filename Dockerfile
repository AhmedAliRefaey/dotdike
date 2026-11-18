FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/Dike.Core/Dike.Core.csproj src/Dike.Core/
COPY src/Dike.Infrastructure/Dike.Infrastructure.csproj src/Dike.Infrastructure/
COPY src/Dike.Web/Dike.Web.csproj src/Dike.Web/
RUN dotnet restore src/Dike.Web/Dike.Web.csproj
COPY src/Dike.Core/ src/Dike.Core/
COPY src/Dike.Infrastructure/ src/Dike.Infrastructure/
COPY src/Dike.Web/ src/Dike.Web/
RUN dotnet publish src/Dike.Web/Dike.Web.csproj --configuration Release --no-restore \
    --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    Hosting__Provider=Vercel \
    Storage__Provider=PostgreSql \
    PORT=8080 \
    DOTNET_EnableDiagnostics=0
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dike.Web.dll"]
