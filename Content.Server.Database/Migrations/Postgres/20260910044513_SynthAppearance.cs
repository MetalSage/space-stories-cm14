using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class SynthAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "synth_eye_color",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "synth_facial_hair_color",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "synth_facial_hair_name",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "synth_hair_color",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "synth_hair_name",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "synth_markings",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "synth_skin_color",
                table: "profile",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "synth_eye_color",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_facial_hair_color",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_facial_hair_name",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_hair_color",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_hair_name",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_markings",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synth_skin_color",
                table: "profile");
        }
    }
}
