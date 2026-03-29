using System.Collections.Concurrent;
using GRID.Data;
using GRID.Models;
using Microsoft.EntityFrameworkCore;

namespace GRID.Services
{
    public class ServiceStatus
    {
        public bool IsUp { get; set; }
        public long ResponseMs { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public interface IServiceStatusService
    {
        ServiceStatus? GetStatus(string token);
    }

    public class ServiceStatusService(IServiceScopeFactory scopeFactory, ILogger<ServiceStatusService> logger)
        : BackgroundService, IServiceStatusService
    {
        private readonly ConcurrentDictionary<string, ServiceStatus> _statuses = new();
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public ServiceStatus? GetStatus(string token) =>
            _statuses.TryGetValue(token, out var s) ? s : null;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAllServicesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task CheckAllServicesAsync(CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var links = await db.ServiceLinks.Where(s => s.IsActive).ToListAsync(ct);

            await Task.WhenAll(links.Select(link => CheckAsync(link, ct)));
        }

        private async Task CheckAsync(ServiceLink link, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool isUp;
            try
            {
                var response = await _http.GetAsync(link.Url, ct);
                isUp = response.IsSuccessStatusCode;
            }
            catch
            {
                isUp = false;
            }
            sw.Stop();

            _statuses[link.Token] = new ServiceStatus
            {
                IsUp = isUp,
                ResponseMs = sw.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow
            };

            logger.LogDebug("Service {Name} ({Token}): {Status} in {Ms}ms", link.Name, link.Token, isUp ? "UP" : "DOWN", sw.ElapsedMilliseconds);
        }
    }
}
