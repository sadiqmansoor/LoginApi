using Microsoft.EntityFrameworkCore.Migrations;

namespace Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE ""Users"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Username"" VARCHAR(100) NOT NULL,
                    ""PasswordHash"" TEXT NOT NULL
                );

                INSERT INTO ""Users"" (""Username"", ""PasswordHash"")
                VALUES ('admin', 'admin123');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""Users"";
            ");
        }
    }
}