using Serilog;
using UnshackledWord.Tooling.SeedDb.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

namespace UnshackledWord.Tooling.SeedDb;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSeedDbServices(builder.Configuration);
        var arguments = new VerbArguments(args);

        // A command on the command line (export-seed, anchor-check) replaces the seeding pipeline.
        if (arguments.Verb is not null)
        {
            builder.Services.AddSingleton(arguments);
            builder.Services.AddHostedService<VerbWorker>();
        }
        else
        {
            builder.Services.AddHostedService<Worker>();
        }

        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        var host = builder.Build();
        host.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
