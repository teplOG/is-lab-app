FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY IsLabApp.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish IsLabApp.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

RUN useradd --system --no-create-home --shell /usr/sbin/nologin isapp \
    && chown -R isapp:isapp /app
USER isapp

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "IsLabApp.dll"]
