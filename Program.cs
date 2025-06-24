using AnimeGameSite.Hubs;
using DemoSRP;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;

string exeDir = AppDomain.CurrentDomain.BaseDirectory;
DirectoryInfo dir = new DirectoryInfo(exeDir);

// Ищем папку "AnimeGameSite" в родительских директориях
while (dir != null && dir.Name != "AnimeGameSite")
{
    dir = dir.Parent;
}

if (dir != null)
{
    Environment.CurrentDirectory = dir.FullName;
    Console.WriteLine($"Установлена директория: {Environment.CurrentDirectory}");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<UserDatabase>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<HttpsRedirectionOptions>(options =>
{
    options.HttpsPort = 5001; // Указываем порт для HTTPS-перенаправления
});

// Настройка JWT
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKey12345678901234567890");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "QuietPlanet",
            ValidAudience = "QuietPlanetUsers",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Content-Disposition");
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
    var currentDirectory = Directory.GetCurrentDirectory();
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        listenOptions.UseHttps(adapterOptions =>
        {
            adapterOptions.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(
                currentDirectory + "/Data/certificate.pfx", // Путь к вашему .pfx файлу
                password: ""
            );
        });
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".woff2"))
        {
            ctx.Context.Response.Headers.Append("Content-Type", "font/woff2");
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        }
        if (ctx.File.Name.EndsWith(".js"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        if (ctx.File.Name.EndsWith(".html"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        if (ctx.File.Name.EndsWith(".png"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        if (ctx.File.Name.EndsWith(".jpg"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        if (ctx.File.Name.EndsWith(".webp"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        if (ctx.File.Name.EndsWith(".css"))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
    }
});
app.UseCors("AllowAll");
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<ConceptArtHub>("/conceptArtHub");

app.Run();