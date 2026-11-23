namespace LibraryManagement.Web.Services;

/// <summary>
/// Chạy nền: định kỳ hủy các yêu cầu đặt giữ đã quá ngày nhận và trả sách về kho,
/// để số "Còn N cuốn" trên trang tìm sách luôn đúng dù không ai mở trang đặt trước.
/// </summary>
public class HoldExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HoldExpiryService> _logger;

    public HoldExpiryService(IServiceScopeFactory scopes, ILogger<HoldExpiryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var reservations = scope.ServiceProvider.GetRequiredService<ReservationService>();
                var expired = await reservations.ExpireHoldsAsync();
                if (expired > 0)
                    _logger.LogInformation("Đã hủy {Count} yêu cầu đặt giữ quá hạn nhận và trả sách về kho.", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi hủy các yêu cầu đặt giữ quá hạn.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
