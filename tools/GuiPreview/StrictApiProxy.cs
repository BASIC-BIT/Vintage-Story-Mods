using System.Reflection;
namespace TheBasics.GuiPreview;

public class StrictApiProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, StrictApiProxy>();
        ((StrictApiProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}
