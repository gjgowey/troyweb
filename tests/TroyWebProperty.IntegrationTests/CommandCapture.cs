using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TroyWebProperty.IntegrationTests;

public sealed class CommandCapture : DbCommandInterceptor
{

    public ConcurrentQueue<string> Commands { get; } = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Commands.Enqueue(command.CommandText);

        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Commands.Enqueue(command.CommandText);

        return ValueTask.FromResult(result);
    }

}