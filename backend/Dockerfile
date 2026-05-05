# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app/src

# Copy project file
COPY NewsAggregator.Api/NewsAggregator.Api.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY NewsAggregator.Api/ ./

# Build application
RUN dotnet build -c Release -o /app/build

# Publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
  CMD dotnet-health || exit 1

# Run application
ENTRYPOINT ["dotnet", "NewsAggregator.Api.dll"]
