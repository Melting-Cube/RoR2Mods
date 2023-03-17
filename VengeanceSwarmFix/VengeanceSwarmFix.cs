using System.Security;
using System.Security.Permissions;
using BepInEx;
using RoR2;


[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]

namespace VengeanceSwarmFix
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class VengeanceSwarmFix : BaseUnityPlugin
    {
        //logger
        public static BepInEx.Logging.ManualLogSource ConsoleLogger = null!;
        //metadata
        private const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
        private const string PluginAuthor = "Melting-Cube";
        private const string PluginName = "VengeanceSwarmFix";
        private const string PluginVersion = "1.0.0";

        public void Awake()
        {
            ConsoleLogger=base.Logger;
            ConsoleLogger.LogInfo("Loaded VengeanceSwarmFix");
            //new DopplegangerSpawn();
            new SwarmSpawn();
        }
    }
}