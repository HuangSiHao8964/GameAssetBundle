using UnityEngine;
#if UNITY_EDITOR || (!USE_WECHAT && !USE_TTSDK)
using System.IO;
using System.IO.Compression;
#endif
using System;
using System.Text;

namespace GameAssetBundle
{

    public enum PathType
    {
        /// <summary>
        /// 本地
        /// </summary>
        Local = 0,
        /// <summary>
        /// 缓存
        /// </summary>
        Cache = 1,
        /// <summary>
        /// 远程
        /// </summary>
        Remote = 2,
        /// <summary>
        /// 资源初始路径
        /// </summary>
        InitData = 3,
        /// <summary>
        /// 声音路径
        /// </summary>
        InitDataEditor = 4,
    }

    /// <summary>
    /// 文件记录信息
    /// </summary>
    public class FileRecord
    {
        /// <summary>
        /// 文件名称
        /// </summary>
        public string F
        {
            get;
            set;
        }

        /// <summary>
        /// 文件MD5码
        /// </summary>
        public string M
        {
            get;
            set;
        }

        ///// <summary>
        ///// HashCode
        ///// </summary>
        //public string HashCode
        //{
        //    get;
        //    set;
        //}

        /// <summary>
        /// 文件大小
        /// </summary>
        public long S
        {
            get;
            set;
        }
    }

    public class FileUtility
    {
        /// <summary>
        /// 获取资源文件路径
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pathType"></param>
        /// <returns></returns>
        public static string GetAssetFilePath(string fileName, PathType pathType)
        {
            string rootPath = string.Empty;
            switch (pathType)
            {
                case PathType.Cache:
                    rootPath = PathUtility.LOCAL_TEMP_PATH;
                    break;
                case PathType.Remote:
                    rootPath = PathUtility.REMOTE_DATA_PATH;
                    break;
                case PathType.InitData:
                    rootPath = PathUtility.LOCAL_INIT_DATA_PATH;
                    break;
                case PathType.InitDataEditor:
                    rootPath = PathUtility.LOCAL_INIT_DATA_EDITOR_PATH;
                    break;
                case PathType.Local:
                default:
                    rootPath = PathUtility.LOCAL_DATA_PATH;
                    break;
            }
            return StringExtensions.Format("{0}/{1}", rootPath.TrimEnd('/'), fileName.TrimStart('/'));
        }


#if UNITY_EDITOR || (!USE_WECHAT && !USE_TTSDK)
        public static void CopyFolder(string from, string to, string ext = "")
        {
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);
            // 子文件夹

            foreach (string sub in Directory.GetDirectories(from))
            {
                var p = to + "/" + Path.GetFileName(sub) + "/";
                CopyFolder(sub + "/", p.Replace("//", "/"), ext);
            }

            // 文件

            foreach (string file in Directory.GetFiles(from))
            {

                string e = Path.GetExtension(file).ToLower();
                if (string.IsNullOrEmpty(ext))
                {
                    if (e != ".meta")
                    {
                        File.Copy(file, to + "/" + Path.GetFileName(file), true);
                    }
                }
                else
                {
                    if (e == ext.ToLower())
                        File.Copy(file, to + "/" + Path.GetFileName(file), true);
                }
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="fileName">文件的相对路径</param>
        /// <param name="pathType">文件类型(仅支持 Local 和 Cache)</param>
        /// <returns></returns>
        public static string ReadFileText(string fileName, PathType pathType)
        {
            string fileFullPath = GetAssetFilePath(fileName, pathType);
            //Debug.LogError(fileFullPath);
            return ReadFileTextByFullName(fileFullPath);
        }

        public static string ReadFileTextByFullName(string fileFillName)
        {
            if (File.Exists(fileFillName))
            {
                return File.ReadAllText(fileFillName);
            }
            else
            {
                return null;
            }
        }

        public static byte[] ReadFileBytes(string fileFullName)
        {
            if (File.Exists(fileFullName))
            {
                // RuntimeDebug.LogFormat("ReadFileBytes:{0}", fileFullName);
                return File.ReadAllBytes(fileFullName);
            }
            else
            {
                return null;
            }
        }

        public static byte[] ReadFileBytes(string fileName, PathType pathType)
        {
            string fileFullPath = GetAssetFilePath(fileName, pathType);
            return ReadFileBytes(fileFullPath);
        }

        public static void CreateDirectory(string path)
        {
            string fileFullPath = Path.GetFullPath(path);
            string directoryName = Path.GetDirectoryName(fileFullPath);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);
        }

