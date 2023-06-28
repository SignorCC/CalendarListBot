# Use the .NET 6.0 SDK as the base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory in the container
WORKDIR /app

# Copy the project file(s) and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy the remaining source code
COPY . .

# Build the application
RUN dotnet publish -c Release -o out

# Use a lighter base image for the runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Set the working directory in the container
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/out .

# Set the entry point for the application
ENTRYPOINT ["dotnet", "CalendarListBot.dll"]
