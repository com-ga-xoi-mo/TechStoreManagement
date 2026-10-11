#!/usr/bin/env bash
# ==============================================================================
# TechStore Management - Script Test Tự Động Các Endpoints Dev 3 (Sprint 1)
# 
# Phạm vi test: 6 Endpoints thuộc Feature 3 (Category & Inventory API)
# 1. GET  /api/categories/tree
# 2. POST /api/categories
# 3. GET  /api/inventory/stocks
# 4. POST /api/inventory/stocks/initialize
# 5. GET  /api/inventory/serials
# 6. POST /api/inventory/serials/batch-import
# ==============================================================================

set -u

BASE_URL="${1:-http://localhost:5176}"
TIMESTAMP=$(date +%s)

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}==============================================================================${NC}"
echo -e "${BLUE} BẮT ĐẦU KIỂM THỬ ENDPOINTS DEV 3 (CATALOG & INVENTORY API) TẠI: ${BASE_URL}${NC}"
echo -e "${BLUE}==============================================================================${NC}\n"

# Helper kiểm tra status code
assert_status() {
    local test_name="$1"
    local expected_status="$2"
    local actual_status="$3"
    local response_body="$4"

    if [ "$actual_status" -eq "$expected_status" ]; then
        echo -e "  [${GREEN}PASS${NC}] ${test_name} (HTTP ${actual_status})"
    else
        echo -e "  [${RED}FAIL${NC}] ${test_name} (Kỳ vọng HTTP ${expected_status}, Nhận HTTP ${actual_status})"
        echo -e "         ${YELLOW}Response Body:${NC} ${response_body}"
    fi
}

# ------------------------------------------------------------------------------
# 1. ENDPOINT 1: GET /api/categories/tree (UC-CAT-01)
# ------------------------------------------------------------------------------
echo -e "${YELLOW}>>> 1. Kiểm thử GET /api/categories/tree (Lấy cây danh mục)${NC}"

RES_1=$(curl -s -w "\n%{http_code}" -X GET "${BASE_URL}/api/categories/tree" \
    -H "Accept: application/json")
BODY_1=$(echo "$RES_1" | sed '$d')
STATUS_1=$(echo "$RES_1" | tail -n1)

assert_status "Lấy cây danh mục sản phẩm (Tree Hierarchy)" 200 "$STATUS_1" "$BODY_1"

# ------------------------------------------------------------------------------
# 2. ENDPOINT 2: POST /api/categories (UC-CAT-02)
# ------------------------------------------------------------------------------
echo -e "\n${YELLOW}>>> 2. Kiểm thử POST /api/categories (Thêm mới danh mục)${NC}"

# 2.1 Tạo danh mục gốc hợp lệ (201)
PARENT_SLUG="test-root-${TIMESTAMP}"
RES_2_1=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/categories" \
    -H "Content-Type: application/json" \
    -d "{
        \"name\": \"Danh mục Gốc ${TIMESTAMP}\",
        \"slug\": \"${PARENT_SLUG}\",
        \"description\": \"Danh mục test tự động\"
    }")
BODY_2_1=$(echo "$RES_2_1" | sed '$d')
STATUS_2_1=$(echo "$RES_2_1" | tail -n1)
assert_status "Tạo danh mục gốc hợp lệ" 201 "$STATUS_2_1" "$BODY_2_1"

# Trích xuất Id danh mục vừa tạo (dùng grep/sed nếu không có jq)
CREATED_CAT_ID=$(echo "$BODY_2_1" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)

# 2.2 Tạo danh mục con với ParentId (201)
if [ -n "$CREATED_CAT_ID" ]; then
    CHILD_SLUG="test-child-${TIMESTAMP}"
    RES_2_2=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/categories" \
        -H "Content-Type: application/json" \
        -d "{
            \"name\": \"Danh mục Con ${TIMESTAMP}\",
            \"slug\": \"${CHILD_SLUG}\",
            \"parentId\": \"${CREATED_CAT_ID}\"
        }")
    BODY_2_2=$(echo "$RES_2_2" | sed '$d')
    STATUS_2_2=$(echo "$RES_2_2" | tail -n1)
    assert_status "Tạo danh mục con hợp lệ với ParentId" 201 "$STATUS_2_2" "$BODY_2_2"
