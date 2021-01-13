FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /app

COPY *.sln .
COPY *.csproj .
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:5.0
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "/app/octomerge.dll"]
