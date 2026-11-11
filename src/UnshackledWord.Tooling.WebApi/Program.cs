using System.Text.Json;
using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using UnshackledWord.Tooling.WebApi.Auth;
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
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);
        builder.Services.AddAuth(builder.Configuration);
        builder.Services.AddWebApiServices(builder.Configuration);

        builder.Services.AddOpenApi();
        builder.Services.AddCors();

        var app = builder.Build();


        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            // The session cookie needs credentialed CORS, which forbids a wildcard origin: reflect the caller instead.
            // (The Vite dev server normally proxies /api same-origin, so this only matters for direct calls.)
            app.UseCors(x => x.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials());
        }

        if (app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
            app.UseCors(x => x
                .WithOrigins(app.Configuration.GetSection("App").Get<AppOptions>()?.FrontendBaseUrl.TrimEnd('/') ?? string.Empty)
                .AllowAnyMethod().AllowAnyHeader().AllowCredentials());
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints(x =>
        {
            x.Endpoints.RoutePrefix = "api";
            x.Serializer.ResponseSerializer = (rsp, dto, cType, jCtx, ct) =>
            {
                rsp.ContentType = cType;
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                return rsp.WriteAsync(
                    JsonSerializer.Serialize(dto, dto.GetType(), options),
                    ct
                );
            };
            x.Endpoints.Configurator = ep =>
            {
                // Everything is public unless it opts in with [RequiresLogin] (account endpoints under /api/auth).
                if (!ep.EndpointType.IsDefined(typeof(RequiresLoginAttribute), false))
                {
                    ep.AllowAnonymous();
                }
            };
        }).UseSwaggerGen();

        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
