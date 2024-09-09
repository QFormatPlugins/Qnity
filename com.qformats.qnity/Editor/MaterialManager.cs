using System.Collections.Generic;
using System.Linq;
using Qnity;
using Qunity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MaterialManager
{
    private const string Pkgpath = "Packages/com.qformats.qnity/Assets/Materials/";

    private static MaterialManager _instance;
    private Material _baseMaterial;
    private string _baseTextureParameter;
    private readonly Dictionary<string, Material> _materialDict = new Dictionary<string, Material>();
    private readonly List<WadTexture2DCollection> _wadCollections = new List<WadTexture2DCollection>();

    public static MaterialManager Instance
    {
        get { return _instance ??= new MaterialManager(); }
    }

    public Material GetBaseMaterial()
    {
        return _baseMaterial;
    }

    public void AddWad(WadTexture2DCollection wad)
    {
        foreach (var savedWad in _wadCollections)
        {
            if (wad == savedWad)
            {
                return;
            }
        }

        _wadCollections.Add(wad);
    }

    private MaterialManager()
    {
        SetupBaseMaterials();
        var guids = AssetDatabase.FindAssets("t:WadTexture2DCollection");
        foreach (var guid in guids)
        {
            var wad = (WadTexture2DCollection)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid),
                typeof(WadTexture2DCollection));
            _wadCollections.Add(wad);
        }
    }

    public Texture2D GetTexture(string textureName, string textureFolder, string materialFolder)
    {
        Texture2D tex = null;
        var mat = GetMaterial(textureName, textureFolder, materialFolder);
        if (mat != null)
        {
            tex = (Texture2D)mat.GetTexture(_baseTextureParameter);
        }

        return tex;
    }

    public Material GetMaterial(string name, string textureFolder, string materialFolder)
    {
        if (_materialDict.TryGetValue(name, out var material))
        {
            if (material == null)
            {
                _materialDict.Remove(name);
            }
            else
            {
                return material;
            }
        }

        Material mat = _baseMaterial;
        if (materialFolder != "")
        {
            var materialPath = QPathTools.GetTextureAssetPath(name, materialFolder, "Material");
            if (materialPath != "")
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
        }
        
        var newMat = new Material(mat)
        {
            name = "mat_" + name
        };

        Texture2D tex = null;
        if (textureFolder != "")
        {
            var texturePath = QPathTools.GetTextureAssetPath(name, textureFolder, "Texture2D");
            if (texturePath != "")
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
        }

        if (tex == null)
        {
            _wadCollections.Any(wads => (tex = wads.FindTexture(name)) != null);
        }

        if (tex != null &&  newMat.GetTexture(_baseTextureParameter) == null)
        {
            newMat.SetTexture(_baseTextureParameter, tex);
        }

        _materialDict.Add(name, newMat);
        return newMat;
    }

    private void SetupBaseMaterials()
    {
        var baseMat = _baseMaterial;
        QPathTools.GetPipelineFolder(out var pipeline, out _baseTextureParameter);
        if (baseMat == null)
        {
            _baseMaterial = (Material)AssetDatabase.LoadAssetAtPath(Pkgpath + pipeline + "/Base.mat", typeof(Material));
        }
    }
}