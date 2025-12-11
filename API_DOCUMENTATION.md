# HelloblueGK API Documentation

## Base URL

**Production:** `https://hellobluegk.onrender.com`

**Swagger UI:** `https://hellobluegk.onrender.com/swagger`

## Authentication

The API uses JWT (JSON Web Tokens) for authentication. Most endpoints require authentication.

### Getting a Token

1. **Register a new user:**
   ```bash
   POST /api/v1/Auth/register
   Content-Type: application/json
   
   {
     "username": "your_username",
     "email": "your_email@example.com",
     "password": "SecurePassword123!",
     "firstName": "Your",
     "lastName": "Name"
   }
   ```

2. **Login to get token:**
   ```bash
   POST /api/v1/Auth/login
   Content-Type: application/json
   
   {
     "username": "your_username",
     "password": "SecurePassword123!"
   }
   ```

   **Response:**
   ```json
   {
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "refreshToken": "base64_refresh_token",
     "expiresIn": 86400,
     "user": {
       "id": 1,
       "username": "your_username",
       "email": "your_email@example.com",
       "isAdmin": false
     }
   }
   ```

### Using the Token

Include the token in the `Authorization` header:

```bash
Authorization: Bearer your_jwt_token_here
```

**Example:**
```bash
curl -X GET https://hellobluegk.onrender.com/api/v1/Auth/me \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

## API Endpoints

### Authentication Endpoints

#### Register User
```http
POST /api/v1/Auth/register
Content-Type: application/json
```

**Request Body:**
```json
{
  "username": "string (required, unique)",
  "email": "string (required, unique, valid email)",
  "password": "string (required, min 6 characters)",
  "firstName": "string (optional)",
  "lastName": "string (optional)"
}
```

**Response:** `201 Created`
```json
{
  "token": "jwt_token",
  "user": {
    "id": 1,
    "username": "string",
    "email": "string",
    "isAdmin": false
  }
}
```

#### Login
```http
POST /api/v1/Auth/login
Content-Type: application/json
```

**Request Body:**
```json
{
  "username": "string (required)",
  "password": "string (required, min 6 characters)"
}
```

**Response:** `200 OK`
```json
{
  "token": "jwt_token",
  "refreshToken": "refresh_token",
  "expiresIn": 86400,
  "user": {
    "id": 1,
    "username": "string",
    "email": "string",
    "isAdmin": false
  }
}
```

**Error Responses:**
- `400 Bad Request` - Validation errors
- `401 Unauthorized` - Invalid credentials

#### Get Current User
```http
GET /api/v1/Auth/me
Authorization: Bearer {token}
```

**Response:** `200 OK`
```json
{
  "id": 1,
  "username": "string",
  "email": "string",
  "firstName": "string",
  "lastName": "string",
  "isAdmin": false
}
```

### Health & System Endpoints

#### Health Check
```http
GET /Health
```

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "timestamp": "2025-12-11T06:51:51.5005353Z",
  "service": "HB-NLP Advanced Engine Design Platform",
  "version": "1.0.0",
  "environment": "Production"
}
```

#### System Health
```http
GET /api/v1/SystemHealth
Authorization: Bearer {token}
```

Returns comprehensive system health information including:
- Database connectivity
- System resources
- Service status
- Performance metrics

### Performance & Metrics Endpoints

#### Performance Metrics
```http
GET /api/v1/Performance
Authorization: Bearer {token}
```

Returns performance monitoring data including:
- System metrics (CPU, memory, threads)
- Application metrics
- Performance trends
- Recommendations

#### Metrics
```http
GET /api/v1/Metrics
Authorization: Bearer {token}
```

Returns Prometheus-compatible metrics.

### Rate Limiting

The API implements rate limiting to prevent abuse. Limits are applied per client IP address.

**Rate Limit Headers:**
- `X-RateLimit-Limit` - Maximum requests allowed
- `X-RateLimit-Remaining` - Remaining requests in current window
- `X-RateLimit-Reset` - Time when the rate limit resets

**Rate Limit Response:** `429 Too Many Requests`
```json
{
  "statusCode": 429,
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 60
}
```

## Error Responses

All errors follow this format:

```json
{
  "statusCode": 400,
  "message": "Error description",
  "details": "Additional error details (optional)",
  "timestamp": "2025-12-11T06:52:23.4313958Z",
  "path": "/api/v1/endpoint",
  "method": "POST",
  "validationErrors": {
    "field": ["error message"]
  }
}
```

### Common Status Codes

- `200 OK` - Request successful
- `201 Created` - Resource created successfully
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Authentication required or invalid
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

## API Versioning

The API uses URL versioning:
- Current version: `v1`
- Base path: `/api/v1/`

Example: `/api/v1/Auth/login`

## Rate Limits

- **Default:** 100 requests per minute per IP
- **Authenticated:** Higher limits (varies by user tier)
- **Admin:** Unlimited

## Best Practices

### 1. Token Management
- Store tokens securely (never in localStorage for web apps)
- Refresh tokens before expiration
- Handle token expiration gracefully

### 2. Error Handling
- Always check response status codes
- Handle rate limit errors (429) with retry logic
- Implement exponential backoff for retries

### 3. Security
- Never expose tokens in URLs or logs
- Use HTTPS only (enforced in production)
- Validate all input data on client side

### 4. Performance
- Cache responses when appropriate
- Use pagination for large datasets
- Monitor rate limit headers

## Code Examples

### JavaScript/TypeScript

```javascript
// Login
async function login(username, password) {
  const response = await fetch('https://hellobluegk.onrender.com/api/v1/Auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ username, password })
  });
  
  if (!response.ok) {
    throw new Error('Login failed');
  }
  
  const data = await response.json();
  localStorage.setItem('token', data.token);
  return data;
}

// Authenticated Request
async function getCurrentUser() {
  const token = localStorage.getItem('token');
  const response = await fetch('https://hellobluegk.onrender.com/api/v1/Auth/me', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  if (!response.ok) {
    throw new Error('Failed to get user');
  }
  
  return await response.json();
}
```

### Python

```python
import requests

BASE_URL = "https://hellobluegk.onrender.com"

# Login
def login(username, password):
    response = requests.post(
        f"{BASE_URL}/api/v1/Auth/login",
        json={"username": username, "password": password}
    )
    response.raise_for_status()
    data = response.json()
    return data["token"]

# Authenticated Request
def get_current_user(token):
    response = requests.get(
        f"{BASE_URL}/api/v1/Auth/me",
        headers={"Authorization": f"Bearer {token}"}
    )
    response.raise_for_status()
    return response.json()

# Usage
token = login("your_username", "your_password")
user = get_current_user(token)
print(user)
```

### cURL

```bash
# Login
TOKEN=$(curl -X POST https://hellobluegk.onrender.com/api/v1/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"your_username","password":"your_password"}' \
  | jq -r '.token')

# Get Current User
curl -X GET https://hellobluegk.onrender.com/api/v1/Auth/me \
  -H "Authorization: Bearer $TOKEN"
```

## Support

- **Documentation:** https://github.com/HelloblueAI/HelloblueGK
- **Issues:** https://github.com/HelloblueAI/HelloblueGK/issues
- **Swagger UI:** https://hellobluegk.onrender.com/swagger

## Changelog

### v1.0.0 (Current)
- Initial API release
- JWT authentication
- User management
- Health monitoring
- Performance metrics
- Rate limiting
