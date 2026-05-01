using System;
using System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Uni_Connect.Hubs;
using Uni_Connect.Models;
using Uni_Connect.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Configure DbContext provider based on connection string (allows SQLite for Development)
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
// Debug: print chosen connection string (trim for safety in logs)
Console.WriteLine($"[DEBUG] DefaultConnection: {defaultConn}");
if (!string.IsNullOrEmpty(defaultConn) && defaultConn.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(defaultConn));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(defaultConn));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login_Page";       
        options.LogoutPath = "/Login/Logout";            
        options.ExpireTimeSpan = TimeSpan.FromHours(24); 
        options.SlidingExpiration = true;              
    });

builder.Services.AddSignalR();
builder.Services.AddScoped<IPointService, PointService>();
builder.Services.AddScoped<IPostService, PostService>();

var app = builder.Build();



// ===== SEED DATABASE WITH FAKE DATA (Development only) =====
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Attempt automatic migrations; if they fail (e.g. LocalDB unavailable), skip seeding to allow the app to start.
        bool migrationsSucceeded = true;
        try
        {
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            migrationsSucceeded = false;
            Console.WriteLine($"[WARN] Database migration failed: {ex}");
        }

        bool schemaRepairSucceeded = true;
        try
        {
            await EnsureMediaColumnsAsync(context);
        }
        catch (Exception ex)
        {
            schemaRepairSucceeded = false;
            Console.WriteLine($"[WARN] Schema repair failed: {ex}");
        }

        // One-time fix: sessions created before IsActive was set correctly
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE PrivateSessions SET IsActive = 1 WHERE IsActive = 0 AND IsDeleted = 0");
        }
        catch { }

        // Seed users regardless of minor schema issues to ensure you can always login
        try
        {
            // Seed the Root Admin (Ahmad)
            var rootAdmin = context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == "ahmadalatrash726@gmail.com");
            if (rootAdmin == null)
            {
                var adminPass = builder.Configuration["AdminPassword"] 
                    ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD") 
                    ?? "@Cometothebored1314@";

                rootAdmin = new User
                {
                    UniversityID = "ADM-726",
                    Name = "Ahmad Alatrash",
                    Username = "ahmad726",
                    Email = "ahmadalatrash726@gmail.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPass),
                    Role = "Admin",
                    Faculty = "Administration",
                    YearOfStudy = "N/A",
                    Points = 9999,
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(rootAdmin);
            }
            else
            {
                rootAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("@Cometothebored1314@");
                rootAdmin.Role = "Admin";
                rootAdmin.IsDeleted = false;
            }

            // Create test user if it doesn't exist (Ignore filters to avoid unique constraint collisions)
            var testUser = context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == "20240001@philadelphia.edu.jo");
            if (testUser == null)
            {
                testUser = new User
                {
                    UniversityID = "20240001",
                    Name = "Student Test Account",
                    Username = "20240001",
                    Email = "20240001@philadelphia.edu.jo",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
                    Role = "Student",
                    Faculty = "IT Faculty",
                    YearOfStudy = "3rd Year",
                    Points = 250,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(testUser);
            }
            else
            {
                testUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234");
                testUser.IsDeleted = false; // Restore if it was deleted
            }
            context.SaveChanges();

            // Create other users for more realistic data
            var users = new List<User>();
            string[] faculties = { "IT Faculty", "Engineering", "Business", "Law", "Pharmacy" };
            string[] years = { "1st Year", "2nd Year", "3rd Year", "4th Year" };
            string[] names = { "Sarah Ahmed", "Omar Khan", "Fatima Ali", "Mohammed Saleh", "Lina Hassan", "Rania Abu" };

            for (int i = 0; i < 5; i++)
            {
                var user = new User
                {
                    UniversityID = (20241000 + i).ToString(),
                    Name = names[i],
                    Username = (20241000 + i).ToString(),
                    Email = $"{(20241000 + i)}@philadelphia.edu.jo",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
                    Role = "Student",
                    Faculty = faculties[i % faculties.Length],
                    YearOfStudy = years[i % years.Length],
                    Points = new Random().Next(100, 500),
                    CreatedAt = DateTime.Now.AddDays(-new Random().Next(30, 180))
                };
                users.Add(user);
            }
            context.Users.AddRange(users);


            // Create categories
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Data Structures", Faculty = "IT Faculty" },
                    new Category { Name = "Algorithms", Faculty = "IT Faculty" },
                    new Category { Name = "Database", Faculty = "IT Faculty" },
                    new Category { Name = "Web Development", Faculty = "IT Faculty" },
                    new Category { Name = "Thermodynamics", Faculty = "Engineering" },
                    new Category { Name = "Finance", Faculty = "Business" },
                    new Category { Name = "Constitutional Law", Faculty = "Law" },
                    new Category { Name = "Pharmacology", Faculty = "Pharmacy" }
                };
                context.Categories.AddRange(categories);
                context.SaveChanges();
            }

            // Create fake posts
            if (!context.Posts.IgnoreQueryFilters().Any())
            {
                var cats = context.Categories.ToList();
                var posts = new List<Post>
                {
                    new Post
                    {
                        UserID = users[1].UserID,
                        CategoryID = cats[1].CategoryID,
                        Title = "Dijkstra vs Bellman-Ford for shortest path",
                        Content = "I'm confused about when to use which algorithm. Both seem to find shortest paths. What are the key differences and when is one better than the other?",
                        ViewsCount = 89,
                        Upvotes = 7,
                        CreatedAt = DateTime.Now.AddDays(-3),
                        IsDeleted = false
                    },
                    new Post
                    {
                        UserID = users[2].UserID,
                        CategoryID = cats[2].CategoryID,
                        Title = "SQL JOIN performance - LEFT vs INNER",
                        Content = "Why does my LEFT JOIN query run slower than INNER JOIN? Are there optimization techniques? What about with multiple tables?",
                        ViewsCount = 312,
                        Upvotes = 25,
                        CreatedAt = DateTime.Now.AddDays(-2),
                        IsDeleted = false
                    },
                    new Post
                    {
                        UserID = users[3].UserID,
                        CategoryID = cats[3].CategoryID,
                        Title = "React hook dependencies array - when to include variables?",
                        Content = "I'm getting warning to include variables in dependency arrays. But when I do, it causes infinite loops. How do I fix this properly?",
                        ViewsCount = 178,
                        Upvotes = 14,
                        CreatedAt = DateTime.Now.AddDays(-1),
                        IsDeleted = false
                    },
                    new Post
                    {
                        UserID = users[4].UserID,
                        CategoryID = cats[4].CategoryID,
                        Title = "Carnot cycle efficiency - does it ever reach 100%?",
                        Content = "I learned that Carnot cycle is the most efficient. Can we ever achieve 100% efficiency in real machines? What are the practical limits?",
                        ViewsCount = 92,
                        Upvotes = 8,
                        CreatedAt = DateTime.Now.AddHours(-12),
                        IsDeleted = false
                    },
                    new Post
                    {
                        UserID = users[5].UserID,
                        CategoryID = cats[5].CategoryID,
                        Title = "Understanding compound interest vs simple interest",
                        Content = "Can someone explain the practical difference? Which is more commonly used in real-world banking and investments?",
                        ViewsCount = 145,
                        Upvotes = 11,
                        CreatedAt = DateTime.Now.AddHours(-6),
                        IsDeleted = false
                    },
                    new Post
                    {
                        UserID = users[0].UserID,
                        CategoryID = cats[6].CategoryID,
                        Title = "What is the difference between civil and criminal law?",
                        Content = "I'm struggling to understand the distinction between these two branches. What are the key differences in justice procedures?",
                        ViewsCount = 201,
                        Upvotes = 16,
                        CreatedAt = DateTime.Now.AddHours(-4),
                        IsDeleted = false
                    }
                };
                context.Posts.AddRange(posts);
                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Data seeding failed: {ex}");
        }
    }
}
else
{
    // Do not run automatic migrations or seed data in non-development environments.
    // Migrations and production data operations should be performed explicitly via CI/CD or admin commands.
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// ===== ADDED: Authentication must come BEFORE Authorization =====
// UseAuthentication = "read the cookie and figure out who this user is"
// UseAuthorization  = "check if this user is ALLOWED to access this page"
// Order matters! You can't check permissions before you know who they are.
app.UseAuthentication();
app.UseAuthorization();


app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task EnsureMediaColumnsAsync(ApplicationDbContext context)
{
    // Older local databases can miss these columns because earlier image-url migrations were empty.
    await EnsureColumnExistsAsync(context, "Posts", "ImageUrl", "nvarchar(max) NULL", "TEXT NULL");
    await EnsureColumnExistsAsync(context, "Answers", "ImageUrl", "nvarchar(max) NULL", "TEXT NULL");
    await EnsureColumnExistsAsync(context, "Users", "ProfileImageUrl", "nvarchar(max) NULL", "TEXT NULL");
    await EnsureColumnExistsAsync(context, "Posts", "CourseCode", "nvarchar(20) NULL", "TEXT NULL");
}

static async Task EnsureColumnExistsAsync(
    ApplicationDbContext context,
    string tableName,
    string columnName,
    string sqlServerDefinition,
    string sqliteDefinition)
{
    if (await ColumnExistsAsync(context, tableName, columnName))
    {
        return;
    }

    string sql;
    if (context.Database.IsSqlServer())
    {
        sql = $"ALTER TABLE [{tableName}] ADD [{columnName}] {sqlServerDefinition};";
    }
    else if (context.Database.IsSqlite())
    {
        sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {sqliteDefinition};";
    }
    else
    {
        throw new NotSupportedException(
            $"Unsupported database provider '{context.Database.ProviderName}' for automatic media-column repair.");
    }

    await context.Database.ExecuteSqlRawAsync(sql);
    Console.WriteLine($"[INFO] Added missing column {tableName}.{columnName}.");
}

static async Task<bool> ColumnExistsAsync(ApplicationDbContext context, string tableName, string columnName)
{
    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (context.Database.IsSqlServer())
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@tableName";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@columnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        if (context.Database.IsSqlite())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        throw new NotSupportedException(
            $"Unsupported database provider '{context.Database.ProviderName}' while checking column existence.");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}
