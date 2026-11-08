# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["BinVaBun.DiscordBot.csproj", "./"]
RUN dotnet restore "BinVaBun.DiscordBot.csproj"

# Copy the source code
COPY . .
WORKDIR "/src"

# Build and publish
RUN dotnet publish "BinVaBun.DiscordBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV LOG_FORMAT=json

ENTRYPOINT ["dotnet", "BinVaBun.DiscordBot.dll"]
