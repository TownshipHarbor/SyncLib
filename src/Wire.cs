using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SyncLib.Items;
using Il2CppAlta.Networking;
using GameStream = Il2CppAlta.Serialization.Stream;
using StringEncoding = Il2CppAlta.Serialization.Stream.StringEncoding;

namespace SyncLib
{
    internal static class Wire
    {
        internal const int Message = 18;
        internal const float Settle = 3f;
        internal const string SharedKey = "SyncLib.Quest.Json";

        static SerializeConnectionMethod _handler, _writer;
        static readonly object _lock = new object();
        static readonly Queue<string> _inbox = new Queue<string>();
        static Connection _conn;
        static long _connKey;
        static Chunks _chunks;
        static bool _done, _chunked, _settling, _reported;
        static float _registeredAt;
        static readonly Queue<string> _outbox = new Queue<string>();
        static string _outgoing;
        static int _tries;
        static float _since;
        static bool _warned;

        internal static int Received { get { return _chunks == null ? 0 : _chunks.Received; } }

        internal static void Prepare()
        {
            _handler = Plat.ConnectionMethod(Receive);
            _writer = Plat.ConnectionMethod(Write);
        }

        static long Key(Connection c)
        {
            return c == null ? 0 : (long)c.Pointer;
        }

        internal static bool Listen(Connection c)
        {
            if (c == null) return false;
            c.SetHandler((MessageType)Message, _handler);
            long k = Key(c);
            if (k == _connKey) return false;
            lock (_lock)
            {
                _inbox.Clear();
                _conn = c;
                _connKey = k;
            }
            _chunks = new Chunks();
            _done = false;
            _chunked = false;
            _settling = false;
            _reported = false;
            _outbox.Clear();
            _since = Time.unscaledTime;
            _warned = false;
            Core.Log("new server connection: waiting for the server's SyncLib registry");
            return true;
        }

        static void Receive(Connection c, GameStream s)
        {
            string text = null;
            s.SerializeString(ref text, (StringEncoding)0);
            lock (_lock)
            {
                if (Key(c) != _connKey) return;
                _inbox.Enqueue(text);
            }
        }

        static void Write(Connection c, GameStream s)
        {
            string t = _outgoing;
            s.SerializeString(ref t, (StringEncoding)0);
        }

        internal static void Tick()
        {
            while (true)
            {
                string text;
                lock (_lock)
                {
                    if (_inbox.Count == 0) break;
                    text = _inbox.Dequeue();
                }
                Take(text);
            }
            if (!_done && !_warned && _conn != null && Time.unscaledTime - _since > 30f)
            {
                _warned = true;
                Core.Log(Received > 0 ? "the server's registry is still arriving (" + Received + "/" + _chunks.Total + " parts)" : "no SyncLib registry from this server after 30 s: it does not run SyncLib");
            }
            if (_settling)
            {
                bool all = NetworkPrefabRegistry.Present().Count >= NetworkPrefabRegistry.Expected();
                if (all || Time.unscaledTime - _registeredAt > Settle)
                {
                    _settling = false;
                    List<int> have = NetworkPrefabRegistry.Present();
                    if (NetworkPrefabRegistry.MissingMods.Count > 0) Core.Warn("this headset lacks the Quest side of: " + string.Join(", ", NetworkPrefabRegistry.MissingMods.ToArray()) + "; their items stay hidden");
                    if (_chunked && !_reported) Report(have);
                }
            }
            Flush();
        }

        static void Take(string text)
        {
            if (_done || text == null) return;
            string json = null;
            try
            {
                if (text.StartsWith(Chunks.Registry, StringComparison.Ordinal))
                {
                    int before = _chunks.Received;
                    if (!_chunks.Accept(text, out json))
                    {
                        if (before == 0 && _chunks.Received == 1) Core.Log("registry transfer started: " + _chunks.Total + " part(s)");
                        return;
                    }
                    _chunked = true;
                }
                else if (text.StartsWith(Chunks.Report, StringComparison.Ordinal))
                {
                    return;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(text)) throw new FormatException("the server sent an empty registry");
                    json = text;
                    _chunked = false;
                }
                HashRegistryFile reg = HashRegistryFile.Parse(json);
                _done = true;
                Core.Log("received the SyncLib registry: " + reg.Mods.Count + " mod(s), " + (_chunked ? _chunks.Total + " part(s)" : "one message") + ", " + json.Length + " chars" + (reg.CraftingProtocolVersion == 1 ? ", crafting on" : ""));
                Share(json);
                NetworkPrefabRegistry.RegisterAll(reg, json, _chunked);
                _registeredAt = Time.unscaledTime;
                _settling = true;
            }
            catch (Exception e)
            {
                _done = true;
                Core.Error("the server's SyncLib registry could not be read: " + e.Message + "; custom items stay off for this server");
            }
        }

        static void Report(List<int> hashes)
        {
            if (_conn == null || _conn.IsDisposed) return;
            HashRegistryFile reg = NetworkPrefabRegistry.ServerRegistry;
            bool crafting = reg != null && reg.CraftingProtocolVersion == 1;
            hashes.Sort();
            string json = HashRegistryFile.Report(hashes, crafting, null);
            foreach (string e in Chunks.Encode(Chunks.Report, json, Chunks.NewTransfer())) _outbox.Enqueue(e);
            _reported = true;
            _tries = 0;
            Core.Log("telling the server this headset can show " + hashes.Count + " custom prefab(s)" + (crafting ? " (crafting report, no Quest recipes)" : ""));
        }

        static void Flush()
        {
            if (_outbox.Count == 0) return;
            if (_conn == null || _conn.IsDisposed)
            {
                _outbox.Clear();
                return;
            }
            ConnectionChannel ch = Plat.Reliable(_conn);
            if (ch == null)
            {
                Core.Error("no reliable channel to send the prefab report on");
                _outbox.Clear();
                return;
            }
            int sent = 0;
            while (_outbox.Count > 0 && sent < 4)
            {
                _outgoing = _outbox.Peek();
                bool ok;
                try
                {
                    ok = _conn.Send(ch, (MessageType)Message, _writer);
                }
                catch (Exception e)
                {
                    Core.Error("sending the prefab report failed: " + e.Message);
                    _outbox.Clear();
                    return;
                }
                if (!ok)
                {
                    if (++_tries >= 10)
                    {
                        Core.Error("the connection refused the prefab report 10 times; giving up");
                        _outbox.Clear();
                    }
                    return;
                }
                _tries = 0;
                _outbox.Dequeue();
                sent++;
            }
            if (_outbox.Count == 0)
            {
                _conn.FlushPacketManagers();
                Core.Log("prefab report sent");
            }
        }

        static void Share(string json)
        {
            try
            {
                AppDomain.CurrentDomain.SetData(SharedKey, json);
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    string type = n == "PortalGunQuest" || n == "PortalGunTwin" ? "PortalGunQuest.Sync" : n == "RepairShopQuest" || n == "RepairShopTwin" ? "RepairShopQuest.Sync" : null;
                    if (type == null) continue;
                    Type t = a.GetType(type);
                    if (t == null) continue;
                    BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic;
                    FieldInfo gate = t.GetField("_lock", f), text = t.GetField("_pending", f), has = t.GetField("_hasPending", f);
                    if (gate == null || text == null || has == null) continue;
                    lock (gate.GetValue(null))
                    {
                        text.SetValue(null, json);
                        has.SetValue(null, true);
                    }
                    Core.Log("passed the registry to " + n);
                }
            }
            catch (Exception e)
            {
                Core.Warn("passing the registry to other headset mods failed: " + e.Message);
            }
        }
    }
}
