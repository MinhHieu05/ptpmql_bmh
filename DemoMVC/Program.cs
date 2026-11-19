using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DemoMvcDb")
    ?? throw new InvalidOperationException("Connection string 'DemoMvcDb' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlite(
        connectionString,
        sqlite => sqlite.MigrationsHistoryTable("__IdentityMigrationsHistory")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "DemoMVC.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddTransient<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddControllersWithViews();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("StudentProfiles.View", policy =>
        policy.RequireRole("User")
            .RequireClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.View));
    options.AddPolicy("StudentProfiles.Create", policy =>
        policy.RequireRole("User")
            .RequireClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.Create));
    options.AddPolicy("StudentProfiles.Edit", policy =>
        policy.RequireRole("User")
            .RequireClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.Edit));
    options.AddPolicy("StudentProfiles.Delete", policy =>
        policy.RequireRole("User")
            .RequireClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.Delete));
});
builder.Services.AddScoped<StudentCodeGenerator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

await IdentitySeeder.SeedAsync(
    app.Services,
    app.Configuration,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder"));

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
   
app.Run();
