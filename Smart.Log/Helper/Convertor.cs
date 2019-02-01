using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Smart.Log.Helper
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
                return (T)obj;
            }
        }

        public static byte CheckSum(this byte[] data)
        {
            long longSum = data.Sum(x => (long)x);
            return unchecked((byte)longSum);
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

        /// <summary>
        /// Extracts a nibble from a large number.
        /// </summary>
        /// <typeparam name="T">Any integer type.</typeparam>
        /// <param name="t">The value to extract nibble from.</param>
        /// <param name="nibblePos">The nibble to check,
        /// where 0 is the least significant nibble.</param>
        /// <returns>The extracted nibble.</returns>
        public static byte GetNibble<T>(this T t, int nibblePos)
            where T : struct, IConvertible
        {
            nibblePos *= 4;
            var value = t.ToInt64(CultureInfo.CurrentCulture);
            return (byte)((value >> nibblePos) & 0xF);
        }
        public static byte[] HexToByteArray(this string hex)
        {
            try
            {
                byte[] bytes = Enumerable.Range(0, hex.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                    .ToArray();
                return bytes;
            }
            catch (Exception e)
            {
                SmartLog.WriteLine(e.Message);
                return null;
            }
        }
    }
}