using EventRecommender.Data;
using EventRecommender.Models;
using EventRecommender.Services;
using EventRecommender.Services.Email;
using EventRecommender.ML;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================
// 📦 DATABASE
// =====================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// =====================
// 👤 IDENTITY
// =====================
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.Cookie.Name = ".EventRec.Auth";
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SameSite = SameSiteMode.Lax; // fine for SPA on same machine/port
    // opt.Cookie.SecurePolicy = CookieSecurePolicy.None; // uncomment if testing over http:// locally
});

// =====================
// 🧩 MVC + RAZOR
// =====================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();

// =====================
// 💡 CORE SERVICES
// =====================
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddSingleton(new RecommenderConfig());
builder.Services.AddScoped<IRecommenderService, RecommenderService>();
builder.Services.AddScoped<ITrendingService, TrendingService>();

// =====================
// ✉️ EMAIL + WEEKLY DIGEST
// =====================
builder.Services.AddScoped<IEmailService, EmailService>();

// Start background digest only if enabled (config/user-secrets).
// In Development it's OFF by default unless WeeklyDigest:RunInDevelopment=true.
var env = builder.Environment;
var digestEnabled = builder.Configuration.GetValue<bool>("WeeklyDigest:Enabled", true);
var runInDev = builder.Configuration.GetValue<bool>("WeeklyDigest:RunInDevelopment", false);

if (digestEnabled && (!env.IsDevelopment() || runInDev))
{
    builder.Services.AddHostedService<EventRecommender.Services.WeeklyDigestService>();
}

// =====================
// 🌐 CORS for SPA
// =====================
builder.Services.AddCors(o => o.AddPolicy("Spa", p =>
    p.WithOrigins("http://localhost:8080") // change if your Vite port differs (e.g., 5173/5174)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
));

var app = builder.Build();

// =====================
// 🚀 DB MIGRATION + SEED
// =====================
using (var scope = app.Services.CreateScope())
{
    var svcs = scope.ServiceProvider;
    var db = svcs.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed demo users/events if needed
    var seeder = svcs.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("Spa");

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
