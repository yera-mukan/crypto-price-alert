using System.ComponentModel.DataAnnotations;

namespace CryptoPriceAlert.Configuration;

public class ApplicationOptions
{
    [Required] [MaxLength(10000)] public string ApiKey { get; set; } = default!;
    [Required] [MaxLength(10000)] public string BaseUrl { get; set; } = default!;
}