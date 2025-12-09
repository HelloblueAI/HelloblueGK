# HelloblueGK - Quick Reference

## Production URLs

**Base URL:** https://hellobluegk.onrender.com

### Essential Endpoints

**Health Check**
```bash
curl https://hellobluegk.onrender.com/Health
```

**Swagger UI** (Interactive API Documentation)
- Browser: https://hellobluegk.onrender.com/swagger
- Swagger JSON: https://hellobluegk.onrender.com/swagger/v1/swagger.json
- Root URL redirects to Swagger: https://hellobluegk.onrender.com/

**Prometheus Metrics**
```bash
curl https://hellobluegk.onrender.com/metrics
```

### API Statistics
- **Total Endpoints:** 33
- **API Version:** v1
- **Status:** ✅ Fully Operational

### Key API Categories
- **Authentication:** `/api/v1/Auth/*` (login, register, me)
- **Health Monitoring:** `/Health`, `/Health/detailed`, `/Health/engine`
- **Performance:** `/api/v1/Performance/*` (7 endpoints)
- **Metrics:** `/api/v1/Metrics/*` (4 endpoints)
- **Rate Limiting:** `/api/v1/RateLimit/*` (7 endpoints)
- **System Health:** `/api/v1/SystemHealth/*` (8 endpoints)

### Authentication
Most endpoints require JWT authentication. Use Swagger UI to:
1. Register/Login at `/api/v1/Auth/register` or `/api/v1/Auth/login`
2. Get JWT token from response
3. Click "Authorize" button in Swagger UI
4. Enter: `Bearer YOUR_TOKEN_HERE`

### Quick Test Commands

```bash
# Health check
curl https://hellobluegk.onrender.com/Health

# Get Swagger JSON spec
curl https://hellobluegk.onrender.com/swagger/v1/swagger.json

# Prometheus metrics
curl https://hellobluegk.onrender.com/metrics

# Register new user (example)
curl -X POST https://hellobluegk.onrender.com/api/v1/Auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","email":"test@example.com","password":"Test1234!"}'
```

---
**Last Updated:** December 8, 2025  
**Deployment Status:** ✅ Production  
**Service:** hellobluegk-production (Render)

