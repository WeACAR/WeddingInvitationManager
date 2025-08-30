# Docker Build and Test Script for Wedding Invitation Manager

Write-Host "Building and testing Wedding Invitation Manager with Docker..." -ForegroundColor Green

# Build the Docker image
Write-Host "Building Docker image..." -ForegroundColor Yellow
docker build -t wedding-invitation-manager .

if ($LASTEXITCODE -eq 0) {
    Write-Host "Docker image built successfully!" -ForegroundColor Green
    
    # Start the full application with database
    Write-Host "Starting application with docker-compose..." -ForegroundColor Yellow
    docker-compose up -d
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Application started successfully!" -ForegroundColor Green
        Write-Host "Application is running at: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "Database is running on: localhost:5432" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To stop the application, run: docker-compose down" -ForegroundColor Magenta
        Write-Host "To view logs, run: docker-compose logs -f" -ForegroundColor Magenta
        
        # Wait a moment for services to start
        Start-Sleep -Seconds 10
        
        # Try to test the application
        Write-Host "Testing application health..." -ForegroundColor Yellow
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5000" -TimeoutSec 30
            if ($response.StatusCode -eq 200) {
                Write-Host "✅ Application is responding correctly!" -ForegroundColor Green
            }
        }
        catch {
            Write-Host "⚠️  Application might still be starting up. Check manually at http://localhost:5000" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Failed to start application with docker-compose" -ForegroundColor Red
    }
}
else {
    Write-Host "Failed to build Docker image" -ForegroundColor Red
    Write-Host "Check the Dockerfile and try again" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Useful Docker commands:" -ForegroundColor Magenta
Write-Host "- View running containers: docker ps" -ForegroundColor White
Write-Host "- View application logs: docker-compose logs -f web" -ForegroundColor White
Write-Host "- View database logs: docker-compose logs -f postgres" -ForegroundColor White
Write-Host "- Stop everything: docker-compose down" -ForegroundColor White
Write-Host "- Remove volumes: docker-compose down -v" -ForegroundColor White
