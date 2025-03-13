using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace EFCore.BulkExtensions.SqlAdapters;

/// <summary>
/// A list of database servers supported by EFCore.BulkExtensions
/// </summary>
public enum SqlType
{
    /// <summary>
    /// Indicates database is Microsoft's SQL Server
    /// </summary>
    SqlServer,

    /// <summary>
    /// Indicates database is SQLite
    /// </summary>
    Sqlite,

    /// <summary>
    /// Indicates database is PostgreSQL
    /// </summary>
    PostgreSql,

    /// <summary>
    ///  Indicates database is Oracle
    /// </summary>
    Oracle,
}

#pragma warning disable CS1591 // No XML comment required here
public static class SqlAdaptersMapping
{
    public static string? ProviderName { get; private set; }

    public static SqlType DatabaseType { get; private set; }

    public static Func<DbContext, IDbServer>? Provider { get; set; }
    
    public static void UpdateProviderName(string? name)
    {
        var ignoreCase = StringComparison.InvariantCultureIgnoreCase;
        if (string.Equals(name, ProviderName, ignoreCase))
        {
            return;
        }

        ProviderName = name;

        DatabaseType = ProviderName switch
        {
            string n when n.EndsWith(SqlType.PostgreSql.ToString(), ignoreCase) => SqlType.PostgreSql, // ProviderName: Npgsql.EntityFrameworkCore.PostgreSQL
            string n when n.EndsWith(SqlType.Sqlite.ToString(), ignoreCase) => SqlType.Sqlite, // ProviderName: Microsoft.EntityFrameworkCore.Sqlite
            string n when n.Contains(SqlType.Oracle.ToString(), ignoreCase) => SqlType.Oracle, // ProviderName: Microsoft.EntityFrameworkCore.Oracle
            _ => SqlType.SqlServer // Default to SqlServer
        };
    }

    private static IDbServer? _dbServer { get; set; }

    /// <summary>
    /// Contains a list of methods to generate Adapters and helpers instances
    /// </summary>
    public static IDbServer DbServer(DbContext context)
    {
        var fromService = TryGetServer(context);
        if (fromService != null)
        {
            return fromService;
        }

        var fromProvider = Provider?.Invoke(context);
        if (fromProvider != null)
        {
            return fromProvider;
        }

        if (_dbServer == null || _dbServer.Type != DatabaseType)
        {
            static Type GetType(SqlType type)
            {
                var typeName = type.ToString();
                var assemblyName = typeof(SqlAdaptersMapping).Assembly.GetName().Name!.Replace(".Core", $".{typeName}");

                return Type.GetType($"EFCore.BulkExtensions.SqlAdapters.{typeName}.{typeName}DbServer,{assemblyName}") ??
                    throw new InvalidOperationException("Failed to resolve type.");
            }

            _dbServer = null;
            if (Enum.IsDefined(DatabaseType))
            {
                var serverType = GetType(DatabaseType);

                _dbServer = Activator.CreateInstance(serverType) as IDbServer;
            }

        }
        return _dbServer ?? throw new InvalidOperationException("Failed to create DbServer");
    }

    private static IDbServer? TryGetServer(DbContext context)
    {
        try
        {
            return context.Database.GetService<IDbServer>();
        }
        catch (InvalidOperationException)
        {
            // There is no "TryGetService" or something similar.
            return null;
        }
    }

#pragma warning restore CS1591 // No XML comment required here

    /// <summary>
    /// Creates the bulk operations adapter
    /// </summary>
    /// <returns></returns>
    public static ISqlOperationsAdapter CreateBulkOperationsAdapter(DbContext dbContext)
    {
        return DbServer(dbContext).Adapter;
    }

    /// <summary>
    /// Returns the Adapter dialect to be used
    /// </summary>
    /// <returns></returns>
    public static IQueryBuilderSpecialization GetAdapterDialect(DbContext dbContext)
    {
        return DbServer(dbContext).Dialect;
    }

    /// <summary>
    /// Returns the Database type
    /// </summary>
    /// <returns></returns>
    public static SqlType GetDatabaseType(DbContext dbContext)
    {
        return DbServer(dbContext).Type;
    }

    /// <summary>
    /// Returns per provider QueryBuilder instance, containing a compilation of SQL queries used in EFCore.
    /// </summary>
    /// <returns></returns>
    public static SqlQueryBuilder GetQueryBuilder(DbContext dbContext)
    {
        return DbServer(dbContext).QueryBuilder;
    }
}
