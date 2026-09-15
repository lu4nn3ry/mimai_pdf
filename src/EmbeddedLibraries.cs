using System;
using System.IO;
using System.Reflection;

namespace TradutorPdfOllama
{
    internal static class EmbeddedLibraries
    {
        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mimai." + name + ".dll"))
                {
                    if (stream == null) return null;
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        return Assembly.Load(bytes.ToArray());
                    }
                }
            };
        }
    }
}