        /// <summary>
        ///   拷贝文件
        /// </summary>
        public static bool CopyFile(string src, string dest, bool overwrite = false)
        {
            //不存在则返回
            if (!File.Exists(src))
                return false;

            //保证路径存在
            string directory = Path.GetDirectoryName(dest);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.Copy(src, dest, overwrite);
            return true;
        }

        public static bool CopyFileByPathType(string fileName, PathType from, PathType to)
        {
            return CopyFile(GetAssetFilePath(fileName, from), GetAssetFilePath(fileName, to), true);
        }


        /// <summary>
        /// 写入文件
        /// </summary>
        /// <param name="path">文件全局路径</param>
        /// <param name="text">写入的内容.</param>
        public static void WriteTextToFile(string path, string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            WriteBytesToFile(path, bytes, bytes.Length);
        }

        /// <summary>
        /// 写入文件
        /// </summary>
        /// <param name="path">文件全局路径</param>
        /// <param name="bytes">写入的内容.</param>
        /// <param name="length">写入长度.</param>
        public static void WriteBytesToFile(string path, byte[] bytes, int length)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            FileInfo t = new FileInfo(path);
            using (Stream sw = t.Open(FileMode.Create, FileAccess.ReadWrite))
            {
                if (bytes != null && length > 0)
                {
                    //以行的形式写入信息
                    sw.Write(bytes, 0, length);
                }
            }
        }

        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool ExistsFile(string fileName)
        {
            return File.Exists(fileName);
        }

        public static bool ExistsFileOfType(string fileName, PathType type)
        {
            return ExistsFile(GetAssetFilePath(fileName, type));
        }

        /// <summary>
        /// 判断文件夹是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool ExistsDirectory(string path)
        {
            return Directory.Exists(path);
        }


#endif
        /// <summary>
        /// 获取字符串的MD5码
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string CreateMD5Hash(string input)
        {
            // Use input string to calculate MD5 hash
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
                // To force the hex string to lower-case letters instead of
                // upper-case, use he following line instead:
                // sb.Append(hashBytes[i].ToString("x2")); 
            }
            return sb.ToString();
        }


#if UNITY_EDITOR
        public static byte[] ZipBytes(byte[] bytes)
        {
            MemoryStream ms = new MemoryStream();
            using (GZipStream gzipStream = new GZipStream(ms, CompressionMode.Compress))
            {
                gzipStream.Write(bytes, 0, bytes.Length);
            }

            return ms.ToArray();
        }


        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] array = new byte[4096];
            int count;
            while ((count = src.Read(array, 0, array.Length)) != 0)
            {
                dest.Write(array, 0, count);
            }
        }

        public static byte[] UnZipBytes(byte[] bytes)
        {
            byte[] array = null;
            using (MemoryStream memoryStream = new MemoryStream(bytes))
            {
                using (MemoryStream memoryStream2 = new MemoryStream())
                {
                    using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        CopyTo(gzipStream, memoryStream2);
                    }
                    array = memoryStream2.ToArray();
                }
            }
            return array;
        }

        public static string GetFileMD5Code(string fileName)
        {
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }

        /// <summary>
        /// 删除文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <param name="recursive"></param>
        public static void DeleteDirectory(string path, bool recursive = true)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        #region 文件IO操作
        public static void DeleteFile(string bundleName)
        {
            if (File.Exists(bundleName))
                File.Delete(bundleName);
        }

        public static void DeleteEmptyDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
        }

        public static void DeleteDirectory(string target_dir)
        {
            if (Directory.Exists(target_dir))
                return;

            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }
        #endregion
#endif
    }
}