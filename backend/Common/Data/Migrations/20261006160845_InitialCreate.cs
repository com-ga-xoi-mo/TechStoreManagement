using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    loyalty_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tier = table.Column<string>(type: "text", nullable: false, defaultValue: "Standard"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.CheckConstraint("ck_customers_dob_valid", "date_of_birth <= CURRENT_DATE");
                    table.CheckConstraint("ck_customers_loyalty_points_ge_zero", "loyalty_points >= 0");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_type = table.Column<string>(type: "text", nullable: false, defaultValue: "Percentage"),
                    discount_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    min_order_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    max_discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    usage_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    used_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.id);
                    table.CheckConstraint("ck_vouchers_discount_value_valid", "discount_value > 0 AND (discount_type <> 'Percentage' OR discount_value <= 100)");
                    table.CheckConstraint("ck_vouchers_expires_after_created", "expires_at > created_at");
                    table.CheckConstraint("ck_vouchers_max_discount_ge_zero", "max_discount_amount IS NULL OR max_discount_amount >= 0");
                    table.CheckConstraint("ck_vouchers_min_order_ge_zero", "min_order_amount >= 0");
                    table.CheckConstraint("ck_vouchers_usage_limit_gt_zero", "usage_limit > 0");
                    table.CheckConstraint("ck_vouchers_used_count_valid", "used_count >= 0 AND used_count <= usage_limit");
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "text", nullable: false),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    specs = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_serial_tracked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.CheckConstraint("ck_products_base_price_ge_zero", "base_price >= 0");
                    table.CheckConstraint("ck_products_cost_price_ge_zero", "cost_price >= 0");
                    table.ForeignKey(
                        name: "FK_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_loyalty_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    points_change = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_loyalty_points", x => x.id);
                    table.CheckConstraint("ck_cust_loyalty_balance_after_ge_zero", "balance_after >= 0");
                    table.CheckConstraint("ck_cust_loyalty_points_change_not_zero", "points_change <> 0");
                    table.ForeignKey(
                        name: "FK_customer_loyalty_points_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pos_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cashier_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    closing_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cash_sales_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    vietqr_sales_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    card_sales_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Open"),
                    opened_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    closed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sessions", x => x.id);
                    table.CheckConstraint("ck_pos_sessions_card_sales_ge_zero", "card_sales_total >= 0");
                    table.CheckConstraint("ck_pos_sessions_cash_sales_ge_zero", "cash_sales_total >= 0");
                    table.CheckConstraint("ck_pos_sessions_opening_ge_zero", "opening_balance >= 0");
                    table.CheckConstraint("ck_pos_sessions_vietqr_sales_ge_zero", "vietqr_sales_total >= 0");
                    table.ForeignKey(
                        name: "FK_pos_sessions_users_cashier_user_id",
                        column: x => x.cashier_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    po_number = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    notes = table.Column<string>(type: "text", nullable: true),
                    ordered_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.CheckConstraint("ck_purchase_orders_total_cost_ge_zero", "total_cost >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_received_by_user_id",
                        column: x => x.received_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_code = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cashier_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pos_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    payment_method = table.Column<string>(type: "text", nullable: false, defaultValue: "Cash"),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.CheckConstraint("ck_orders_discount_amount_ge_zero", "discount_amount >= 0");
                    table.CheckConstraint("ck_orders_subtotal_ge_zero", "subtotal >= 0");
                    table.CheckConstraint("ck_orders_tax_amount_ge_zero", "tax_amount >= 0");
                    table.CheckConstraint("ck_orders_total_amount_ge_zero", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_orders_users_cashier_user_id",
                        column: x => x.cashier_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_name = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    specs = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.UniqueConstraint("AK_product_variants_id_product_id", x => new { x.id, x.product_id });
                    table.CheckConstraint("ck_product_variants_price_ge_zero", "price >= 0");
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vietqr_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pos_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_code = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: true),
                    provider_transaction_id = table.Column<string>(type: "text", nullable: true),
                    bank_bin = table.Column<string>(type: "text", nullable: false),
                    bank_account_number = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    qr_content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vietqr_transactions", x => x.id);
                    table.CheckConstraint("chk_vietqr_confirmation_evidence", "(status = 'Confirmed' AND confirmed_at IS NOT NULL AND (confirmed_by_user_id IS NOT NULL OR provider_transaction_id IS NOT NULL)) OR (status <> 'Confirmed' AND confirmed_at IS NULL AND confirmed_by_user_id IS NULL)");
                    table.CheckConstraint("chk_vietqr_expiry_after_creation", "expires_at > created_at");
                    table.CheckConstraint("chk_vietqr_provider_reference_pair", "(provider IS NULL) = (provider_transaction_id IS NULL)");
                    table.CheckConstraint("ck_vietqr_tx_amount_gt_zero", "amount > 0");
                    table.ForeignKey(
                        name: "FK_vietqr_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vietqr_transactions_pos_sessions_pos_session_id",
                        column: x => x.pos_session_id,
                        principalTable: "pos_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vietqr_transactions_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_stocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    min_stock_alert = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_stocks", x => x.id);
                    table.CheckConstraint("ck_inventory_stocks_min_alert_ge_zero", "min_stock_alert >= 0");
                    table.CheckConstraint("ck_inventory_stocks_quantity_ge_zero", "quantity >= 0");
                    table.CheckConstraint("ck_inventory_stocks_reserved_ge_zero", "reserved_quantity >= 0");
                    table.CheckConstraint("ck_inventory_stocks_reserved_le_quantity", "reserved_quantity <= quantity");
                    table.ForeignKey(
                        name: "FK_inventory_stocks_product_variants_variant_id_product_id",
                        columns: x => new { x.variant_id, x.product_id },
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_stocks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordered_quantity = table.Column<int>(type: "integer", nullable: false),
                    received_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_items", x => x.id);
                    table.CheckConstraint("ck_po_items_ordered_qty_gt_zero", "ordered_quantity > 0");
                    table.CheckConstraint("ck_po_items_received_qty_valid", "received_quantity >= 0 AND received_quantity <= ordered_quantity");
                    table.CheckConstraint("ck_po_items_unit_cost_ge_zero", "unit_cost >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_items_product_variants_variant_id_product_id",
                        columns: x => new { x.variant_id, x.product_id },
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "serial_imeis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    imei = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "InStock"),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warranty_months = table.Column<int>(type: "integer", nullable: false, defaultValue: 12),
                    warranty_start_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    warranty_end_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_imeis", x => x.id);
                    table.UniqueConstraint("AK_serial_imeis_id_product_id", x => new { x.id, x.product_id });
                    table.CheckConstraint("ck_serial_imeis_identifier_present", "NULLIF(BTRIM(serial_number), '') IS NOT NULL OR NULLIF(BTRIM(imei), '') IS NOT NULL");
                    table.CheckConstraint("ck_serial_imeis_imei_format", "imei IS NULL OR imei ~ '^[0-9]{15}$'");
                    table.CheckConstraint("ck_serial_imeis_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
                    table.CheckConstraint("ck_serial_imeis_warranty_months", "warranty_months >= 0");
                    table.ForeignKey(
                        name: "FK_serial_imeis_product_variants_variant_id_product_id",
                        columns: x => new { x.variant_id, x.product_id },
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_serial_imeis_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_serial_imeis_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    inventory_stock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "text", nullable: false),
                    quantity_change = table.Column<int>(type: "integer", nullable: false),
                    quantity_after = table.Column<int>(type: "integer", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.id);
                    table.CheckConstraint("ck_inventory_movements_qty_after_ge_zero", "quantity_after >= 0");
                    table.CheckConstraint("ck_inventory_movements_qty_change_not_zero", "quantity_change <> 0");
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventory_stocks_inventory_stock_id",
                        column: x => x.inventory_stock_id,
                        principalTable: "inventory_stocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_purchase_order_items_purchase_order_ite~",
                        column: x => x.purchase_order_item_id,
                        principalTable: "purchase_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serial_imei_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    variant_name = table.Column<string>(type: "text", nullable: true),
                    sku = table.Column<string>(type: "text", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.UniqueConstraint("AK_order_items_id_order_id", x => new { x.id, x.order_id });
                    table.CheckConstraint("chk_order_items_serial_single_unit", "serial_imei_id IS NULL OR quantity = 1");
                    table.CheckConstraint("ck_order_items_discount_ge_zero", "discount_amount >= 0");
                    table.CheckConstraint("ck_order_items_quantity_gt_zero", "quantity > 0");
                    table.CheckConstraint("ck_order_items_total_price_ge_zero", "total_price >= 0");
                    table.CheckConstraint("ck_order_items_unit_cost_valid", "unit_cost IS NULL OR unit_cost >= 0");
                    table.CheckConstraint("ck_order_items_unit_price_ge_zero", "unit_price >= 0");
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_product_variants_variant_id_product_id",
                        columns: x => new { x.variant_id, x.product_id },
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_serial_imeis_serial_imei_id_product_id",
                        columns: x => new { x.serial_imei_id, x.product_id },
                        principalTable: "serial_imeis",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_method = table.Column<string>(type: "text", nullable: false),
                    refund_pos_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    is_restocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_returns", x => x.id);
                    table.CheckConstraint("ck_order_returns_qty_gt_zero", "quantity > 0");
                    table.CheckConstraint("ck_order_returns_refund_amt_ge_zero", "refund_amount >= 0");
                    table.ForeignKey(
                        name: "FK_order_returns_order_items_order_item_id_order_id",
                        columns: x => new { x.order_item_id, x.order_id },
                        principalTable: "order_items",
                        principalColumns: new[] { "id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_returns_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_returns_users_processed_by_user_id",
                        column: x => x.processed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "idx_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cust_loyalty_customer_id",
                table: "customer_loyalty_points",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_cust_loyalty_order_id",
                table: "customer_loyalty_points",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_customers_phone",
                table: "customers",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_customers_tier",
                table: "customers",
                column: "tier");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_movements_order_item_id",
                table: "inventory_movements",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_movements_po_item_id",
                table: "inventory_movements",
                column: "purchase_order_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_movements_return_id",
                table: "inventory_movements",
                column: "order_return_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_movements_stock_created_at",
                table: "inventory_movements",
                columns: new[] { "inventory_stock_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_inventory_movements_user_id",
                table: "inventory_movements",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_stocks_product_variant",
                table: "inventory_stocks",
                columns: new[] { "product_id", "variant_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "idx_inventory_stocks_variant_id",
                table: "inventory_stocks",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stocks_variant_id_product_id",
                table: "inventory_stocks",
                columns: new[] { "variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "idx_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_items_serial_imei_id",
                table: "order_items",
                column: "serial_imei_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_items_variant_id",
                table: "order_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_serial_imei_id_product_id",
                table: "order_items",
                columns: new[] { "serial_imei_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_variant_id_product_id",
                table: "order_items",
                columns: new[] { "variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "uq_order_items_id_order",
                table: "order_items",
                columns: new[] { "id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_order_items_order_serial",
                table: "order_items",
                columns: new[] { "order_id", "serial_imei_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_item_id",
                table: "order_returns",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_order_id",
                table: "order_returns",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_refund_session_id",
                table: "order_returns",
                column: "refund_pos_session_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_refunded_at",
                table: "order_returns",
                column: "refunded_at");

            migrationBuilder.CreateIndex(
                name: "idx_order_returns_user_id",
                table: "order_returns",
                column: "processed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_returns_order_item_id_order_id",
                table: "order_returns",
                columns: new[] { "order_item_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "idx_orders_cashier_id",
                table: "orders",
                column: "cashier_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_code",
                table: "orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_orders_created_at",
                table: "orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_session_id",
                table: "orders",
                column: "pos_session_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_orders_voucher_id",
                table: "orders",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "idx_pos_sessions_opened_at",
                table: "pos_sessions",
                column: "opened_at");

            migrationBuilder.CreateIndex(
                name: "idx_pos_sessions_status",
                table: "pos_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_pos_sessions_cashier_open",
                table: "pos_sessions",
                column: "cashier_user_id",
                unique: true,
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "idx_product_variants_barcode",
                table: "product_variants",
                column: "barcode");

            migrationBuilder.CreateIndex(
                name: "idx_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_product_variants_sku",
                table: "product_variants",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_product_variants_specs_gin",
                table: "product_variants",
                column: "specs")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "uq_product_variants_id_product",
                table: "product_variants",
                columns: new[] { "id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_products_barcode",
                table: "products",
                column: "barcode");

            migrationBuilder.CreateIndex(
                name: "idx_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_products_specs_gin",
                table: "products",
                column: "specs")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_po_items_po_id",
                table: "purchase_order_items",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "idx_po_items_product_id",
                table: "purchase_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_po_items_variant_id",
                table: "purchase_order_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_variant_id_product_id",
                table: "purchase_order_items",
                columns: new[] { "variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "uq_po_items_order_product_variant",
                table: "purchase_order_items",
                columns: new[] { "purchase_order_id", "product_id", "variant_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_created_by",
                table: "purchase_orders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_po_number",
                table: "purchase_orders",
                column: "po_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_received_by",
                table: "purchase_orders",
                column: "received_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_status",
                table: "purchase_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_active",
                table: "refresh_tokens",
                columns: new[] { "user_id", "token_hash" },
                filter: "is_revoked = false");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_imei",
                table: "serial_imeis",
                column: "imei",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_instock",
                table: "serial_imeis",
                columns: new[] { "product_id", "status" },
                filter: "status = 'InStock'");

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_po_id",
                table: "serial_imeis",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_product_id",
                table: "serial_imeis",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_serial",
                table: "serial_imeis",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_serial_imeis_variant_id",
                table: "serial_imeis",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_serial_imeis_variant_id_product_id",
                table: "serial_imeis",
                columns: new[] { "variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "uq_serial_imeis_id_product",
                table: "serial_imeis",
                columns: new[] { "id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_suppliers_phone",
                table: "suppliers",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "idx_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_code",
                table: "vietqr_transactions",
                column: "transaction_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_confirmed_by",
                table: "vietqr_transactions",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_expires_at",
                table: "vietqr_transactions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_session_id",
                table: "vietqr_transactions",
                column: "pos_session_id");

            migrationBuilder.CreateIndex(
                name: "idx_vietqr_tx_status",
                table: "vietqr_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_vietqr_confirmed_order",
                table: "vietqr_transactions",
                column: "order_id",
                unique: true,
                filter: "status = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "uq_vietqr_provider_transaction",
                table: "vietqr_transactions",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vouchers_active",
                table: "vouchers",
                columns: new[] { "is_active", "expires_at" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_vouchers_code",
                table: "vouchers",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_loyalty_points");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "order_returns");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "vietqr_transactions");

            migrationBuilder.DropTable(
                name: "inventory_stocks");

            migrationBuilder.DropTable(
                name: "purchase_order_items");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "pos_sessions");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "serial_imeis");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
