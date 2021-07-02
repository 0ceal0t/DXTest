using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Reflection;

namespace SamplePlugin {
    public partial class Plugin : IDalamudPlugin {
        public string Name => "Sample Plugin";

        private const string commandName = "/pmycommand";

        private DalamudPluginInterface PluginInterface;
        private Configuration Config;
        
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = Assembly.GetExecutingAssembly().Location;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            PluginInterface = pluginInterface;

            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            PluginInterface.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand) {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            PluginInterface.UiBuilder.OnBuildUi += DrawUI;

            InitDX();
        }

        public void Dispose() {
            PluginInterface.UiBuilder.OnBuildUi -= DrawUI;

            DisposeDX();

            PluginInterface.CommandManager.RemoveHandler(commandName);
            PluginInterface.Dispose();
        }

        private void OnCommand(string command, string args) {
        }

        private void DrawUI() {
            //Render();
        }
    }
}
