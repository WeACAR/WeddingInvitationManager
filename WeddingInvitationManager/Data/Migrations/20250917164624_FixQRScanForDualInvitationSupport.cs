using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingInvitationManager.Migrations
{
    /// <inheritdoc />
    public partial class FixQRScanForDualInvitationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QRScans_Invitations_InvitationId",
                table: "QRScans");

            migrationBuilder.AlterColumn<int>(
                name: "InvitationId",
                table: "QRScans",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "AnonymousInvitationId",
                table: "QRScans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "QRScans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "QRScans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "QRScans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsVip",
                table: "QRScans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_QRScans_AnonymousInvitationId",
                table: "QRScans",
                column: "AnonymousInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_QRScans_EventId",
                table: "QRScans",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_QRScans_AnonymousInvitations_AnonymousInvitationId",
                table: "QRScans",
                column: "AnonymousInvitationId",
                principalTable: "AnonymousInvitations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QRScans_Invitations_InvitationId",
                table: "QRScans",
                column: "InvitationId",
                principalTable: "Invitations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QRScans_AnonymousInvitations_AnonymousInvitationId",
                table: "QRScans");

            migrationBuilder.DropForeignKey(
                name: "FK_QRScans_Invitations_InvitationId",
                table: "QRScans");

            migrationBuilder.DropIndex(
                name: "IX_QRScans_AnonymousInvitationId",
                table: "QRScans");

            migrationBuilder.DropIndex(
                name: "IX_QRScans_EventId",
                table: "QRScans");

            migrationBuilder.DropColumn(
                name: "AnonymousInvitationId",
                table: "QRScans");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "QRScans");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "QRScans");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "QRScans");

            migrationBuilder.DropColumn(
                name: "IsVip",
                table: "QRScans");

            migrationBuilder.AlterColumn<int>(
                name: "InvitationId",
                table: "QRScans",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QRScans_Invitations_InvitationId",
                table: "QRScans",
                column: "InvitationId",
                principalTable: "Invitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
