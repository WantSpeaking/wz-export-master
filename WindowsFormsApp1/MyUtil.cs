using System;
using System.Windows.Forms;
using WindowsFormsApp1;

namespace WzExport
{
    public static class MyUtil
    {
        /// <summary>
        ///  1 是带上前缀 , 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string ReplaceName(string path, byte type)
        {
            var indexOf = path.IndexOf(".img", StringComparison.Ordinal);
            var first = path.Substring(0, indexOf + 5).Replace(".wz", "");
            switch (type)
            {
                case 1:
                    first = Format(first);
                    first = first.Substring(0, first.Length - 1) + (Settings.ResAll ? "_" : ",");
                    break;
            }

            var last = Format(path.Substring(indexOf + 5));
            var result = first + last;
            if (Settings.ResAll)
                result = result.Replace(".img", "");
            return result;
        }

        public static string ReplaceNameWithOutWz(string path, byte type)
        {
            var indexOf = path.IndexOf(".img", StringComparison.Ordinal);
            var of = path.IndexOf(".wz", StringComparison.Ordinal) + 4;
            var first = Settings.ResAll
                ? path.Substring(of, indexOf - of) + "_"
                : path.Substring(0, indexOf + 5).Replace(".wz", "");
            var last = Format(path.Substring(indexOf + 5));
            var result = first + last;
            if (Settings.ResAll)
                result = result.Replace(".img", "");
            return Format(result);
        }

        private static string Format(string str)
        {
            while (str.Contains(@"/"))
                str = str.Replace(@"/", "_");
            while (str.Contains(@"\"))
                str = str.Replace(@"\", "_");
            while (str.Contains(@"\\"))
                str = str.Replace(@"\\", "_");
            while (str.Contains(":")) // 冒号
                str = str.Replace(":", "colon");
            while (str.Contains("?")) // 问号
                str = str.Replace("?", "quest");
            while (str.Contains("|")) // 竖杠
                str = str.Replace("|", "vertical");
            while (str.Contains("*")) // 星号
                str = str.Replace("*", "asterisk");


            return str;
        }
    }
}