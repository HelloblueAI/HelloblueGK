#!/bin/bash
# Bash script to create database and run migrations
# Usage: ./Scripts/CreateDatabase.sh

echo "Creating HelloblueGK database..."

# Check if dotnet-ef is installed
if ! command -v dotnet-ef &> /dev/null; then
    echo "Installing dotnet-ef tool..."
    dotnet tool install --global dotnet-ef
fi

# Navigate to WebAPI directory
cd "$(dirname "$0")/.."

# Create initial migration
echo "Creating initial migration..."
dotnet ef migrations add InitialCreate --project . --startup-project .

# Update database
echo "Updating database..."
dotnet ef database update --project . --startup-project .

echo "Database created successfully!"

