using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Services;
using Pomelo.EntityFrameworkCore.MySql;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(10, 4, 32))));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddHttpContextAccessor();

var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var jwtKey = builder.Configuration["Jwt:Key"]!;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Student Management API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhap token theo dang: Bearer {your JWT token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var resetUsersOnStartup = builder.Configuration.GetValue<bool>("SeedOptions:ResetUsersOnStartup");
    await HealEfMigrationHistoryIfSchemaMissingAsync(db);
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db, resetUsersOnStartup);
}

/// <summary>
/// Nếu __EFMigrationsHistory đã ghi migration nhưng bảng nghiệp vụ chưa có (xóa tay / DB lệch),
/// xóa lịch sử để Migrate() chạy lại script tạo bảng.
/// </summary>
static async Task HealEfMigrationHistoryIfSchemaMissingAsync(AppDbContext db)
{
    if (!await db.Database.CanConnectAsync())
    {
        return;
    }

    await db.Database.OpenConnectionAsync();
    try
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND LOWER(table_name) = '__efmigrationshistory'
            """;
        var histTable = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        if (histTable == 0)
        {
            return;
        }

        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND LOWER(table_name) = 'roles'
            """;
        var rolesTable = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

        cmd.CommandText = "SELECT COUNT(*) FROM `__EFMigrationsHistory`";
        var histRows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

        if (histRows > 0 && rolesTable == 0)
        {
            cmd.CommandText = "DELETE FROM `__EFMigrationsHistory`";
            await cmd.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

app.Run();
