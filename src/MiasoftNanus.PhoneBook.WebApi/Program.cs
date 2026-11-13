using MiasoftNanus.PhoneBook.WebApi.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logger = builder.Logging.InitLogging();

try
{
    logger.Information(
        "LOG INITIALIZED in {GetEnvironmentVariable}",
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "ENVIRONMENT NOT DEFINED."
    );

    #region Services

    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetailsAndExceptionsBuilderExtensions();
    builder.Services.AddApplicationConfigurationServices(builder.Configuration);
    builder.Services.AddApplicationLayersServices(builder.Configuration);

    #endregion

    var app = builder.Build();

    #region Middleware

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.MapOpenApi();
    app.MapScalarApiReferenceOpenApiExtension();
    app.UseSwaggerUiOpenApiExtension()
        .UseReDocOpenApiExtension();
    if (builder.Environment.IsDevelopment())
    {
        await app.MigrateDatabaseAsync();
    }

    app.UseHttpsRedirection();
    app.MapEndpoints();

    #endregion

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.Fatal(ex, "An unhandled exception has occurred in the middleware of the application");
}
finally
{
    await Log.CloseAndFlushAsync();
}