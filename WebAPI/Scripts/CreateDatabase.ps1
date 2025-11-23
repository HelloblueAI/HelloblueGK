# PowerShell script to create database and run migrations
# Usage: .\Scripts\CreateDatabase.ps1

Write-Host "Creating HelloblueGK database..." -ForegroundColor Green

# Check if dotnet-ef is installed
$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efInstalled) {
    Write-Host "Installing dotnet-ef tool..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# Navigate to WebAPI directory
Set-Location $PSScriptRoot\..

# Create initial migration
Write-Host "Creating initial migration..." -ForegroundColor Green
dotnet ef migrations add InitialCreate --project . --startup-project .

# Update database
Write-Host "Updating database..." -ForegroundColor Green
dotnet ef database update --project . --startup-project .

Write-Host "Database created successfully!" -ForegroundColor Green

