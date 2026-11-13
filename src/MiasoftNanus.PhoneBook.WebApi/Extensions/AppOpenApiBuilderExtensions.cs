using Scalar.AspNetCore;

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
    /// <param name="app">The application builder used to add Swagger UI middleware to the request pipeline.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Configures Swagger UI middleware to display the generated OpenAPI document for the application.
        /// </summary>
        public IApplicationBuilder UseSwaggerUiOpenApiExtension()
        {
            app.UseSwaggerUI(options =>
            {
                // Swagger UI reads our OpenAPI document and renders the interactive page at /swagger
                options.SwaggerEndpoint("/openapi/v1.json", "v1");
            });
            return app;
        }

        /// <summary>
        /// Configures ReDoc middleware to display the generated OpenAPI document for the application.
        /// </summary>
        /// <returns> The application builder with ReDoc middleware configured. </returns>
        public IApplicationBuilder UseReDocOpenApiExtension()
        {
            // ReDoc reads our OpenAPI document and renders the interactive page at /redoc
            app.UseReDoc(options =>
            {
                options.RoutePrefix = "redoc";
                options.SpecUrl("/openapi/v1.json");
            });
            return app;
        }
    }

    /// <summary>
    /// Maps the Scalar API Reference endpoint to serve the OpenAPI document for the application.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used to map the Scalar API Reference endpoint.</param>
    /// <returns> The endpoint route builder with the Scalar API Reference endpoint mapped. </returns>
    public static IEndpointRouteBuilder MapScalarApiReferenceOpenApiExtension(this IEndpointRouteBuilder endpoints)
    {
        // Map the Scalar API Reference endpoint to serve the OpenAPI document
        endpoints.MapScalarApiReference();
        return endpoints;
    }
}