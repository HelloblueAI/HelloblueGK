# Professional Setup Guide - HelloblueGK

This guide covers professional deployment, configuration, and best practices for production use.

## Table of Contents

1. [Production Deployment](#production-deployment)
2. [Environment Configuration](#environment-configuration)
3. [Security Best Practices](#security-best-practices)
4. [Database Setup](#database-setup)
5. [Monitoring & Logging](#monitoring--logging)
6. [Backup Strategy](#backup-strategy)
7. [Scaling & Performance](#scaling--performance)
8. [CI/CD Pipeline](#cicd-pipeline)

## Production Deployment

### Prerequisites

- [ ] GitHub repository with code
- [ ] Render account (or alternative hosting)
- [ ] Domain name (optional but recommended)
- [ ] SSL certificate (automatically provided by Render)

### Deployment Steps

1. **Create PostgreSQL Database**
   - See: [RENDER_POSTGRESQL_SETUP.md](Docs/Deployment/RENDER_POSTGRESQL_SETUP.md)
   - Use Basic plan or higher for production
   - Enable storage autoscaling

2. **Create Web Service**
   - Connect GitHub repository
   - Set branch to `main` (after PR merge)
   - Configure Dockerfile: `Docker/Dockerfile.render`
   - Set health check: `/Health`

3. **Configure Environment Variables**
   - See [Environment Configuration](#environment-configuration) below

4. **Deploy and Verify**
   - Monitor deployment logs
   - Test health endpoint
   - Verify database connection
   - Test authentication

## Environment Configuration

### Required Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:$PORT

# JWT Authentication (REQUIRED - must be secure)
Jwt__Key=your-secure-jwt-key-minimum-32-characters-long
Jwt__Issuer=hellobluegk
Jwt__Audience=hellobluegk-api

# Database (REQUIRED)
ConnectionStrings__DefaultConnection=Host=host;Port=5432;Database=db;Username=user;Password=pass
```

### Optional Environment Variables

```bash
# JWT Token Expiration (default: 24 hours)
Jwt__TokenExpirationHours=24

# CORS Origins (comma-separated)
Cors__Origins=https://yourdomain.com,https://app.yourdomain.com

# Logging
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft.AspNetCore=Warning
```

### Generating Secure JWT Key

**Option 1: Using OpenSSL**
```bash
openssl rand -base64 32
```

**Option 2: Using .NET**
```csharp
var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
```

**Option 3: Online Generator**
- Use a secure random string generator
- Minimum 32 characters
- Mix of letters, numbers, and symbols

## Security Best Practices

### 1. Secrets Management

✅ **DO:**
- Store secrets in environment variables
- Use Render's secure environment variable storage
- Rotate secrets regularly
- Use different secrets for dev/staging/prod

❌ **DON'T:**
- Commit secrets to git
- Share secrets in documentation
- Use default/example secrets in production
- Store secrets in code

### 2. Database Security

✅ **DO:**
- Use strong database passwords
- Enable SSL for database connections (Render does this automatically)
- Use internal database URLs (same region)
- Regular security updates

❌ **DON'T:**
- Expose database publicly
- Use default passwords
- Share database credentials

### 3. API Security

✅ **DO:**
- Use HTTPS only (enforced)
- Implement rate limiting (already configured)
- Validate all input
- Use JWT tokens with expiration
- Implement refresh tokens

❌ **DON'T:**
- Expose tokens in URLs
- Use weak passwords
- Skip input validation

### 4. Network Security

✅ **DO:**
- Use CORS to restrict origins
- Use internal networking (same region)
- Monitor for suspicious activity
- Implement DDoS protection (Render provides)

## Database Setup

### PostgreSQL Configuration

**Recommended Settings:**
- **Plan:** Basic-256mb or higher for production
- **Storage:** 15GB minimum (with autoscaling)
- **Region:** Same as web service
- **High Availability:** Enable for production (Pro plan)

### Database Maintenance

1. **Regular Backups**
   - Render Pro plan includes automatic backups
   - Or set up manual backup schedule

2. **Monitoring**
   - Monitor database connections
   - Track query performance
   - Set up alerts for high usage

3. **Optimization**
   - Regular VACUUM (PostgreSQL handles automatically)
   - Monitor index usage
   - Optimize slow queries

## Monitoring & Logging

### Application Logs

Render provides built-in log streaming:
- Access via Render dashboard
- Real-time log viewing
- Log retention based on plan

### Health Monitoring

**Endpoints:**
- `/Health` - Basic health check
- `/api/v1/SystemHealth` - Detailed system health

**Monitoring Tools:**
- Render's built-in monitoring
- Prometheus metrics: `/metrics`
- Custom health checks

### Metrics

**Available Metrics:**
- Request rate
- Response times
- Error rates
- Database connection pool
- Memory usage
- CPU usage

## Backup Strategy

### Database Backups

**Render Pro Plan:**
- Automatic daily backups
- 7-day retention
- Point-in-time recovery

**Manual Backups:**
```bash
# Using pg_dump
pg_dump -h host -U user -d database > backup.sql

# Restore
psql -h host -U user -d database < backup.sql
```

### Application Backups

- Code: GitHub (version control)
- Configuration: Environment variables in Render
- Data: Database backups

## Scaling & Performance

### Horizontal Scaling

Render supports:
- Multiple instances
- Load balancing (automatic)
- Auto-scaling (Pro plan)

### Performance Optimization

1. **Database**
   - Connection pooling (configured)
   - Query optimization
   - Index optimization

2. **Application**
   - Response caching
   - Static asset optimization
   - API response compression

3. **CDN**
   - Use Render's CDN for static assets
   - Custom domain with CDN

## CI/CD Pipeline

### GitHub Actions

The repository includes CI/CD workflows:
- Automated testing
- Code quality checks
- Security scanning
- Automated deployment (optional)

### Deployment Workflow

1. **Development**
   - Work on feature branch
   - Create PR
   - Automated tests run

2. **Review**
   - Code review
   - Security review
   - Approval

3. **Merge**
   - Merge to `main`
   - Automated deployment (if configured)
   - Or manual deployment via Render

4. **Production**
   - Monitor deployment
   - Verify health checks
   - Test critical endpoints

## Production Checklist

### Pre-Deployment

- [ ] All environment variables configured
- [ ] Secure JWT key generated and set
- [ ] Database created and configured
- [ ] Connection string verified
- [ ] Health checks configured
- [ ] Monitoring set up
- [ ] Backup strategy in place

### Post-Deployment

- [ ] Health endpoint responds
- [ ] Database connection works
- [ ] Authentication works
- [ ] API endpoints functional
- [ ] Swagger UI accessible
- [ ] Logs are being captured
- [ ] Monitoring is active

### Ongoing Maintenance

- [ ] Regular security updates
- [ ] Monitor performance metrics
- [ ] Review and rotate secrets
- [ ] Database maintenance
- [ ] Backup verification
- [ ] Security audits

## Support & Resources

- **Documentation:** [README.md](README.md)
- **API Docs:** [API_DOCUMENTATION.md](API_DOCUMENTATION.md)
- **Deployment:** [Docs/Deployment/](Docs/Deployment/)
- **Issues:** GitHub Issues
- **Swagger UI:** https://hellobluegk.onrender.com/swagger

## Troubleshooting

### Common Issues

**Database Connection Failed**
- Verify connection string format
- Check database is running
- Verify credentials
- Check network connectivity

**Authentication Fails**
- Verify JWT key is set correctly
- Check token expiration
- Verify user exists in database

**500 Internal Server Error**
- Check application logs
- Verify environment variables
- Check database connectivity
- Review error details in logs

**Rate Limit Exceeded**
- Wait for rate limit window to reset
- Implement exponential backoff
- Consider upgrading rate limits

## Next Steps

1. ✅ Complete production deployment
2. ✅ Set up monitoring
3. ✅ Configure backups
4. ✅ Set up custom domain (optional)
5. ✅ Implement CI/CD (optional)
6. ✅ Performance optimization
7. ✅ Security hardening

---

**Last Updated:** December 2025
**Version:** 1.0.0
