# 🚀 Quick Start Guide

## Application is Running! ✅

Your HelloblueGK WebAPI is now running at:
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/Health
- **Metrics**: http://localhost:5000/metrics

---

## 📝 First Steps

### 1. **Access Swagger UI**
Open your browser and go to: **http://localhost:5000/swagger**

You'll see all available API endpoints with interactive documentation.

### 2. **Register a User**
Use the Swagger UI to test the authentication:

1. Find the `POST /api/v1/auth/register` endpoint
2. Click "Try it out"
3. Enter user details:
```json
{
  "username": "admin",
  "email": "admin@example.com",
  "password": "Admin1234!",
  "firstName": "Admin",
  "lastName": "User"
}
```
4. Click "Execute"
5. Copy the `token` from the response

### 3. **Use the Token**
1. Click the "Authorize" button at the top of Swagger
2. Enter: `Bearer YOUR_TOKEN_HERE`
3. Click "Authorize"
4. Now you can test protected endpoints!

### 4. **Test Other Endpoints**
- `GET /api/v1/auth/me` - Get current user info
- `GET /metrics` - View Prometheus metrics
- `GET /Health` - Health check

---

## 🔧 Database

The database is automatically created on first run using SQLite (development mode).

Database file location: `WebAPI/hellobluegk.db`

**Note**: For production, update the connection string in `appsettings.Production.json` to use SQL Server or PostgreSQL.

---

## 🎯 Next Steps

1. **Explore the API** - Use Swagger to test all endpoints
2. **Check Metrics** - Visit http://localhost:5000/metrics
3. **Review Documentation** - See `README_IMPLEMENTATION.md` for details
4. **Customize** - Update `appsettings.json` for your needs

---

## 🐛 Troubleshooting

### Port Already in Use
If port 5000 is busy, set a different port:
```bash
export PORT=5001
dotnet run
```

### Database Issues
The database auto-creates on first run. If you need to reset:
```bash
rm WebAPI/hellobluegk.db
dotnet run
```

### Static Files Warning
The `wwwroot` directory is now created. This warning will disappear on next run.

---

**Enjoy your enterprise-grade aerospace engine simulation API!** 🚀