fi

# 2.3 Bắt trùng Slug (409 Conflict)
RES_2_3=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/categories" \
    -H "Content-Type: application/json" \
    -d "{
        \"name\": \"Danh mục Trùng Slug\",
        \"slug\": \"${PARENT_SLUG}\"
    }")
BODY_2_3=$(echo "$RES_2_3" | sed '$d')
STATUS_2_3=$(echo "$RES_2_3" | tail -n1)
assert_status "Bắt lỗi trùng slug (Conflict)" 409 "$STATUS_2_3" "$BODY_2_3"

# 2.4 Validate thiếu tên danh mục (400 Bad Request)
RES_2_4=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/categories" \
    -H "Content-Type: application/json" \
    -d "{
        \"slug\": \"no-name-category\"
    }")
BODY_2_4=$(echo "$RES_2_4" | sed '$d')
STATUS_2_4=$(echo "$RES_2_4" | tail -n1)
assert_status "Validate thiếu tên danh mục (Bad Request)" 400 "$STATUS_2_4" "$BODY_2_4"

# ------------------------------------------------------------------------------
# 3. ENDPOINT 3: GET /api/inventory/stocks (UC-INV-01)
# ------------------------------------------------------------------------------
echo -e "\n${YELLOW}>>> 3. Kiểm thử GET /api/inventory/stocks (Tra cứu số dư tồn kho)${NC}"

# 3.1 Lấy toàn bộ danh sách tồn kho mặc định (200)
RES_3_1=$(curl -s -w "\n%{http_code}" -X GET "${BASE_URL}/api/inventory/stocks?page=1&pageSize=10" \
    -H "Accept: application/json")
BODY_3_1=$(echo "$RES_3_1" | sed '$d')
STATUS_3_1=$(echo "$RES_3_1" | tail -n1)
assert_status "Lấy danh sách tồn kho có phân trang" 200 "$STATUS_3_1" "$BODY_3_1"

# 3.2 Lọc theo cờ cảnh báo tồn kho thấp lowStock=true (200)
RES_3_2=$(curl -s -w "\n%{http_code}" -X GET "${BASE_URL}/api/inventory/stocks?lowStock=true" \
    -H "Accept: application/json")
BODY_3_2=$(echo "$RES_3_2" | sed '$d')
STATUS_3_2=$(echo "$RES_3_2" | tail -n1)
assert_status "Lọc danh sách cảnh báo tồn kho thấp (lowStock=true)" 200 "$STATUS_3_2" "$BODY_3_2"

# ------------------------------------------------------------------------------
# SETUP: Tạo 1 sản phẩm mẫu để test khởi tạo tồn kho & nạp IMEI
# ------------------------------------------------------------------------------
echo -e "\n${BLUE}--- [Setup] Tạo sản phẩm mẫu phục vụ test tồn kho & IMEI ---${NC}"
SAMPLE_CAT_ID="${CREATED_CAT_ID:-00000000-0000-0000-0000-000000000000}"
CREATE_PROD_RES=$(curl -s -X POST "${BASE_URL}/api/products" \
    -H "Content-Type: application/json" \
    -d "{
        \"categoryId\": \"${SAMPLE_CAT_ID}\",
        \"name\": \"Sản phẩm Test Tồn Kho ${TIMESTAMP}\",
        \"sku\": \"SKU-TEST-${TIMESTAMP}\",
        \"brand\": \"TechStore Test\",
        \"basePrice\": 15000000,
        \"costPrice\": 12000000,
        \"isSerialTracked\": true
    }")
TEST_PRODUCT_ID=$(echo "$CREATE_PROD_RES" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)

