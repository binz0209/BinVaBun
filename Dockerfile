# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["MemoriesApp.Api/MemoriesApp.Api.csproj", "MemoriesApp.Api/"]
RUN dotnet restore "MemoriesApp.Api/MemoriesApp.Api.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/MemoriesApp.Api"

# Build and publish
RUN dotnet publish "MemoriesApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .

# Set environment variables for Render (optional but good practice)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MemoriesApp.Api.dll"]
