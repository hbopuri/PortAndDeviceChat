using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Smart.Agent.Helper
{
    public static class Convertor
    {
        public static T ToType<T>(this byte[] data)
        {
            if (data == null)
                return default(T);
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(data))
            {
                object obj = bf.Deserialize(ms);
                return (T) obj;
            }
        }

        public static string ToHex(this byte[] ba)
        {
            //var hex = new StringBuilder(ba.Length * 2);
            //foreach (byte b in ba)
            //    hex.AppendFormat("{0:x2}", b);
            //return hex.ToString().ToUpper();
            return  BitConverter.ToString(ba);
        }

    }
}