using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

// Only the explicitly tagged, native-independent rules run here. Unity tests stay in the Editor.
internal static class ManagedNUnitRunner
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Expected test assembly and result XML paths."); return 2; }
        try
        {
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(Assembly.LoadFrom(Path.GetFullPath(args[0])),
                    new Dictionary<string, object> { { "NumberOfTestWorkers", 0 } });
                var result = runner.Run(TestListener.NULL, new CategoryFilter("Managed"));
                File.WriteAllText(args[1], result.ToXml(true).OuterXml);
                PrintFailures(result);
                Console.WriteLine("Managed NUnit: " + result.PassCount + " passed; " + result.FailCount +
                    " failed; " + result.SkipCount + " skipped. No Unity native runtime exercised.");
                return result.PassCount > 0 && result.FailCount == 0 && result.SkipCount == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }

    private static void PrintFailures(ITestResult result)
    {
        if (!result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed)
            Console.Error.WriteLine(result.FullName + ": " + result.Message + Environment.NewLine + result.StackTrace);
        foreach (var child in result.Children) PrintFailures(child);
    }
}
