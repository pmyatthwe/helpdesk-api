FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY HelpdeskApi.Domain/*.csproj HelpdeskApi.Domain/
COPY HelpdeskApi.Application/*.csproj HelpdeskApi.Application/
COPY HelpdeskApi.Infrastructure/*.csproj HelpdeskApi.Infrastructure/
COPY HelpdeskApi.Api/*.csproj HelpdeskApi.Api/
RUN dotnet restore HelpdeskApi.Api/HelpdeskApi.Api.csproj

COPY . .
RUN dotnet publish HelpdeskApi.Api/HelpdeskApi.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "HelpdeskApi.Api.dll"]