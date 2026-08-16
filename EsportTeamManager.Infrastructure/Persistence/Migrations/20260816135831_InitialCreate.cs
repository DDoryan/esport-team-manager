using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EsportTeamManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pseudo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    PendingEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    NormalizedPendingEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    PendingEmailExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AccountStatus = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    MinimumAgeDeclaredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.CheckConstraint("CK_AspNetUsers_AccountStatus", "\"AccountStatus\" IN ('PendingConfirmation', 'Active', 'Suspended')");
                });

            migrationBuilder.CreateTable(
                name: "LegalDocumentVersions",
                columns: table => new
                {
                    LegalDocumentVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    VersionNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequiresAcceptance = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocumentVersions", x => x.LegalDocumentVersionId);
                    table.CheckConstraint("CK_LegalDocumentVersions_DocumentType", "\"DocumentType\" IN ('TermsOfService', 'PrivacyPolicy')");
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    MapId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.MapId);
                });

            migrationBuilder.CreateTable(
                name: "ReservedIdentities",
                columns: table => new
                {
                    IdentityHash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    ReservedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservedIdentities", x => x.IdentityHash);
                    table.CheckConstraint("CK_ReservedIdentities_IdentityHash", "LENGTH(\"IdentityHash\") = 64");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 6, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DeletedMemberCounter = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamId);
                    table.CheckConstraint("CK_Teams_DeletedMemberCounter", "\"DeletedMemberCounter\" >= 0");
                    table.ForeignKey(
                        name: "FK_Teams_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalAcceptances",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LegalDocumentVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAcceptances", x => new { x.UserId, x.LegalDocumentVersionId });
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_LegalDocumentVersions_LegalDocumentVersionId",
                        column: x => x.LegalDocumentVersionId,
                        principalTable: "LegalDocumentVersions",
                        principalColumn: "LegalDocumentVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionTraces",
                columns: table => new
                {
                    ActionTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActionCode = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ObjectType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ObjectIdentifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionTraces", x => x.ActionTraceId);
                    table.CheckConstraint("CK_ActionTraces_Expiration", "\"ExpiresAtUtc\" > \"OccurredAtUtc\"");
                    table.CheckConstraint("CK_ActionTraces_Outcome", "\"Outcome\" IN ('Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "FK_ActionTraces_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActionTraces_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActivityTypes",
                columns: table => new
                {
                    ActivityTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTypes", x => x.ActivityTypeId);
                    table.CheckConstraint("CK_ActivityTypes_SystemOwnership", "(\"IsSystem\" = TRUE AND \"TeamId\" IS NULL) OR (\"IsSystem\" = FALSE AND \"TeamId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ActivityTypes_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormerMembers",
                columns: table => new
                {
                    FormerMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormerMembers", x => x.FormerMemberId);
                    table.CheckConstraint("CK_FormerMembers_LocalNumber", "\"LocalNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_FormerMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamRoles",
                columns: table => new
                {
                    TeamRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRoles", x => x.TeamRoleId);
                    table.CheckConstraint("CK_TeamRoles_SystemOwnership", "(\"IsSystem\" = TRUE AND \"TeamId\" IS NULL) OR (\"IsSystem\" = FALSE AND \"TeamId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TeamRoles_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMemberships",
                columns: table => new
                {
                    TeamMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FormerMemberId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TeamRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LeftAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberships", x => x.TeamMembershipId);
                    table.CheckConstraint("CK_TeamMemberships_Closure", "(\"Status\" = 'Active' AND \"LeftAtUtc\" IS NULL) OR (\"Status\" IN ('Left', 'Removed') AND \"LeftAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_TeamMemberships_Holder", "(\"UserId\" IS NOT NULL AND \"FormerMemberId\" IS NULL) OR (\"UserId\" IS NULL AND \"FormerMemberId\" IS NOT NULL)");
                    table.CheckConstraint("CK_TeamMemberships_LeftAtUtc", "\"LeftAtUtc\" IS NULL OR \"LeftAtUtc\" >= \"JoinedAtUtc\"");
                    table.CheckConstraint("CK_TeamMemberships_Status", "\"Status\" IN ('Active', 'Left', 'Removed')");
                    table.ForeignKey(
                        name: "FK_TeamMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_FormerMembers_FormerMemberId",
                        column: x => x.FormerMemberId,
                        principalTable: "FormerMembers",
                        principalColumn: "FormerMemberId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_TeamRoles_TeamRoleId",
                        column: x => x.TeamRoleId,
                        principalTable: "TeamRoles",
                        principalColumn: "TeamRoleId");
                    table.ForeignKey(
                        name: "FK_TeamMemberships_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Report = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    PlannedStartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PlannedEndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityId);
                    table.CheckConstraint("CK_Activities_CancellationReason", "\"Status\" = 'Cancelled' OR \"CancellationReason\" IS NULL");
                    table.CheckConstraint("CK_Activities_Schedule", "\"PlannedEndUtc\" > \"PlannedStartUtc\"");
                    table.CheckConstraint("CK_Activities_Status", "\"Status\" IN ('Planned', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_Activities_UpdatedAtUtc", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_Activities_ActivityTypes_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "ActivityTypes",
                        principalColumn: "ActivityTypeId");
                    table.ForeignKey(
                        name: "FK_Activities_TeamMemberships_CreatedByMembershipId",
                        column: x => x.CreatedByMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId");
                    table.ForeignKey(
                        name: "FK_Activities_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposedTeamRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedMembershipId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.InvitationId);
                    table.CheckConstraint("CK_Invitations_CreatedMembership", "(\"Status\" = 'Accepted' AND \"CreatedMembershipId\" IS NOT NULL) OR (\"Status\" <> 'Accepted' AND \"CreatedMembershipId\" IS NULL)");
                    table.CheckConstraint("CK_Invitations_Resolution", "(\"Status\" = 'Pending' AND \"ResolvedAtUtc\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ResolvedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Invitations_ResolvedAtUtc", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Invitations_Status", "\"Status\" IN ('Pending', 'Accepted', 'Refused', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_TeamMemberships_CreatedMembershipId",
                        column: x => x.CreatedMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_TeamRoles_ProposedTeamRoleId",
                        column: x => x.ProposedTeamRoleId,
                        principalTable: "TeamRoles",
                        principalColumn: "TeamRoleId");
                    table.ForeignKey(
                        name: "FK_Invitations_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnershipTransfers",
                columns: table => new
                {
                    OwnershipTransferId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitiatorMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnershipTransfers", x => x.OwnershipTransferId);
                    table.CheckConstraint("CK_OwnershipTransfers_Memberships", "\"InitiatorMembershipId\" <> \"RecipientMembershipId\"");
                    table.CheckConstraint("CK_OwnershipTransfers_Resolution", "(\"Status\" = 'Pending' AND \"ResolvedAtUtc\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ResolvedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_OwnershipTransfers_ResolvedAtUtc", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_OwnershipTransfers_Status", "\"Status\" IN ('Pending', 'Accepted', 'Refused', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_OwnershipTransfers_TeamMemberships_InitiatorMembershipId",
                        column: x => x.InitiatorMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId");
                    table.ForeignKey(
                        name: "FK_OwnershipTransfers_TeamMemberships_RecipientMembershipId",
                        column: x => x.RecipientMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId");
                    table.ForeignKey(
                        name: "FK_OwnershipTransfers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MapId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    ExternalUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.StrategyId);
                    table.CheckConstraint("CK_Strategies_ExternalUrl", "\"ExternalUrl\" IS NULL OR LOWER(\"ExternalUrl\") LIKE 'http://%' OR LOWER(\"ExternalUrl\") LIKE 'https://%'");
                    table.CheckConstraint("CK_Strategies_Side", "\"Side\" IN ('Attack', 'Defense')");
                    table.CheckConstraint("CK_Strategies_UpdatedAtUtc", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_Strategies_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "MapId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Strategies_TeamMemberships_CreatedByMembershipId",
                        column: x => x.CreatedByMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId");
                    table.ForeignKey(
                        name: "FK_Strategies_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLinks",
                columns: table => new
                {
                    ActivityLinkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLinks", x => x.ActivityLinkId);
                    table.CheckConstraint("CK_ActivityLinks_Url", "LOWER(\"Url\") LIKE 'http://%' OR LOWER(\"Url\") LIKE 'https://%'");
                    table.ForeignKey(
                        name: "FK_ActivityLinks_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityParticipants",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Attendance = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityParticipants", x => new { x.ActivityId, x.TeamMembershipId });
                    table.CheckConstraint("CK_ActivityParticipants_Attendance", "\"Attendance\" IS NULL OR \"Attendance\" IN ('Present', 'Absent')");
                    table.ForeignKey(
                        name: "FK_ActivityParticipants_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityParticipants_TeamMemberships_TeamMembershipId",
                        column: x => x.TeamMembershipId,
                        principalTable: "TeamMemberships",
                        principalColumn: "TeamMembershipId");
                });

            migrationBuilder.CreateTable(
                name: "MatchDetails",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpponentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TeamScore = table.Column<int>(type: "INTEGER", nullable: true),
                    OpponentScore = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchDetails", x => x.ActivityId);
                    table.CheckConstraint("CK_MatchDetails_CompleteScore", "(\"TeamScore\" IS NULL AND \"OpponentScore\" IS NULL) OR (\"TeamScore\" IS NOT NULL AND \"OpponentScore\" IS NOT NULL)");
                    table.CheckConstraint("CK_MatchDetails_OpponentScore", "\"OpponentScore\" IS NULL OR \"OpponentScore\" >= 0");
                    table.CheckConstraint("CK_MatchDetails_TeamScore", "\"TeamScore\" IS NULL OR \"TeamScore\" >= 0");
                    table.ForeignKey(
                        name: "FK_MatchDetails_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnershipTransferId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.CheckConstraint("CK_Notifications_ReadAtUtc", "\"ReadAtUtc\" IS NULL OR \"ReadAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Notifications_Source", "(\"InvitationId\" IS NOT NULL AND \"OwnershipTransferId\" IS NULL) OR (\"InvitationId\" IS NULL AND \"OwnershipTransferId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "Invitations",
                        principalColumn: "InvitationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_OwnershipTransfers_OwnershipTransferId",
                        column: x => x.OwnershipTransferId,
                        principalTable: "OwnershipTransfers",
                        principalColumn: "OwnershipTransferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityStrategies",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityStrategies", x => new { x.ActivityId, x.StrategyId });
                    table.ForeignKey(
                        name: "FK_ActivityStrategies_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityStrategies_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "StrategyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageFiles",
                columns: table => new
                {
                    ImageFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamLogoForTeamId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StrategyImageForStrategyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InternalFileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    WidthPixels = table.Column<int>(type: "INTEGER", nullable: false),
                    HeightPixels = table.Column<int>(type: "INTEGER", nullable: false),
                    OptimizedStorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ThumbnailStorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageFiles", x => x.ImageFileId);
                    table.CheckConstraint("CK_ImageFiles_FileSizeBytes", "\"FileSizeBytes\" > 0");
                    table.CheckConstraint("CK_ImageFiles_HeightPixels", "\"HeightPixels\" > 0");
                    table.CheckConstraint("CK_ImageFiles_MediaType", "\"MediaType\" IN ('image/png', 'image/jpeg', 'image/webp')");
                    table.CheckConstraint("CK_ImageFiles_Owner", "(\"TeamLogoForTeamId\" IS NOT NULL AND \"StrategyImageForStrategyId\" IS NULL) OR (\"TeamLogoForTeamId\" IS NULL AND \"StrategyImageForStrategyId\" IS NOT NULL)");
                    table.CheckConstraint("CK_ImageFiles_WidthPixels", "\"WidthPixels\" > 0");
                    table.ForeignKey(
                        name: "FK_ImageFiles_Strategies_StrategyImageForStrategyId",
                        column: x => x.StrategyImageForStrategyId,
                        principalTable: "Strategies",
                        principalColumn: "StrategyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImageFiles_Teams_TeamLogoForTeamId",
                        column: x => x.TeamLogoForTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionTraces_ActionCode",
                table: "ActionTraces",
                column: "ActionCode");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTraces_ActorUserId",
                table: "ActionTraces",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTraces_ExpiresAtUtc",
                table: "ActionTraces",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTraces_OccurredAtUtc",
                table: "ActionTraces",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActionTraces_TeamId",
                table: "ActionTraces",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActivityTypeId",
                table: "Activities",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_CreatedByMembershipId",
                table: "Activities",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TeamId_PlannedStartUtc",
                table: "Activities",
                columns: new[] { "TeamId", "PlannedStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TeamId_Status",
                table: "Activities",
                columns: new[] { "TeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLinks_ActivityId",
                table: "ActivityLinks",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipants_TeamMembershipId",
                table: "ActivityParticipants",
                column: "TeamMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityStrategies_StrategyId",
                table: "ActivityStrategies",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "UX_ActivityTypes_SystemCode",
                table: "ActivityTypes",
                column: "Code",
                unique: true,
                filter: "\"TeamId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ActivityTypes_TeamId_Code",
                table: "ActivityTypes",
                columns: new[] { "TeamId", "Code" },
                unique: true,
                filter: "\"TeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedPendingEmail",
                table: "AspNetUsers",
                column: "NormalizedPendingEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FormerMembers_TeamId_LocalNumber",
                table: "FormerMembers",
                columns: new[] { "TeamId", "LocalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ImageFiles_InternalFileName",
                table: "ImageFiles",
                column: "InternalFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ImageFiles_OptimizedStorageKey",
                table: "ImageFiles",
                column: "OptimizedStorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ImageFiles_StrategyImageForStrategyId",
                table: "ImageFiles",
                column: "StrategyImageForStrategyId",
                unique: true,
                filter: "\"StrategyImageForStrategyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ImageFiles_TeamLogoForTeamId",
                table: "ImageFiles",
                column: "TeamLogoForTeamId",
                unique: true,
                filter: "\"TeamLogoForTeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ImageFiles_ThumbnailStorageKey",
                table: "ImageFiles",
                column: "ThumbnailStorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CreatedMembershipId",
                table: "Invitations",
                column: "CreatedMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ProposedTeamRoleId",
                table: "Invitations",
                column: "ProposedTeamRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RecipientUserId_Status",
                table: "Invitations",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SenderUserId",
                table: "Invitations",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TeamId_Status",
                table: "Invitations",
                columns: new[] { "TeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_PendingRecipient",
                table: "Invitations",
                columns: new[] { "TeamId", "RecipientUserId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_LegalAcceptances_LegalDocumentVersionId",
                table: "LegalAcceptances",
                column: "LegalDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_LegalDocumentVersions_DocumentType_VersionNumber",
                table: "LegalDocumentVersions",
                columns: new[] { "DocumentType", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Maps_Name",
                table: "Maps",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_ReadAtUtc",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_InvitationId",
                table: "Notifications",
                column: "InvitationId",
                unique: true,
                filter: "\"InvitationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_OwnershipTransferId",
                table: "Notifications",
                column: "OwnershipTransferId",
                unique: true,
                filter: "\"OwnershipTransferId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransfers_InitiatorMembershipId",
                table: "OwnershipTransfers",
                column: "InitiatorMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransfers_RecipientMembershipId",
                table: "OwnershipTransfers",
                column: "RecipientMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransfers_TeamId_Status",
                table: "OwnershipTransfers",
                columns: new[] { "TeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_OwnershipTransfers_PendingTeam",
                table: "OwnershipTransfers",
                column: "TeamId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_CreatedByMembershipId",
                table: "Strategies",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_MapId",
                table: "Strategies",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_Side",
                table: "Strategies",
                column: "Side");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_TeamId_IsActive",
                table: "Strategies",
                columns: new[] { "TeamId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_UpdatedAtUtc",
                table: "Strategies",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_FormerMemberId",
                table: "TeamMemberships",
                column: "FormerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_TeamId_Status",
                table: "TeamMemberships",
                columns: new[] { "TeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_TeamRoleId",
                table: "TeamMemberships",
                column: "TeamRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_UserId_Status",
                table: "TeamMemberships",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_TeamMemberships_ActiveUser",
                table: "TeamMemberships",
                columns: new[] { "TeamId", "UserId" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TeamRoles_SystemCode",
                table: "TeamRoles",
                column: "Code",
                unique: true,
                filter: "\"TeamId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TeamRoles_TeamId_Code",
                table: "TeamRoles",
                columns: new[] { "TeamId", "Code" },
                unique: true,
                filter: "\"TeamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerUserId",
                table: "Teams",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionTraces");

            migrationBuilder.DropTable(
                name: "ActivityLinks");

            migrationBuilder.DropTable(
                name: "ActivityParticipants");

            migrationBuilder.DropTable(
                name: "ActivityStrategies");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ImageFiles");

            migrationBuilder.DropTable(
                name: "LegalAcceptances");

            migrationBuilder.DropTable(
                name: "MatchDetails");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ReservedIdentities");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "LegalDocumentVersions");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "OwnershipTransfers");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "ActivityTypes");

            migrationBuilder.DropTable(
                name: "TeamMemberships");

            migrationBuilder.DropTable(
                name: "FormerMembers");

            migrationBuilder.DropTable(
                name: "TeamRoles");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
