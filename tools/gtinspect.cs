using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var path = @"Y:\UnityInstalls\6000.5.0b1\Editor\Data\Managed\UnityEngine\UnityEditor.GraphToolkitModule.dll";
using var stream = File.OpenRead(path);
using var peReader = new PEReader(stream);
var reader = peReader.GetMetadataReader();
foreach (var handle in reader.TypeDefinitions)
{
    var type = reader.GetTypeDefinition(handle);
    var ns = reader.GetString(type.Namespace);
    var name = reader.GetString(type.Name);
    var full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    if (full.Contains("Context") || full.Contains("Block") || full.Contains("Node") || full.Contains("Part") || full.Contains("Option"))
        Console.WriteLine(full);
}
