using MaxMind.GeoIP2;
using Microsoft.EntityFrameworkCore;
using UserMetaApi.Interfaces;
using UserMetaApi.Providers;

namespace UserMetaApi.Services
{
    public class UserMetaService : IUserMetaService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserMetaService> _logger;

        public UserMetaService(AppDbContext context, ILogger<UserMetaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CaptureAsync(Guid token, HttpContext context)
        {
            var ip = GetClientIp(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var referer = context.Request.Headers["Referer"].ToString();
            var origin = context.Request.Headers[""].ToString();
     
            var sql = "INSERT INTO WebValidations (Token, Ip, UserAgent, Browser, Country, Referer, CreatedDate,UpdatedAt, IsDeleted) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)";
            _context.Database.ExecuteSqlRaw(sql, token, ip, userAgent, ParseBrowser(userAgent), GetCountry(ip), referer, DateTime.UtcNow, DateTime.UtcNow, 0 );

            return;
        }

        private string GetClientIp(HttpContext context)
        {
            string ip;
            
            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ip = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
            }
            else
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                ip = remoteIp?.MapToIPv4().ToString() ?? "Unknown";
            }

            return ip;
        }

        private string ParseBrowser(string ua)
        {
            if (ua.Contains("Chrome")) return "Chrome";
            if (ua.Contains("Firefox")) return "Firefox";
            if (ua.Contains("Safari") && !ua.Contains("Chrome")) return "Safari";
            if (ua.Contains("Edg")) return "Edge";
            return "Unknown";
        }

        private string GetCountry(string ip)
        {
            try
            {
                using var reader = new DatabaseReader("/app/GeoLite2-Country.mmdb");
                var response = reader.Country(ip);
                return response.Country.Name;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Geo lookup failed");
                return "";
            }
        }
    }
}
