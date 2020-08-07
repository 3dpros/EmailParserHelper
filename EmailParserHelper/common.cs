using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    class common
    {
        public static string MatchRegex(string body, string pattern, int group = 0)
        {
            var result = Regex.Match(body, pattern, RegexOptions.Singleline)?.Groups[group]?.Value;
            if (string.IsNullOrEmpty(result))
            {
                return string.Empty;
            }
            else
                return result;
        }
    }
}
