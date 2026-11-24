using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using SyncLib.Items;
using Il2Cpp;
using Il2CppAlta.Networking;

[assembly: MelonInfo(typeof(SyncLib.SyncLib), "SyncLib", SyncLib.Core.Version, "MrDuckTheFifth, ported by Shadow")]
[assembly: MelonGame("Alta", "Att_Quest")]

namespace SyncLib
{
    internal static class Core
    {
        internal const string Version = "0.1.0";
        internal static MelonLogger.Instance Logger;

        internal static void Log(string m) { if (Logger != null) Logger.Msg(m); }
        internal static void Warn(string m) { if (Logger != null) Logger.Warning(m); }
        internal static void Error(string m) { if (Logger != null) Logger.Error(m); }
    }

    public class SyncLib : MelonMod
    {
        public static SyncLib instance;
        public static MessageType JsonSync = (MessageType)18;
        static bool _running, _reset;
        string _last;

        public override void OnInitializeMelon()
        {
            instance = this;
            Core.Logger = LoggerInstance;
            try
            {
                Embedded.Hook();
                Wire.Prepare();
                MethodInfo target = AccessTools.Method(typeof(JoinServerPipeline), "ConnectionApprovedByServer");
                if (target == null) throw new MissingMethodException("JoinServerPipeline.ConnectionApprovedByServer");
                HarmonyMethod post = new HarmonyMethod(typeof(SyncLib), "Approved");
                post.priority = -1000;
                HarmonyInstance.Patch(target, null, post);
                _running = true;
                Core.Log(Core.Version + " started (" + (Plat.Quest ? "Quest" : "PC twin") + "), speaks the SyncLib 1.0.4 and 1.1.x registry on message 18");
            }
            catch (Exception e)
            {
                Core.Error("failed to start: " + e);
            }
        }

        static void Approved(Connection connection)
        {
            try
            {
                if (Wire.Listen(connection)) _reset = true;
            }
            catch (Exception e) { Core.Error("new connection: " + e); }
        }

        public override void OnUpdate()
        {
            if (!_running) return;
            try
            {
                if (_reset)
                {
                    _reset = false;
                    NetworkPrefabRegistry.UnregisterAll();
                }
                Wire.Tick();
            }
            catch (Exception e)
            {
                string m = e.GetType().Name + ": " + e.Message;
                if (m == _last) return;
                _last = m;
                Core.Error("tick threw: " + e);
            }
        }
    }
}
