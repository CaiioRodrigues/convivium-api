using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convivium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_cycles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competence = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    apportionable_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reserve_fund_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    reserve_fund_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    charged_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_cycles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "condominiums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_district = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    address_city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_zip_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    billing_due_day = table.Column<int>(type: "integer", nullable: false),
                    billing_reserve_fund_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    billing_late_fee_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    billing_monthly_interest_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    billing_default_apportionment_method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pix_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    pix_key_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    pix_receiver_name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    pix_receiver_city = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condominiums", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_address = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    to_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    text_body = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attach_charge_pdf = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_apportionable = table.Column<bool>(type: "boolean", nullable: false),
                    is_group = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_accounts_ledger_accounts_parent_id",
                        column: x => x.parent_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_people", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "utility_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raw_text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reference_month = table.Column<int>(type: "integer", nullable: true),
                    installation_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consumption_kwh = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    consumption_cubic_meters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    barcode_line = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    parse_warnings = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    imported_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utility_bills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bank_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    agency = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    account_number = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    opening_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_reserve_fund = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_bank_accounts_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_blocks_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_memberships_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_memberships_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    competence = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_apportionable = table.Column<bool>(type: "boolean", nullable: false),
                    document_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    utility_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_expenses_ledger_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    competence = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    document_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reconciled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_entries_bank_accounts_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ledger_entries_ledger_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_id = table.Column<Guid>(type: "uuid", nullable: true),
                    identifier = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ideal_fraction = table.Column<decimal>(type: "numeric(12,8)", precision: 12, scale: 8, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_units_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_units_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_cycle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    competence = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    public_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pix_payload = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    external_slip_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    barcode_line = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                    table.ForeignKey(
                        name: "fk_charges_billing_cycles_billing_cycle_id",
                        column: x => x.billing_cycle_id,
                        principalTable: "billing_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_charges_people_payer_person_id",
                        column: x => x.payer_person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_charges_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unit_occupancies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_billing_responsible = table.Column<bool>(type: "boolean", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit_occupancies", x => x.id);
                    table.ForeignKey(
                        name: "fk_unit_occupancies_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_unit_occupancies_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charge_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charge_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_charge_items_charges_charge_id",
                        column: x => x.charge_id,
                        principalTable: "charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_charge_items_ledger_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registered_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_charges_charge_id",
                        column: x => x.charge_id,
                        principalTable: "charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_accounts_condominium_id_name",
                table: "bank_accounts",
                columns: new[] { "condominium_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_billing_cycles_condominium_id_competence",
                table: "billing_cycles",
                columns: new[] { "condominium_id", "competence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blocks_condominium_id_name",
                table: "blocks",
                columns: new[] { "condominium_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charge_items_charge_id",
                table: "charge_items",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_charge_items_ledger_account_id",
                table: "charge_items",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_charges_billing_cycle_id_unit_id",
                table: "charges",
                columns: new[] { "billing_cycle_id", "unit_id" },
                unique: true,
                filter: "billing_cycle_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_charges_condominium_id_status_due_date",
                table: "charges",
                columns: new[] { "condominium_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_condominium_id_unit_id_competence",
                table: "charges",
                columns: new[] { "condominium_id", "unit_id", "competence" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_payer_person_id",
                table: "charges",
                column: "payer_person_id");

            migrationBuilder.CreateIndex(
                name: "ix_charges_public_token",
                table: "charges",
                column: "public_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_unit_id",
                table: "charges",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_condominiums_cnpj",
                table: "condominiums",
                column: "cnpj",
                unique: true,
                filter: "cnpj IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_charge_id",
                table: "email_messages",
                column: "charge_id",
                filter: "charge_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_condominium_id",
                table: "email_messages",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_status_scheduled_for",
                table: "email_messages",
                columns: new[] { "status", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_condominium_id_competence_status",
                table: "expenses",
                columns: new[] { "condominium_id", "competence", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_condominium_id_due_date_status",
                table: "expenses",
                columns: new[] { "condominium_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_ledger_account_id",
                table: "expenses",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_supplier_id",
                table: "expenses",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_utility_bill_id",
                table: "expenses",
                column: "utility_bill_id",
                filter: "utility_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_accounts_condominium_id_code",
                table: "ledger_accounts",
                columns: new[] { "condominium_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_accounts_parent_id",
                table: "ledger_accounts",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_bank_account_id",
                table: "ledger_entries",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_condominium_id_bank_account_id_date",
                table: "ledger_entries",
                columns: new[] { "condominium_id", "bank_account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_condominium_id_competence_ledger_account_id",
                table: "ledger_entries",
                columns: new[] { "condominium_id", "competence", "ledger_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_expense_id",
                table: "ledger_entries",
                column: "expense_id",
                filter: "expense_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_ledger_account_id",
                table: "ledger_entries",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_payment_id",
                table: "ledger_entries",
                column: "payment_id",
                filter: "payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_condominium_id_person_id",
                table: "memberships",
                columns: new[] { "condominium_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_memberships_person_id",
                table: "memberships",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_charge_id",
                table: "payments",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_condominium_id_external_id",
                table: "payments",
                columns: new[] { "condominium_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_condominium_id_paid_on",
                table: "payments",
                columns: new[] { "condominium_id", "paid_on" });

            migrationBuilder.CreateIndex(
                name: "ix_people_cpf",
                table: "people",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_people_email",
                table: "people",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_person_id_expires_at",
                table: "refresh_tokens",
                columns: new[] { "person_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_condominium_id_document",
                table: "suppliers",
                columns: new[] { "condominium_id", "document" },
                unique: true,
                filter: "document IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_condominium_id_name",
                table: "suppliers",
                columns: new[] { "condominium_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_unit_occupancies_condominium_id",
                table: "unit_occupancies",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ix_unit_occupancies_person_id",
                table: "unit_occupancies",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_unit_occupancies_unit_id_person_id_relation",
                table: "unit_occupancies",
                columns: new[] { "unit_id", "person_id", "relation" });

            migrationBuilder.CreateIndex(
                name: "ix_units_block_id",
                table: "units",
                column: "block_id");

            migrationBuilder.CreateIndex(
                name: "ix_units_condominium_id_block_id_identifier",
                table: "units",
                columns: new[] { "condominium_id", "block_id", "identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utility_bills_condominium_id_content_hash",
                table: "utility_bills",
                columns: new[] { "condominium_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utility_bills_condominium_id_provider_status",
                table: "utility_bills",
                columns: new[] { "condominium_id", "provider", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_items");

            migrationBuilder.DropTable(
                name: "email_messages");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "unit_occupancies");

            migrationBuilder.DropTable(
                name: "utility_bills");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "ledger_accounts");

            migrationBuilder.DropTable(
                name: "charges");

            migrationBuilder.DropTable(
                name: "billing_cycles");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "blocks");

            migrationBuilder.DropTable(
                name: "condominiums");
        }
    }
}
