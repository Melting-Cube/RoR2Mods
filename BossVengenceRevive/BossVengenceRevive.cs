using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Configuration;
using Rewired.Utils;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]


namespace BossVengenceRevive
{
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class BossVengenceRevive : BaseUnityPlugin
    {
        //metadata
        public const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Melting-Cube";
        public const string PluginName = "BossVengenceRevive";
        public const string PluginVersion = "2.0.0";
        
        //add config entries
        public static ConfigEntry<bool> Enabled { get; set; }
        public static ConfigEntry<bool> TeleporterEvent { get; set; }
        public static ConfigEntry<bool> TeleporterBoss { get; set; }
        public static ConfigEntry<bool> MiscBoss { get; set; }
        public static ConfigEntry<bool> Doppelganger { get; set; }

        //respawn method to call
    public void RespawnChar()
        {
            //see if they are playing with others
            Logger.LogInfo("boss event cleared");
            bool solo = RoR2.RoR2Application.isInSinglePlayer || !NetworkServer.active;
            if (!solo)
            {
                //loop through every player and res the ones that are dead
                foreach (RoR2.PlayerCharacterMasterController playerCharacterMasterController
                         in RoR2.PlayerCharacterMasterController.instances)
                {
                    bool playerConnected = playerCharacterMasterController.isConnected;
                    bool isDead = !playerCharacterMasterController.master.GetBody()
                                || playerCharacterMasterController.master.IsDeadAndOutOfLivesServer()
                                || !playerCharacterMasterController.master.GetBody().healthComponent.alive;
                    if (playerConnected && isDead)
                        playerCharacterMasterController.master.RespawnExtraLife();
                }
            }
            return;
        }

        public void Awake()
        {
            //make config entries
            Enabled = Config.Bind<bool>(
                "Mod Status",
                "Enabled",
                true,
                "Turn the mod on or off."
                );
            TeleporterEvent = Config.Bind<bool>(
                "Boss Types",
                "Teleporter Event",
                true,
                "Revive dead players after you complete a teleporter event."
            );
            TeleporterBoss = Config.Bind<bool>(
                "Boss Types",
                "Teleporter Boss",
                false,
                "Revive dead players after you defeat a teleporter boss." +
                "\nDoes not work with tricorn."
                );
            Doppelganger = Config.Bind<bool>(
                "Boss Types",
                "Doppelganger",
                true,
                "Revive dead players after you defeat a Doppelganger." +
                "\nDoppelgangers are enabled by the Vengeance Artifact"
                );
            MiscBoss = Config.Bind<bool>(
                "Boss Types",
                "Special Bosses",
                false,
                "Revive dead players after you defeat a Special Boss." +
                "\nSpecial bosses are ones such as Aurelionite or Alloy Worship Unit."
                );
            
            //add config options in-game
            ModSettingsManager.AddOption(new CheckBoxOption(Enabled));
            ModSettingsManager.AddOption((new CheckBoxOption(TeleporterEvent)));
            ModSettingsManager.AddOption((new CheckBoxOption(TeleporterBoss)));
            ModSettingsManager.AddOption((new CheckBoxOption(MiscBoss)));
            ModSettingsManager.AddOption(new CheckBoxOption(Doppelganger));

            //On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };
            Logger.LogInfo("Loaded BossVengenceRevive");
            
            //reload the config on run start
            On.RoR2.BossGroup.Start += (orig, self) =>
            {
                orig(self);
                Debug.Log("settings updated");
                Config.Reload();
            };

            //is mod enabled
            if (!Enabled.Value)
                return;

            //every portal completion
            On.RoR2.TeleporterInteraction.ChargedState.OnEnter += (orig, self) =>
            {
                //call original method
                orig(self);

                if (!TeleporterEvent.Value) return;
                RespawnChar();
                Logger.LogInfo("teleporter finished charging, reviving dead players");
            };
            
            //everytime a character dies
            On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) => 
            {

                //exit if not a boss
                if (!damageReport.victimIsBoss)
                    return;

                //is character last member
                if (BossGroup.FindBossGroup(damageReport.victimBody).combatSquad.memberCount <= 1)
                {
                    //is defeated boss group a doppelganger
                    if (!damageReport.victim.body.doppelgangerEffectInstance.IsNullOrDestroyed() && Doppelganger.Value)
                    {
                        RespawnChar();
                        Logger.LogInfo("Doppelganger killed, revived dead players");
                        return;
                    }
                    //is defeated bossgroup a teleporter boss
                    else if (BossGroup.FindBossGroup(damageReport.victimBody).GetComponent<TeleporterInteraction>()
                        && TeleporterBoss.Value)
                    {
                        RespawnChar();
                        Logger.LogInfo("Teleporter Boss killed, revived dead players");
                        return;
                    }
                    //any other bose
                    else if(MiscBoss.Value)
                    {
                        RespawnChar();
                        Logger.LogInfo("Special Boss killed, revived dead players");
                        return;
                    }
                }

                    //call original method
                orig(self, damageReport);
            };
        }
    }
}