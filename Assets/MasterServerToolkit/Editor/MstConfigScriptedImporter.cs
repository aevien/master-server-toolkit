using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MasterServerToolkit.Editor
{
    /// <summary>
    /// Imports MST .cfg files placed under Assets as TextAsset instances.
    /// </summary>
    [ScriptedImporter(1, "cfg")]
    public class MstConfigScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            string text = File.ReadAllText(context.assetPath, Encoding.UTF8);
            var asset = new TextAsset(text)
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath)
            };

            context.AddObjectToAsset("config", asset);
            context.SetMainObject(asset);
        }
    }
}
