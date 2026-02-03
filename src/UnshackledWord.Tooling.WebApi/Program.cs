using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using UnshackledWord.Tooling.WebApi.Extensions;

namespace UnshackledWord.Tooling.WebApi;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));
        builder.Services.AddFastEndpoints()
            .SwaggerDocument();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddWebApiServices(builder.Configuration);

        builder.Services.AddOpenApi();
        builder.Services.AddCors();
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        var app = builder.Build();


        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        }

        if (app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
            app.UseCors();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints(x =>
        {
            x.Endpoints.RoutePrefix = "api";
            x.Endpoints.Configurator = ep =>
            {
                ep.AllowAnonymous();
            };
        }).UseSwaggerGen();

        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
