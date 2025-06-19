using FastEndpoints;
using Serilog;

namespace UnshackledWord.Tooling.WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));
        builder.Services.AddFastEndpoints();

        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.UseFastEndpoints(x =>
        {
            x.Endpoints.RoutePrefix = "api";
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        if (app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        app.Run();
    }
}
