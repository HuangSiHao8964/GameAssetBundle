using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

namespace GameAssetBundle
{
    public class AssetBundleRecord
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;
        public string[] bundleName = Array.Empty<string>();
        public string[] md5 = Array.Empty<string>();
        public Dictionary<string, int> assetMap = new Dictionary<string, int>();
        public bool IsEncrypted { get; set; } = true;
        private Dictionary<string, string> bundleMap = new Dictionary<string, string>();
        public void CreateBundleMap()
        {
            bundleMap.Clear();
            int count = bundleName.Length;
            for (int i = 0; i < count; i++)
            {
                bundleMap.Add(bundleName[i], md5[i]);
            }
        }

        public string GetAssetBundleRealName(string assetBundle)
        {
            string real = string.Empty;
            bundleMap.TryGetValue(assetBundle, out real);
            return real;
        }

        private static void WriteUtf8String(BinaryWriter writer, string value)
        {
            byte[] bytes = Utf8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadUtf8String(BinaryReader reader)
        {
            int byteLength = reader.ReadInt32();
            if (byteLength <= 0)
                return string.Empty;
            return Utf8.GetString(reader.ReadBytes(byteLength));
        }

#if UNITY_EDITOR
        public void SaveRecord(string dest)
        {
            // string json = JsonMapper.ToJson(this);
            // System.IO.File.WriteAllText(dest, json);
            // return;
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream, Utf8, true);
            ushort count = Convert.ToUInt16(bundleName.Length);
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                WriteUtf8String(writer, bundleName[i]);
            }
            for (int i = 0; i < count; i++)
            {
                WriteUtf8String(writer, md5[i]);
            }

            count = Convert.ToUInt16(assetMap.Count);
            writer.Write(count);

            foreach (var v in assetMap)
            {
                WriteUtf8String(writer, v.Key);
                writer.Write(v.Value);
            }
            writer.Write(IsEncrypted);
            writer.Flush();
            File.WriteAllBytes(dest, stream.ToArray());
        }
#endif

        public void LoadRecord(byte[] data)
        {
            using MemoryStream stream = new MemoryStream(data);
            using BinaryReader reader = new BinaryReader(stream, Utf8, true);
            ushort count = reader.ReadUInt16();
            bundleName = new string[count];
            md5 = new string[count];
            for (ushort i = 0; i < count; i++)
            {
                bundleName[i] = ReadUtf8String(reader);
            }
            for (ushort i = 0; i < count; i++)
            {
                md5[i] = ReadUtf8String(reader);
            }
            count = reader.ReadUInt16();
            assetMap.Clear();
            for (ushort i = 0; i < count; i++)
            {
                var a = ReadUtf8String(reader);
                Debug.LogError(a);
                assetMap.Add(a, reader.ReadInt32());
            }
            // The encryption flag was appended to preserve compatibility with old records.
            IsEncrypted = stream.Position >= stream.Length || reader.ReadBoolean();
            CreateBundleMap();
        }
    }

}
