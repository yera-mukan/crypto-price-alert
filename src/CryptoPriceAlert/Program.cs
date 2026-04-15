using CryptoPriceAlert.Configuration;
using DotNetEnv;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddOptions<ApplicationOptions>().Bind(builder.Configuration).ValidateDataAnnotations();

builder.Services.AddProblemDetails();

builder.Services.AddHttpClient<ICoinGeckoService, CoinGeckoService>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add("x-cg-demo-api-key", options.ApiKey);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
    });

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policyBuilder => policyBuilder.Expire(TimeSpan.FromSeconds(30)));
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/price/{cryptoId}", async (string cryptoId, ICoinGeckoService cryptoService) =>
{
    try
    {
        var price = await cryptoService.GetPriceAsync(cryptoId);

        return Results.Ok(new
        {
            id = cryptoId,
            price_usd = price,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).CacheOutput();

app.Run();

internal interface ICoinGeckoService
{
    Task<decimal> GetPriceAsync(string cryptoId);
}

internal record PriceResponse(decimal Usd);

internal class CoinGeckoService : ICoinGeckoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoinGeckoService> _logger;

    public CoinGeckoService(HttpClient httpClient, ILogger<CoinGeckoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetPriceAsync(string cryptoId)
    {
        try 
        {
            var url = $"simple/price?ids={cryptoId}&vs_currencies=usd";
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, PriceResponse>>(url);

            if (response != null && response.TryGetValue(cryptoId, out var priceInfo))
            {
                return priceInfo.Usd;
            }

            throw new KeyNotFoundException($"Crypto ID '{cryptoId}' not found in response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price for {CryptoId}", cryptoId);
            throw;
        }
    }
}