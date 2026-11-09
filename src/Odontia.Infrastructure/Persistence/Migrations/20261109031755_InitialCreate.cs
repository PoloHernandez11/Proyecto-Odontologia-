using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Specialty = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    LicenseNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SecurityStamp = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Consultorios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    LegalName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Website = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddressLine = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    TimeZoneId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    DefaultAppointmentMinutes = table.Column<int>(type: "int", nullable: false),
                    SlotIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    ReminderHoursBefore = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consultorios", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DentalPieces",
                columns: table => new
                {
                    Code = table.Column<int>(type: "int", nullable: false),
                    Quadrant = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Dentition = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalPieces", x => x.Code);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DentalSurfaces",
                columns: table => new
                {
                    Code = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Abbreviation = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalSurfaces", x => x.Code);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClaimType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ClaimValue = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ProviderKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FamilyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedReason = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "char(36)", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserNotificationSettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotifyTaskAssigned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotifyTaskDue = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotifyLabUpdates = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotifyAppointmentChanges = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DailyAgendaSummary = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserNotificationSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConsultorioMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsProfessional = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultAppointmentMinutes = table.Column<int>(type: "int", nullable: true),
                    AgendaColor = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: true),
                    LockVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultorioMembers", x => x.Id);
                    table.UniqueConstraint("AK_ConsultorioMembers_ConsultorioId_UserId", x => new { x.ConsultorioId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ConsultorioMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultorioMembers_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConsultorioSequences",
                columns: table => new
                {
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultorioSequences", x => new { x.ConsultorioId, x.Name });
                    table.ForeignKey(
                        name: "FK_ConsultorioSequences_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Gabinetes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gabinetes", x => x.Id);
                    table.UniqueConstraint("AK_Gabinetes_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Gabinetes_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HealthInsurances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthInsurances", x => x.Id);
                    table.UniqueConstraint("AK_HealthInsurances_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_HealthInsurances_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsProfessional = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    ContactName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddressLine = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.Id);
                    table.UniqueConstraint("AK_Laboratories_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Laboratories_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    EntityType = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OdontogramConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: true),
                    MarksToothAbsent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdontogramConditions_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OutboundMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Recipient = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    Body = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                    Purpose = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundMessages_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Procedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    RequiresTooth = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procedures", x => x.Id);
                    table.UniqueConstraint("AK_Procedures_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Procedures_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScheduleBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    GabineteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBlocks_ConsultorioMembers_ConsultorioId_Professional~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleBlocks_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleBlocks_Gabinetes_ConsultorioId_GabineteId",
                        columns: x => new { x.ConsultorioId, x.GabineteId },
                        principalTable: "Gabinetes",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    GabineteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkingHours_ConsultorioMembers_ConsultorioId_ProfessionalUs~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingHours_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingHours_Gabinetes_ConsultorioId_GabineteId",
                        columns: x => new { x.ConsultorioId, x.GabineteId },
                        principalTable: "Gabinetes",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    DocumentNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Sex = table.Column<int>(type: "int", nullable: false),
                    Occupation = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    PhoneNormalized = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddressLine = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    HealthInsuranceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    InsurancePlan = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    AffiliateNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    AdministrativeNotes = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SearchName = table.Column<string>(type: "varchar(170)", maxLength: 170, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.UniqueConstraint("AK_Patients_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Patients_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Patients_HealthInsurances_ConsultorioId_HealthInsuranceId",
                        columns: x => new { x.ConsultorioId, x.HealthInsuranceId },
                        principalTable: "HealthInsurances",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InvitationRoles",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationRoles", x => new { x.InvitationId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_InvitationRoles_Invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvitationRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MemberRoles",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRoles", x => new { x.MemberId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_MemberRoles_ConsultorioMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "ConsultorioMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Permission = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    GabineteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    AdministrativeNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsOverbooking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ArrivedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AttendedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancellationReason = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    ReminderQueuedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<Guid>(type: "char(36)", nullable: false),
                    RescheduleCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.UniqueConstraint("AK_Appointments_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Appointments_ConsultorioMembers_ConsultorioId_ProfessionalUs~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Gabinetes_ConsultorioId_GabineteId",
                        columns: x => new { x.ConsultorioId, x.GabineteId },
                        principalTable: "Gabinetes",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalRecords", x => x.Id);
                    table.UniqueConstraint("AK_ClinicalRecords_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MedicalHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Diabetes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Hypertension = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HeartDisease = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Anticoagulated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Pregnant = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Asthma = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Hepatitis = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Smoker = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Allergies = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CurrentMedication = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    OtherConditions = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalHistories_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicalHistories_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Odontograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Odontograms", x => x.Id);
                    table.UniqueConstraint("AK_Odontograms_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Odontograms_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Odontograms_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PatientAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Details = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    VisibleToReception = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAlerts_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAlerts_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TreatmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPlans", x => x.Id);
                    table.UniqueConstraint("AK_TreatmentPlans_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_TreatmentPlans_ConsultorioMembers_ConsultorioId_Professional~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlans_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlans_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AppointmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Action = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentHistories_Appointments_ConsultorioId_AppointmentId",
                        columns: x => new { x.ConsultorioId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentHistories_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DueNotifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Appointments_ConsultorioId_AppointmentId",
                        columns: x => new { x.ConsultorioId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_ConsultorioMembers_ConsultorioId_AssignedToUserId",
                        columns: x => new { x.ConsultorioId, x.AssignedToUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClinicalRecordId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Indications = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    AppointmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsVoided = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    VoidReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntries", x => x.Id);
                    table.UniqueConstraint("AK_ClinicalEntries_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_Appointments_ConsultorioId_AppointmentId",
                        columns: x => new { x.ConsultorioId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_ClinicalRecords_ConsultorioId_ClinicalRecord~",
                        columns: x => new { x.ConsultorioId, x.ClinicalRecordId },
                        principalTable: "ClinicalRecords",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_ConsultorioMembers_ConsultorioId_Professiona~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OdontogramHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OdontogramId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: false),
                    Surface = table.Column<int>(type: "int", nullable: true),
                    ConditionId = table.Column<Guid>(type: "char(36)", nullable: true),
                    FindingId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_ConsultorioMembers_ConsultorioId_Professio~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_DentalSurfaces_Surface",
                        column: x => x.Surface,
                        principalTable: "DentalSurfaces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_OdontogramConditions_ConditionId",
                        column: x => x.ConditionId,
                        principalTable: "OdontogramConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramHistory_Odontograms_ConsultorioId_OdontogramId",
                        columns: x => new { x.ConsultorioId, x.OdontogramId },
                        principalTable: "Odontograms",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ToothNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OdontogramId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WrittenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToothNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToothNotes_ConsultorioMembers_ConsultorioId_ProfessionalUser~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothNotes_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothNotes_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothNotes_Odontograms_ConsultorioId_OdontogramId",
                        columns: x => new { x.ConsultorioId, x.OdontogramId },
                        principalTable: "Odontograms",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.UniqueConstraint("AK_Budgets_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Budgets_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Budgets_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Budgets_TreatmentPlans_ConsultorioId_TreatmentPlanId",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanId },
                        principalTable: "TreatmentPlans",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TreatmentChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TreatmentPlanItemId = table.Column<Guid>(type: "char(36)", nullable: true),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentChanges_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentChanges_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentChanges_TreatmentPlans_ConsultorioId_TreatmentPlanId",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanId },
                        principalTable: "TreatmentPlans",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicalEntryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClinicalEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Indications = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Teeth = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryRevisions_AspNetUsers_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryRevisions_ClinicalEntries_ConsultorioId_Clinica~",
                        columns: x => new { x.ConsultorioId, x.ClinicalEntryId },
                        principalTable: "ClinicalEntries",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryRevisions_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicalEntryTeeth",
                columns: table => new
                {
                    ClinicalEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntryTeeth", x => new { x.ClinicalEntryId, x.ToothCode });
                    table.ForeignKey(
                        name: "FK_ClinicalEntryTeeth_ClinicalEntries_ClinicalEntryId",
                        column: x => x.ClinicalEntryId,
                        principalTable: "ClinicalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryTeeth_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Diagnoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ToothCode = table.Column<int>(type: "int", nullable: true),
                    Surface = table.Column<int>(type: "int", nullable: true),
                    DiagnosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClinicalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnoses", x => x.Id);
                    table.UniqueConstraint("AK_Diagnoses_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Diagnoses_ClinicalEntries_ConsultorioId_ClinicalEntryId",
                        columns: x => new { x.ConsultorioId, x.ClinicalEntryId },
                        principalTable: "ClinicalEntries",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diagnoses_ConsultorioMembers_ConsultorioId_ProfessionalUserId",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diagnoses_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diagnoses_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diagnoses_DentalSurfaces_Surface",
                        column: x => x.Surface,
                        principalTable: "DentalSurfaces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diagnoses_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ClinicalEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TreatmentPlanId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DeletionReason = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.UniqueConstraint("AK_Documents_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_ClinicalEntries_ConsultorioId_ClinicalEntryId",
                        columns: x => new { x.ConsultorioId, x.ClinicalEntryId },
                        principalTable: "ClinicalEntries",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_TreatmentPlans_ConsultorioId_TreatmentPlanId",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanId },
                        principalTable: "TreatmentPlans",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BudgetStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    BudgetId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetStatusChanges_Budgets_ConsultorioId_BudgetId",
                        columns: x => new { x.ConsultorioId, x.BudgetId },
                        principalTable: "Budgets",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetStatusChanges_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BudgetId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReceiptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    VoidReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.UniqueConstraint("AK_Payments_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_Payments_Budgets_ConsultorioId_BudgetId",
                        columns: x => new { x.ConsultorioId, x.BudgetId },
                        principalTable: "Budgets",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_ConsultorioMembers_ConsultorioId_ReceivedByUserId",
                        columns: x => new { x.ConsultorioId, x.ReceivedByUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TreatmentPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProcedureName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: true),
                    Surfaces = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: true),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DiagnosisId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPlanItems", x => x.Id);
                    table.UniqueConstraint("AK_TreatmentPlanItems_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_ConsultorioMembers_ConsultorioId_Professi~",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_Diagnoses_ConsultorioId_DiagnosisId",
                        columns: x => new { x.ConsultorioId, x.DiagnosisId },
                        principalTable: "Diagnoses",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_Procedures_ConsultorioId_ProcedureId",
                        columns: x => new { x.ConsultorioId, x.ProcedureId },
                        principalTable: "Procedures",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_TreatmentPlans_ConsultorioId_TreatmentPla~",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanId },
                        principalTable: "TreatmentPlans",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AcceptedByName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    AcceptedByDocument = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    AcceptedByRelationship = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DecisionRecordedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SignedDocumentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consents_AspNetUsers_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consents_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consents_Documents_ConsultorioId_SignedDocumentId",
                        columns: x => new { x.ConsultorioId, x.SignedDocumentId },
                        principalTable: "Documents",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consents_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consents_TreatmentPlans_ConsultorioId_TreatmentPlanId",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanId },
                        principalTable: "TreatmentPlans",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AccountMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    BudgetId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PaymentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReversesMovementId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMovements", x => x.Id);
                    table.UniqueConstraint("AK_AccountMovements_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_AccountMovements_AccountMovements_ConsultorioId_ReversesMove~",
                        columns: x => new { x.ConsultorioId, x.ReversesMovementId },
                        principalTable: "AccountMovements",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMovements_Budgets_ConsultorioId_BudgetId",
                        columns: x => new { x.ConsultorioId, x.BudgetId },
                        principalTable: "Budgets",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMovements_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMovements_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMovements_Payments_ConsultorioId_PaymentId",
                        columns: x => new { x.ConsultorioId, x.PaymentId },
                        principalTable: "Payments",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BudgetItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    BudgetId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    TreatmentPlanItemId = table.Column<Guid>(type: "char(36)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetItems_Budgets_ConsultorioId_BudgetId",
                        columns: x => new { x.ConsultorioId, x.BudgetId },
                        principalTable: "Budgets",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetItems_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetItems_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetItems_Procedures_ConsultorioId_ProcedureId",
                        columns: x => new { x.ConsultorioId, x.ProcedureId },
                        principalTable: "Procedures",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetItems_TreatmentPlanItems_ConsultorioId_TreatmentPlanIt~",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanItemId },
                        principalTable: "TreatmentPlanItems",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LabWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfessionalUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WorkType = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EstimatedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlacementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    TreatmentPlanItemId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabWorks", x => x.Id);
                    table.UniqueConstraint("AK_LabWorks_ConsultorioId_Id", x => new { x.ConsultorioId, x.Id });
                    table.ForeignKey(
                        name: "FK_LabWorks_ConsultorioMembers_ConsultorioId_ProfessionalUserId",
                        columns: x => new { x.ConsultorioId, x.ProfessionalUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorks_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorks_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorks_Laboratories_ConsultorioId_LaboratoryId",
                        columns: x => new { x.ConsultorioId, x.LaboratoryId },
                        principalTable: "Laboratories",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorks_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorks_TreatmentPlanItems_ConsultorioId_TreatmentPlanItemId",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanItemId },
                        principalTable: "TreatmentPlanItems",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ToothFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OdontogramId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PatientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ToothCode = table.Column<int>(type: "int", nullable: false),
                    Surface = table.Column<int>(type: "int", nullable: true),
                    ConditionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ClosedReason = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true),
                    SupersededByFindingId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TreatmentPlanItemId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToothFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToothFindings_ConsultorioMembers_ConsultorioId_RecordedByUse~",
                        columns: x => new { x.ConsultorioId, x.RecordedByUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_DentalPieces_ToothCode",
                        column: x => x.ToothCode,
                        principalTable: "DentalPieces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_DentalSurfaces_Surface",
                        column: x => x.Surface,
                        principalTable: "DentalSurfaces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_OdontogramConditions_ConditionId",
                        column: x => x.ConditionId,
                        principalTable: "OdontogramConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_Odontograms_ConsultorioId_OdontogramId",
                        columns: x => new { x.ConsultorioId, x.OdontogramId },
                        principalTable: "Odontograms",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_Patients_ConsultorioId_PatientId",
                        columns: x => new { x.ConsultorioId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToothFindings_TreatmentPlanItems_ConsultorioId_TreatmentPlan~",
                        columns: x => new { x.ConsultorioId, x.TreatmentPlanItemId },
                        principalTable: "TreatmentPlanItems",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LabWorkStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    LabWorkId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabWorkStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabWorkStatusChanges_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorkStatusChanges_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabWorkStatusChanges_LabWorks_ConsultorioId_LabWorkId",
                        columns: x => new { x.ConsultorioId, x.LabWorkId },
                        principalTable: "LabWorks",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "DentalPieces",
                columns: new[] { "Code", "Dentition", "Name", "Position", "Quadrant", "Type" },
                values: new object[,]
                {
                    { 11, 1, "Incisivo central superior derecho", 1, 1, 1 },
                    { 12, 1, "Incisivo lateral superior derecho", 2, 1, 1 },
                    { 13, 1, "Canino superior derecho", 3, 1, 2 },
                    { 14, 1, "1.er premolar superior derecho", 4, 1, 3 },
                    { 15, 1, "2.º premolar superior derecho", 5, 1, 3 },
                    { 16, 1, "1.er molar superior derecho", 6, 1, 4 },
                    { 17, 1, "2.º molar superior derecho", 7, 1, 4 },
                    { 18, 1, "3.er molar superior derecho", 8, 1, 4 },
                    { 21, 1, "Incisivo central superior izquierdo", 1, 2, 1 },
                    { 22, 1, "Incisivo lateral superior izquierdo", 2, 2, 1 },
                    { 23, 1, "Canino superior izquierdo", 3, 2, 2 },
                    { 24, 1, "1.er premolar superior izquierdo", 4, 2, 3 },
                    { 25, 1, "2.º premolar superior izquierdo", 5, 2, 3 },
                    { 26, 1, "1.er molar superior izquierdo", 6, 2, 4 },
                    { 27, 1, "2.º molar superior izquierdo", 7, 2, 4 },
                    { 28, 1, "3.er molar superior izquierdo", 8, 2, 4 },
                    { 31, 1, "Incisivo central inferior izquierdo", 1, 3, 1 },
                    { 32, 1, "Incisivo lateral inferior izquierdo", 2, 3, 1 },
                    { 33, 1, "Canino inferior izquierdo", 3, 3, 2 },
                    { 34, 1, "1.er premolar inferior izquierdo", 4, 3, 3 },
                    { 35, 1, "2.º premolar inferior izquierdo", 5, 3, 3 },
                    { 36, 1, "1.er molar inferior izquierdo", 6, 3, 4 },
                    { 37, 1, "2.º molar inferior izquierdo", 7, 3, 4 },
                    { 38, 1, "3.er molar inferior izquierdo", 8, 3, 4 },
                    { 41, 1, "Incisivo central inferior derecho", 1, 4, 1 },
                    { 42, 1, "Incisivo lateral inferior derecho", 2, 4, 1 },
                    { 43, 1, "Canino inferior derecho", 3, 4, 2 },
                    { 44, 1, "1.er premolar inferior derecho", 4, 4, 3 },
                    { 45, 1, "2.º premolar inferior derecho", 5, 4, 3 },
                    { 46, 1, "1.er molar inferior derecho", 6, 4, 4 },
                    { 47, 1, "2.º molar inferior derecho", 7, 4, 4 },
                    { 48, 1, "3.er molar inferior derecho", 8, 4, 4 },
                    { 51, 2, "Incisivo central temporal superior derecho", 1, 5, 1 },
                    { 52, 2, "Incisivo lateral temporal superior derecho", 2, 5, 1 },
                    { 53, 2, "Canino temporal superior derecho", 3, 5, 2 },
                    { 54, 2, "1.er molar temporal superior derecho", 4, 5, 4 },
                    { 55, 2, "2.º molar temporal superior derecho", 5, 5, 4 },
                    { 61, 2, "Incisivo central temporal superior izquierdo", 1, 6, 1 },
                    { 62, 2, "Incisivo lateral temporal superior izquierdo", 2, 6, 1 },
                    { 63, 2, "Canino temporal superior izquierdo", 3, 6, 2 },
                    { 64, 2, "1.er molar temporal superior izquierdo", 4, 6, 4 },
                    { 65, 2, "2.º molar temporal superior izquierdo", 5, 6, 4 },
                    { 71, 2, "Incisivo central temporal inferior izquierdo", 1, 7, 1 },
                    { 72, 2, "Incisivo lateral temporal inferior izquierdo", 2, 7, 1 },
                    { 73, 2, "Canino temporal inferior izquierdo", 3, 7, 2 },
                    { 74, 2, "1.er molar temporal inferior izquierdo", 4, 7, 4 },
                    { 75, 2, "2.º molar temporal inferior izquierdo", 5, 7, 4 },
                    { 81, 2, "Incisivo central temporal inferior derecho", 1, 8, 1 },
                    { 82, 2, "Incisivo lateral temporal inferior derecho", 2, 8, 1 },
                    { 83, 2, "Canino temporal inferior derecho", 3, 8, 2 },
                    { 84, 2, "1.er molar temporal inferior derecho", 4, 8, 4 },
                    { 85, 2, "2.º molar temporal inferior derecho", 5, 8, 4 }
                });

            migrationBuilder.InsertData(
                table: "DentalSurfaces",
                columns: new[] { "Code", "Abbreviation", "Name" },
                values: new object[,]
                {
                    { 1, "O", "Oclusal" },
                    { 2, "I", "Incisal" },
                    { 3, "M", "Mesial" },
                    { 4, "D", "Distal" },
                    { 5, "V", "Vestibular" },
                    { 6, "L", "Lingual" },
                    { 7, "P", "Palatina" }
                });

            migrationBuilder.InsertData(
                table: "OdontogramConditions",
                columns: new[] { "Id", "Code", "Color", "ConsultorioId", "CreatedAtUtc", "CreatedByUserId", "IsActive", "IsSystem", "MarksToothAbsent", "Name", "Scope", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000001"), "CARIES", "#E5484D", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Caries", 3, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000002"), "FILLING", "#3E63DD", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Obturación", 2, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000003"), "SEALANT", "#12A594", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Sellante", 2, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000004"), "CROWN", "#F5A524", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Corona", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000005"), "ROOT_CANAL", "#8E4EC6", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Endodoncia", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000006"), "EXTRACTION", "#E5484D", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, true, "Extracción", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000007"), "MISSING", "#8B8D98", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, true, "Ausente", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000008"), "IMPLANT", "#0090FF", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Implante", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000009"), "FRACTURE", "#D6409F", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Fractura", 3, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000010"), "PROSTHESIS", "#AD7F58", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Prótesis", 1, null, null },
                    { new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000011"), "ROOT_REMNANT", "#BD4B00", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, false, "Resto radicular", 1, null, null }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConsultorioId", "CreatedAtUtc", "CreatedByUserId", "Description", "IsSystem", "Name", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Gestión del consultorio, usuarios, roles y configuración", true, "Administrador", null, null },
                    { new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Atención clínica: agenda propia, historia clínica, odontograma, tratamientos", true, "Odontólogo", null, null },
                    { new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Recepción: agenda, turnos, pacientes, tareas y administración", true, "Secretaria", null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Permission", "RoleId" },
                values: new object[,]
                {
                    { "agenda.all_professionals", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "agenda.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "agenda.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "audit.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "budgets.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "catalog.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "consultorio.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "consultorio.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "dashboard.reception", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "documents.delete", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "documents.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "finance.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "insurances.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "laboratories.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "messaging.send", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "patients.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "payments.void", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "roles.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "tasks.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "tasks.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "users.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "users.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000001") },
                    { "agenda.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "agenda.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "appointments.overbook", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "appointments.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "appointments.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "budgets.approve", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "budgets.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "budgets.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "clinical.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "clinical.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "consents.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "consents.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "consultorio.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "dashboard.clinical", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "documents.clinical.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "documents.delete", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "documents.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "documents.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "finance.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "lab.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "lab.track", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "lab.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "odontogram.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "odontogram.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "patients.alerts.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "patients.medical.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "patients.medical.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "patients.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "patients.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "payments.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "tasks.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "tasks.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "treatments.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "treatments.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "users.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000002") },
                    { "agenda.all_professionals", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "agenda.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "agenda.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "appointments.overbook", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "appointments.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "appointments.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "budgets.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "consents.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "consents.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "consultorio.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "dashboard.reception", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "documents.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "documents.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "finance.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "insurances.manage", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "lab.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "lab.track", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "messaging.send", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "patients.alerts.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "patients.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "patients.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "payments.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "tasks.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "tasks.write", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") },
                    { "users.read", new Guid("0f6b7a52-1c1e-4f7a-9c11-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_ConsultorioId_BudgetId",
                table: "AccountMovements",
                columns: new[] { "ConsultorioId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_ConsultorioId_PatientId_OccurredAtUtc",
                table: "AccountMovements",
                columns: new[] { "ConsultorioId", "PatientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_ConsultorioId_PaymentId",
                table: "AccountMovements",
                columns: new[] { "ConsultorioId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_ConsultorioId_ReversesMovementId",
                table: "AccountMovements",
                columns: new[] { "ConsultorioId", "ReversesMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHistories_AppointmentId_ChangedAtUtc",
                table: "AppointmentHistories",
                columns: new[] { "AppointmentId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHistories_ConsultorioId_AppointmentId",
                table: "AppointmentHistories",
                columns: new[] { "ConsultorioId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConsultorioId_GabineteId_StartUtc",
                table: "Appointments",
                columns: new[] { "ConsultorioId", "GabineteId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConsultorioId_PatientId_StartUtc",
                table: "Appointments",
                columns: new[] { "ConsultorioId", "PatientId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConsultorioId_ProfessionalUserId_StartUtc",
                table: "Appointments",
                columns: new[] { "ConsultorioId", "ProfessionalUserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConsultorioId_Status_StartUtc",
                table: "Appointments",
                columns: new[] { "ConsultorioId", "Status", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ConsultorioId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "ConsultorioId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_ConsultorioId_BudgetId",
                table: "BudgetItems",
                columns: new[] { "ConsultorioId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_ConsultorioId_ProcedureId",
                table: "BudgetItems",
                columns: new[] { "ConsultorioId", "ProcedureId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_ConsultorioId_TreatmentPlanItemId",
                table: "BudgetItems",
                columns: new[] { "ConsultorioId", "TreatmentPlanItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_ToothCode",
                table: "BudgetItems",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_ConsultorioId_Number",
                table: "Budgets",
                columns: new[] { "ConsultorioId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_ConsultorioId_PatientId_Status",
                table: "Budgets",
                columns: new[] { "ConsultorioId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_ConsultorioId_TreatmentPlanId",
                table: "Budgets",
                columns: new[] { "ConsultorioId", "TreatmentPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetStatusChanges_BudgetId_ChangedAtUtc",
                table: "BudgetStatusChanges",
                columns: new[] { "BudgetId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetStatusChanges_ConsultorioId_BudgetId",
                table: "BudgetStatusChanges",
                columns: new[] { "ConsultorioId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_ConsultorioId_AppointmentId",
                table: "ClinicalEntries",
                columns: new[] { "ConsultorioId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_ConsultorioId_ClinicalRecordId",
                table: "ClinicalEntries",
                columns: new[] { "ConsultorioId", "ClinicalRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_ConsultorioId_PatientId_OccurredAtUtc",
                table: "ClinicalEntries",
                columns: new[] { "ConsultorioId", "PatientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_ConsultorioId_ProfessionalUserId",
                table: "ClinicalEntries",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryRevisions_ClinicalEntryId_Version",
                table: "ClinicalEntryRevisions",
                columns: new[] { "ClinicalEntryId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryRevisions_ConsultorioId_ClinicalEntryId",
                table: "ClinicalEntryRevisions",
                columns: new[] { "ConsultorioId", "ClinicalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryRevisions_EditedByUserId",
                table: "ClinicalEntryRevisions",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryTeeth_ToothCode",
                table: "ClinicalEntryTeeth",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_ConsultorioId_PatientId",
                table: "ClinicalRecords",
                columns: new[] { "ConsultorioId", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ConsultorioId_PatientId_Status",
                table: "Consents",
                columns: new[] { "ConsultorioId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ConsultorioId_SignedDocumentId",
                table: "Consents",
                columns: new[] { "ConsultorioId", "SignedDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ConsultorioId_TreatmentPlanId",
                table: "Consents",
                columns: new[] { "ConsultorioId", "TreatmentPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ResponsibleUserId",
                table: "Consents",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultorioMembers_UserId",
                table: "ConsultorioMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ConsultorioId_ClinicalEntryId",
                table: "Diagnoses",
                columns: new[] { "ConsultorioId", "ClinicalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ConsultorioId_PatientId_DiagnosedAtUtc",
                table: "Diagnoses",
                columns: new[] { "ConsultorioId", "PatientId", "DiagnosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ConsultorioId_ProfessionalUserId",
                table: "Diagnoses",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_Surface",
                table: "Diagnoses",
                column: "Surface");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ToothCode",
                table: "Diagnoses",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ConsultorioId_ClinicalEntryId",
                table: "Documents",
                columns: new[] { "ConsultorioId", "ClinicalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ConsultorioId_PatientId_IsDeleted",
                table: "Documents",
                columns: new[] { "ConsultorioId", "PatientId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ConsultorioId_TreatmentPlanId",
                table: "Documents",
                columns: new[] { "ConsultorioId", "TreatmentPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StorageKey",
                table: "Documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gabinetes_ConsultorioId_Name",
                table: "Gabinetes",
                columns: new[] { "ConsultorioId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthInsurances_ConsultorioId_Name",
                table: "HealthInsurances",
                columns: new[] { "ConsultorioId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitationRoles_RoleId",
                table: "InvitationRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ConsultorioId_Email_Status",
                table: "Invitations",
                columns: new[] { "ConsultorioId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Laboratories_ConsultorioId_Name",
                table: "Laboratories",
                columns: new[] { "ConsultorioId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_LaboratoryId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "LaboratoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_PatientId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_ProfessionalUserId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_Status_EstimatedDeliveryDate",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "Status", "EstimatedDeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_TreatmentPlanItemId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "TreatmentPlanItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ToothCode",
                table: "LabWorks",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkStatusChanges_ChangedByUserId",
                table: "LabWorkStatusChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkStatusChanges_ConsultorioId_LabWorkId",
                table: "LabWorkStatusChanges",
                columns: new[] { "ConsultorioId", "LabWorkId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkStatusChanges_LabWorkId_ChangedAtUtc",
                table: "LabWorkStatusChanges",
                columns: new[] { "LabWorkId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalHistories_ConsultorioId_PatientId",
                table: "MedicalHistories",
                columns: new[] { "ConsultorioId", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalHistories_PatientId",
                table: "MedicalHistories",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberRoles_RoleId",
                table: "MemberRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ConsultorioId_UserId_ReadAtUtc",
                table: "Notifications",
                columns: new[] { "ConsultorioId", "UserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramConditions_ConsultorioId_Code",
                table: "OdontogramConditions",
                columns: new[] { "ConsultorioId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_ConditionId",
                table: "OdontogramHistory",
                column: "ConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_ConsultorioId_OdontogramId",
                table: "OdontogramHistory",
                columns: new[] { "ConsultorioId", "OdontogramId" });

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_ConsultorioId_ProfessionalUserId",
                table: "OdontogramHistory",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_OdontogramId_ToothCode_ChangedAtUtc",
                table: "OdontogramHistory",
                columns: new[] { "OdontogramId", "ToothCode", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_Surface",
                table: "OdontogramHistory",
                column: "Surface");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramHistory_ToothCode",
                table: "OdontogramHistory",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_Odontograms_ConsultorioId_PatientId",
                table: "Odontograms",
                columns: new[] { "ConsultorioId", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_ConsultorioId_CreatedAtUtc",
                table: "OutboundMessages",
                columns: new[] { "ConsultorioId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_RelatedEntityType_RelatedEntityId",
                table: "OutboundMessages",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAlerts_ConsultorioId_PatientId_IsActive",
                table: "PatientAlerts",
                columns: new[] { "ConsultorioId", "PatientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ConsultorioId_DocumentNumber",
                table: "Patients",
                columns: new[] { "ConsultorioId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ConsultorioId_HealthInsuranceId",
                table: "Patients",
                columns: new[] { "ConsultorioId", "HealthInsuranceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ConsultorioId_PhoneNormalized",
                table: "Patients",
                columns: new[] { "ConsultorioId", "PhoneNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ConsultorioId_SearchName",
                table: "Patients",
                columns: new[] { "ConsultorioId", "SearchName" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ConsultorioId_Status",
                table: "Patients",
                columns: new[] { "ConsultorioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConsultorioId_BudgetId",
                table: "Payments",
                columns: new[] { "ConsultorioId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConsultorioId_PatientId_PaidAtUtc",
                table: "Payments",
                columns: new[] { "ConsultorioId", "PatientId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConsultorioId_ReceiptNumber",
                table: "Payments",
                columns: new[] { "ConsultorioId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConsultorioId_ReceivedByUserId",
                table: "Payments",
                columns: new[] { "ConsultorioId", "ReceivedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Procedures_ConsultorioId_Code",
                table: "Procedures",
                columns: new[] { "ConsultorioId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_FamilyId",
                table: "RefreshTokens",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ConsultorioId_Name",
                table: "Roles",
                columns: new[] { "ConsultorioId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBlocks_ConsultorioId_GabineteId",
                table: "ScheduleBlocks",
                columns: new[] { "ConsultorioId", "GabineteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBlocks_ConsultorioId_ProfessionalUserId",
                table: "ScheduleBlocks",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBlocks_ConsultorioId_StartUtc_EndUtc",
                table: "ScheduleBlocks",
                columns: new[] { "ConsultorioId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ConsultorioId_AppointmentId",
                table: "Tasks",
                columns: new[] { "ConsultorioId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ConsultorioId_AssignedToUserId_Status",
                table: "Tasks",
                columns: new[] { "ConsultorioId", "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ConsultorioId_PatientId",
                table: "Tasks",
                columns: new[] { "ConsultorioId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ConsultorioId_Status_DueAtUtc",
                table: "Tasks",
                columns: new[] { "ConsultorioId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ConditionId",
                table: "ToothFindings",
                column: "ConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ConsultorioId_OdontogramId",
                table: "ToothFindings",
                columns: new[] { "ConsultorioId", "OdontogramId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ConsultorioId_PatientId",
                table: "ToothFindings",
                columns: new[] { "ConsultorioId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ConsultorioId_RecordedByUserId",
                table: "ToothFindings",
                columns: new[] { "ConsultorioId", "RecordedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ConsultorioId_TreatmentPlanItemId",
                table: "ToothFindings",
                columns: new[] { "ConsultorioId", "TreatmentPlanItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_OdontogramId_IsCurrent_ToothCode",
                table: "ToothFindings",
                columns: new[] { "OdontogramId", "IsCurrent", "ToothCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_Surface",
                table: "ToothFindings",
                column: "Surface");

            migrationBuilder.CreateIndex(
                name: "IX_ToothFindings_ToothCode",
                table: "ToothFindings",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_ToothNotes_ConsultorioId_OdontogramId",
                table: "ToothNotes",
                columns: new[] { "ConsultorioId", "OdontogramId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothNotes_ConsultorioId_ProfessionalUserId",
                table: "ToothNotes",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothNotes_OdontogramId_ToothCode",
                table: "ToothNotes",
                columns: new[] { "OdontogramId", "ToothCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ToothNotes_ToothCode",
                table: "ToothNotes",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentChanges_ChangedByUserId",
                table: "TreatmentChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentChanges_ConsultorioId_TreatmentPlanId",
                table: "TreatmentChanges",
                columns: new[] { "ConsultorioId", "TreatmentPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentChanges_TreatmentPlanId_ChangedAtUtc",
                table: "TreatmentChanges",
                columns: new[] { "TreatmentPlanId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ConsultorioId_DiagnosisId",
                table: "TreatmentPlanItems",
                columns: new[] { "ConsultorioId", "DiagnosisId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ConsultorioId_ProcedureId",
                table: "TreatmentPlanItems",
                columns: new[] { "ConsultorioId", "ProcedureId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ConsultorioId_ProfessionalUserId",
                table: "TreatmentPlanItems",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ConsultorioId_Status",
                table: "TreatmentPlanItems",
                columns: new[] { "ConsultorioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ConsultorioId_TreatmentPlanId",
                table: "TreatmentPlanItems",
                columns: new[] { "ConsultorioId", "TreatmentPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_ToothCode",
                table: "TreatmentPlanItems",
                column: "ToothCode");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlans_ConsultorioId_PatientId_Status",
                table: "TreatmentPlans",
                columns: new[] { "ConsultorioId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlans_ConsultorioId_ProfessionalUserId",
                table: "TreatmentPlans",
                columns: new[] { "ConsultorioId", "ProfessionalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHours_ConsultorioId_GabineteId",
                table: "WorkingHours",
                columns: new[] { "ConsultorioId", "GabineteId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHours_ConsultorioId_ProfessionalUserId_DayOfWeek",
                table: "WorkingHours",
                columns: new[] { "ConsultorioId", "ProfessionalUserId", "DayOfWeek" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMovements");

            migrationBuilder.DropTable(
                name: "AppointmentHistories");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BudgetItems");

            migrationBuilder.DropTable(
                name: "BudgetStatusChanges");

            migrationBuilder.DropTable(
                name: "ClinicalEntryRevisions");

            migrationBuilder.DropTable(
                name: "ClinicalEntryTeeth");

            migrationBuilder.DropTable(
                name: "Consents");

            migrationBuilder.DropTable(
                name: "ConsultorioSequences");

            migrationBuilder.DropTable(
                name: "InvitationRoles");

            migrationBuilder.DropTable(
                name: "LabWorkStatusChanges");

            migrationBuilder.DropTable(
                name: "MedicalHistories");

            migrationBuilder.DropTable(
                name: "MemberRoles");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OdontogramHistory");

            migrationBuilder.DropTable(
                name: "OutboundMessages");

            migrationBuilder.DropTable(
                name: "PatientAlerts");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScheduleBlocks");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "ToothFindings");

            migrationBuilder.DropTable(
                name: "ToothNotes");

            migrationBuilder.DropTable(
                name: "TreatmentChanges");

            migrationBuilder.DropTable(
                name: "UserNotificationSettings");

            migrationBuilder.DropTable(
                name: "WorkingHours");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "LabWorks");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "OdontogramConditions");

            migrationBuilder.DropTable(
                name: "Odontograms");

            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "Laboratories");

            migrationBuilder.DropTable(
                name: "TreatmentPlanItems");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "Procedures");

            migrationBuilder.DropTable(
                name: "TreatmentPlans");

            migrationBuilder.DropTable(
                name: "ClinicalEntries");

            migrationBuilder.DropTable(
                name: "DentalPieces");

            migrationBuilder.DropTable(
                name: "DentalSurfaces");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ClinicalRecords");

            migrationBuilder.DropTable(
                name: "ConsultorioMembers");

            migrationBuilder.DropTable(
                name: "Gabinetes");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "HealthInsurances");

            migrationBuilder.DropTable(
                name: "Consultorios");
        }
    }
}
