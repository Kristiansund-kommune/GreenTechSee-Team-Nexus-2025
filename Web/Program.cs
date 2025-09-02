var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

// Security headers
app.Use(async (context, next) =>
{
    var env = app.Environment;

    // Prevent MIME type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Basic clickjacking protection
    context.Response.Headers["X-Frame-Options"] = "DENY";

    // Reduce referrer leakage
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    // Opt-in older XSS filters (harmless in modern browsers)
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

    // Limit powerful APIs by default (loose and safe defaults)
    context.Response.Headers["Permissions-Policy"] =
        "geolocation=(), camera=(), fullscreen=(self)";

    await next();
});

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
