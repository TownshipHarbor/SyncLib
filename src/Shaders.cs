using System;
using System.IO;
using UnityEngine;

namespace SyncLib
{
    internal static class Shaders
    {
        internal const string Fallback = "Township Tale/Cheap/SimpleStandard";
        static Shader _fallback;
        static bool? _questRule;

        internal static bool QuestRule
        {
            get
            {
                if (_questRule == null)
                {
                    bool on = Plat.Quest;
                    if (!on)
                    {
                        try { on = File.Exists(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "SyncLibQuestShaders.txt")); }
                        catch (Exception) { on = false; }
                    }
                    _questRule = on;
                }
                return _questRule.Value;
            }
        }

        internal static bool BlackOnQuest(string name)
        {
            return name == "Standard" || name == "Standard (Specular setup)" || name.StartsWith("Legacy Shaders/", StringComparison.Ordinal);
        }

        static bool Broken(Shader s)
        {
            if (s == null || !s.isSupported) return true;
            string n = s.name;
            if (n == "Hidden/InternalErrorShader") return true;
            return QuestRule && BlackOnQuest(n);
        }

        internal static int Fix(GameObject root)
        {
            int n = 0;
            foreach (Renderer r in Plat.Children<Renderer>(root.transform, true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || !Broken(m.shader)) continue;
                    if (_fallback == null) _fallback = Shader.Find(Fallback);
                    if (_fallback == null) return n;
                    bool hasColor = m.HasProperty("_Color");
                    Color color = hasColor ? m.color : Color.white;
                    Texture tex = m.HasProperty("_MainTex") ? m.mainTexture : null;
                    m.shader = _fallback;
                    if (hasColor && m.HasProperty("_Color")) m.color = color;
                    if (tex != null && m.HasProperty("_MainTex")) m.mainTexture = tex;
                    m.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    n++;
                }
            }
            return n;
        }
    }
}
