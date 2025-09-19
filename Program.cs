using LoginApi.Data;
using LoginApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--migrate"))
{
    using var scope = Host.CreateApplicationBuilder(args).Build().Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Console.WriteLine("✅ EF Core migrations applied.");
    return;
}

// ───── Configure Kestrel ─────
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000);
});

// ───── Load Connection String ─────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString);
var targetDatabase = npgsqlBuilder.Database;

// ───── Check & Create Database If Missing ─────
npgsqlBuilder.Database = "postgres";
var adminConnectionString = npgsqlBuilder.ToString();

try
{
    using var connection = new NpgsqlConnection(adminConnectionString);
    connection.Open();

    using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{targetDatabase}'", connection);
    var exists = checkCmd.ExecuteScalar();

    if (exists == null)
    {
        using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{targetDatabase}\" OWNER \"{npgsqlBuilder.Username}\"", connection);
        createCmd.ExecuteNonQuery();
        Console.WriteLine($"✅ Database '{targetDatabase}' created.");
    }
    else
    {
        Console.WriteLine($"ℹ️ Database '{targetDatabase}' already exists.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database check/create failed: {ex.Message}");
}

// ───── Add Services ─────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Login API", Version = "v1" });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key is missing")))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// ───── Apply EF Core Migrations & Seed Admin User ─────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();

        // Check if Users table exists
        var tableExists = db.Database.ExecuteSqlRaw(
            "SELECT to_regclass('\"Users\"') IS NOT NULL"
        ) == 1;

        if (tableExists && !db.Users.Any())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = "admin123"
                
            });
            db.SaveChanges();
            Console.WriteLine("✅ Seeded admin user.");
        }
        else
        {
            Console.WriteLine("ℹ️ Users table already exists or has data.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Migration or seeding error: {ex.Message}");
    }
}

// ───── Configure Middleware ─────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Login API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();