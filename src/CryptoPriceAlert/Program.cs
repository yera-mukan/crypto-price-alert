using CryptoPriceAlert.Configuration;
using DotNetEnv;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddOptions<ApplicationOptions>().Bind(builder.Configuration).ValidateDataAnnotations();

builder.Services.AddHttpClient<ICoinGeckoService, CoinGeckoService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("x-cg-demo-api-key", options.ApiKey);
});

var app = builder.Build();

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
});

app.Run();

internal interface ICoinGeckoService
{
    Task<decimal> GetPriceAsync(string cryptoId);
}

internal class CoinGeckoService : ICoinGeckoService
{
    private readonly HttpClient _httpClient;

    public CoinGeckoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetPriceAsync(string cryptoId)
    {
        var url = $"simple/price?ids={cryptoId}&vs_currencies=usd";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"API Error: {response.StatusCode} - {errorContent}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseBody);
        var priceToken = json[cryptoId]?["usd"];

        return priceToken?.Value<decimal>() ??
               throw new Exception($"Invalid crypto ID '{cryptoId}' or data unavailable.");
    }
}