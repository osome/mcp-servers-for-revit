#if NET
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Loads a command set and its private dependencies (Roslyn, Nice3point, ...) without putting them in
    /// Revit's default load context, where they could clash with other add-ins such as pyRevit (issue #31).
    /// Anything the plugin ships itself (RevitMCPSDK, Newtonsoft.Json) comes from the plugin's own context,
    /// so the command set's IRevitCommand is the same type the plugin looks for (issues #47, #48).
    /// Everything else (the Revit API, the framework) falls through to Revit's default context.
    /// </summary>
    internal class CommandSetLoadContext : AssemblyLoadContext
    {
        private readonly string _commandSetDirectory;
        private readonly string _pluginDirectory;
        private readonly AssemblyLoadContext _pluginContext;

        public CommandSetLoadContext(string commandSetAssemblyPath)
            : base($"RevitMCP.CommandSet:{Path.GetFileNameWithoutExtension(commandSetAssemblyPath)}")
        {
            _commandSetDirectory = Path.GetDirectoryName(commandSetAssemblyPath);

            Assembly pluginAssembly = typeof(CommandSetLoadContext).Assembly;
            _pluginDirectory = Path.GetDirectoryName(pluginAssembly.Location);
            _pluginContext = GetLoadContext(pluginAssembly) ?? Default;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string fileName = assemblyName.Name + ".dll";

            if (File.Exists(Path.Combine(_pluginDirectory, fileName)))
                return _pluginContext.LoadFromAssemblyName(assemblyName);

            string privatePath = Path.Combine(_commandSetDirectory, fileName);
            if (File.Exists(privatePath))
                return LoadFromAssemblyPath(privatePath);

            return null;
        }
    }
}
#endif
