using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace CryptoPriceAlert;

public abstract class Program
{
    public static async Task Main(string[] args)
    {
        Env.Load();
        var apiKey = Environment.GetEnvironmentVariable("API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API_KEY not found in .env file.");
            return;
        }

        var serviceProvider = new ServiceCollection()
            .AddHttpClient("CoinGecko", client =>
            {
                client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
                client.DefaultRequestHeaders.Add("vs_currencies", "usd");
                client.DefaultRequestHeaders.Add("ids", "bitcoin");
                client.DefaultRequestHeaders.Add("x-cg-demo-api-key", apiKey);
            })
            .Services
            .BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        try
        {
            Console.WriteLine("Fetching Bitcoin price...");
            var price = await GetCryptoPriceAsync(httpClientFactory, "bitcoin");
            Console.WriteLine($"Current Bitcoin Price: ${price}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Operation failed: {ex.Message}");
        }
    }

    private static async Task<decimal> GetCryptoPriceAsync(IHttpClientFactory factory, string cryptoId)
    {
        var client = factory.CreateClient("CoinGecko");

        var url = $"simple/price?ids={cryptoId}&vs_currencies=usd";

        var response = await client.GetAsync(url);

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