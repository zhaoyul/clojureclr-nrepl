using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using clojure.lang;

namespace clojureCLR_nrepl
{
    public static class BencodeCodec
    {
        public static byte[] Encode(object obj)
        {
            using (var ms = new MemoryStream())
            {
                EncodeObject(ms, obj);
                return ms.ToArray();
            }
        }
    
            private static void EncodeObject(MemoryStream ms, object obj)
            {
                switch (obj)
                {
                    case string s:
                        EncodeString(ms, s);
                        break;
                    case int i:
                        EncodeInt(ms, i);
                        break;
                    case long l:
                        EncodeInt(ms, l);
                        break;
                    case Keyword k:
                        EncodeString(ms, k.Name);
                        break;
                    case IPersistentVector vec:
                        EncodeList(ms, vec);
                        break;
                    case IPersistentMap map:
                        EncodeDict(ms, map);
                        break;
                    case List<string> strList:
                        EncodeStringList(ms, strList);
                        break;
                    case List<object> list:
                        EncodeGenericList(ms, list);
                        break;
                    case Dictionary<string, object> dict:
                        EncodeGenericDict(ms, dict);
                        break;
                    case System.Collections.IList list:
                        // Handle any list type (List<Dictionary<string, object>>, etc.)
                        EncodeIList(ms, list);
                        break;
                    default:
                        EncodeString(ms, obj?.ToString() ?? "nil");
                        break;
                }
            }
    
            private static void EncodeIList(MemoryStream ms, System.Collections.IList list)
            {
                ms.WriteByte((byte)'l');
                foreach (var item in list)
                {
                    EncodeObject(ms, item);
                }
                ms.WriteByte((byte)'e');
            }
    
            private static void EncodeString(MemoryStream ms, string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                var lenBytes = Encoding.UTF8.GetBytes(bytes.Length.ToString());
                ms.Write(lenBytes, 0, lenBytes.Length);
                ms.WriteByte((byte)':');
                ms.Write(bytes, 0, bytes.Length);
            }
    
            private static void EncodeInt(MemoryStream ms, long n)
            {
                var bytes = Encoding.UTF8.GetBytes($"i{n}e");
                ms.Write(bytes, 0, bytes.Length);
            }
    
            private static void EncodeList(MemoryStream ms, IPersistentVector vec)
            {
                ms.WriteByte((byte)'l');
                for (int i = 0; i < vec.count(); i++)
                {
                    EncodeObject(ms, vec.nth(i));
                }
                ms.WriteByte((byte)'e');
            }
    
            private static void EncodeStringList(MemoryStream ms, List<string> list)
            {
                ms.WriteByte((byte)'l');
                foreach (var item in list)
                {
                    EncodeString(ms, item);
                }
                ms.WriteByte((byte)'e');
            }
    
            private static void EncodeGenericList(MemoryStream ms, List<object> list)
            {
                ms.WriteByte((byte)'l');
                foreach (var item in list)
                {
                    EncodeObject(ms, item);
                }
                ms.WriteByte((byte)'e');
            }
    
            private static void EncodeDict(MemoryStream ms, IPersistentMap map)
            {
                ms.WriteByte((byte)'d');
                foreach (var entry in map)
                {
                    var key = ((Keyword)entry.key()).Name;
                    EncodeString(ms, key);
                    EncodeObject(ms, entry.val());
                }
                ms.WriteByte((byte)'e');
            }
    
            private static void EncodeGenericDict(MemoryStream ms, Dictionary<string, object> dict)
            {
                ms.WriteByte((byte)'d');
                foreach (var entry in dict)
                {
                    EncodeString(ms, entry.Key);
                    EncodeObject(ms, entry.Value);
                }
                ms.WriteByte((byte)'e');
            }
    
            public static Dictionary<string, object> DecodeDict(byte[] data)
            {
                using (var ms = new MemoryStream(data))
                {
                    return ReadDict(ms);
                }
            }
    
            public static Dictionary<string, object> DecodeDict(Stream stream)
            {
                return ReadDict(stream);
            }
    
            private static Dictionary<string, object> ReadDict(Stream stream)
            {
                var b = stream.ReadByte();
                if (b != 'd')
                    throw new InvalidDataException($"Expected 'd', got '{(char)b}' ({b})");
    
                var result = new Dictionary<string, object>();
    
                while (true)
                {
                    var peek = stream.ReadByte();
                    if (peek == 'e' || peek == -1)
                        break;
    
                    stream.Seek(-1, SeekOrigin.Current);
                    var key = ReadString(stream);
                    var value = ReadValue(stream);
                    result[key] = value;
                }
    
                return result;
            }
    
            private static object ReadValue(Stream stream)
            {
                var b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();
    
                stream.Seek(-1, SeekOrigin.Current);
    
                if (b == 'd')
                    return ReadDict(stream);
                if (b == 'l')
                    return ReadList(stream);
                if (b == 'i')
                    return ReadInt(stream);
    
                return ReadString(stream);
            }
    
            private static List<object> ReadList(Stream stream)
            {
                var b = stream.ReadByte();
                if (b != 'l')
                    throw new InvalidDataException($"Expected 'l', got '{(char)b}'");
    
                var result = new List<object>();
    
                while (true)
                {
                    var peek = stream.ReadByte();
                    if (peek == 'e' || peek == -1)
                        break;
    
                    stream.Seek(-1, SeekOrigin.Current);
                    result.Add(ReadValue(stream));
                }
    
                return result;
            }
    
            private static long ReadInt(Stream stream)
            {
                var b = stream.ReadByte();
                if (b != 'i')
                    throw new InvalidDataException($"Expected 'i', got '{(char)b}'");
    
                var numBytes = new MemoryStream();
                while (true)
                {
                    b = stream.ReadByte();
                    if (b == 'e' || b == -1)
                        break;
                    numBytes.WriteByte((byte)b);
                }
    
                return long.Parse(Encoding.UTF8.GetString(numBytes.ToArray()));
            }
    
            private static string ReadString(Stream stream)
            {
                var lenBytes = new MemoryStream();
                while (true)
                {
                    var b = stream.ReadByte();
                    if (b == ':' || b == -1)
                        break;
                    lenBytes.WriteByte((byte)b);
                }
    
                var len = int.Parse(Encoding.UTF8.GetString(lenBytes.ToArray()));
                var buf = new byte[len];
                var read = stream.Read(buf, 0, len);
                if (read < len)
                    throw new EndOfStreamException($"Expected {len} bytes, got {read}");
    
                return Encoding.UTF8.GetString(buf);
            }
    }

    [Obsolete("Use BencodeCodec instead.")]
    public static class Bencode
    {
        public static byte[] Encode(object obj) => BencodeCodec.Encode(obj);

        public static Dictionary<string, object> DecodeDict(byte[] data) =>
            BencodeCodec.DecodeDict(data);

        public static Dictionary<string, object> DecodeDict(Stream stream) =>
            BencodeCodec.DecodeDict(stream);
    }
}
