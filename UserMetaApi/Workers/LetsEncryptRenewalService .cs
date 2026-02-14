using System.Security.Cryptography.X509Certificates;
using UserMetaApi.Services;

namespace UserMetaApi.Workers
{
    public class LetsEncryptRenewalService : BackgroundService
    {
        private readonly LetsEncryptService _service;
        private readonly ILogger<LetsEncryptRenewalService> _logger;
        private readonly IHostApplicationLifetime _lifetime;

        public LetsEncryptRenewalService(
            LetsEncryptService service, 
            ILogger<LetsEncryptRenewalService> logger,
            IHostApplicationLifetime lifetime)
        {
            _service = service;
            _logger = logger;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit for the app to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var domain = Environment.GetEnvironmentVariable("DOMAIN") ?? "";
                    var certPath = Path.Combine(Directory.GetCurrentDirectory(), "certs", $"{domain}.pfx");
                    bool needsRestart = false;

                    // Check if certificate exists and is valid
                    if (File.Exists(certPath))
                    {
                        try
                        {
                            var existingCert = new X509Certificate2(certPath, "");
                            var certAge = DateTime.UtcNow - existingCert.NotBefore;
                            if (certAge < TimeSpan.FromDays(30))
                            {
                                _logger.LogInformation($"Certificate is only {certAge.Days} days old. No renewal needed yet.");
                            }
                            else
                            {
                                _logger.LogInformation($"Certificate is {certAge.Days} days old. Renewing...");
                                await _service.GetCertificateAsync();
                                needsRestart = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not read existing certificate: {ex.Message}. Will generate new one.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No certificate found. Generating new certificate...");
                        await _service.GetCertificateAsync();
                        needsRestart = true;
                    }

                    // Restart application if certificate was generated/renewed
                    if (needsRestart)
                    {
                        _logger.LogInformation("Certificate generated/renewed successfully. Restarting application to load new certificate...");
                        await Task.Delay(2000, stoppingToken);
                        _lifetime.StopApplication();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during certificate check/renewal");

                }

                // Calculate time until next 5 AM
                var now = DateTime.UtcNow;
                var next5AM = DateTime.UtcNow.Date.AddHours(5);
                
                // If it's already past 5 AM today, schedule for tomorrow at 5 AM
                if (now >= next5AM)
                {
                    next5AM = next5AM.AddDays(1);
                }

                var timeUntil5AM = next5AM - now;
                _logger.LogInformation($"Next certificate check scheduled at {next5AM:yyyy-MM-dd HH:mm:ss} UTC (in {timeUntil5AM.TotalHours:F1} hours)");
                
                await Task.Delay(timeUntil5AM, stoppingToken);
            }
        }
    }
}
