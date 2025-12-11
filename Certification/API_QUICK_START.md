# Certification Systems API - Quick Start

## 🚀 All Systems Are Live!

All flight software certification systems are now integrated into the WebAPI and ready to use.

## Available Endpoints

### Base URL
```
https://hellobluegk.onrender.com/api/v1/certification
```

### Authentication
All endpoints require JWT authentication. Include token in header:
```
Authorization: Bearer YOUR_TOKEN
```

## 1. Requirements Management

### Create Requirement
```bash
POST /api/v1/certification/requirements
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN

{
  "requirementNumber": "REQ-001",
  "title": "Engine Thrust Control",
  "description": "System shall control engine thrust within ±1%",
  "priority": "Critical"
}
```

### Link Requirement to Code
```bash
POST /api/v1/certification/requirements/{id}/link-code
Content-Type: application/json

{
  "codeFile": "EngineController.cs",
  "lineStart": 45,
  "lineEnd": 67,
  "functionName": "ControlThrust"
}
```

### Generate Requirements Traceability Matrix
```bash
GET /api/v1/certification/requirements/rtm
```

### Verify Traceability
```bash
GET /api/v1/certification/requirements/verify
```

## 2. Problem Reporting

### Create Problem Report
```bash
POST /api/v1/certification/problem-reports
Content-Type: application/json

{
  "title": "Thrust calculation error",
  "description": "Thrust calculation returns incorrect value at high altitude",
  "impact": "Safety-critical: May cause mission failure"
}
```

### Update Problem Report Status
```bash
PUT /api/v1/certification/problem-reports/{reportNumber}/status
Content-Type: application/json

{
  "status": "UnderInvestigation",
  "resolution": "Investigating root cause"
}
```

### Get Problem Report Summary
```bash
GET /api/v1/certification/problem-reports/summary
```

### Verify Compliance
```bash
GET /api/v1/certification/problem-reports/verify-compliance
```

## 3. Configuration Management

### Create Software Baseline
```bash
POST /api/v1/certification/configuration/baselines
Content-Type: application/json

{
  "baselineName": "Flight Software v1.0",
  "version": "1.0.0",
  "description": "Initial flight software baseline"
}
```

### Create Change Request
```bash
POST /api/v1/certification/configuration/change-requests
Content-Type: application/json

{
  "title": "Update thrust calculation algorithm",
  "description": "Improve accuracy at high altitude",
  "justification": "Fixes problem report PR-2025-0001"
}
```

### Generate Software Configuration Index (SCI)
```bash
GET /api/v1/certification/configuration/baselines/{id}/sci
```

### Perform Configuration Audit
```bash
GET /api/v1/certification/configuration/baselines/{id}/audit
```

## 4. Test Coverage

### Record Coverage
```bash
POST /api/v1/certification/test-coverage/record
Content-Type: application/json

{
  "filePath": "EngineController.cs",
  "statementCoverage": 100.0,
  "branchCoverage": 100.0,
  "conditionCoverage": 100.0,
  "mcdcCoverage": 100.0,
  "totalStatements": 150,
  "coveredStatements": 150,
  "totalBranches": 45,
  "coveredBranches": 45
}
```

### Mark as Safety-Critical
```bash
PUT /api/v1/certification/test-coverage/{filePath}/safety-critical
Content-Type: application/json

{
  "isSafetyCritical": true
}
```

### Get Coverage Report
```bash
GET /api/v1/certification/test-coverage/report
```

### Verify Coverage Compliance
```bash
GET /api/v1/certification/test-coverage/verify-compliance
```

## 5. Code Reviews

### Create Code Review
```bash
POST /api/v1/certification/code-reviews
Content-Type: application/json

{
  "filePath": "EngineController.cs",
  "functionName": "ControlThrust",
  "lineStart": 45,
  "lineEnd": 67
}
```

### Assign Certified Reviewer
```bash
POST /api/v1/certification/code-reviews/{id}/assign-reviewer
Content-Type: application/json

{
  "reviewerName": "Certified Reviewer",
  "isCertified": true
}
```

### Submit Review Findings
```bash
POST /api/v1/certification/code-reviews/{id}/findings
Content-Type: application/json

{
  "findings": [
    {
      "lineNumber": 50,
      "severity": "Major",
      "category": "Correctness",
      "description": "Missing null check",
      "recommendation": "Add null check before accessing object"
    }
  ]
}
```

### Approve Code Review
```bash
POST /api/v1/certification/code-reviews/{id}/approve
```

### Get Code Review Summary
```bash
GET /api/v1/certification/code-reviews/summary
```

## Swagger UI

All endpoints are documented in Swagger:
```
https://hellobluegk.onrender.com/swagger
```

Look for the "Certification" tag groups:
- Certification - Requirements
- Certification - Problem Reports
- Certification - Configuration Management
- Certification - Test Coverage
- Certification - Code Reviews

## Example Workflow

### 1. Create a Requirement
```bash
curl -X POST https://hellobluegk.onrender.com/api/v1/certification/requirements \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "requirementNumber": "REQ-001",
    "title": "Engine Thrust Control",
    "description": "System shall control engine thrust within ±1%",
    "priority": "Critical"
  }'
```

### 2. Link to Code
```bash
curl -X POST https://hellobluegk.onrender.com/api/v1/certification/requirements/{requirementId}/link-code \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "codeFile": "EngineController.cs",
    "lineStart": 45,
    "lineEnd": 67,
    "functionName": "ControlThrust"
  }'
```

### 3. Record Test Coverage
```bash
curl -X POST https://hellobluegk.onrender.com/api/v1/certification/test-coverage/record \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "EngineController.cs",
    "statementCoverage": 100.0,
    "branchCoverage": 100.0,
    "mcdcCoverage": 100.0,
    "totalStatements": 150,
    "coveredStatements": 150
  }'
```

### 4. Verify Compliance
```bash
# Verify traceability
curl https://hellobluegk.onrender.com/api/v1/certification/requirements/verify \
  -H "Authorization: Bearer YOUR_TOKEN"

# Verify coverage
curl https://hellobluegk.onrender.com/api/v1/certification/test-coverage/verify-compliance \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Status

✅ **All systems integrated and ready!**

- Requirements Traceability: ✅ Live
- Problem Reporting: ✅ Live
- Configuration Management: ✅ Live
- Test Coverage: ✅ Live
- Code Reviews: ✅ Live

All endpoints are authenticated, documented in Swagger, and ready for production use.

---

**Next:** Start using the APIs to build your certification data!
