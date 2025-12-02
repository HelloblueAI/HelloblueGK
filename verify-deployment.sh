#!/bin/bash
# Deployment Verification Script for HelloblueGK
# Checks if deployment is ready and all endpoints are accessible

set -e

echo "🔍 HelloblueGK Deployment Verification"
echo "========================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if URL is provided
if [ -z "$1" ]; then
    echo -e "${YELLOW}Usage: ./verify-deployment.sh <YOUR_RENDER_URL>${NC}"
    echo "Example: ./verify-deployment.sh https://hellobluegk-production.onrender.com"
    exit 1
fi

URL=$1
echo "Testing deployment at: ${URL}"
echo ""

# Remove trailing slash
URL="${URL%/}"

# Function to check endpoint
check_endpoint() {
    local endpoint=$1
    local expected_status=$2
    local description=$3
    
    echo -n "Checking $description... "
    
    response=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "${URL}${endpoint}" 2>&1 || echo "000")
    
    if [ "$response" == "$expected_status" ]; then
        echo -e "${GREEN}✓${NC} (Status: $response)"
        return 0
    else
        echo -e "${RED}✗${NC} (Status: $response, expected: $expected_status)"
        return 1
    fi
}

# Function to check endpoint with content
check_endpoint_content() {
    local endpoint=$1
    local keyword=$2
    local description=$3
    
    echo -n "Checking $description... "
    
    response=$(curl -s --max-time 10 "${URL}${endpoint}" 2>&1)
    
    if echo "$response" | grep -q "$keyword"; then
        echo -e "${GREEN}✓${NC} (Found: $keyword)"
        return 0
    else
        echo -e "${RED}✗${NC} (Keyword '$keyword' not found)"
        return 1
    fi
}

# Track results
passed=0
failed=0

# Check endpoints
echo "📡 Testing API Endpoints:"
echo "------------------------"

if check_endpoint "/Health" "200" "Health Check"; then
    ((passed++))
else
    ((failed++))
fi

if check_endpoint "/swagger" "200" "Swagger UI"; then
    ((passed++))
else
    ((failed++))
fi

if check_endpoint "/swagger/index.html" "200" "Swagger Index"; then
    ((passed++))
else
    ((failed++))
fi

if check_endpoint "/metrics" "200" "Prometheus Metrics"; then
    ((passed++))
else
    ((failed++))
fi

echo ""
echo "📊 Testing API Functionality:"
echo "-----------------------------"

# Check if health endpoint returns JSON
if check_endpoint_content "/Health" "Healthy\|status" "Health Check Content"; then
    ((passed++))
else
    ((failed++))
fi

echo ""
echo "========================================"
echo "Results:"
echo -e "  ${GREEN}Passed: ${passed}${NC}"
if [ $failed -gt 0 ]; then
    echo -e "  ${RED}Failed: ${failed}${NC}"
else
    echo -e "  ${GREEN}Failed: ${failed}${NC}"
fi
echo ""

if [ $failed -eq 0 ]; then
    echo -e "${GREEN}✅ All checks passed! Deployment is working correctly.${NC}"
    echo ""
    echo "🌐 Your API is live at:"
    echo "   - API: ${URL}"
    echo "   - Swagger: ${URL}/swagger"
    echo "   - Health: ${URL}/Health"
    echo "   - Metrics: ${URL}/metrics"
    exit 0
else
    echo -e "${YELLOW}⚠️  Some checks failed. Please review the errors above.${NC}"
    echo ""
    echo "Common issues:"
    echo "  - Service might still be building (wait 5-10 minutes)"
    echo "  - URL might be incorrect"
    echo "  - Service might be sleeping (free tier)"
    exit 1
fi

