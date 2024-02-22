using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Microsoft.Azure.Functions.Worker;

// Follows implementation of HttpContextAccessor at https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/HttpContextAccessor.cs

public interface IFunctionContextAccessor
{
    FunctionContext? FunctionContext { get; set; }
}

[DebuggerDisplay("FunctionContext = {FunctionContext}")]
public class FunctionContextAccessor : IFunctionContextAccessor
{
    static readonly AsyncLocal<FunctionContextHolder> current = new AsyncLocal<FunctionContextHolder>();

    public virtual FunctionContext? FunctionContext
    {
        get => current.Value?.Context;
        set
        {
            var holder = current.Value;
            if (holder != null)
            {
                // Clear current context trapped in the AsyncLocals, as its done.
                holder.Context = default;
            }

            if (value != null)
            {
                // Use an object indirection to hold the context in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                current.Value = new FunctionContextHolder { Context = value };
            }
        }
    }

    class FunctionContextHolder
    {
        public FunctionContext? Context;
    }
}

public class FunctionContextAccessorMiddleware(IFunctionContextAccessor accessor) : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        accessor.FunctionContext = context;
        return next(context);
    }
}