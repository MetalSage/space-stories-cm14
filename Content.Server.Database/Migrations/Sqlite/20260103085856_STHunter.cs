using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class STHunter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "st_hunter_profile",
                columns: table => new
                {
                    st_hunter_profile_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    char_name = table.Column<string>(type: "TEXT", nullable: false),
                    gender = table.Column<string>(type: "TEXT", nullable: false),
                    age = table.Column<int>(type: "INTEGER", nullable: false),
                    flavor_text = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    skin_color = table.Column<string>(type: "TEXT", nullable: false),
                    quill_marking_id = table.Column<string>(type: "TEXT", nullable: false),
                    armor_prototype = table.Column<string>(type: "TEXT", nullable: false),
                    mask_prototype = table.Column<string>(type: "TEXT", nullable: false),
                    greaves_prototype = table.Column<string>(type: "TEXT", nullable: false),
                    caster_prototype = table.Column<string>(type: "TEXT", nullable: false),
                    voice = table.Column<string>(type: "TEXT", nullable: false),
                    head_accessory = table.Column<string>(type: "TEXT", nullable: false),
                    translator_sound = table.Column<string>(type: "TEXT", nullable: false),
                    cloak_sound = table.Column<string>(type: "TEXT", nullable: false),
                    cape_color = table.Column<string>(type: "TEXT", nullable: false),
                    bracer_prototype = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_st_hunter_profile", x => x.st_hunter_profile_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_st_hunter_profile_user_id",
                table: "st_hunter_profile",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "st_hunter_profile");
        }
    }
}
