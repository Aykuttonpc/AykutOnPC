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
RUN mkdir -p /app/keys
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Start the application
ENTRYPOINT ["dotnet", "AykutOnPC.Web.dll"]