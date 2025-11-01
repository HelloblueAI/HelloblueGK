# HelloblueGK Interactive Demo

This guide will help you run the interactive demo of the HelloblueGK aerospace engine simulation platform.

## Quick Start

### Option 1: Run with Docker (Recommended)

```bash
# Build the Docker image
docker build -t hellobluegk-demo .

# Run the container
docker run -p 5000:8080 hellobluegk-demo

# Access the demo at http://localhost:5000
```

### Option 2: Run Locally

```bash
# Navigate to WebAPI directory
cd WebAPI

# Restore dependencies
dotnet restore

# Run the API server
dotnet run

# Access the demo at http://localhost:5000
```

## What's Available

### Interactive API Documentation (Swagger)

Once the server is running, navigate to:
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/Health
- **System Health**: http://localhost:5000/api/v1/SystemHealth/comprehensive

### Available API Endpoints

#### Health & Status
- `GET /Health` - Basic health check
- `GET /Health/detailed` - Detailed health metrics
- `GET /api/v1/SystemHealth/comprehensive` - Comprehensive system health report

#### Performance Monitoring
- `GET /api/v1/Performance/snapshot` - Current performance snapshot
- `GET /api/v1/Performance/report` - Detailed performance report
- `GET /api/v1/Performance/metrics/{category}` - Metrics by category
- `GET /api/v1/Performance/trend/{metricName}` - Performance trend analysis

#### Rate Limiting
- `GET /api/v1/RateLimit/status/{identifier}` - Rate limit status
- `GET /api/v1/RateLimit/report` - Rate limiting report

## Demo Use Cases

### 1. Check System Health

```bash
curl http://localhost:5000/Health
```

### 2. Get Performance Snapshot

```bash
curl http://localhost:5000/api/v1/Performance/snapshot
```

### 3. View Comprehensive System Health

```bash
curl http://localhost:5000/api/v1/SystemHealth/comprehensive
```

## Interactive Testing

The Swagger UI provides an interactive interface where you can:
1. Browse all available endpoints
2. See request/response schemas
3. Try API calls directly from the browser
4. View example responses

## Troubleshooting

### Port Already in Use

If port 5000 is in use, you can change it:

```bash
# Using environment variable
ASPNETCORE_URLS=http://localhost:5001 dotnet run

# Or in appsettings.json
{
  "Urls": "http://localhost:5001"
}
```

### Docker Issues

If Docker build fails:
- Ensure Docker is running
- Check that you have sufficient disk space
- Verify .NET 9.0 SDK is available in Docker

## Next Steps

- **Explore the API**: Use Swagger UI to test endpoints
- **Read the Documentation**: Check [README.md](README.md) for full documentation
- **Contribute**: See [CONTRIBUTING.md](CONTRIBUTING.md) to contribute improvements

## Need Help?

- Open an [issue](https://github.com/HelloblueAI/HelloblueGK/issues) on GitHub
- Check the [documentation](README.md)
- Review [API examples](README.md#api-documentation)

---

**Happy Simulating! 🚀**

