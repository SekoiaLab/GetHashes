using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsdeepNET
{
    static internal class FuzzyConstants
    {
        public const int RollingWindow = 7;
        public const int MinBlocksize = 3;
        public const int NumBlockhashes = 31;
        public const int SpamSumLength = 64;        
        public const int MaxResultLength = 2 * SpamSumLength + 20;

        public static readonly byte[] Base64;

        static FuzzyConstants()
        {
            var b64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".ToCharArray();
            Base64 = new byte[b64Chars.Length];
            for (int i = 0; i < b64Chars.Length; i++)
                Base64[i] = (byte)b64Chars[i];
        }
    }
}