if [ -z "$TEST_PRODUCT_ID" ]; then
    # Fallback lấy productId đầu tiên có sẵn trong hệ thống nếu có
    TEST_PRODUCT_ID=$(echo "$BODY_3_1" | grep -o '"productId":"[^"]*' | head -1 | cut -d'"' -f4)
fi
echo -e "  Product ID sử dụng: ${TEST_PRODUCT_ID:-'Chưa xác định'}"

# ------------------------------------------------------------------------------
# 4. ENDPOINT 4: POST /api/inventory/stocks/initialize (UC-INV-02)
# ------------------------------------------------------------------------------
echo -e "\n${YELLOW}>>> 4. Kiểm thử POST /api/inventory/stocks/initialize (Khởi tạo số dư đầu kỳ)${NC}"

if [ -n "$TEST_PRODUCT_ID" ]; then
    # 4.1 Khởi tạo tồn kho hợp lệ (201)
    RES_4_1=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/stocks/initialize" \
        -H "Content-Type: application/json" \
        -d "{
            \"productId\": \"${TEST_PRODUCT_ID}\",
            \"quantity\": 50,
            \"minStockAlert\": 10,
            \"reason\": \"Khởi tạo số dư đầu kỳ test script\"
        }")
    BODY_4_1=$(echo "$RES_4_1" | sed '$d')
    STATUS_4_1=$(echo "$RES_4_1" | tail -n1)
    assert_status "Khởi tạo số dư tồn kho ban đầu hợp lệ" 201 "$STATUS_4_1" "$BODY_4_1"

    # 4.2 Chống khởi tạo trùng lặp cho cùng sản phẩm (409 Conflict)
    RES_4_2=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/stocks/initialize" \
        -H "Content-Type: application/json" \
        -d "{
            \"productId\": \"${TEST_PRODUCT_ID}\",
            \"quantity\": 20
        }")
    BODY_4_2=$(echo "$RES_4_2" | sed '$d')
    STATUS_4_2=$(echo "$RES_4_2" | tail -n1)
    assert_status "Chặn khởi tạo trùng số dư cho cùng sản phẩm (Conflict)" 409 "$STATUS_4_2" "$BODY_4_2"
fi

# 4.3 Validate số lượng âm (400 Bad Request)
RES_4_3=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/stocks/initialize" \
    -H "Content-Type: application/json" \
    -d "{
        \"productId\": \"00000000-0000-0000-0000-000000000001\",
        \"quantity\": -10
    }")
BODY_4_3=$(echo "$RES_4_3" | sed '$d')
STATUS_4_3=$(echo "$RES_4_3" | tail -n1)
assert_status "Validate số lượng âm quantity < 0 (Bad Request)" 400 "$STATUS_4_3" "$BODY_4_3"

# ------------------------------------------------------------------------------
# 5. ENDPOINT 5: GET /api/inventory/serials (UC-IMEI-01)
# ------------------------------------------------------------------------------
echo -e "\n${YELLOW}>>> 5. Kiểm thử GET /api/inventory/serials (Tra cứu Serial/IMEI)${NC}"

# 5.1 Tra cứu Serial/IMEI trạng thái InStock (200)
RES_5_1=$(curl -s -w "\n%{http_code}" -X GET "${BASE_URL}/api/inventory/serials?status=InStock&page=1&pageSize=10" \
    -H "Accept: application/json")
BODY_5_1=$(echo "$RES_5_1" | sed '$d')
STATUS_5_1=$(echo "$RES_5_1" | tail -n1)
assert_status "Tra cứu Serial/IMEI theo trạng thái InStock" 200 "$STATUS_5_1" "$BODY_5_1"

# ------------------------------------------------------------------------------
# 6. ENDPOINT 6: POST /api/inventory/serials/batch-import (UC-IMEI-02)
# ------------------------------------------------------------------------------
echo -e "\n${YELLOW}>>> 6. Kiểm thử POST /api/inventory/serials/batch-import (Nạp Serial/IMEI)${NC}"

