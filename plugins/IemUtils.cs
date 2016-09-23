﻿//Requires: ZoneManager
using System;
using System.Reflection;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = System.Random;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("Incursion Utilities", "tolland", "0.1.0")]
    public class IemUtils : RustPlugin
    {

        [PluginReference]
        Plugin ZoneManager;
		static Oxide.Game.Rust.Libraries.Rust rust = GetLibrary<Oxide.Game.Rust.Libraries.Rust>();
		static FieldInfo monumentsField = typeof(TerrainPath).GetField("Monuments", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
		public static List<MonumentInfo> monuments = new List<MonumentInfo>();

        static IemUtils iemUtils = null;

        void Init()
        {
            iemUtils = this;
            LogL("");
            LogL("");
            LogL("");
            LogL("Init in iemutils");
        }


        void Loaded()
        {
            LogL("iemutils: server loaded");
        }

        void OnServerInitialized()
        {
            LogL("iemutils: server initialized");
        }

        #region player modifications

        public static void SetMetabolismValues(BasePlayer player)
        {
            player.metabolism.calories.max = 500;
            player.metabolism.calories.value = 500;
            player.health = 100;
            //player.metabolism.health.max = 100;
            player.metabolism.hydration.max = 250;
            player.metabolism.hydration.value = 250;

            
        }


        public static void SetMetabolismNoNutrition(BasePlayer player)
        {
            player.metabolism.calories.max = 500;
            player.metabolism.calories.value = 500;
            player.metabolism.hydration.max = 250;
            player.metabolism.hydration.value = 250;
        }

        

        #endregion


        void DeleteEverything()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }


        #region Functions
        //from em
        public bool hasAccess(ConsoleSystem.Arg arg)
        {
            if (arg.connection?.authLevel < 1)
            {
                SendReply(arg, GetMessage("MessagesPermissionsNotAllowed"));
                return false;
            }
            return true;
        }

        //private bool hasPermission(BasePlayer player, string permname)
        //{
        //    return isAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
        //}

        public static bool isAdmin(BasePlayer player)
        {
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        private string GetMessage(string key) => lang.GetMessage(key, this);

        public static void DLog(string message)
        {
            ConVar.Server.Log("oxide/logs/ESMlog.txt", message);
            iemUtils.Puts(message);
        }

        public static void SLog(string message)
        {
            ConVar.Server.Log("oxide/logs/Statelog.txt", message);
            //iemUtils.Puts(message);
        }

        public static void DDLog(string message)
        {
            ConVar.Server.Log("oxide/logs/DDlog.txt", message);
            //iemUtils.Puts(message);
        }

        public static void LogL(string message)
        {
            ConVar.Server.Log("oxide/logs/Loadlog.txt", message);
            ConVar.Server.Log("oxide/logs/ESMlog.txt", message);
            iemUtils.Puts(message);
        }

        private static string prefix;
        public static void SendMessage(BasePlayer player, string message, params object[] args)
        {
            prefix = Convert.ToString("<color=#FA58AC>Debug:</color> ");
            if (player != null)
            {
                if (args.Length > 0)
                    message = string.Format(message, args);
                iemUtils.SendReply(player, $"{prefix}{message}");
            }
            else
                iemUtils.Puts(message);
        }


        public static void BroadcastChat(string message)
        {
            iemUtils.rust.BroadcastChat(message);
        }

        #endregion

        #region zone utils

        public static void CreateZone(string name, Vector3 location, int radius)
        {
            //iemUtils.Puts("creating zone");

            //ZoneManager.Call("EraseZone", "zone_" + name);

            iemUtils.ZoneManager.Call("CreateOrUpdateZone",
                "zone_" + name,
                new string[]
                {
                    "radius", radius.ToString(),
                    "autolights", "true",
                    "eject", "false",
                    "enter_message", "",
                    "leave_message", "",
                    "killsleepers", "true"
                }, location);

            CreateSphere(location, (radius * 2) + 1);

        }

        private const string SphereEnt = "assets/prefabs/visualization/sphere.prefab";

        public static void CreateSphere(Vector3 position, float radius)
        {
            // Puts("CreateSphere works!");
            BaseEntity sphere = GameManager.server.CreateEntity(SphereEnt,
                position, new Quaternion(), true);
            SphereEntity ent = sphere.GetComponent<SphereEntity>();
            //iemUtils.Puts("prefabID " + sphere.prefabID);

            ent.currentRadius = radius;
            ent.lerpSpeed = 0f;
            sphere?.Spawn();


        }

        #endregion


        #region finding stuff

        static int doorColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction Trigger", "Construction" });


        T FindComponentNearestToLocation<T>(Vector3 location, int radius)
        {
            T component = default(T);
            foreach (Collider col in Physics.OverlapSphere(location, 2f, doorColl))
            {
                if (col.GetComponentInParent<Door>() == null) continue;


                if (Mathf.Ceil(col.transform.position.x) == Mathf.Ceil(location.x)
                    && Mathf.Ceil(col.transform.position.y) == Mathf.Ceil(location.y)
                    && Mathf.Ceil(col.transform.position.z) == Mathf.Ceil(location.z))
                {
                    //Plugins.IemUtils.DLog("found the door");
                    component = col.GetComponentInParent<T>();
                }
            }
            if (component != null)
                return component;
            return default(T);
        }

        public static BasePlayer FindPlayerByID(ulong steamid)
        {
            BasePlayer targetplayer = BasePlayer.FindByID(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            targetplayer = BasePlayer.FindSleeping(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            return null;
        }



        #endregion


        #region geo stuff

        public static Vector3 Vector3Down = new Vector3(0f, -1f, 0f);
        public static int groundLayer = LayerMask.GetMask("Construction", "Terrain", "World");

        public static float? GetGroundY(Vector3 position)
        {

            position = position + Vector3.up;
            RaycastHit hitinfo;
            if (Physics.Raycast(position, Vector3Down, out hitinfo, 100f, groundLayer))
            {
                return hitinfo.point.y;
            }
            return null;
        }


        static void TeleportPlayerPosition(BasePlayer player, Vector3 destination)
        {
            //DLog("teleporting player from " + player.transform.position.ToString());
            //DLog("teleporting player to   " + destination.ToString());
            player.MovePosition(destination);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer(null, player, "StartLoading", null, null, null, null, null);
            player.SendFullSnapshot();
        }

        public static void MovePlayerTo(BasePlayer player, Vector3 loc)
        {
			if (player.inventory.loot.IsLooting())
            {
                player.EndLooting();
            }
			player.CancelInvoke("InventoryUpdate");
            player.inventory.crafting.CancelAll(true);
	        rust.ForcePlayerPosition(player, loc.x, loc.y, loc.z);
            player.SendNetworkUpdateImmediate();
        }

        static Random random = new System.Random();

        public static Vector3 GetRandomPointOnCircle(Vector3 centre, float radius)
        {
            
            //get a random angli in radians
            float randomAngle = (float)random.NextDouble() * (float)Math.PI * 2.0f;

            DLog("random angle is " + randomAngle.ToString());

            Vector3 loc = centre;

            //iemUtils.Puts("x modifyier is " + ((float)Math.Cos(randomAngle) * radius));
            //iemUtils.Puts("z modifyier is " + ((float)Math.Sin(randomAngle) * radius));

            loc.x = loc.x + ((float)Math.Cos(randomAngle) * radius);
            loc.z = loc.z + ((float)Math.Sin(randomAngle) * radius);

            return loc;
        }

        #endregion


        #region environment modifications

        public static void RunServerCommand(string key, string val)
        {

            iemUtils
            .rust.RunServerCommand("env.time", "12");
        }

        #endregion

        public class ScheduledEvent
        {
            public DateTime Start { get; set; }
            DateTime End;   // ??
            public int Length { get; set; }
            public List<ScheduledEventTeam> seTeam = new List<ScheduledEventTeam>();
            public List<ScheduledEventPlayer> sePlayer = new List<ScheduledEventPlayer>();

            public ScheduledEvent(DateTime newStart, int newLength)
            {
                Start = newStart;
                Length = newLength;
            }

            public class ScheduledEventTeam
            {
                public string TeamName { get; set; }
                public string Color { get; set; }
                public List<ScheduledEventPlayer> sePlayer = new List<ScheduledEventPlayer>();
                public string JoinCommand { get; set; }
                public bool TeamOpen { get; set; }

                public ScheduledEventTeam(string newTeamName, 
                    string newColor, 
                    string newJoinCommand,
                    bool newTeamOpen)
                {
                    TeamName = newTeamName;
                    Color = newColor;
                    JoinCommand = newJoinCommand;
                    TeamOpen = newTeamOpen;
                }
            }

            public class ScheduledEventPlayer
            {
                public string steamId { get; set; }

                public ScheduledEventPlayer(string newSteamid)
                {
                    steamId = newSteamid;
                }
            }
        }
    }
}