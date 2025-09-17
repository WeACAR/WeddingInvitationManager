using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WeddingInvitationManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymousInvitationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnonymousInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GuestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QRCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DownloadType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymousInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnonymousInvitations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousInvitations_EventId_BatchNumber",
                table: "AnonymousInvitations",
                columns: new[] { "EventId", "BatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousInvitations_EventId_GuestNumber",
                table: "AnonymousInvitations",
                columns: new[] { "EventId", "GuestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnonymousInvitations_QRCode",
                table: "AnonymousInvitations",
                column: "QRCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnonymousInvitations");
        }
    }
}
