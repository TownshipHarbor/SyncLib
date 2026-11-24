using System;
using System.IO;
using System.Reflection;

namespace SyncLib
{
    internal static class Embedded
    {
        internal const string Tools = "SyncLib.AssetsTools.NET.dll";
        static Assembly _tools;
        static bool _hooked;

        internal static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            if (new AssemblyName(e.Name).Name != "AssetsTools.NET") return null;
            if (_tools != null) return _tools;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name == "AssetsTools.NET") { _tools = a; return a; }
            }
            using (Stream s = typeof(Embedded).Assembly.GetManifestResourceStream(Tools))
            {
                if (s == null) return null;
                byte[] b = new byte[s.Length];
                int read = 0;
                while (read < b.Length)
                {
                    int n = s.Read(b, read, b.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                _tools = Assembly.Load(b);
                return _tools;
            }
        }
    }
}
