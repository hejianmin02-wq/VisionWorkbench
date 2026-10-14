using System.Reflection;
using System.Text.Json;
using VisionWorkbench.Tests;
using Xunit;

// A socket-free verification entry point for restricted environments. It executes the same
// synchronous Fact/InlineData assertions, but does not claim to be a VSTest adapter run.
var outputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/verification");
Directory.CreateDirectory(outputDirectory);
var results = new List<object>();
var passed = 0;
var failed = 0;
foreach (var method in typeof(ImageProcessorTests).GetMethods(BindingFlags.Public | BindingFlags.Instance)
    .Where(method => method.GetCustomAttribute<FactAttribute>() is not null).OrderBy(method => method.Name))
{
    var rows = method.GetCustomAttributes<InlineDataAttribute>().SelectMany(attribute => attribute.GetData(method)).ToArray();
    if (rows.Length == 0) rows = [Array.Empty<object>()];
    foreach (var row in rows)
    {
        var name = method.Name + (row.Length == 0 ? "" : "(" + string.Join(", ", row) + ")");
        try
        {
            method.Invoke(new ImageProcessorTests(), row);
            passed++;
            results.Add(new { Case = name, Outcome = "Passed" });
            Console.WriteLine("PASS " + name);
        }
        catch (TargetInvocationException exception)
        {
            failed++;
            results.Add(new { Case = name, Outcome = "Failed", Error = exception.InnerException?.ToString() ?? exception.ToString() });
            Console.WriteLine("FAIL " + name + ": " + exception.InnerException?.Message);
        }
    }
}
File.WriteAllText(Path.Combine(outputDirectory, "in-process-verification.json"), JsonSerializer.Serialize(new
{
    Mode = "in-process Fact/InlineData assertions; VSTest transport and adapter are not exercised",
    Passed = passed, Failed = failed, Cases = results
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"In-process assertions: passed {passed}, failed {failed}.");
return failed == 0 ? 0 : 1;
