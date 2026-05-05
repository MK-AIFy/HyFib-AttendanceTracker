FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AttendTrack.Web/AttendTrack.Web.csproj", "src/AttendTrack.Web/"]
COPY ["src/AttendTrack.Application/AttendTrack.Application.csproj", "src/AttendTrack.Application/"]
COPY ["src/AttendTrack.Domain/AttendTrack.Domain.csproj", "src/AttendTrack.Domain/"]
COPY ["src/AttendTrack.Infrastructure/AttendTrack.Infrastructure.csproj", "src/AttendTrack.Infrastructure/"]
RUN dotnet restore "src/AttendTrack.Web/AttendTrack.Web.csproj"
COPY . .
WORKDIR "/src/src/AttendTrack.Web"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Run as non-root user (security best practice)
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/publish .

# Create writable directories before switching to non-root user
RUN mkdir -p /app/face-captures /app/hikvision_events /app/logs && \
    chown -R appuser:appgroup /app/face-captures /app/hikvision_events /app/logs

USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "AttendTrack.Web.dll"]
