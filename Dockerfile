# Use the official .NET 8.0 SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy csproj and restore dependencies
COPY WeddingInvitationManager/*.csproj ./WeddingInvitationManager/
WORKDIR /app/WeddingInvitationManager
RUN dotnet restore

# Copy everything else and build
WORKDIR /app
COPY . ./
WORKDIR /app/WeddingInvitationManager
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install necessary packages for file processing
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=build-env /app/WeddingInvitationManager/out .

# Create directories for uploads and generated files
RUN mkdir -p wwwroot/uploads/contacts wwwroot/uploads/images wwwroot/uploads/temp
RUN mkdir -p wwwroot/generated/invitations wwwroot/generated/qrcodes

# Set permissions for directories
RUN chmod -R 755 wwwroot/uploads wwwroot/generated

# Expose port 80
EXPOSE 80

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Run the application
ENTRYPOINT ["dotnet", "WeddingInvitationManager.dll"]
