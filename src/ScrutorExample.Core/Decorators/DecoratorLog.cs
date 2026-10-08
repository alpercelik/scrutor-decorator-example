using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Shared logging behaviour for the decorators.
/// </summary>
/// <remarks>
/// All messages go through the <c>[LoggerMessage]</c> source generator, which emits
/// strongly typed, allocation-free log delegates and removes the need for
/// <c>if (logger.IsEnabled(...))</c> guards.
/// </remarks>
internal static partial class DecoratorLog
{
    /// <summary>
    /// Runs an asynchronous decorated call with structured entry/exit/error logging.
    /// </summary>
    /// <typeparam name="TResult">The awaited result type.</typeparam>
    /// <param name="logger">Target logger.</param>
    /// <param name="serviceType">The decorated interface, e.g. <c>IProductService</c>.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="next">Delegate invoking the inner implementation.</param>
    /// <param name="method">
    /// Filled in automatically by the compiler with the name of the decorating
    /// method, so decorators never have to pass a name by hand.
    /// </param>
    /// <returns>The inner call's result.</returns>
    public static async Task<TResult> RunAsync<TResult>(
        ILogger logger,
        Type serviceType,
        object?[] arguments,
        Func<Task<TResult>> next,
        [CallerMemberName] string method = "")
    {
        var service = serviceType.Name;
        var argumentsText = DescribeArguments(arguments);

        Entering(logger, service, method, argumentsText);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next().ConfigureAwait(false);
            stopwatch.Stop();

            // Formatting happens before the call: the logging delegates are only
            // invoked when the level is enabled, but the arguments are always
            // evaluated by the caller.
            var elapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3);
            var resultText = DescribeResult(result);

            Completed(logger, service, method, elapsedMs, resultText);
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            // Log and rethrow: a decorator must never swallow failures.
            Failed(logger, exception, service, method, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3));
            throw;
        }
    }

    /// <summary>
    /// Renders the invocation arguments for a log message.
    /// </summary>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>A compact representation, e.g. <c>2f1c…, CancellationToken</c>.</returns>
    public static string DescribeArguments(object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ", ",
            arguments.Select(argument => argument switch
            {
                null => "null",
                CancellationToken => "CancellationToken",
                string text => $"\"{text}\"",
                _ => argument.ToString() ?? "null",
            }));
    }

    /// <summary>
    /// Renders a result for a log message, summarising collections instead of
    /// dumping every element.
    /// </summary>
    /// <param name="result">The produced result.</param>
    /// <returns>A compact representation.</returns>
    public static string DescribeResult(object? result) => result switch
    {
        null => "<null>",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable collection =>
            $"{result.GetType().Name} with {collection.Cast<object?>().Count()} item(s)",
        _ => result.ToString() ?? "<null>",
    };

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "→ {Service}.{Method}({Arguments})")]
    private static partial void Entering(ILogger logger, string service, string method, string arguments);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "← {Service}.{Method} completed in {ElapsedMs} ms with {Result}")]
    private static partial void Completed(ILogger logger, string service, string method, double elapsedMs, string result);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "✗ {Service}.{Method} failed after {ElapsedMs} ms")]
    private static partial void Failed(ILogger logger, Exception exception, string service, string method, double elapsedMs);
}
