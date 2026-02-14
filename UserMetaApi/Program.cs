using Microsoft.EntityFrameworkCore;
using UserMetaApi.Interfaces;
using UserMetaApi.Providers;
using UserMetaApi.Services;
using UserMetaApi.Workers;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();

var domain = Environment.GetEnvironmentVariable("DOMAIN") ?? "";
var email = Environment.GetEnvironmentVariable("EMAIL") ?? "";

// Configure Forwarded Headers to read real client IP
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    
    // Trust all proxies (Docker networks, reverse proxies, etc.)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    
    // Allow forwarded headers from any source
    // In production, consider restricting this to known proxy IPs
});

// Add services to the container.
builder.Services.AddScoped<IUserMetaService, UserMetaService>();
builder.Services.AddSingleton<LetsEncryptService>();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//builder.Services.AddHostedService<LetsEncryptRenewalService>();

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = Path.Combine(builder.Environment.ContentRootPath, "certs", $"{domain}.pfx");

    // Always listen on HTTP for ACME challenge
    options.ListenAnyIP(80);
    Console.WriteLine("Kestrel listening on port 80 (HTTP)");

    // Only listen on HTTPS if certificate exists
    if (File.Exists(certPath))
    {
        try
        {
            var cert = new X509Certificate2(certPath, "");
            options.ListenAnyIP(443, listenOptions =>
            {
                listenOptions.UseHttps(cert);
            });
            Console.WriteLine($"Kestrel listening on port 443 (HTTPS) with certificate: {certPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load certificate: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"Certificate not found at: {certPath}. HTTPS (443) will not be available until certificate is generated.");
    }
});

var app = builder.Build();

// IMPORTANT: Add Forwarded Headers Middleware FIRST (before other middleware)
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serve static files from wwwroot
app.UseStaticFiles();

// Explicitly serve .well-known directory (in case it's being filtered)
var wellKnownPath = Path.Combine(app.Environment.WebRootPath, ".well-known");
Directory.CreateDirectory(wellKnownPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wellKnownPath),
    RequestPath = "/.well-known",
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/plain"
});

app.UseAuthorization();
app.MapControllers();

app.Run();

