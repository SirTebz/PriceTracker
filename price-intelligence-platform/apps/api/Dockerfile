FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PriceIntelligence.sln .
COPY PriceIntelligence.Domain/PriceIntelligence.Domain.csproj PriceIntelligence.Domain/
COPY PriceIntelligence.Application/PriceIntelligence.Application.csproj PriceIntelligence.Application/
COPY PriceIntelligence.Infrastructure/PriceIntelligence.Infrastructure.csproj PriceIntelligence.Infrastructure/
COPY PriceIntelligence.API/PriceIntelligence.API.csproj PriceIntelligence.API/

RUN dotnet restore

COPY . .
RUN dotnet publish PriceIntelligence.API/PriceIntelligence.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PriceIntelligence.API.dll"]