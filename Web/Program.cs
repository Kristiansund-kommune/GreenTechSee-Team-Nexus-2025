var builder = WebApplication.CreateBuilder(args);

// Legg til tjenester i containeren.

builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

// Konfigurer HTTP-forespørselsrørledningen.

app.UseHttpsRedirection();

// Sikkerhetsheadere
app.Use(async (context, next) =>
{
    var env = app.Environment;

    // Hindre MIME-type-sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Grunnleggende beskyttelse mot clickjacking
    context.Response.Headers["X-Frame-Options"] = "DENY";

    // Reduser lekkasje av referrer
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    // Aktiver eldre XSS-filtre (ufarlig i moderne nettlesere)
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

    // Begrens kraftige API-er som standard (løs og trygg standard)
    context.Response.Headers["Permissions-Policy"] =
        "geolocation=(), camera=(), fullscreen=(self)";

    await next();
});

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
