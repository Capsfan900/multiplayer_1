using System;
using System.IO;
using System.Runtime.Serialization.Json;
using Wildstone.Maps;

internal static class ExportLayouts
{
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        var maps = ArenaLayouts.CreateAll();
        foreach (var map in maps)
        {
            var errors = map.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(map.Id + ": " + string.Join("; ", errors));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        using (var stream = File.Create(args[0])) new DataContractJsonSerializer(typeof(ArenaDefinition[])).WriteObject(stream, maps);
        Console.WriteLine("Exported three authored layout definitions; Unity collision validation remains pending.");
        return 0;
    }
}
