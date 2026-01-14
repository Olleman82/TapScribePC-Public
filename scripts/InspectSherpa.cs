using System;
using System.Reflection;
using System.IO;
using System.Linq;

// Inspect the SherpaOnnx assembly to find the correct Diarization API
class Program
{
    static void Main()
    {
        try
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SherpaOnnx.dll");
            if (!File.Exists(dllPath))
            {
                // Try looking in NuGet cache or output dir
                dllPath = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "SherpaOnnx.dll", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (dllPath == null)
            {
                Console.WriteLine("Could not find SherpaOnnx.dll");
                return;
            }

            var asm = Assembly.LoadFrom(dllPath);
            Console.WriteLine($"Loaded Assembly: {asm.FullName}");

            var types = asm.GetTypes()
                .Where(t => t.Name.Contains("Diarization") || t.Name.Contains("Speaker"))
                .OrderBy(t => t.Name);

            foreach (var type in types)
            {
                Console.WriteLine($"Type: {type.Name}");
                foreach (var prop in type.GetProperties())
                {
                    Console.WriteLine($"  Prop: {prop.PropertyType.Name} {prop.Name}");
                }
                foreach (var method in type.GetMethods().Where(m => m.IsPublic && !m.IsSpecialName))
                {
                    Console.WriteLine($"  Method: {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
