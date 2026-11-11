namespace MiasoftNanus.PhoneBook.WebApi.Extensions;

/// <summary>
/// Provides extension methods for configuring OpenAPI-related middleware for the application.
/// </summary>
/// <remarks>
/// This class contains application builder extensions used to register Swagger UI integration
/// for displaying the generated OpenAPI document.
/// </remarks>
public static class AppOpenApiBuilderExtensions
{
    /// <summary>
    /// Configures Swagger UI middleware to display the generated OpenAPI document for the application.
    /// </summary>
    /// <param name="app">The application builder used to add Swagger UI middleware to the request pipeline.</param>
    public static void UseSwaggerUiOpenApiExtension(this IApplicationBuilder app)
    {
        app.UseSwaggerUI(options =>
        {
            // Swagger UI lee nuestro documento OpenAPI y pinta la página interactiva en /swagger
            options.SwaggerEndpoint("/openapi/v1.json", "MiasoftNanus PhoneBook WebApi v1");
        });
    }
}