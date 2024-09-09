using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qunity
{
    public class QPathTools
    {
        public static string GetTextureAssetPath(string texName, string folder, string type)
        {
            var fullPath = folder;
            var segments = texName.Split("/");
            texName = segments[^1];
            for (int i = 0; i < segments.Length - 1; i++)
            {
                fullPath += "/" + segments[i];
            }

            var guids = AssetDatabase.FindAssets($"\"{texName}\" t:{type}", new[] { fullPath });
            if (guids.Length > 0)
            {
                if (guids.Length <= 1) return AssetDatabase.GUIDToAssetPath(guids[0]);
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid).Split("/");
                    var cmpName = p[^1];
                    cmpName = cmpName[..cmpName.LastIndexOf('.')];
                    if (cmpName == texName)
                    {
                        return AssetDatabase.GUIDToAssetPath(guids[0]);
                    }
                }
                return "";
            }
            return "";
        }

        public static void GetPipelineFolder(out string pipelinePath, out string baseColorShaderParam)
        {
            pipelinePath = "Base";
            baseColorShaderParam = "_MainTex";
            if (GraphicsSettings.renderPipelineAsset == null) return;
            switch (GraphicsSettings.renderPipelineAsset.GetType().Name)
            {
                case "UniversalRenderPipelineAsset":
                {
                    pipelinePath = "URP";
                    baseColorShaderParam = "_BaseMap";
                    break;
                }
                case "HDRenderPipelineAsset":
                {
                    pipelinePath = "HDRP";
                    baseColorShaderParam = "_BaseColorMap";
                    break;
                }
            }
        }
    }
}
