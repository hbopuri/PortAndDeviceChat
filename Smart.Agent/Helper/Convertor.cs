using System;
using System.Collections;
using System.IO;
using System.Linq;
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

        public static string ToHex(this byte[] ba, bool stripHyphens = false)
        {
            //var hex = new StringBuilder(ba.Length * 2);
            //foreach (byte b in ba)
            //    hex.AppendFormat("{0:x2}", b);
            //return hex.ToString().ToUpper();
            if (stripHyphens)
                return BitConverter.ToString(ba).Replace("-", string.Empty);
            return BitConverter.ToString(ba);
        }

        public static string ToBinary(this byte[] ba)
        {
            string[] b = ba.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')).ToArray();
            return string.Join("-", b);
        }
        public static BitArray ToBitArray(this byte[] ba)
        {
            var bitArray = new BitArray(ba);
            return bitArray;
        }

        public static decimal ToDecimal(this byte[] src, int offset)
        {
            var decimalValue = Convert.ToInt64(src.ToHex(true), 16);
            return decimalValue;
        }

        public static float ToFloat(this byte[] src, int offset)
        {
            uint num = uint.Parse(src.ToHex(true), System.Globalization.NumberStyles.AllowHexSpecifier);

            byte[] floatArray = BitConverter.GetBytes(num);
            float single = BitConverter.ToSingle(floatArray, 0);
            return single;
        }
        public static byte[] AddAtLast(this byte[] bArray, byte newByte)
        {
            byte[] newArray = new byte[bArray.Length + 1];
            bArray.CopyTo(newArray, 0);
            //newArray[0] = newByte;
            newArray[bArray.Length] = newByte;
            return newArray;
        }
    }
}