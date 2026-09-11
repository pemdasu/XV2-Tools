using System;

namespace Xv2CoreLib
{
    public static class Extensions
    {
        /// <summary>
        /// Splits string on (possibly) multiple elements.
        /// </summary>
        /// <param name="str">String to split.</param>
        /// <param name="options">Options to use while splitting.</param>
        /// <param name="splitStrings">Elements to split string on. (Delimiters)</param>
        /// <returns></returns>
        public static string[] Split(this string str, StringSplitOptions options, params string[] splitStrings)
        {
            return str.Split(splitStrings, options);
        }

    }
}