#!/usr/bin/env bash

# ==============================================================================
# TechStore Management System - Identity Endpoints Manual Test Script
# ==============================================================================
# Usage:
#   ./scripts/test-identity-endpoints.sh [BASE_URL]
# Example:
#   ./scripts/test-identity-endpoints.sh http://localhost:5176
# ==============================================================================

set -e

BASE_URL="${1:-http://localhost:5176}"
echo "=========================================================="
echo " Testing TechStore Identity API on: $BASE_URL"
echo "=========================================================="

GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

pass() {
    echo -e "${GREEN}✓ PASS:${NC} $1"
}

fail() {
    echo -e "${RED}✗ FAIL:${NC} $1"
    exit 1
}

# 1. Health check
echo -e "\n1. Checking Server Health..."
HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/health" || echo "failed")
if [ "$HEALTH_STATUS" = "200" ]; then
    pass "Backend API is reachable and healthy (HTTP 200)"
else
    echo "Warning: Health endpoint returned $HEALTH_STATUS. Continuing with test..."
fi

# 2. Login as Admin
echo -e "\n2. Testing POST /api/auth/login with Admin credentials..."
LOGIN_ADMIN_RES=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Password123!","rememberMe":true}')

ADMIN_TOKEN=$(echo "$LOGIN_ADMIN_RES" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4 || true)
ADMIN_REFRESH_TOKEN=$(echo "$LOGIN_ADMIN_RES" | grep -o '"refreshToken":"[^"]*' | cut -d'"' -f4 || true)

if [ -n "$ADMIN_TOKEN" ]; then
    pass "Admin login successful. JWT AccessToken acquired."
    echo "   AccessToken prefix: ${ADMIN_TOKEN:0:25}..."
    echo "   RefreshToken prefix: ${ADMIN_REFRESH_TOKEN:0:25}..."
else
    fail "Admin login failed. Response: $LOGIN_ADMIN_RES"
fi

# 3. Login as Cashier
echo -e "\n3. Testing POST /api/auth/login with Cashier credentials..."
LOGIN_CASHIER_RES=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"cashier1","password":"Password123!","rememberMe":true}')

CASHIER_TOKEN=$(echo "$LOGIN_CASHIER_RES" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4 || true)
if [ -n "$CASHIER_TOKEN" ]; then
    pass "Cashier login successful. Token acquired."
else
    fail "Cashier login failed. Response: $LOGIN_CASHIER_RES"
fi

# 4. Login Failure Test
echo -e "\n4. Testing POST /api/auth/login with invalid password (Expect 401)..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"WrongPassword!"}')

if [ "$HTTP_CODE" = "401" ]; then
    pass "Server rejected invalid password with HTTP 401 Unauthorized."
else
    fail "Expected HTTP 401 but received HTTP $HTTP_CODE"
fi

# 5. Get Profile (/api/auth/me) with Admin Bearer Token
echo -e "\n5. Testing GET /api/auth/me with Admin Bearer Token..."
PROFILE_RES=$(curl -s -X GET "$BASE_URL/api/auth/me" \
  -H "Authorization: Bearer $ADMIN_TOKEN")

ADMIN_USERNAME=$(echo "$PROFILE_RES" | grep -o '"username":"[^"]*' | cut -d'"' -f4 || true)
if [ "$ADMIN_USERNAME" = "admin" ]; then
    pass "Profile retrieved successfully for user '$ADMIN_USERNAME'."
else
    fail "Get profile failed. Response: $PROFILE_RES"
fi

# 6. Get Store Settings (/api/settings/store)
echo -e "\n6. Testing GET /api/settings/store (Public/Authenticated)..."
STORE_SETTINGS_RES=$(curl -s -X GET "$BASE_URL/api/settings/store")
STORE_NAME=$(echo "$STORE_SETTINGS_RES" | grep -o '"storeName":"[^"]*' | cut -d'"' -f4 || true)

if [ -n "$STORE_NAME" ]; then
    pass "Store settings retrieved. StoreName: '$STORE_NAME'"
else
    fail "Get store settings failed. Response: $STORE_SETTINGS_RES"
fi

# 7. Update Store Settings (/api/settings/store) as Admin
echo -e "\n7. Testing PUT /api/settings/store with Admin Token..."
TIMESTAMP=$(date +"%T")
UPDATE_RES=$(curl -s -X PUT "$BASE_URL/api/settings/store" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"storeName\": \"TechStore Central - Updated $TIMESTAMP\",
    \"address\": \"123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh\",
    \"phone\": \"028 3822 6868\",
    \"email\": \"contact@techstore.vn\",
    \"taxId\": \"0315892410\",
    \"description\": \"Hệ thống bán lẻ thiết bị công nghệ hàng đầu\"
  }")

UPDATED_NAME=$(echo "$UPDATE_RES" | grep -o '"storeName":"[^"]*' | cut -d'"' -f4 || true)
if [[ "$UPDATED_NAME" == *"Updated $TIMESTAMP"* ]]; then
    pass "Store settings updated successfully to: '$UPDATED_NAME'"
else
    fail "Update store settings failed. Response: $UPDATE_RES"
fi

# 8. Update Store Settings (/api/settings/store) as Cashier (Expect 403 Forbidden)
echo -e "\n8. Testing PUT /api/settings/store with Cashier Token (Expect 403)..."
FORBIDDEN_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE_URL/api/settings/store" \
  -H "Authorization: Bearer $CASHIER_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"storeName\": \"Should Fail\", \"address\": \"A\", \"phone\": \"0123\"}")

if [ "$FORBIDDEN_CODE" = "403" ]; then
    pass "Role-Based Access Control enforced: Cashier received HTTP 403 Forbidden."
else
    fail "Expected HTTP 403 Forbidden for Cashier update, but received HTTP $FORBIDDEN_CODE"
fi

# 9. Logout
echo -e "\n9. Testing POST /api/auth/logout with Refresh Token..."
LOGOUT_HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$ADMIN_REFRESH_TOKEN\"}")

if [ "$LOGOUT_HTTP_CODE" = "200" ]; then
    pass "Admin logout succeeded with HTTP 200 OK. Token invalidated."
else
    fail "Logout failed with HTTP $LOGOUT_HTTP_CODE"
fi

echo -e "\n=========================================================="
echo -e "${GREEN} ALL IDENTITY ENDPOINT TESTS PASSED SUCCESSFULLY!${NC}"
echo "=========================================================="
