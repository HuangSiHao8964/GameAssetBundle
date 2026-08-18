#if UNITY_EDITOR

using GameAssetBundle;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameAssetBundle.Edit
{
public class AssetBundleCollectData
{
    public const string ConfigAssetPath = "Packages/com.haofang.game-asset-bundle/Editor/Settings/AssetBundleCollectConfig.asset";

    private static AssetBundleCollectConfig config = null;

    public static AssetBundleCollectConfig Config
    {
        get
        {
            if (config == null)
            {
                config = LoadCollectConfig();
            }
            return config;
        }
    }

    private static AssetBundleCollectConfig LoadCollectConfig()
    {
        AssetBundleCollectConfig instance = AssetDatabase.LoadAssetAtPath<AssetBundleCollectConfig>(ConfigAssetPath);
        if (instance == null)
        {
            Debug.LogError($"无法加载AssetBundleCollectConfig: {ConfigAssetPath}. 请确认配置资产存在且已完成导入.");
        }

        return instance;
    }

    public static void ReloadConfig()
    {
        config = null;
    }

    public static bool CheckFolderPath(string path)
    {
        if (Config == null)
            return false;

        List<AssetBundleCollect> collects = Config.collect;
        foreach (var v in collects)
        {
            if (v.folder.Equals(path))
                return true;
        }
        return false;
    }
    public static void CollectAbPath(ref List<string> abPath)
    {
        if (Config == null)
            return;

        foreach (var v in Config.collect)
        {
            CollectConfigPath(v, ref abPath);
        }
    }

    public static void PackCollect(bool pack = false)
    {
        TryPackCollect(Config, pack);
    }

    public static void PackCollect(AssetBundleCollectConfig collectConfig, bool pack = false)
    {
        TryPackCollect(collectConfig, pack);
    }

    private static bool TryPackCollect(bool pack = false)
    {
        return TryPackCollect(Config, pack);
    }

    private static bool TryPackCollect(AssetBundleCollectConfig currentConfig, bool pack = false)
    {
        if (currentConfig == null)
            return false;

        isPacking = pack;
        AssetRecordInfoDict.Clear();
        foreach (var v in currentConfig.collect)
        {
            Collect(v);
        }
        return true;
    }

    //#endif


    static bool isPacking = false;
    public static Dictionary<string, AssetRecord> AssetRecordInfoDict = new Dictionary<string, AssetRecord>();
    static private void CollectConfigPath(AssetBundleCollect pConfig, ref List<string> abPath)
    {
        if (string.IsNullOrEmpty(pConfig.folder))
            return;
        string targetFolder = pConfig.folder;
        if (Directory.Exists(targetFolder) == false)
            return;
        if (pConfig.type == PackageType.PackCurDirectory)
        {
            string[] files = Directory.GetFiles(targetFolder);
            if (files.Length > 0)
                abPath.Add(targetFolder);
        }
        if (pConfig.type == PackageType.PackTopDirectory)
        {
            string[] directory = Directory.GetDirectories(targetFolder, "*", SearchOption.TopDirectoryOnly);
            foreach (var v in directory)
            {

                string[] files = Directory.GetFiles(v);
                if (files.Length > 0)
                    abPath.Add(v);
            }
            //}
        }
        if (pConfig.type == PackageType.PackAllFileWithExt)
        {
            string[] files = Directory.GetFiles(targetFolder, GetExt(pConfig.ext), SearchOption.AllDirectories);
            foreach (var v in files)
            {
                if (Path.GetExtension(v).Equals(".meta") == false)
                    abPath.Add(v);
            }
        }
    }

    static string[] extStr = { "*.png", "*.prefab", "*.asset", "*.shadervariants", "*.*", "*.bytes", "*.*", "sound", "*.spriteatlas" };
    static string GetExt(PackageExt e)
    {
        //if (e == PackageExt.None)
        //    return string.Empty;
        return extStr[(int)e];
    }

    static private void Collect(AssetBundleCollect pConfig)
    {
        if (string.IsNullOrEmpty(pConfig.folder))
            return;
        if (pConfig.ext == PackageExt.None)
            return;

        string targetFolder = pConfig.folder;
        if (Directory.Exists(targetFolder) == false)
            return;
        if (pConfig.type == PackageType.PackCurDirectory)
        {
            if (pConfig.ext != PackageExt.Lua)
                WithSingleDirectory(targetFolder, GetExt(pConfig.ext));
            else
                WithSingleDirectoryLua(targetFolder);
        }
        if (pConfig.type == PackageType.PackTopDirectory)
        {
            WithTopDirectory(targetFolder, GetExt(pConfig.ext));
        }
        if (pConfig.type == PackageType.PackAllFileWithExt)
        {
            WithSingleFile(targetFolder, GetExt(pConfig.ext));
        }
    }
    /// <summary>
    /// 从顶层目录
    /// </summary>
    static public void WithTopDirectory(string directory, string ext)
    {
        string[] dirs = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        foreach (var dir in dirs)
        {
            WithSingleDirectory(dir, ext);
        }
    }
    /// <summary>
    /// 从一个文件目录
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="ext">文件后缀参数</param>
    /// <param name="targetAssetBundleName"></param>
    static public void WithSingleDirectory(string directory, string ext, string targetAssetBundleName = "")
    {
        if (ext.Equals("sound"))
        {
            WithSingleDirectory(directory, "*.mp3");
            WithSingleDirectory(directory, "*.wav");
            WithSingleDirectory(directory, "*.ogg");
            return;
        }

        string[] files = Directory.GetFiles(directory, ext, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.Contains(".meta"))
                continue;
            if (string.IsNullOrEmpty(targetAssetBundleName))
                AddAssetRecordInfo(file, PathToAssetBundleName(directory));
            else
                AddAssetRecordInfo(file, targetAssetBundleName);
        }
    }
    /// <summary>
    /// 从Lua文件目录
    /// </summary>
    /// <param name="directory"></param>
    static public void WithSingleDirectoryLua(string directory)
    {
// #if USE_XLUA
//         string[] files;
//         string lusDir = directory;
//         if (isPacking == false)
//             files = Directory.GetFiles(lusDir, "*.lua", SearchOption.AllDirectories);
//         else
//         {
//             lusDir = lusDir.Replace("LuaScript", "Primitives");
//             files = Directory.GetFiles(lusDir, "*.bytes", SearchOption.AllDirectories);
//         }

//         foreach (var file in files)
//         {

//             if (file.Contains(".meta"))
//                 continue;
//             AddAssetRecordInfo(file, PathToAssetBundleName(lusDir));
//         }
// #endif
    }
    /// <summary>
    /// 从单个文件
    /// </summary>
    static public void WithSingleFile(string directory, string ext)
    {
        if (ext.Equals("sound"))
        {
            WithSingleFile(directory, "*.mp3");
            WithSingleFile(directory, "*.wav");
            WithSingleFile(directory, "*.ogg");
            return;
        }

        string[] files = Directory.GetFiles(directory, ext, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.Contains(".meta"))
                continue;
            AddAssetRecordInfo(file, PathToAssetBundleName(file, true));
        }
    }
    /// <summary>
    /// 添加一条资源记录
    /// </summary>
    /// <param name="assetPath">资源路径</param>
    /// <param name="assetBundleName">资源AB名</param>
    static void AddAssetRecordInfo(string assetPath, string assetBundleName)
    {
#if UNITY_EDITOR
        AssetRecord info = GetAssetRecordInfo(assetPath, assetBundleName);
        string assetKey = AssetRecord.ToAssetRecordKey(info.RP);
        if (isPacking == true)
            info.RP = "";
        if (AssetRecordInfoDict.ContainsKey(assetKey))
            Debug.LogError(string.Format("Some asset with the same assetrecordkey[{0}], please check and fix it! in assetBundleName:\n{1}\n{2}", assetKey, AssetRecordInfoDict[assetKey].ABN, info.RP));
        else
        {
            AssetRecordInfoDict.Add(assetKey, info);
        }
#endif
    }
    /// <summary>
    /// 创建一条资源记录
    /// </summary>
    static AssetRecord GetAssetRecordInfo(string assetPath, string assetBundleName)
    {
        string relativePath = assetPath.Replace('\\', '/').Replace(Application.dataPath, "Assets");
        AssetRecord assetRecordInfo = new AssetRecord(assetBundleName, relativePath);
        return assetRecordInfo;
    }
    /// <summary>
    /// 根据相应规则，从资源路径计算资源AB名
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    static string PathToAssetBundleName(string path, bool isFile = false)
    {
        string assetBundleName = path.Replace("\\", "/").Replace(Application.dataPath, "Assets").ToLower();
        if (isFile)
        {
            string fileName = Path.GetFileName(assetBundleName);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(assetBundleName);
            assetBundleName = assetBundleName.Replace(fileName, fileNameWithoutExt);
        }
        return assetBundleName + PathUtility.AssetBundleExt;
    }


    static public void GetAssetRecordInfoDict()
    {
        if (TryPackCollect() == false)
            return;

        AssetRecordInfoDictToAssetBundleRecord();
        //AtlasMgr.Init();
    }

    static void AssetRecordInfoDictToAssetBundleRecord()
    {
        List<string> realPath = new List<string>();
        Dictionary<string, int> realPathMap = new Dictionary<string, int>();
        foreach (var v in AssetRecordInfoDict)
        {
            var path = v.Value.RP;
            if (realPath.Contains(path) == false)
            {
                realPathMap.Add(path, realPath.Count);
                realPath.Add(path);
            }
        }
        AssetBundleRecord record = new AssetBundleRecord();
        record.assetMap.Clear();
        record.bundleName = realPath.ToArray();
        foreach (var v in AssetRecordInfoDict)
        {
            record.assetMap.Add(v.Key, realPathMap[v.Value.RP]);
        }
        AssetBundleManager.Instance.AssetBundleRecord = record;
    }

    public static AssetRecord GetAssetRecordInfoByAsset(string assetRecordKey)
    {
        if (AssetRecordInfoDict.ContainsKey(assetRecordKey))
        {
            return AssetRecordInfoDict[assetRecordKey];
        }
        else
        {
            Debug.LogError(string.Format("Can't find asset record key[{0}] in AssetRecords, please check it!", assetRecordKey));
            return null;
        }
    }
}
}

#endif
