/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using WzExport;
using WzExport.MapleLib.WzLib;
using WzExport.MapleLib.WzLib.Util;
using WzExport.MapleLib.WzLib.WzProperties;

namespace WindowsFormsApp1.MapleLib.WzLib
{
    public abstract class ProgressingWzSerializer
    {
        protected int total = 0;
        protected int curr = 0;

        public int Total
        {
            get { return total; }
        }

        public int Current
        {
            get { return curr; }
        }

        protected static void createDirSafe(ref string path)
        {
            if (path.Substring(path.Length - 1, 1) == @"\")
                path = path.Substring(0, path.Length - 1);

            string basePath = path;
            int curridx = 0;
            while (Directory.Exists(path) || File.Exists(path))
            {
                curridx++;
                path = basePath + curridx;
            }

            Directory.CreateDirectory(path);
        }

        private static string regexSearch =
            ":" + new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

        private static Regex regex_invalidPath = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));

        /// <summary>
        /// Escapes invalid file name and paths (if nexon uses any illegal character that causes issue during saving)
        /// </summary>
        /// <param name="path"></param>
        public static string EscapeInvalidFilePathNames(string path)
        {
            return regex_invalidPath.Replace(path, "");
        }
    }

    public abstract class WzSerializer : ProgressingWzSerializer
    {
        protected string indent;
        protected string lineBreak;

        public WzSerializer(int indentation, LineBreak lineBreakType)
        {
            switch (lineBreakType)
            {
                case LineBreak.None:
                    lineBreak = "";
                    break;
                case LineBreak.Windows:
                    lineBreak = "\r\n";
                    break;
                case LineBreak.Unix:
                    lineBreak = "\n";
                    break;
            }

            char[] indentArray = new char[indentation];
            for (int i = 0; i < indentation; i++)
                indentArray[i] = (char)0x20;
            indent = new string(indentArray);
        }

        private const string TypeName = "_type_";
        private const string FileName = "_file_";
        private const string CanvasName = "_canvas_";
        private const string IntName = "_int_";
        private const string ShortName = "_short_";
        private const string LongName = "_long_";
        private const string DoubleName = "_double_";
        private const string FloatName = "_float_";
        private const string SoundName = "_sound_";
        private const string StringName = "_string_";
        private const string SubName = "_sub_";
        private const string UolName = "_uol_";
        private const string VectorName = "_vector_";
        private const string ConvexName = "_convex_";
        private const string LuaName = "_lua_";
        private const string NullName = "_null_";
        private const string FieldWidthName = "width";
        private const string FieldHeightName = "height";
        private const string FieldXName = "x";
        private const string FieldYName = "y";

        /// <summary>
        /// Writes WzImageProperty to Json or Bson
        /// </summary>
        /// <param name="json"></param>
        /// <param name="prop"></param>
        protected void WritePropertyToJsonBson(JObject json, WzImageProperty prop)
        {
            var fullPath = Settings.FullPath;
            switch (prop)
            {
                // short
                case WzShortProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // int
                case WzIntProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // long
                case WzLongProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // float
                case WzFloatProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // double
                case WzDoubleProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // canvas
                case WzCanvasProperty property:
                {
                    var value = new JObject
                    {
                        {
                            FileName,
                            MyUtil.ReplaceNameWithOutWz(property.FullPath, 1)
                        },
                        { FieldWidthName, property.PngProperty.Width },
                        { FieldHeightName, property.PngProperty.Height },
                    };
                    if (json.ContainsKey(property.Name)) return;
                    foreach (var imageProperty in property.WzProperties)
                    {
                        WritePropertyToJsonBson(value, imageProperty);
                    }

                    json.Add(new JProperty(property.Name, value));

                    break;
                }
                // sound
                case WzSoundProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        var mp3 = $"{MyUtil.ReplaceName(property.FullPath, 1)}{(Settings.Extensions ? ".mp3" : "")}";
                        json.Add(new JProperty(property.Name, mp3));
                    }

                    break;
                }
                // string
                case WzStringProperty property:
                {
                    if (property.Name != "_hash" && !json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, property.Value));
                    }

                    break;
                }
                // sub
                case WzSubProperty property:
                {
                    var jsonSub = new JObject();
                    foreach (var imageProperty in property.WzProperties)
                    {
                        WritePropertyToJsonBson(jsonSub, imageProperty);
                    }

                    if (!json.ContainsKey(property.Name))
                    {
                        var jProperty = jsonSub.Count == 0
                            ? new JProperty(property.Name, NullName)
                            : new JProperty(property.Name, jsonSub);
                        json.Add(jProperty);
                    }

                    break;
                }
                // uol
                case WzUOLProperty property:
                {
                    if (!json.ContainsKey(property.Name) && property.WzValue != null)
                    {
                        var replaceName =
                            MyUtil.ReplaceName(((WzObject)property.WzValue).FullPath.Replace(".wz", ""), 1);
                        if (Settings.UseUol)
                        {
                            json.Add(new JProperty(property.Name, UolName + replaceName));
                        }
                        else
                        {
                            var np = property.LinkValue;
                            while (np is WzUOLProperty s)
                                np = s.LinkValue;
                            while (np is WzSubProperty d && d.WzProperties.Count == 1)
                                np = d.WzProperties[0];
                            while (np is WzUOLProperty s)
                                np = s.LinkValue;

                            if (np is WzCanvasProperty p)
                            {
                                var value = new JObject
                                {
                                    {
                                        FileName,
                                        MyUtil.ReplaceNameWithOutWz(p.FullPath, 1)
                                    },
                                    { FieldWidthName, p.PngProperty.Width },
                                    { FieldHeightName, p.PngProperty.Height },
                                };
                                if (json.ContainsKey(property.Name)) return;
                                foreach (var imageProperty in p.WzProperties)
                                {
                                    WritePropertyToJsonBson(value, imageProperty);
                                }

                                json.Add(new JProperty(property.Name, value));
                            }
                            else if (np is WzSoundProperty p2)
                            {
                                var mp3 = $"{MyUtil.ReplaceName(p2.FullPath, 1)}{(Settings.Extensions ? ".mp3" : "")}";
                                json.Add(new JProperty(property.Name, mp3));
                            }
                            else if (np is WzSubProperty p3)
                            {
                                var jsonSub = new JObject();
                                foreach (var imageProperty in p3.WzProperties)
                                {
                                    WritePropertyToJsonBson(jsonSub, imageProperty);
                                }

                                if (!json.ContainsKey(property.Name))
                                {
                                    var jProperty = jsonSub.Count == 0
                                        ? new JProperty(property.Name, NullName)
                                        : new JProperty(property.Name, jsonSub);

                                    json.Add(jProperty);
                                }
                            }
                            else
                            {
                                Console.WriteLine(1);
                            }


                            // todo deedy
                        }
                    }

                    break;
                }
                // vector
                case WzVectorProperty property:
                {
                    var jsonVector = new JObject
                    {
                        { FieldXName, property.X.Value },
                        { FieldYName, property.Y.Value },
                    };

                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, jsonVector));
                    }

                    break;
                }
                // convex
                case WzConvexProperty property:
                {
                    var jsonArray = new JArray();
                    foreach (var imageProperty in property.WzProperties)
                    {
                        var jsonConvex = new JObject();
                        WritePropertyToJsonBson(jsonConvex, imageProperty);
                        jsonArray.Add(jsonConvex);
                    }

                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, jsonArray));
                    }

                    break;
                }
                // lua
                case WzLuaProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, LuaName + property.Parent.Name));
                    }

                    break;
                }
                // null
                case WzNullProperty property:
                {
                    if (!json.ContainsKey(property.Name))
                    {
                        json.Add(new JProperty(property.Name, NullName));
                    }

                    break;
                }
            }
        }
    }

    public interface IWzFileSerializer
    {
        void SerializeFile(WzFile file, string path);
    }

    public interface IWzDirectorySerializer : IWzFileSerializer
    {
        void SerializeDirectory(WzDirectory dir, string path);
    }

    public interface IWzImageSerializer : IWzDirectorySerializer
    {
        void SerializeImage(WzImage img, string path);
    }

    public interface IWzObjectSerializer
    {
        void SerializeObject(WzObject file, string path);
    }

    public enum LineBreak
    {
        None,
        Windows,
        Unix
    }

    public class NoBase64DataException : Exception
    {
        public NoBase64DataException() : base()
        {
        }

        public NoBase64DataException(string message) : base(message)
        {
        }

        public NoBase64DataException(string message, Exception inner) : base(message, inner)
        {
        }

        protected NoBase64DataException(SerializationInfo info,
            StreamingContext context)
        {
        }
    }

    public class WzImgSerializer : ProgressingWzSerializer, IWzImageSerializer
    {
        public byte[] SerializeImage(WzImage img)
        {
            total = 1;
            curr = 0;

            using (MemoryStream stream = new MemoryStream())
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter);
                    byte[] result = stream.ToArray();

                    return result;
                }
            }
        }

        public void SerializeImage(WzImage img, string outPath)
        {
            total = 1;
            curr = 0;
            if (Path.GetExtension(outPath) != ".img")
            {
                outPath += ".img";
            }

            using (FileStream stream = File.Create(outPath))
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter);
                }
            }
        }

        public void SerializeDirectory(WzDirectory dir, string outPath)
        {
            total = dir.CountImages();
            curr = 0;

            if (!Directory.Exists(outPath))
                createDirSafe(ref outPath);

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
            {
                outPath += @"\";
            }

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                SerializeDirectory(subdir, outPath + subdir.Name + @"\");
            }

            foreach (WzImage img in dir.WzImages)
            {
                SerializeImage(img, outPath + img.Name);
            }
        }

        public void SerializeFile(WzFile f, string outPath)
        {
            SerializeDirectory(f.WzDirectory, outPath);
        }
    }


    public class WzImgDeserializer : ProgressingWzSerializer
    {
        private readonly bool freeResources;

        public WzImgDeserializer(bool freeResources)
            : base()
        {
            this.freeResources = freeResources;
        }

        public WzImage WzImageFromIMGBytes(byte[] bytes, WzMapleVersion version, string name, bool freeResources)
        {
            byte[] iv = WzTool.GetIvByMapleVersion(version);
            MemoryStream stream = new MemoryStream(bytes);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);
            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = bytes.Length
            };
            img.CalculateAndSetImageChecksum(bytes);

            img.Offset = 0;
            if (freeResources)
            {
                img.ParseEverything = true;
                img.ParseImage(true);

                img.Changed = true;
                wzReader.Close();
            }

            return img;
        }

        /// <summary>
        /// Parse a WZ image from .img file/
        /// </summary>
        /// <param name="inPath"></param>
        /// <param name="iv"></param>
        /// <param name="name"></param>
        /// <param name="successfullyParsedImage"></param>
        /// <returns></returns>
        public WzImage WzImageFromIMGFile(string inPath, byte[] iv, string name, out bool successfullyParsedImage)
        {
            FileStream stream = File.OpenRead(inPath);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);

            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = (int)stream.Length
            };
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            stream.Position = 0;
            img.CalculateAndSetImageChecksum(bytes);
            img.Offset = 0;

            if (freeResources)
            {
                img.ParseEverything = true;

                successfullyParsedImage = img.ParseImage(true);
                img.Changed = true;
                wzReader.Close();
            }
            else
            {
                successfullyParsedImage = true;
            }

            return img;
        }
    }


    public class WzPngMp3Serializer : ProgressingWzSerializer, IWzImageSerializer, IWzObjectSerializer
    {
        //List<WzImage> imagesToUnparse = new List<WzImage>();
        private string outPath;

        public void SerializeObject(WzObject obj, string outPath)
        {
            //imagesToUnparse.Clear();
            total = 0;
            curr = 0;
            this.outPath = outPath;
            if (!Directory.Exists(outPath))
            {
                createDirSafe(ref outPath);
            }

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
                outPath += @"\";

            total = CalculateTotal(obj);
            ExportRecursion(obj, outPath);
            /*foreach (WzImage img in imagesToUnparse)
                img.UnparseImage();
            imagesToUnparse.Clear();*/
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeDirectory(WzDirectory file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeImage(WzImage file, string path)
        {
            SerializeObject(file, path);
        }

        private int CalculateTotal(WzObject currObj)
        {
            int result = 0;
            if (currObj is WzFile)
                result += ((WzFile)currObj).WzDirectory.CountImages();
            else if (currObj is WzDirectory)
                result += ((WzDirectory)currObj).CountImages();
            return result;
        }

        private void ExportRecursion(WzObject currObj, string outPath)
        {
            if (currObj is WzFile)
                ExportRecursion(((WzFile)currObj).WzDirectory, outPath);
            else if (currObj is WzDirectory)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);
                foreach (WzDirectory subdir in ((WzDirectory)currObj).WzDirectories)
                {
                    ExportRecursion(subdir, outPath + subdir.Name + @"\");
                }

                foreach (WzImage subimg in ((WzDirectory)currObj).WzImages)
                {
                    ExportRecursion(subimg, outPath + subimg.Name + @"\");
                }
            }
            else if (currObj is WzCanvasProperty)
            {
                var bmp = ((WzCanvasProperty)currObj).PngProperty.GetImage(false);

                var path = outPath + EscapeInvalidFilePathNames(currObj.Name) + ".png";

                bmp.Save(path, ImageFormat.Png);
                //curr++;
            }
            else if (currObj is WzSoundProperty)
            {
                string path = outPath + EscapeInvalidFilePathNames(currObj.Name) + ".mp3";
                ((WzSoundProperty)currObj).SaveToFile(path);
            }
            else if (currObj is WzImage)
            {
                WzImage wzImage = ((WzImage)currObj);

                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);

                bool parse = wzImage.Parsed || wzImage.Changed;
                if (!parse)
                {
                    wzImage.ParseImage();
                }

                foreach (WzImageProperty subprop in wzImage.WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }

                if (!parse)
                {
                    wzImage.UnparseImage();
                }

                curr++;
            }
            else if (currObj is IPropertyContainer)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + ".";
                foreach (WzImageProperty subprop in ((IPropertyContainer)currObj).WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }
            }
            else if (currObj is WzUOLProperty)
                ExportRecursion(((WzUOLProperty)currObj).LinkValue, outPath);
        }
    }

    public class WzJsonBsonSerializer : WzSerializer, IWzImageSerializer
    {
        private readonly bool bExportAsJson; // otherwise bson

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="indentation"></param>
        /// <param name="lineBreakType"></param>
        /// <param name="bExportAsJson"></param>
        public WzJsonBsonSerializer(int indentation, LineBreak lineBreakType, bool bExportAsJson)
            : base(indentation, lineBreakType)
        {
            this.bExportAsJson = bExportAsJson;
        }

        public void ExportInternal(WzImage img, string path)
        {
            var parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();
            curr++;

            // TODO: Use System.Text.Json after .NET 5.0 or above 
            // for better performance via SMID related intrinsics
            var jsonObject = new JObject();
            foreach (var property in img.WzProperties)
            {
                WritePropertyToJsonBson(jsonObject, property);
            }

            if (File.Exists(path))
                File.Delete(path);
            using (var file = File.Create(path))
            {
                if (!bExportAsJson)
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BsonWriter(ms))
                        {
                            var serializer = new JsonSerializer();
                            serializer.Serialize(writer, jsonObject);

                            using (var st = new StreamWriter(file))
                            {
                                st.WriteLine(Convert.ToBase64String(ms.ToArray()));
                            }
                        }
                    }
                }
                else // json string
                {
                    using (var st = new StreamWriter(file))
                    {
                        st.WriteLine(JsonCompress(jsonObject.ToString()));
                    }
                }
            }

            if (!parsed)
                img.UnparseImage();
        }

        private static string JsonCompress(string json)
        {
            var sb = new StringBuilder();
            using (var reader = new StringReader(json))
            {
                var ch = -1;
                var lastch = -1;
                var isQuoteStart = false;
                while ((ch = reader.Read()) > -1)
                {
                    if ((char)lastch != '\\' && (char)ch == '\"')
                    {
                        isQuoteStart = !isQuoteStart;
                    }

                    if (!char.IsWhiteSpace((char)ch) || isQuoteStart)
                    {
                        sb.Append((char)ch);
                    }

                    lastch = ch;
                }
            }

            return sb.ToString();
        }

        private void ExportDirInternal(WzDirectory dir, string path)
        {
            if (!Directory.Exists(path))
                createDirSafe(ref path);

            if (path.Substring(path.Length - 1) != @"\")
                path += @"\";

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                ExportDirInternal(subdir,
                    path + EscapeInvalidFilePathNames(subdir.name) + @"\");
            }

            foreach (WzImage subimg in dir.WzImages)
            {
                ExportInternal(subimg,
                    path + EscapeInvalidFilePathNames(subimg.Name) +
                    (bExportAsJson ? ".json" : ".bin"));
            }
        }

        public void SerializeImage(WzImage img, string path)
        {
            total = 1;
            curr = 0;

            if (Path.GetExtension(path) != (bExportAsJson ? ".json" : ".bin"))
                path += (bExportAsJson ? ".json" : ".bin");
            ExportInternal(img, path);
        }

        public void SerializeDirectory(WzDirectory dir, string path)
        {
            total = dir.CountImages();
            curr = 0;
            ExportDirInternal(dir, path);
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeDirectory(file.WzDirectory, path);
        }
    }
}