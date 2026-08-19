
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssetBundle.Edit
{

public enum PackageType
{
    [InspectorName("不打包")]
    PackNone,
    [InspectorName("顶层目录")]
    PackTopDirectory,
    [InspectorName("当前目录")]
    PackCurDirectory,
    [InspectorName("按文件扩展名")]
    PackAllFileWithExt,
}

public enum PackageExt
{
    [InspectorName("PNG 图片")]
    Png,
    [InspectorName("Prefab 预制体")]
    Prefab,
    [InspectorName("Asset 资源")]
    Asset,
    [InspectorName("Shader 变体")]
    ShaderVariants,
    [InspectorName("全部文件")]
    All,
    [InspectorName("Lua 脚本")]
    Lua,
    [InspectorName("不设置")]
    None,
    [InspectorName("音频文件")]
    Sound,
    [InspectorName("SpriteAtlas 图集")]
    Atlas,
}

[Serializable]
public class AssetBundleCollect
{
    [ObjectReference] public string folder = string.Empty;
    //public bool locked = false;
    public PackageType type = PackageType.PackAllFileWithExt;
    public PackageExt ext = PackageExt.None;
}

[Serializable]
public class AssetBundleCollectConfig : ScriptableObject
{
    public List<AssetBundleCollect> collect = new List<AssetBundleCollect>();
}

}

#endif
