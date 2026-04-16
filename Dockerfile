# Use the official .NET 9 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file and project files
COPY ["AykutOnPC.sln", "./"]
COPY ["AykutOnPC.Core/AykutOnPC.Core.csproj", "AykutOnPC.Core/"]
COPY ["AykutOnPC.Infrastructure/AykutOnPC.Infrastructure.csproj", "AykutOnPC.Infrastructure/"]
COPY ["AykutOnPC.Web/AykutOnPC.Web.csproj", "AykutOnPC.Web/"]

# Restore dependencies
RUN dotnet restore "AykutOnPC.sln"

# Copy the rest of the code
COPY . .

# Build and publish the application
WORKDIR "/src/AykutOnPC.Web"
RUN dotnet publish "AykutOnPC.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the ASP.NET Core runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create a non-root user and group for security
RUN groupadd -r appgroup && useradd -r -g appgroup -s /sbin/nologin appuser

# Create the keys directory and set ownership
RUN mkdir -p /app/keys && chown -R appuser:appgroup /app/keys

COPY --from=build /app/publish .

# Ensure the app directory is owned by the non-root user
RUN chown -R appuser:appgroup /app

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Switch to the non-root user
USER appuser

# Start the application
ENTRYPOINT ["dotnet", "AykutOnPC.Web.dll"]