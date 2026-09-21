using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xv2CoreLib
{
    public static class StringEx
    {
        public enum EncodingType
        {
            ASCII,
            UTF8,
            Unicode
        }

        /// <summary>
        /// Write a null-terminated ASCII encoded string to a byte array
        /// </summary>
        /// <param name="span">Input array. This must contain enough space for the input string.</param>
        /// <param name="str">Input string</param>
        /// <returns>The number of bytes written</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteStringASCII(this byte[] bytes, int offset, string str)
        {
            int length = Encoding.ASCII.GetBytes(str, 0, str.Length, bytes, offset);
            bytes[offset + length] = 0;
            return length + 1;
        }

        /// <summary>
        /// Write a null-terminated UTF8 encoded string to a byte array
        /// </summary>
        /// <param name="span">Input array. This must contain enough space for the input string.</param>
        /// <param name="str">Input string</param>
        /// <returns>The number of bytes written</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteStringUTF8(this byte[] bytes, int offset, string str)
        {
            int length = Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, offset);
            bytes[offset + length] = 0;
            return length + 1;
        }

        /// <summary>
        /// Write a null-terminated UTF16 encoded string to a byte array
        /// </summary>
        /// <param name="span">Input array. This must contain enough space for the input string.</param>
        /// <param name="str">Input string</param>
        /// <returns>The number of bytes written</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteStringUTF16(this byte[] bytes, int offset, string str)
        {
            int length = Encoding.Unicode.GetBytes(str, 0, str.Length, bytes, offset);
            bytes[offset + length] = 0;
            return length + 1;
        }

        /// <summary>
        /// Reads a null-terminated ASCII encoded string from the byte array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetStringASCII(this byte[] bytes, int index)
        {
            int nullByte = FindNullTerminatorByte(bytes, index);
            return nullByte > index ? Encoding.ASCII.GetString(bytes, index, nullByte - index) : null;
        }

        /// <summary>
        /// Reads a null-terminated UTF8 encoded string from the byte array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetStringUTF8(this byte[] bytes, int index)
        {
            int nullByte = FindNullTerminatorByte(bytes, index);
            return nullByte > index ? Encoding.UTF8.GetString(bytes, index, nullByte - index) : null;
        }

        /// <summary>
        /// Reads a null-terminated UTF16 encoded string from the byte array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetStringUTF16(this byte[] bytes, int index)
        {
            int nullByte = FindNullTerminatorBytesUTF16(bytes, index);
            return nullByte > index ? Encoding.Unicode.GetString(bytes, index, nullByte - index) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindNullTerminatorByte(byte[] bytes, int index)
        {
            if (index >= bytes.Length)
                throw new ArgumentOutOfRangeException($"StringEx.FindNullTerminatorByte: index was out of range");

            for (int i = index; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                    return i;
            }

            throw new Exception("StringEx.FindNullTerminatorByte: null terminator byte not found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindNullTerminatorBytesUTF16(byte[] bytes, int index)
        {
            if (index >= bytes.Length)
                throw new ArgumentOutOfRangeException($"StringEx.FindNullTerminatorByte: index was out of range");

            for (int i = index; i < bytes.Length; i += 2)
            {
                if (i >= bytes.Length) break;

                if (bytes[i] == 0 && bytes[i + 1] == 0)
                {
                    return i;

                }
            }

            throw new Exception("StringEx.FindNullTerminatorByteUTF16: null terminator byte not found");
        }

        public static string GetXmlStringUTF8(byte[] bytes)
        {
            if (bytes[0] == 254)
                throw new Exception("GetStringUTF8: invalid BOM");

            string xmlText = Encoding.UTF8.GetString(bytes);
            return bytes[0] == 239 ? xmlText.Substring(1) : xmlText;
        }

        /// <summary>
        /// Search for a string at the inputted index and return it. Supports ASCII and UTF8 encoding as well as fixed length strings and null terminated ones.
        /// </summary>
        public static string GetString(List<byte> bytes, int index, bool useNullText = true, EncodingType encodingType = EncodingType.ASCII, int maxSize = int.MaxValue, bool useNullTerminator = true)
        {
            if (index == 0)
            {
                return (useNullText) ? "NULL" : "";
            }

            if (index > bytes.Count - 1)
            {
                throw new IndexOutOfRangeException(String.Format("GetString: index is out of range.\nIndex = {0}\nSize = {1}", index, bytes.Count));
            }

            //Get size
            int desiredSize = maxSize;
            if (useNullTerminator)
            {

                maxSize = GetStringSize(bytes, index);

                if (maxSize > desiredSize)
                {
                    maxSize = desiredSize;
                }
            }

            if (maxSize == 0)
            {
                return (useNullText) ? "NULL" : "";
            }

            if (encodingType == EncodingType.ASCII)
            {
                string value = Encoding.ASCII.GetString(bytes.GetRange(index, maxSize).ToArray());
                return (string.IsNullOrWhiteSpace(value) && useNullText) ? "NULL" : value;
            }
            else if (encodingType == EncodingType.UTF8)
            {
                string value = Encoding.UTF8.GetString(bytes.GetRange(index, maxSize).ToArray());
                return (string.IsNullOrWhiteSpace(value) && useNullText) ? "NULL" : value;
            }
            else
            {
                throw new Exception("GetString: Unsupported EncodingType = " + encodingType);
            }
        }

        /// <summary>
        /// Search for a string at the inputted index and return it. Supports ASCII and UTF8 encoding as well as fixed length strings and null terminated ones.
        /// </summary>
        public static string GetString(byte[] bytes, int index, bool useNullText = true, EncodingType encodingType = EncodingType.ASCII, int maxSize = int.MaxValue, bool useNullTerminator = true, bool allowZeroIndex = false)
        {
            if (index == 0 && !allowZeroIndex)
            {
                return (useNullText) ? "NULL" : "";
            }

            if (index > bytes.Length - 1)
            {
                throw new IndexOutOfRangeException(String.Format("GetString: index is out of range.\nIndex = {0}\nSize = {1}", index, bytes.Length));
            }

            //Get string size
            int desiredSize = maxSize;

            if (useNullTerminator)
            {
                maxSize = GetStringSize(bytes, index, (encodingType == EncodingType.Unicode));

                if (maxSize > desiredSize)
                {
                    maxSize = desiredSize;
                }
            }

            if (maxSize == 0)
            {
                return (useNullText) ? "NULL" : "";
            }

            if (encodingType == EncodingType.ASCII)
            {
                string value = Encoding.ASCII.GetString(bytes, index, maxSize);
                return (string.IsNullOrWhiteSpace(value) && useNullText) ? "NULL" : value;
            }
            else if (encodingType == EncodingType.UTF8)
            {
                string value = Encoding.UTF8.GetString(bytes, index, maxSize);
                return (string.IsNullOrWhiteSpace(value) && useNullText) ? "NULL" : value;
            }
            else
            {
                string value = Encoding.Unicode.GetString(bytes, index, maxSize);
                return (string.IsNullOrWhiteSpace(value) && useNullText) ? "NULL" : value;
                //throw new Exception("GetString: Unsupported EncodingType = " + encodingType);
            }
        }

        private static int GetStringSize(byte[] bytes, int index, bool unicode = false)
        {
            if (unicode)
            {
                for (int i = index; i < bytes.Length; i += 2)
                {
                    if (i >= bytes.Length) break;

                    if (bytes[i] == 0 && bytes[i + 1] == 0)
                    {
                        return i - index;

                    }
                }
            }
            else
            {
                for (int i = index; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0)
                    {
                        return i - index;

                    }
                }
            }

            throw new InvalidDataException(String.Format("GetStringSize: Could not find the null terminator byte.\nIndex = {0}\nPosition = {1}", index, bytes.Length - 1));
        }

        private static int GetStringSize(List<byte> bytes, int index)
        {
            for (int i = index; i < bytes.Count; i++)
            {
                if (bytes[i] == 0)
                    return i - index;
            }

            throw new InvalidDataException(String.Format("GetStringSize: Could not find the null terminator byte.\nIndex = {0}\nPosition = {1}", index, bytes.Count - 1));
        }

        public static int GetPaddedUnicodeStringByteSize(string unicodeString, int blockSize)
        {
            int size = unicodeString.Length * 2;
            size += 1; //Null terminator
            return Utils.CalculatePadding(size, blockSize) + size;
        }

        public static byte[] WriteFixedSizeString(string str, int maxSize)
        {
            //Trim if too long
            if (str.Length > maxSize)
            {
                str = str.Substring(0, maxSize);
            }

            //Write name, and pad it out to max size
            List<byte> bytes = new List<byte>(maxSize - str.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(str));
            bytes.AddRange(new byte[maxSize - str.Length]);

            return bytes.ToArray();
        }

        public static int GetMinIndexOfSubstrings(this string str, int startIndex, params char[] substrings)
        {
            int idx = int.MaxValue;

            foreach (var sub in substrings)
            {
                int _idx = str.IndexOf(sub, startIndex);
                if (_idx < idx && _idx != -1) idx = _idx;
            }

            return idx == int.MaxValue ? -1 : idx;
        }

        public static bool IsFloat(this string str)
        {
            if (str == null || str.Length == 0) return false;
            bool hasDecimalPoint = false;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '.' && !hasDecimalPoint && i > 0)
                {
                    hasDecimalPoint = true;
                    continue;
                }
                if (!char.IsDigit(str[i])) return false;
            }

            return true;
        }
    }

}
