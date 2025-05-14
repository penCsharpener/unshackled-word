using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.Sqlite.Migration.Services;

public sealed class SqliteMigrator : IDbMigrator
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public SqliteMigrator(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }
}
