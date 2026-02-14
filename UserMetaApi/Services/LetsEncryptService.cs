using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Directory = System.IO.Directory;

namespace UserMetaApi.Services
{
    public class LetsEncryptService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LetsEncryptService> _logger;
        private readonly string _domain;
        private readonly string _email;
        public LetsEncryptService(IWebHostEnvironment env, ILogger<LetsEncryptService> logger)
        {
            _env = env;
            _logger = logger;
          //  _certPath = Path.Combine(_env.ContentRootPath, "certs", $"{_domain}.pfx");

            _domain = Environment.GetEnvironmentVariable("DOMAIN") ?? "";
            _email = Environment.GetEnvironmentVariable("EMAIL") ?? "";
        }

        public async Task GetCertificateAsync()
        {
            // Your domain
 

            _logger.LogInformation("Requesting new Let's Encrypt certificate...");

            // 1. Create account
            var acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
            var account = await acme.NewAccount(_email, true);

            // 1. Create order
            var order = await acme.NewOrder(new[] { _domain });

            // 2. Get authorization & HTTP challenge
            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            // 3. Write challenge token to wwwroot/.well-known/acme-challenge
            var tokenPath = Path.Combine(_env.WebRootPath, ".well-known", "acme-challenge");
            Directory.CreateDirectory(tokenPath);
            File.WriteAllText(Path.Combine(tokenPath, httpChallenge.Token), httpChallenge.KeyAuthz);

            // 4. Trigger validation
            await httpChallenge.Validate();

            // 5. Wait until authorization is valid
            int maxAttempts = 30; // 60 seconds total
            int attempts = 0;
            while (attempts < maxAttempts)
            {
                var refreshedAuthz = await authz.Resource();
                _logger.LogInformation($"Authorization status: {refreshedAuthz.Status}");
                
                if (refreshedAuthz.Status == AuthorizationStatus.Valid) break;
                
                if (refreshedAuthz.Status == AuthorizationStatus.Invalid)
                {
                    var challengeDetails = await httpChallenge.Resource();
                    var error = challengeDetails.Error;
                    _logger.LogError($"Challenge failed! Error: {error?.Detail ?? "Unknown"}");
                    _logger.LogError($"Challenge URL: http://{_domain}/.well-known/acme-challenge/{httpChallenge.Token}");
                    _logger.LogError($"Expected content: {httpChallenge.KeyAuthz}");
                    throw new Exception($"Challenge failed! {error?.Detail ?? "Unknown error"}");
                }
                
                await Task.Delay(2000);
                attempts++;
            }

            if (attempts >= maxAttempts)
            {
                throw new Exception("Challenge validation timed out");
            }

            // 6. Generate private key and CSR
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var csr = await order.CreateCsr(privateKey);
            csr.AddName($"CN={_domain}");
            csr.SubjectAlternativeNames.Add(_domain);

            // 7. Finalize order with CSR
            var finalizedOrder = await order.Finalize(csr.Generate());

            // 8. Wait for order to be ready
            Order orderStatus;
            do
            {
                await Task.Delay(2000);
                orderStatus = await order.Resource();
            } while (orderStatus.Status == OrderStatus.Processing);

            if (orderStatus.Status != OrderStatus.Valid)
            {
                throw new Exception($"Order failed with status: {orderStatus.Status}");
            }

            // 9. Download certificate
            var cert = await order.Download();

            // 10. Export PFX
            var pfxBuilder = cert.ToPfx(privateKey);
            var pfx = pfxBuilder.Build(_domain, "");
            var certDirectory = Path.Combine(_env.ContentRootPath, "certs");
            Directory.CreateDirectory(certDirectory);
            File.WriteAllBytes(Path.Combine(certDirectory, $"{_domain}.pfx"), pfx);

            _logger.LogInformation($"Certificate saved to {Path.Combine(certDirectory, $"{_domain}.pfx")}");
        }
    }
}
