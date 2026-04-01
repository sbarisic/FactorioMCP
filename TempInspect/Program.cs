using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

// Force the extension assembly to load
var builder = new ServiceCollection().AddMcpServer();

foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
{
    Type[] types;
    try { types = asm.GetExportedTypes(); } catch { continue; }
    foreach (var t in types.Where(t => t.Name.Contains("FilterBuilder")))
    {
        Console.WriteLine($"Type: {t.FullName} in {asm.GetName().Name}");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.Contains("CallTool")))
        {
            Console.WriteLine($"  Method: {m.Name}");
            foreach (var p in m.GetParameters())
                Console.WriteLine($"    Param: {p.Name} ({p.ParameterType})");
        }
    }
}
