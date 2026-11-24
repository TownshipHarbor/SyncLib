using System.IO;
using UnityEditor;

public class ExportQuestAssets {
    [MenuItem("ATT Workshop/Build All Assets For Quest")]
    public static void BuildAssets() {
        string dir = "Assets/Exported Assets/Quest";
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }

        BuildPipeline.BuildAssetBundles(dir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.Android);
    }
}
