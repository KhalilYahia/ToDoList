using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsManager.Repository.Migrations
{
    /// <inheritdoc />
    public partial class firs_one : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    monthly_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    yearly_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    max_users = table.Column<int>(type: "integer", nullable: false),
                    max_branches = table.Column<int>(type: "integer", nullable: false),
                    max_storage_mb = table.Column<int>(type: "integer", nullable: false),
                    features = table.Column<string>(type: "jsonb", nullable: false),
                    grace_period_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                    table.CheckConstraint("ck_subscription_plans_limits", "\"max_users\" > 0 AND \"max_branches\" > 0 AND \"max_storage_mb\" >= 0");
                    table.CheckConstraint("ck_subscription_plans_prices", "(\"monthly_price\" IS NULL OR \"monthly_price\" >= 0) AND (\"yearly_price\" IS NULL OR \"yearly_price\" >= 0)");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    profile_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    account_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: false),
                    new_values = table.Column<string>(type: "jsonb", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: false),
                    new_values = table.Column<string>(type: "jsonb", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_platform_audit_logs_platform_users_actor_platform_user_id",
                        column: x => x.actor_platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    billing_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    trial_started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    trial_ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    grace_period_ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    activated_by_platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suspended_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    suspended_by_platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_subscriptions", x => x.id);
                    table.CheckConstraint("ck_organization_subscriptions_period", "\"ends_at\" IS NULL OR \"starts_at\" IS NULL OR \"ends_at\" >= \"starts_at\"");
                    table.CheckConstraint("ck_organization_subscriptions_trial", "\"trial_ends_at\" IS NULL OR \"trial_started_at\" IS NULL OR \"trial_ends_at\" > \"trial_started_at\"");
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_platform_users_activated_by_plat~",
                        column: x => x.activated_by_platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_members_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_refresh_tokens_owner", "(\"user_id\" IS NOT NULL AND \"platform_user_id\" IS NULL AND \"organization_id\" IS NOT NULL) OR (\"user_id\" IS NULL AND \"platform_user_id\" IS NOT NULL AND \"organization_id\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_refresh_tokens_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_platform_users_platform_user_id",
                        column: x => x.platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id",
                        column: x => x.replaced_by_token_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    supervisor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                    table.ForeignKey(
                        name: "FK_departments_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_departments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_departments_users_supervisor_user_id",
                        column: x => x.supervisor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manual_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payment_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by_platform_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_payments", x => x.id);
                    table.CheckConstraint("ck_manual_payments_amount", "\"amount\" >= 0");
                    table.CheckConstraint("ck_manual_payments_period", "\"period_end\" >= \"period_start\"");
                    table.ForeignKey(
                        name: "FK_manual_payments_organization_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "organization_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payments_platform_users_recorded_by_platform_user_id",
                        column: x => x.recorded_by_platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    new_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    old_ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    new_ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_by_platform_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_history_organization_subscriptions_subscriptio~",
                        column: x => x.subscription_id,
                        principalTable: "organization_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscription_history_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscription_history_platform_users_changed_by_platform_use~",
                        column: x => x.changed_by_platform_user_id,
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    target_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    visibility = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.id);
                    table.ForeignKey(
                        name: "FK_complaints_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaints_departments_target_department_id",
                        column: x => x.target_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaints_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    source_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    allow_custom_items = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_templates", x => x.id);
                    table.CheckConstraint("ck_order_templates_departments", "\"source_department_id\" <> \"target_department_id\"");
                    table.ForeignKey(
                        name: "FK_order_templates_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_templates_departments_source_department_id",
                        column: x => x.source_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_templates_departments_target_department_id",
                        column: x => x.target_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_templates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    default_priority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_templates", x => x.id);
                    table.CheckConstraint("ck_task_templates_duration", "\"default_duration_minutes\" IS NULL OR \"default_duration_minutes\" > 0");
                    table.ForeignKey(
                        name: "FK_task_templates_departments_default_department_id",
                        column: x => x.default_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_templates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_departments", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_departments_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_departments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_departments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complaint_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaint_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_complaint_messages_complaints_complaint_id",
                        column: x => x.complaint_id,
                        principalTable: "complaints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    unit_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    custom_unit_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    default_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    minimum_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_template_items", x => x.id);
                    table.CheckConstraint("ck_order_template_items_custom_unit", "\"unit_code\" <> 'Custom' OR NULLIF(BTRIM(\"custom_unit_label\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_order_template_items_default_quantity", "\"default_quantity\" IS NULL OR \"default_quantity\" >= 0");
                    table.CheckConstraint("ck_order_template_items_minimum_quantity", "\"minimum_quantity\" IS NULL OR \"minimum_quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_order_template_items_order_templates_order_template_id",
                        column: x => x.order_template_id,
                        principalTable: "order_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_template_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recurrence_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    weekdays = table.Column<short[]>(type: "smallint[]", nullable: false),
                    month_days = table.Column<short[]>(type: "smallint[]", nullable: false),
                    include_last_day_of_month = table.Column<bool>(type: "boolean", nullable: false),
                    recurrence_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    recurrence_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    execution_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    execution_due_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    execution_due_day_offset = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedules", x => x.id);
                    table.CheckConstraint("ck_task_schedules_dates", "\"recurrence_end_date\" IS NULL OR \"recurrence_end_date\" >= \"recurrence_start_date\"");
                    table.CheckConstraint("ck_task_schedules_due_offset", "\"execution_due_day_offset\" IN (0, 1) AND (\"execution_due_day_offset\" = 1 OR \"execution_due_time\" > \"execution_start_time\")");
                    table.CheckConstraint("ck_task_schedules_recurrence_fields", "(\"recurrence_type\" = 'Daily' AND cardinality(\"weekdays\") = 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\") OR (\"recurrence_type\" = 'Weekly' AND cardinality(\"weekdays\") > 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\") OR (\"recurrence_type\" = 'Monthly' AND cardinality(\"weekdays\") = 0 AND (cardinality(\"month_days\") > 0 OR \"include_last_day_of_month\")) OR (\"recurrence_type\" = 'SpecificDates' AND cardinality(\"weekdays\") = 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\")");
                    table.ForeignKey(
                        name: "FK_task_schedules_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedules_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedules_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedules_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    evidence_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_template_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_template_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_template_items_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complaint_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    file_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaint_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_complaint_attachments_complaint_messages_complaint_message_~",
                        column: x => x.complaint_message_id,
                        principalTable: "complaint_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_attachments_complaints_complaint_id",
                        column: x => x.complaint_id,
                        principalTable: "complaints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_attachments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_distributions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignment_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_start_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_distributions", x => x.id);
                    table.CheckConstraint("ck_task_distributions_due_after_start", "\"due_at\" > \"scheduled_start_at\"");
                    table.ForeignKey(
                        name: "FK_task_distributions_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_distributions_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_distributions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_distributions_task_schedules_task_schedule_id",
                        column: x => x.task_schedule_id,
                        principalTable: "task_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_distributions_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_schedule_assignees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedule_assignees", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_schedule_assignees_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_assignees_task_schedules_task_schedule_id",
                        column: x => x.task_schedule_id,
                        principalTable: "task_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_assignees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_schedule_dates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedule_dates", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_schedule_dates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_dates_task_schedules_task_schedule_id",
                        column: x => x.task_schedule_id,
                        principalTable: "task_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_template_item_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_template_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    file_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_template_item_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_template_item_attachments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_template_item_attachments_task_template_items_task_tem~",
                        column: x => x.task_template_item_id,
                        principalTable: "task_template_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_distribution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_start_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    priority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    is_schedule_override = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    submitted_for_approval_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    blocked_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.CheckConstraint("ck_tasks_due_after_start", "\"due_at\" > \"scheduled_start_at\"");
                    table.ForeignKey(
                        name: "FK_tasks_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_task_distributions_task_distribution_id",
                        column: x => x.task_distribution_id,
                        principalTable: "task_distributions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_task_schedules_task_schedule_id",
                        column: x => x.task_schedule_id,
                        principalTable: "task_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_tasks_parent_task_id",
                        column: x => x.parent_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    priority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    required_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    general_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    delivered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_orders", x => x.id);
                    table.CheckConstraint("ck_department_orders_departments", "\"source_department_id\" <> \"target_department_id\"");
                    table.ForeignKey(
                        name: "FK_department_orders_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_orders_departments_source_department_id",
                        column: x => x.source_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_orders_departments_target_department_id",
                        column: x => x.target_department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_orders_order_templates_order_template_id",
                        column: x => x.order_template_id,
                        principalTable: "order_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_orders_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_orders_tasks_linked_task_id",
                        column: x => x.linked_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_assignment_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_assignee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_assignment_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_assignment_history_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_assignment_history_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_assignment_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_assignment_history_users_new_assignee_user_id",
                        column: x => x.new_assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_assignment_history_users_previous_assignee_user_id",
                        column: x => x.previous_assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    evidence_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_items_task_template_items_template_item_id",
                        column: x => x.template_item_id,
                        principalTable: "task_template_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_items_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    new_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_status_history_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_status_history_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_description_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    unit_code_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    custom_unit_label_snapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    requested_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    fulfilled_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fulfillment_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_custom_item = table.Column<bool>(type: "boolean", nullable: false),
                    prepared_by = table.Column<Guid>(type: "uuid", nullable: true),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_order_items", x => x.id);
                    table.CheckConstraint("ck_department_order_items_custom_unit", "\"unit_code_snapshot\" <> 'Custom' OR NULLIF(BTRIM(\"custom_unit_label_snapshot\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_department_order_items_fulfilled_quantity", "\"fulfilled_quantity\" >= 0");
                    table.CheckConstraint("ck_department_order_items_received_quantity", "\"received_quantity\" >= 0");
                    table.CheckConstraint("ck_department_order_items_requested_quantity", "\"requested_quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_department_order_items_department_orders_department_order_id",
                        column: x => x.department_order_id,
                        principalTable: "department_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_order_items_order_template_items_template_item_id",
                        column: x => x.template_item_id,
                        principalTable: "order_template_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_order_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_order_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    new_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_department_order_status_history_department_orders_departmen~",
                        column: x => x.department_order_id,
                        principalTable: "department_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_order_status_history_organizations_organization_~",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    file_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    attachment_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_attachments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_attachments_task_items_task_item_id",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_attachments_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_order_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    file_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_order_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_department_order_attachments_department_order_items_order_i~",
                        column: x => x.order_item_id,
                        principalTable: "department_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_order_attachments_department_orders_department_o~",
                        column: x => x.department_order_id,
                        principalTable: "department_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_order_attachments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_organization_id_created_at",
                table: "audit_logs",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_organization_id_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "organization_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_branches_organization_id_is_primary",
                table: "branches",
                columns: new[] { "organization_id", "is_primary" },
                unique: true,
                filter: "\"is_primary\" AND \"is_active\" AND \"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_branches_organization_id_name",
                table: "branches",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_complaint_attachments_complaint_id",
                table: "complaint_attachments",
                column: "complaint_id");

            migrationBuilder.CreateIndex(
                name: "IX_complaint_attachments_complaint_message_id",
                table: "complaint_attachments",
                column: "complaint_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_complaint_attachments_organization_id",
                table: "complaint_attachments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_complaint_messages_complaint_id_created_at",
                table: "complaint_messages",
                columns: new[] { "complaint_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_complaint_messages_organization_id_is_internal",
                table: "complaint_messages",
                columns: new[] { "organization_id", "is_internal" });

            migrationBuilder.CreateIndex(
                name: "IX_complaints_branch_id",
                table: "complaints",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_organization_id_complaint_number",
                table: "complaints",
                columns: new[] { "organization_id", "complaint_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_complaints_organization_id_status_created_at",
                table: "complaints",
                columns: new[] { "organization_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_complaints_target_department_id",
                table: "complaints",
                column: "target_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_attachments_department_order_id",
                table: "department_order_attachments",
                column: "department_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_attachments_order_item_id",
                table: "department_order_attachments",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_attachments_organization_id",
                table: "department_order_attachments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_items_department_order_id",
                table: "department_order_items",
                column: "department_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_items_organization_id",
                table: "department_order_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_items_template_item_id",
                table: "department_order_items",
                column: "template_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_order_status_history_department_order_id_created~",
                table: "department_order_status_history",
                columns: new[] { "department_order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_department_order_status_history_organization_id",
                table: "department_order_status_history",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_branch_id",
                table: "department_orders",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_linked_task_id",
                table: "department_orders",
                column: "linked_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_order_template_id",
                table: "department_orders",
                column: "order_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_organization_id_order_number",
                table: "department_orders",
                columns: new[] { "organization_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_organization_id_source_department_id_crea~",
                table: "department_orders",
                columns: new[] { "organization_id", "source_department_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_organization_id_target_department_id_stat~",
                table: "department_orders",
                columns: new[] { "organization_id", "target_department_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_source_department_id",
                table: "department_orders",
                column: "source_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_orders_target_department_id",
                table: "department_orders",
                column: "target_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_departments_branch_id_name",
                table: "departments",
                columns: new[] { "branch_id", "name" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_departments_organization_id_is_active",
                table: "departments",
                columns: new[] { "organization_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_departments_supervisor_user_id",
                table: "departments",
                column: "supervisor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payments_organization_id_payment_status_paid_at",
                table: "manual_payments",
                columns: new[] { "organization_id", "payment_status", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payments_recorded_by_platform_user_id",
                table: "manual_payments",
                column: "recorded_by_platform_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payments_subscription_id",
                table: "manual_payments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_organization_id_user_id_is_read_created_at",
                table: "notifications",
                columns: new[] { "organization_id", "user_id", "is_read", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_template_items_order_template_id_sort_order",
                table: "order_template_items",
                columns: new[] { "order_template_id", "sort_order" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_order_template_items_organization_id",
                table: "order_template_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_templates_branch_id",
                table: "order_templates",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_templates_organization_id_branch_id_name",
                table: "order_templates",
                columns: new[] { "organization_id", "branch_id", "name" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_order_templates_source_department_id",
                table: "order_templates",
                column: "source_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_templates_target_department_id",
                table: "order_templates",
                column: "target_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_organization_id_role_is_active",
                table: "organization_members",
                columns: new[] { "organization_id", "role", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_organization_id_user_id",
                table: "organization_members",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_user_id",
                table: "organization_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_activated_by_platform_user_id",
                table: "organization_subscriptions",
                column: "activated_by_platform_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_organization_id_ends_at",
                table: "organization_subscriptions",
                columns: new[] { "organization_id", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_organization_id_status",
                table: "organization_subscriptions",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_plan_id",
                table: "organization_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                table: "organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_status",
                table: "organizations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_actor_platform_user_id",
                table: "platform_audit_logs",
                column: "actor_platform_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_organization_id_created_at",
                table: "platform_audit_logs",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_normalized_email",
                table: "platform_users",
                column: "normalized_email",
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_organization_id",
                table: "refresh_tokens",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_platform_user_id_expires_at",
                table: "refresh_tokens",
                columns: new[] { "platform_user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_replaced_by_token_id",
                table: "refresh_tokens",
                column: "replaced_by_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_expires_at",
                table: "refresh_tokens",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_history_changed_by_platform_user_id",
                table: "subscription_history",
                column: "changed_by_platform_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_history_organization_id",
                table: "subscription_history",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_history_subscription_id_created_at",
                table: "subscription_history",
                columns: new[] { "subscription_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_code",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_is_active",
                table: "subscription_plans",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_history_changed_by",
                table: "task_assignment_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_history_new_assignee_user_id",
                table: "task_assignment_history",
                column: "new_assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_history_organization_id_task_id_occurred_at",
                table: "task_assignment_history",
                columns: new[] { "organization_id", "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_history_previous_assignee_user_id",
                table: "task_assignment_history",
                column: "previous_assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_history_task_id",
                table: "task_assignment_history",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_organization_id",
                table: "task_attachments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_task_id",
                table: "task_attachments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_task_item_id",
                table: "task_attachments",
                column: "task_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_distributions_branch_id",
                table: "task_distributions",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_distributions_department_id",
                table: "task_distributions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_distributions_organization_id_department_id_occurrence~",
                table: "task_distributions",
                columns: new[] { "organization_id", "department_id", "occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "IX_task_distributions_task_schedule_id_occurrence_date_schedul~",
                table: "task_distributions",
                columns: new[] { "task_schedule_id", "occurrence_date", "scheduled_start_at" },
                unique: true,
                filter: "\"task_schedule_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_task_distributions_task_template_id",
                table: "task_distributions",
                column: "task_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_organization_id",
                table: "task_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_task_id_sort_order",
                table: "task_items",
                columns: new[] { "task_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_items_template_item_id",
                table: "task_items",
                column: "template_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_assignees_organization_id_user_id",
                table: "task_schedule_assignees",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_assignees_task_schedule_id_user_id",
                table: "task_schedule_assignees",
                columns: new[] { "task_schedule_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_assignees_user_id",
                table: "task_schedule_assignees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_dates_organization_id_task_schedule_id",
                table: "task_schedule_dates",
                columns: new[] { "organization_id", "task_schedule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_dates_task_schedule_id_occurrence_date",
                table: "task_schedule_dates",
                columns: new[] { "task_schedule_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_branch_id",
                table: "task_schedules",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_department_id",
                table: "task_schedules",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_organization_id_is_active",
                table: "task_schedules",
                columns: new[] { "organization_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_organization_id_recurrence_start_date",
                table: "task_schedules",
                columns: new[] { "organization_id", "recurrence_start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_task_template_id",
                table: "task_schedules",
                column: "task_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_status_history_organization_id_task_id_occurred_at",
                table: "task_status_history",
                columns: new[] { "organization_id", "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_status_history_task_id",
                table: "task_status_history",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_item_attachments_organization_id",
                table: "task_template_item_attachments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_item_attachments_task_template_item_id",
                table: "task_template_item_attachments",
                column: "task_template_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_items_organization_id",
                table: "task_template_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_items_task_template_id_sort_order",
                table: "task_template_items",
                columns: new[] { "task_template_id", "sort_order" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_default_department_id",
                table: "task_templates",
                column: "default_department_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_organization_id_default_department_id",
                table: "task_templates",
                columns: new[] { "organization_id", "default_department_id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_organization_id_is_active",
                table: "task_templates",
                columns: new[] { "organization_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_assignee_user_id",
                table: "tasks",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_branch_id",
                table: "tasks",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_department_id",
                table: "tasks",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_organization_id_assignee_user_id_occurrence_date",
                table: "tasks",
                columns: new[] { "organization_id", "assignee_user_id", "occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_organization_id_assignee_user_id_status",
                table: "tasks",
                columns: new[] { "organization_id", "assignee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_organization_id_department_id_status",
                table: "tasks",
                columns: new[] { "organization_id", "department_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_organization_id_occurrence_date",
                table: "tasks",
                columns: new[] { "organization_id", "occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_parent_task_id",
                table: "tasks",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_task_distribution_id_assignee_user_id",
                table: "tasks",
                columns: new[] { "task_distribution_id", "assignee_user_id" },
                unique: true,
                filter: "\"task_distribution_id\" IS NOT NULL AND \"assignee_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_task_schedule_id_occurrence_date_scheduled_start_at_a~",
                table: "tasks",
                columns: new[] { "task_schedule_id", "occurrence_date", "scheduled_start_at", "assignee_user_id" },
                unique: true,
                filter: "\"task_schedule_id\" IS NOT NULL AND \"assignee_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_task_template_id",
                table: "tasks",
                column: "task_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_department_id",
                table: "user_departments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_organization_id_department_id",
                table: "user_departments",
                columns: new[] { "organization_id", "department_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_user_id_department_id",
                table: "user_departments",
                columns: new[] { "user_id", "department_id" },
                unique: true,
                filter: "\"left_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "\"normalized_email\" IS NOT NULL AND \"deleted_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "complaint_attachments");

            migrationBuilder.DropTable(
                name: "department_order_attachments");

            migrationBuilder.DropTable(
                name: "department_order_status_history");

            migrationBuilder.DropTable(
                name: "manual_payments");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "organization_members");

            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "subscription_history");

            migrationBuilder.DropTable(
                name: "task_assignment_history");

            migrationBuilder.DropTable(
                name: "task_attachments");

            migrationBuilder.DropTable(
                name: "task_schedule_assignees");

            migrationBuilder.DropTable(
                name: "task_schedule_dates");

            migrationBuilder.DropTable(
                name: "task_status_history");

            migrationBuilder.DropTable(
                name: "task_template_item_attachments");

            migrationBuilder.DropTable(
                name: "user_departments");

            migrationBuilder.DropTable(
                name: "complaint_messages");

            migrationBuilder.DropTable(
                name: "department_order_items");

            migrationBuilder.DropTable(
                name: "organization_subscriptions");

            migrationBuilder.DropTable(
                name: "task_items");

            migrationBuilder.DropTable(
                name: "complaints");

            migrationBuilder.DropTable(
                name: "department_orders");

            migrationBuilder.DropTable(
                name: "order_template_items");

            migrationBuilder.DropTable(
                name: "platform_users");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "task_template_items");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "order_templates");

            migrationBuilder.DropTable(
                name: "task_distributions");

            migrationBuilder.DropTable(
                name: "task_schedules");

            migrationBuilder.DropTable(
                name: "task_templates");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
