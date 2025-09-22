using AcadSync.Processor.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcadSync.Web.Data;
using AcadSync.Web.Hubs;
using AcadSync.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// When running in Development, explicitly bind Kestrel to both HTTP and HTTPS so Swagger is reachable on http://localhost:5000 and https://localhost:5001
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");
}

// Configuration & Logging
var configuration = builder.Configuration;
builder.Logging.AddConsole();

// Database (reuse AcadSyncAudit connection string per plan)
var connectionString = configuration.GetConnectionString("AcadSyncAudit")
                       ?? throw new InvalidOperationException("Connection string 'AcadSyncAudit' not found.");

// Add EF Core with Identity
builder.Services.AddDbContext<AcadSyncWebDbContext>(options =>
    options.UseSqlServer(connectionString));

 // Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AcadSyncWebDbContext>()
.AddDefaultTokenProviders();

// Reuse existing processor services
builder.Services.AddSingleton<IConfiguration>(configuration);
builder.Services.AddAcadSyncProcessor(configuration);

 // Application services
builder.Services.AddScoped<IRuleRepository, RuleRepository>();
// Register RunManager as a singleton and also start it as a hosted service so we can inject it elsewhere
builder.Services.AddSingleton<RunManager>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RunManager>());

// SignalR
builder.Services.AddSignalR();

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

 // Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

 // Enable Swagger when explicitly allowed via configuration (AcadSync:EnableSwagger = true)
if (configuration.GetValue<bool>("AcadSync:EnableSwagger"))
{
    // Register the OpenAPI generator
    app.UseSwagger();

    // Protect Swagger UI and endpoints:
    //  - Only allow requests from loopback addresses (localhost)
    //  - Require an authenticated user (cookie auth)
    app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"), appBranch =>
    {
        appBranch.Use(async (ctx, next) =>
        {
            // Restrict to loopback (localhost) only
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote == null || !System.Net.IPAddress.IsLoopback(remote))
            {
                ctx.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsync("Forbidden");
                return;
            }

            // Require authentication
            if (!(ctx.User?.Identity?.IsAuthenticated ?? false))
            {
                // Return 401 - caller can then authenticate via the web login
                ctx.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("Unauthorized - please sign in");
                return;
            }

            await next();
        });
    });

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve React build placed in wwwroot

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map hubs & controllers
app.MapControllers();
app.MapHub<RunsHub>("/hubs/runs");

// Fallback to index.html for SPA routes
app.MapFallbackToFile("index.html");

// Seed initial data (admin user) if configured
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // Run seeding synchronously during startup
    AcadSync.Web.Data.SeedData.EnsureAdminAsync(services, configuration).GetAwaiter().GetResult();
}

app.Run();
