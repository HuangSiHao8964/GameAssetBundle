#if UNITY_EDITOR
namespace GameAssetBundle.Edit
{
public class AssetRecord
{

    public AssetRecord(string assetBundleName, string relativePath)
    {
        this.ABN = assetBundleName;
        #if UNITY_EDITOR
        this.RP = relativePath;
        #endif
    }

    public AssetRecord(string assetBundleName)
    {
        //this.AssetName = assetKey;
        this.ABN = assetBundleName;
        #if UNITY_EDITOR
        this.RP = string.Empty;
        #endif
    }

    ///// <summary>
    ///// 资源名称
    ///// </summary>
    //public string AssetName { get; set; }

    /// <summary>
    /// 包名称
    /// </summary>
    public string ABN { get; set; }

    #if UNITY_EDITOR
    /// <summary>
    /// 相对路径
    /// </summary>
    public string RP { get; set; }
    #endif

    /// <summary>
    /// 根据相应规则，从文件名计算出资源记录键值
    /// </summary>
    public static string ToAssetRecordKey(string fileName)
    {
        fileName = fileName.Replace('\\', '/');
        int lastIndex = fileName.LastIndexOf('/');
        string recordKey;
        if (lastIndex > -1)
        {
            recordKey = fileName.Substring(lastIndex + 1);
        }
        else
        {
            recordKey = fileName;
        }

        return recordKey;
    }
}
}
#endif
