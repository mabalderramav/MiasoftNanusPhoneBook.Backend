namespace MiasoftNanus.PhoneBook.WebApi.Config;

public class ApiConfig
{
    public int Timeout { get; set; } = 60;
    public string BaseUrl { get; set; } = string.Empty;
    public string TokenAdmin { get; set; } = string.Empty;
}