# Tạo 2 mã IMEI 15 chữ số duy nhất dựa trên timestamp
IMEI_1="860000$(printf "%09d" "$((TIMESTAMP % 1000000000))")"
IMEI_2="860001$(printf "%09d" "$((TIMESTAMP % 1000000000))")"
SERIAL_1="SN-${TIMESTAMP}-01"
SERIAL_2="SN-${TIMESTAMP}-02"

if [ -n "$TEST_PRODUCT_ID" ]; then
    # 6.1 Nạp lô Serial/IMEI hợp lệ (201)
    RES_6_1=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/serials/batch-import" \
        -H "Content-Type: application/json" \
        -d "{
            \"productId\": \"${TEST_PRODUCT_ID}\",
            \"reason\": \"Nạp lô hàng đầu kỳ test\",
            \"defaultUnitCost\": 12000000,
            \"items\": [
                {
                    \"serialNumber\": \"${SERIAL_1}\",
                    \"imei\": \"${IMEI_1}\",
                    \"warrantyMonths\": 12
                },
                {
                    \"serialNumber\": \"${SERIAL_2}\",
                    \"imei\": \"${IMEI_2}\",
                    \"warrantyMonths\": 24
                }
            ]
        }")
    BODY_6_1=$(echo "$RES_6_1" | sed '$d')
    STATUS_6_1=$(echo "$RES_6_1" | tail -n1)
    assert_status "Nạp lô Serial/IMEI hợp lệ (201 Created)" 201 "$STATUS_6_1" "$BODY_6_1"

    # 6.2 Chặn trùng IMEI đã có trong cơ sở dữ liệu (409 Conflict)
    RES_6_2=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/serials/batch-import" \
        -H "Content-Type: application/json" \
        -d "{
            \"productId\": \"${TEST_PRODUCT_ID}\",
            \"items\": [
                {
                    \"serialNumber\": \"${SERIAL_1}\",
                    \"imei\": \"${IMEI_1}\"
                }
            ]
        }")
    BODY_6_2=$(echo "$RES_6_2" | sed '$d')
    STATUS_6_2=$(echo "$RES_6_2" | tail -n1)
    assert_status "Chặn trùng lặp Serial/IMEI đã có trong DB (Conflict)" 409 "$STATUS_6_2" "$BODY_6_2"
fi

# 6.3 Chặn trùng Serial/IMEI ngay trong nội bộ Payload gửi lên (409 Conflict)
RES_6_3=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/serials/batch-import" \
    -H "Content-Type: application/json" \
    -d "{
        \"productId\": \"00000000-0000-0000-0000-000000000001\",
        \"items\": [
            { \"serialNumber\": \"DUPE-PAYLOAD-01\" },
            { \"serialNumber\": \"DUPE-PAYLOAD-01\" }
        ]
    }")
BODY_6_3=$(echo "$RES_6_3" | sed '$d')
STATUS_6_3=$(echo "$RES_6_3" | tail -n1)
assert_status "Chặn trùng Serial trong cùng Payload (Conflict)" 409 "$STATUS_6_3" "$BODY_6_3"

# 6.4 Validate định dạng IMEI không đúng 15 chữ số (400 Bad Request)
RES_6_4=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/api/inventory/serials/batch-import"
    -H "Content-Type: application/json" \
    -d "{
        \"productId\": \"00000000-0000-0000-0000-000000000001\",
        \"items\": [
            {
                \"serialNumber\": \"SN-TEST-INVALID-IMEI\",
                \"imei\": \"12345\"
            }
        ]
    }")
BODY_6_4=$(echo "$RES_6_4" | sed '$d')
STATUS_6_4=$(echo "$RES_6_4" | tail -n1)
assert_status "Validate IMEI không đủ 15 chữ số (Bad Request)" 400 "$STATUS_6_4" "$BODY_6_4"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${GREEN} HOÀN TẤT KIỂM THỬ TẤT CẢ ENDPOINTS DEV 3.${NC}"
echo -e "${BLUE}==============================================================================${NC}"
