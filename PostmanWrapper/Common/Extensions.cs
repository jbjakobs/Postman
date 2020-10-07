using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postman.Common
{
    public static class Extensions
    {
        public static string RemoveSpecialCharacters(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
                {
                    sb.Append(c);
                }
            }
            string result = sb.ToString();
            var digits = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            result = result.TrimStart(digits);
            return result;
        }

        public static string EnquoteIfSpaces(this string str)
        {
            if (str.Contains(" "))
                return string.Format("\"{0}\"", str);
            else
                return str;
        }
    }
}
