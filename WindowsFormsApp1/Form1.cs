using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using WindowsFormsApp1.MapleLib.WzLib;
using WzExport;
using WzExport.MapleLib.WzLib;
using WzExport.MapleLib.WzLib.WzProperties;

namespace WindowsFormsApp1
{
	// todo string int short 
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		FolderBrowserDialog dialog = new FolderBrowserDialog();

		private void button1_Click(object sender, EventArgs e)
		{
			dialog.Description = "请选择文件路径";
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				label1.Text = dialog.SelectedPath;
				init_selects(dialog.SelectedPath);
				Settings.Path = dialog.SelectedPath + @"\";
				Settings.ExportPath = Settings.Path + @"deedy\";
			}
		}

		private void init_selects(string path)
		{
			var root = new DirectoryInfo(path);
			checkedListBox1.Items.Clear();
			foreach (var f in root.GetFiles())
			{
				var name = f.Name;
				if (name.EndsWith(".wz"))
					checkedListBox1.Items.Add(name);
			}
		}

		private void radioButton1_CheckedChanged(object sender, EventArgs e)
		{
			Settings.Version = WzMapleVersion.GMS;
		}

		private void radioButton2_CheckedChanged(object sender, EventArgs e)
		{
			Settings.Version = WzMapleVersion.EMS;
		}

		private void radioButton3_CheckedChanged(object sender, EventArgs e)
		{
			Settings.Version = WzMapleVersion.BMS;
		}

		private void textBox1_TextChanged(object sender, EventArgs e)
		{
			var text = textBox1.Text;
			var sb = new StringBuilder();
			foreach (var c in text.ToCharArray())
			{
				if (char.IsNumber(c))
				{
					sb.Append(c);
				}
			}

			textBox1.Text = sb.ToString();
			Settings.GameVersion = short.Parse(textBox1.Text);
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			Settings.Extensions = checkBox1.Checked;
		}

		private int _currentSize = 0;
		private int _deltaSize = 0;

		private void button2_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(Settings.Path))
			{
				MessageBox.Show("请先选择WZ文件所在文件夹!");
				return;
			}

			if (Settings.GameVersion <= 0)
			{
				MessageBox.Show("请输入游戏版本!");
				return;
			}

			var wzList = new List<string>();
			for (var i = 0; i < checkedListBox1.CheckedItems.Count; i++)
			{
				wzList.Add(checkedListBox1.CheckedItems[i].ToString().Replace(".wz", ""));
			}

			if (wzList.Count == 0)
			{
				MessageBox.Show("请选择需要导出资源的wz文件!");
				return;
			}

			label2.Text = $"开始提取...共有{wzList.Count}个wz文件需要提取...\r\n";
			Application.DoEvents();
			var start = CurrentTimes;
			foreach (var name in wzList)
			{
				try
				{
					var s = CurrentTimes;
					var wzFile = new WzFile($"{Settings.Path}{name}.wz", Settings.GameVersion,
						Settings.Version);
					wzFile.ParseWzFile();
					_currentSize = 0;
					DirForeach(wzFile.wzDir);
					label2.Text += $"  提取[ {name}.wz ]花费:{FormatTime(s, CurrentTimes)}\r\n";
					Application.DoEvents();
					try
					{
						wzFile.Dispose();
					}
					catch (Exception)
					{
						// ignored
					}
				}
				catch (Exception exception)
				{
					label2.Text += $"  提取[ {name}.wz ]失败,原因:{exception.Message}\r\n";
					Application.DoEvents();
				}
			}

			label5.Text = $"已处理:{_deltaSize},需要处理:{_deltaSize} (100%)";
			label4.Text = $"全部提取完成,共花费:{FormatTime(start, CurrentTimes)}\r\n导出在{Settings.ExportPath}目录下...";
			Application.DoEvents();
		}

		private void DirForeach(WzDirectory directory)
		{
			_currentSize += directory.images.Count;
			foreach (var wzDirImage in directory.images)
			{
				ImgForeach(wzDirImage);
			}

			foreach (var subDir in directory.subDirs)
			{
				DirForeach(subDir);
			}
		}

		private void ImgForeach(WzImage image)
		{
			image.ParseImage();

			var wzImgSerializer = new WzJsonBsonSerializer(0, LineBreak.Unix, true);

			var exportPath = $@"{Settings.ExportPath}{image.Parent.FullPath.Replace(".wz", "")}" +
							 $@"\{image.name}.json";
			//$@"\{image.name.Replace(".img", "")}.json";
			CreateDir(exportPath);
			wzImgSerializer.ExportInternal(image, exportPath);
			if (!Settings.OnlyJSON)
			{
				foreach (var propertyA in image.properties)
				{
					switch (propertyA)
					{
						case WzCanvasProperty canvasProperty:
							Png(canvasProperty);
							break;
						case WzSoundProperty binaryProperty:
							Mp3(binaryProperty.GetBytes(), binaryProperty.FullPath);
							break;
						case WzSubProperty property0:
							SubForeach(property0);
							break;
					}
				}
			}

			_deltaSize++;
			label4.Text = $"JSON:{exportPath}";
			var cs = _currentSize >= _deltaSize ? _currentSize : _deltaSize + 1;
			label5.Text = $"已处理:{_deltaSize},需要处理:{cs} ({(_deltaSize * 100f / cs).ToString("#0.00")}%)";
			Application.DoEvents();
		}

		private void SubForeach(WzSubProperty subProperty)
		{
			foreach (var propertyA in subProperty.properties)
			{
				switch (propertyA)
				{
					case WzCanvasProperty canvasProperty:
						Png(canvasProperty);
						break;
					case WzSoundProperty binaryProperty:
						Mp3(binaryProperty.GetBytes(), binaryProperty.FullPath);
						break;
					case WzSubProperty property:

						try
						{
							SubForeach(property);
						}
						catch (Exception e)
						{
							Console.WriteLine(e);
						}
						break;
				}
			}
		}

		private void Png(WzCanvasProperty canvasProperty)
		{
			var pngProperty = canvasProperty.imageProp;
			var bitmap = pngProperty.GetImage(false);
			if (bitmap == null) return;
			var replace = MyUtil.ReplaceName(pngProperty.FullPath, 0);
			// if (Settings.ResAll)
			// {
			//     while (replace.IndexOf(@"\", StringComparison.Ordinal) != -1)
			//         replace = replace.Replace(@"\", "_");
			//     replace = ReplaceFirst(replace, "_", @"\");
			// }
			var exportPath = Settings.ExportPath + replace.Replace("_PNG", Settings.Extensions ? ".png" : "");
			CreateDir(exportPath);
			bitmap.Save(exportPath, ImageFormat.Png);
			canvasProperty.Dispose();
			label4.Text = $"PNG:{exportPath}";
			Application.DoEvents();
		}

		public static string ReplaceFirst(string input, string oldValue, string newValue)
		{
			var regEx = new Regex(oldValue, RegexOptions.Multiline);
			return regEx.Replace(input, newValue ?? "", 1);
		}

		private void Mp3(byte[] pReadByte, string fullPath)
		{
			var replace = MyUtil.ReplaceName(fullPath, 0);
			// if (Settings.ResAll)
			// {
			//     while (replace.IndexOf(@"\", StringComparison.Ordinal) != -1)
			//         replace = replace.Replace(@"\", "_");
			//     replace = ReplaceFirst(replace, "_", @"\");
			// }
			var exportPath = Settings.ExportPath + replace + (Settings.Extensions ? ".mp3" : "");
			CreateDir(exportPath);
			var pFileStream = new FileStream(exportPath, FileMode.OpenOrCreate);
			pFileStream.Write(pReadByte, 0, pReadByte.Length);
			pFileStream.Close();
			label4.Text = $"MP3:{exportPath}";
			Application.DoEvents();
		}

		private void CreateDir(string path)
		{
			var exportPath = path.Replace(".wz", "");

			var lastIndexOf = exportPath.LastIndexOf(@"\", StringComparison.Ordinal);
			var substring = exportPath.Substring(0, lastIndexOf);

			if (Directory.Exists(substring)) return;
			Directory.CreateDirectory(substring);
			label4.Text = $"新建文件夹:{substring}";
			Application.DoEvents();
		}

		private static long CurrentTimes =>
			Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);

		private static string FormatTime(long start, long end)
		{
			const int minute = 1000 * 60;
			const int hour = minute * 60;
			var diffValue = end - start;
			var resultTime = "";
			if (diffValue < 1000)
			{
				resultTime = $"{diffValue}毫秒!";
			}
			else if (diffValue < minute)
			{
				var s = (int)(diffValue / 1000);
				var ms = (int)(diffValue - s * 1000);
				resultTime = $"{s}秒{ms}毫秒";
			}
			else if (diffValue < hour)
			{
				var m = (int)(diffValue / minute);
				var s = (int)((diffValue - m * minute) / 1000);
				var ms = (int)(diffValue - m * minute - s * 1000);
				resultTime = $"{m}分钟{s}秒{ms}毫秒";
			}

			return $"[{resultTime}]";
		}

		private void checkBox2_CheckedChanged(object sender, EventArgs e)
		{
			Settings.FullPath = checkBox2.Checked;
		}

		private void checkBox3_CheckedChanged(object sender, EventArgs e)
		{
			Settings.UseUol = checkBox3.Checked;
		}

		private void checkBox4_CheckedChanged(object sender, EventArgs e)
		{
			Settings.OnlyJSON = checkBox4.Checked;
		}

		private void checkBox5_CheckedChanged(object sender, EventArgs e)
		{
			Settings.ResAll = checkBox5.Checked;
			if (Settings.ResAll)
				checkBox2.Checked = true;
		}

		private string processText(string value)
		{
			if (value == null) return null;
			var quoteFlag = false;//标记是否添加过双引号
			if (value.Contains("\""))
			{
				value = value.Replace("\"", "\"\"");
				value = "\"" + value + "\"";
				quoteFlag = true;
			}
			if (value.Contains(",") && !quoteFlag)//若发现有逗号  需前后加引号
			{
				value = "\"" + value + "\"";
			}
			return value;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			//var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\String.wz", 083, WzMapleVersion.GMS);
			//var wzFile = new WzFile(@"N:\360Downloads\String_000GmsLatest.wz", 243, WzMapleVersion.BMS);

			/*var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\destinationPath\String-original.wz", 083,WzMapleVersion.GMS);
			wzFile.ParseWzFile();
			(wzFile["Map.img"] as WzImage).ParseImage();
			StringBuilder csvContent = new StringBuilder();
			csvContent.AppendLine($"mapid,地区名,streetName,mapName");
			foreach (var maple in (wzFile["Map.img"] as WzImage).properties)
			{
				foreach (var maple_0 in (maple as WzSubProperty).properties)
				{
					csvContent.AppendLine($"{processText(maple_0.Name)},{processText(maple.Name)},{processText(maple_0["streetName"]?.GetString())},{processText(maple_0["mapName"]?.GetString())}");
				}
			}
			File.WriteAllText(@"N:\mapinfoGMS083.csv", csvContent.ToString(), Encoding.UTF8);*/


			/*StringBuilder csvContent = new StringBuilder();
			csvContent.AppendLine($"mapid,传送门名,目标地图id,目标传送门名,x,y");

			for (int i = 0; i <= 9; i++)
            {
				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Map\Map\Map{i}\Map{i}_000.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var mapImg in wzFile.wzDir.WzImages)
				{
					var mapid = mapImg.name.TrimStart('0').Replace(".img", "");

					mapImg.ParseImage();
					if (mapImg["portal"] is WzSubProperty portal)
					{
						foreach (var portal_0 in portal.properties)
						{
							if (portal_0["pt"].GetInt() == 2)
							{
								csvContent.AppendLine($"{mapid},{portal_0["pn"]},{portal_0["tm"]},{portal_0["tn"]},{portal_0["x"]},{portal_0["y"]}");
							}

						}
					}

				}
			}
			
			File.WriteAllText(@"N:\portalinfo.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*StringBuilder csvContent = new StringBuilder();
			csvContent.AppendLine($"mapid,传送门名,目标地图id,目标传送门名,x,y");

			var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\Map.wz", 083, WzMapleVersion.GMS);
			wzFile.ParseWzFile();

			foreach (var Map0 in (wzFile["Map"] as WzDirectory).subDirs)
			{
				foreach (var Map0_000000000 in Map0.images)
				{
					Map0_000000000.ParseImage();
					var mapid = Map0_000000000.name.TrimStart('0').Replace(".img", "");

					if (Map0_000000000["portal"] is WzSubProperty portal)
					{
						foreach (var portal_0 in portal.properties)
						{
							if (portal_0["pt"].GetInt() == 2)
							{
								csvContent.AppendLine($"{mapid},{portal_0["pn"]},{portal_0["tm"]},{portal_0["tn"]},{portal_0["x"]},{portal_0["y"]}");
							}
						}
					}
				}
			}

			File.WriteAllText(@"N:\portalinfoGMS083.csv", csvContent.ToString(), Encoding.UTF8);*/


			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();

			for (int i = 0; i <= 58; i++)
			{

				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Mob\_Canvas\_Canvas_{i.ToString().PadLeft(3,'0')}.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var mapImg in wzFile.wzDir.WzImages)
				{
					var mapid = mapImg.name.TrimStart('0').Replace(".img", "");

					mapImg.ParseImage();
					foreach (var stand in mapImg.properties)
					{
						if (stand.PropertyType == WzPropertyType.SubProperty )
						{
							dict[stand.Name] = stand.Name;
						}
					}
					
					
				}
			}

			foreach (var stand in dict)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\mobAnimInfoCMS206.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();

			for (int i = 0; i <= 17; i++)
			{
				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Npc\_Canvas\_Canvas_{i.ToString().PadLeft(3, '0')}.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var mapImg in wzFile.wzDir.WzImages)
				{
					mapImg.ParseImage();
					foreach (var stand in mapImg.properties)
					{
						if (stand.PropertyType == WzPropertyType.SubProperty)
						{
							dict[stand.Name] = stand.Name;
						}
					}


				}
			}

			foreach (var stand in dict)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\NpcActionNameCMS206.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();

			for (int i = 0; i <= 33; i++)
			{
				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Map\Obj\_Canvas\_Canvas_{i.ToString().PadLeft(3, '0')}.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var mapImg in wzFile.wzDir.WzImages)
				{
					mapImg.ParseImage();
					foreach (var stand in mapImg.properties)
					{
						if (stand.PropertyType == WzPropertyType.SubProperty)
						{
							dict[stand.Name] = stand.Name;
						}
					}


				}
			}



			foreach (var stand in dict)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\NpcActionNameCMS206.csv", csvContent.ToString(), Encoding.UTF8);*/
			/*
			Dictionary<string, string> dict_l0 = new Dictionary<string, string>();
			Dictionary<string, string> dict_l1 = new Dictionary<string, string>();
			Dictionary<string, string> dict_l2 = new Dictionary<string, string>();

			StringBuilder csvContent = new StringBuilder();
			//List<(string,string,string)> list = new List<(string, string, string)>();
			for (int i = 0; i <= 33; i++)
			{
				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Map\Obj\_Canvas\_Canvas_{i.ToString().PadLeft(3, '0')}.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var l0 in wzFile.wzDir.WzImages)
				{
					l0.ParseImage();
					var l0s = l0.name.Replace(".img", "");
					dict_l0[l0s] = l0s;
					foreach (var l1 in l0.properties)
					{
						if (l1 is WzSubProperty l1_ws)
						{
							dict_l1[l1.Name] = l1.Name;
							foreach(var l2 in l1_ws.properties)
							{
								dict_l2[l2.Name] = l2.Name;
								//list.Add((l0s, l1.Name, l2.Name));
								csvContent.AppendLine($"{l0s},{l1.Name},{l2.Name}");
							}
						}
					}
				}
			}

			/*foreach (var stand in dict_l0)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\Obj_l0.csv", csvContent.ToString(), Encoding.UTF8);

			csvContent.Clear();
			foreach (var stand in dict_l1)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\Obj_l1.csv", csvContent.ToString(), Encoding.UTF8);

			csvContent.Clear();
			foreach (var stand in dict_l2)
			{
				Console.WriteLine(stand.Key);
				csvContent.AppendLine(stand.Key);
			}
			File.WriteAllText(@"N:\Obj_l2.csv", csvContent.ToString(), Encoding.UTF8);

			File.WriteAllText(@"N:\Obj.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();

			for (int i = 0; i <= 58; i++)
			{

				var wzFile = new WzFile($@"N:\Program Files (x86)\上海数龙科技有限公司\冒险岛online\Data\Mob\_Canvas\_Canvas_{i.ToString().PadLeft(3, '0')}.wz", 206,
						WzMapleVersion.BMS);
				wzFile.ParseWzFile();
				foreach (var mapImg in wzFile.wzDir.WzImages)
				{
					var mapid = mapImg.name.TrimStart('0').Replace(".img", "");

					csvContent.AppendLine(mapid);
				}
			}

			File.WriteAllText(@"N:\MobIdCMS083.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();
			var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\Item.wz", 083, WzMapleVersion.GMS);
			wzFile.ParseWzFile();
			foreach (var wzDirectory in wzFile.wzDir.WzDirectories)
			{
				if (wzDirectory.Name.Contains("Special"))
				{
					continue;
				}
				if (wzDirectory.Name.Contains("Pet"))
				{
					foreach (var wzImage in wzDirectory.WzImages)
					{
						var mapid = wzImage.Name.TrimStart('0').Replace(".img", "");
						csvContent.AppendLine(mapid);

					}
				}
				else
				{
					foreach (var wzImage in wzDirectory.WzImages)
					{
						wzImage.ParseImage();
						foreach (var property in wzImage.properties)
						{
							var mapid = property.Name.TrimStart('0').Replace(".img", "");
							csvContent.AppendLine(mapid);
						}
					}
				}
			}

			File.WriteAllText(@"N:\ItemIdCMS083.csv", csvContent.ToString(), Encoding.UTF8);*/

			/*Dictionary<string, string> dict = new Dictionary<string, string>();
			StringBuilder csvContent = new StringBuilder();
			var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\Npc.wz", 083, WzMapleVersion.GMS);
			wzFile.ParseWzFile();
			foreach (var wzImage in wzFile.wzDir.WzImages)
			{
				var mapid = wzImage.Name.TrimStart('0').Replace(".img", "");
				csvContent.AppendLine(mapid);
			}

			File.WriteAllText(@"N:\NpcIdCMS083.csv", csvContent.ToString(), Encoding.UTF8);*/

			// Load JSON from a file
			StringBuilder csvContent = new StringBuilder();
			csvContent.AppendLine($"fullpath,guid");
			string mapTemplatePath = $@"N:\mswProjects\Start4\RootDesk\MyDesk\Map.wz\Obj\connectGL.img";
			var directoryInfo = new DirectoryInfo(mapTemplatePath);
			foreach (var fileInfo in directoryInfo.GetFiles())
			{
				string jsonContent = File.ReadAllText(fileInfo.FullName);
				JObject jsonObject = JObject.Parse(jsonContent);
				//Console.WriteLine(jsonObject["ContentProto"]["Json"]["resource_guid"]);
				csvContent.AppendLine($"{fileInfo.Name.Replace(".sprite","")},{jsonObject["ContentProto"]["Json"]["resource_guid"]}");
			}
			File.WriteAllText(@"N:\MapObjconnectGL.imgCMS083.csv", csvContent.ToString(), Encoding.UTF8);
			/*Directory.GetFiles(mapTemplatePath,)
			string jsonContent = File.ReadAllText(mapTemplatePath);
			jsonContent = jsonContent.Replace("map_template", mapid);*/
		}

		private void button4_Click(object sender, EventArgs e)
		{
			var wzFile = new WzFile(@"G:\Program Files (x86)\MapleStory\Map.wz", 083, WzMapleVersion.GMS);
			wzFile.ParseWzFile();

			foreach (var Map0 in (wzFile["Map"] as WzDirectory).subDirs)
			{
				if (Map0.name != "Map0")
				{
					continue;
				}
				foreach (var Map0_000000000 in Map0.images)
				{
					var tempMapId = "40002";
					if (Map0_000000000.name != $"{tempMapId.PadLeft(9, '0')}.img")
					{
						continue;
					}
					Map0_000000000.ParseImage();
					string mapid = Map0_000000000.name.TrimStart('0').Replace(".img", "");

					// Load JSON from a file
					string mapTemplateFileName = "map_template";
					string mapTemplatePath = $"N:\\mswProjects\\Start4\\map\\{mapTemplateFileName}.map";
					string jsonContent = File.ReadAllText(mapTemplatePath);
					jsonContent = jsonContent.Replace("map_template", mapid);


					// 原始文本
					string text = jsonContent;

					string pattern = "\"(?<key>id)\":\\s*\"(?<value>[^\"]+)\"";

					// 替换匹配到的 "id" 值为新的 UUID
					string updatedText = Regex.Replace(text, pattern, match =>
					{
						string newId = Guid.NewGuid().ToString(); // 生成新的 UUID
						return $"\"{match.Groups["key"].Value}\": \"{newId}\"";
					});

					jsonContent = updatedText;

					// Parse JSON into a JObject (dynamic JSON structure)
					JObject jsonObject = JObject.Parse(jsonContent);
					List<JToken> jo_portals = new List<JToken>();
					List<JToken> jo_spawnLocations = new List<JToken>();
					List<WzObject> wz_portals = new List<WzObject>();
					List<WzObject> wz_spawnPoints = new List<WzObject>();

					var global_offsetX = 0f;
					var global_offsetY = 0f;

					for (int i = 0; i <= 7; i++)
					{
						if (i != 0)
						{
							//continue;
						}

						var joLayer = i + 1;
						var joMap = jsonObject["ContentProto"]["Entities"][joLayer];
						var joTiles = joMap["jsonString"]["@components"][1]["Tiles"] as JArray;
						joTiles.RemoveAll();
						Dictionary<string, List<WzObject>> tileType_Tile_Dict = new Dictionary<string, List<WzObject>>();
						List<(int, int, int, int)> missingTileInfos = new List<(int, int, int, int)>();

						if (Map0_000000000[i.ToString()] is WzSubProperty Map0_000000000_0)
						{
							var tS = Map0_000000000_0["info"]?["tS"]?.GetString();
							if (string.IsNullOrEmpty(tS)) continue;

							var tileImg = wzFile["Tile"][$"{tS}.img"] as WzImage;
							if (tileImg == null) continue;
							var bsc_tile = tileImg["bsc"]?["0"] as WzCanvasProperty;
							if (bsc_tile == null) continue;
							var bsc_bitmap = bsc_tile.GetLinkedWzCanvasBitmap();
							var bsc_width = bsc_bitmap.Width;
							var bsc_height = bsc_bitmap.Height;
							if (Map0_000000000_0["tile"] is WzSubProperty Map0_000000000_0_tile)
							{
								var offsetX = 0f;
								var offsetY = 0f;

								foreach (var Map0_000000000_0_tile_0 in Map0_000000000_0_tile.properties)
								{

									var u = Map0_000000000_0_tile_0["u"]?.GetString();
									var no = Map0_000000000_0_tile_0["no"]?.GetInt();
									var x = Map0_000000000_0_tile_0["x"]?.GetInt();
									var y = Map0_000000000_0_tile_0["y"]?.GetInt();

									var tile = tileImg[u]?[no.ToString()] as WzCanvasProperty;
									if (Map0_000000000_0_tile_0.FullPath != "Map.wz\\Map\\Map0\\001000000.img\\1\\tile\\1")
									{
										//continue;
									}
									var bitmap = tile.GetLinkedWzCanvasBitmap();
									var width = bitmap.Width;
									var height = bitmap.Height;
									var origin = tile.GetCanvasOriginPosition();
									var tileIndexX = 0;
									var tileIndexY = 0;
									tileIndexX = (int)Math.Round((double)(2.0 * (x + 45 - origin.X + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
									tileIndexY = (int)Math.Round((double)(-2.0 * (y + 30 - origin.Y + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
									//Console.WriteLine($"FullPath:{Map0_000000000_0_tile_0.FullPath,-50},u:{u,5},x:{x,5},y:{y,5},tileIndexX:{tileIndexX,5}.tileIndexX:{tileIndexY,5}");

									if (tile == null) continue;
									if (!tileType_Tile_Dict.TryGetValue(u, out var wzObjects))
									{
										wzObjects = new List<WzObject>();
										tileType_Tile_Dict[u] = wzObjects;
									}
									else
									{
										wzObjects.Add(Map0_000000000_0_tile_0);
									}

									switch (u)
									{
										case "bsc":
											if (tileIndexX % 2 == 0)
											{
												offsetX = (bsc_width / 2.0f) * +1.0f;
												global_offsetX = offsetX;
											}
											if (tileIndexY % 2 == 0)
											{
												offsetY = (bsc_height / 2.0f) * -1.0f;
												global_offsetY = offsetY;
											}
											Console.WriteLine($"offsetX:{offsetX},offsetY:{offsetY}");
											break;
										default:
											break;
									}
								}

								if (tileType_Tile_Dict.TryGetValue("bsc", out var wzObjects0))
								{
									foreach (var bscTileWzObject in tileType_Tile_Dict["bsc"])
									{
										var u = bscTileWzObject["u"]?.GetString();
										var bsc_no = bscTileWzObject["no"]?.GetInt() ?? 0;
										var bsc_x = bscTileWzObject["x"]?.GetInt() ?? 0;
										var bsc_y = bscTileWzObject["y"]?.GetInt() ?? 0;
										var bsc_TileIndexX = (int)Math.Round((double)(2.0 * (bsc_x + offsetX + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
										var bsc_TileIndexY = (int)Math.Round((double)(-2.0 * (bsc_y + offsetY + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);

										var left_exist = false;
										if (tileType_Tile_Dict.TryGetValue("enV0", out var wzObjects))
										{
											foreach (var tileWzObject in tileType_Tile_Dict["enV0"])//左
											{
												var left_x = tileWzObject["x"]?.GetInt() ?? 0;
												var left_y = tileWzObject["y"]?.GetInt() ?? 0;
												if (Math.Abs(bsc_y - left_y) < 0.1f)//必须是同一个高度的左侧tile
												{
													if (left_x <= bsc_x)//存在一个左侧tile在bsc的左边 就说明有
													{
														left_exist = true;
														break;
													}
												}
											}
										}

										if (left_exist == false)
										{
											var left_TileIndexX = bsc_TileIndexX - 1;
											missingTileInfos.Add((TileTypeNameToInt("enV0"), left_TileIndexX, bsc_TileIndexY, 0));
										}

										var right_exist = false;
										if (tileType_Tile_Dict.TryGetValue("enV1", out var wzObjects1))
										{
											foreach (var item in tileType_Tile_Dict["enV1"])//右
											{
												var left_x = item["x"]?.GetInt() ?? 0;
												var left_y = item["y"]?.GetInt() ?? 0;
												if (Math.Abs(bsc_y - left_y) < 0.1f)//必须是同一个高度的左侧tile
												{
													if (left_x - bsc_width >= bsc_x)//存在一个右侧tile在bsc的右边 就说明有
													{
														right_exist = true;
														break;
													}
												}
											}
										}

										if (right_exist == false)
										{
											var right_TileIndexX = bsc_TileIndexX + 1;
											missingTileInfos.Add((TileTypeNameToInt("enV1"), right_TileIndexX, bsc_TileIndexY, 0));
										}

									}
								}
									

								foreach (var Map0_000000000_0_tile_0 in Map0_000000000_0_tile.properties)
								{

									var u = Map0_000000000_0_tile_0["u"]?.GetString();
									var no = Map0_000000000_0_tile_0["no"]?.GetInt();
									var x = Map0_000000000_0_tile_0["x"]?.GetInt();
									var y = Map0_000000000_0_tile_0["y"]?.GetInt();

									var tile = tileImg[u]?[no.ToString()] as WzCanvasProperty;
									if (Map0_000000000_0_tile_0.FullPath != "Map.wz\\Map\\Map0\\001000000.img\\1\\tile\\1")
									{
										//continue;
									}
									var bitmap = tile.GetLinkedWzCanvasBitmap();
									var width = bitmap.Width;
									var height = bitmap.Height;
									var origin = tile.GetCanvasOriginPosition();
									var tileIndexX = 0;
									var tileIndexY = 0;

									if (tile == null) continue;


									switch (u)
									{
										case "bsc":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "slLU":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX - bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY - bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "slRU":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY - bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "slLD":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX - bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "slRD":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "enH0":
										case "enH1":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "edU":
										case "edD":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "enV0":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										case "enV1":
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY + bsc_height) / bsc_height), MidpointRounding.AwayFromZero);
											break;
										default:
											tileIndexX = (int)Math.Round((double)(2.0 * (x + offsetX - origin.X + bsc_width / 2.0f) / bsc_width), MidpointRounding.AwayFromZero);
											tileIndexY = (int)Math.Round((double)(-2.0 * (y + offsetY - origin.Y + bsc_height / 2.0f) / bsc_height), MidpointRounding.AwayFromZero);
											break;
									}

									Console.WriteLine($"FullPath:{Map0_000000000_0_tile_0.FullPath,-50},u:{u,5},x:{x,5},y:{y,5},tileIndexX:{tileIndexX,5}.tileIndexY:{tileIndexY,5}");
									JObject newItem = new JObject
									{
										["type"] = TileTypeNameToInt(u),
										["position"] = new JObject { ["x"] = tileIndexX, ["y"] = tileIndexY },
										["tileIndex"] = no
									};
									joTiles.Add(newItem);
								}
							}

							if (Map0_000000000_0["obj"] is WzSubProperty Map0_000000000_0_obj)
							{
								foreach (var Map0_000000000_0_obj_0 in Map0_000000000_0_obj.properties)
								{
									var oS = Map0_000000000_0_obj_0["oS"]?.GetString();
									if (oS is "connect" || oS is "connectGL")
									{
										var l0 = Map0_000000000_0_obj_0["l0"]?.GetString();
										var l1 = Map0_000000000_0_obj_0["l1"]?.GetString();
										var l2 = Map0_000000000_0_obj_0["l2"]?.GetString();
										var x = Map0_000000000_0_obj_0["x"]?.GetInt();
										var y = Map0_000000000_0_obj_0["y"]?.GetInt();
										var z = Map0_000000000_0_obj_0["z"]?.GetInt();
										var zM = Map0_000000000_0_obj_0["zM"]?.GetInt();
										var f = Map0_000000000_0_obj_0["f"]?.GetInt();


									}
								}
							}
						}

						foreach (var missingTileInfo in missingTileInfos)
						{
							JObject newItem = new JObject
							{
								["type"] = missingTileInfo.Item1,
								["position"] = new JObject { ["x"] = missingTileInfo.Item2, ["y"] = missingTileInfo.Item3 },
								["tileIndex"] = missingTileInfo.Item4
							};
							joTiles.Add(newItem);
						}
					}

					if (Map0_000000000["portal"] is WzSubProperty Map0_000000000_portal)
					{
						foreach (var Map0_000000000_portal_0 in Map0_000000000_portal.WzProperties)
						{
							if (Map0_000000000_portal_0["pt"]?.GetInt() != 0)
							{
								wz_portals.Add(Map0_000000000_portal_0);
							}
							else
							{
								wz_spawnPoints.Add(Map0_000000000_portal_0);
							}
						}


						var MapWzPortalCount = wz_portals.Count;
						var ja_Entities = jsonObject["ContentProto"]["Entities"] as JArray;
						jo_portals.Clear();
						foreach (var ja_Entity in ja_Entities)
						{
							//Console.WriteLine(ja_Entity["path"]);
							if (ja_Entity["path"]?.ToString()?.Contains("portal") ?? false)
							{
								jo_portals.Add(ja_Entity);
							}
						}
						var presetJsonFilePortalCount = jo_portals.Count;
						for (int i = presetJsonFilePortalCount - 1; i >= MapWzPortalCount; i--)
						{
							ja_Entities.Remove(jo_portals[i]);
							jo_portals.RemoveAt(i);
						}

						for (int i = 0; i < MapWzPortalCount; i++)
						{
							var Map0_000000000_portal_0 = wz_portals[i];
							var x = Map0_000000000_portal_0["x"]?.GetInt() ?? 0;
							var y = Map0_000000000_portal_0["y"]?.GetInt() ?? 0;

							var targetPortalName = Map0_000000000_portal_0["tn"]?.GetString() ?? "";
							var targetMapId = Map0_000000000_portal_0["tm"]?.GetInt() ?? 100000000;
							var portalType = Map0_000000000_portal_0["pt"]?.GetInt() ?? 2;
							var portalName = Map0_000000000_portal_0["pn"]?.GetString() ?? "";

							var jo_portal = jo_portals[i];
							jo_portal["jsonString"]["@components"][0]["Position"]["x"] = (x + global_offsetX - 41) / 100.0f;
							jo_portal["jsonString"]["@components"][0]["Position"]["y"] = (y + global_offsetY) / -100.0f;
							jo_portal["jsonString"]["@components"][3]["TargetMapId"] = targetMapId;
							jo_portal["jsonString"]["@components"][3]["TargetPortalName"] = targetPortalName;
							jo_portal["jsonString"]["@components"][3]["PortalType"] = portalType;
							jo_portal["jsonString"]["@components"][3]["PortalName"] = portalName;

							if (portalType != 2)
							{
								jo_portal["jsonString"]["@components"][1]["SpriteRUID"] = "";
							}

							if (string.IsNullOrEmpty(portalName))
							{
								portalName = $"Portal_{i}";
							}
							else
							{
								portalName = $"Portal_{portalName}";
							}

							var oldName = jo_portal["jsonString"]["name"].ToString();
							jo_portal["jsonString"]["name"] = portalName;

							var oldPath = jo_portal["path"].ToString();
							var newPath = oldPath.Replace(oldName, portalName);
							jo_portal["path"] = newPath;

							var oldJsonStringPath = jo_portal["jsonString"]["path"].ToString();
							var newJsonStringPath = oldJsonStringPath.Replace(oldName, portalName);
							jo_portal["jsonString"]["path"] = newJsonStringPath;


						}



						var MapWzSpawnLocationCount = wz_spawnPoints.Count;
						jo_spawnLocations.Clear();
						foreach (var ja_Entity in ja_Entities)
						{
							//Console.WriteLine(ja_Entity["path"]);
							if (ja_Entity["path"]?.ToString()?.Contains("SpawnLocation") ?? false)
							{
								jo_spawnLocations.Add(ja_Entity);
							}
						}
						var presetJsonFileSpawnLocationCount = jo_spawnLocations.Count;
						for (int i = presetJsonFileSpawnLocationCount - 1; i >= MapWzSpawnLocationCount; i--)
						{
							ja_Entities.Remove(jo_spawnLocations[i]);
							jo_spawnLocations.RemoveAt(i);
						}

						for (int i = 0; i < MapWzSpawnLocationCount; i++)
						{
							var Map0_000000000_portal_0 = wz_spawnPoints[i];
							var x = Map0_000000000_portal_0["x"]?.GetInt() ?? 0;
							var y = Map0_000000000_portal_0["y"]?.GetInt() ?? 0;

							var jo_spawnLocation = jo_spawnLocations[i];
							jo_spawnLocation["jsonString"]["@components"][0]["Position"]["x"] = (x + global_offsetX - 41) / 100.0f;
							jo_spawnLocation["jsonString"]["@components"][0]["Position"]["y"] = (y - global_offsetY) / -100.0f;

							var oldName = jo_spawnLocation["jsonString"]["name"].ToString();
							jo_spawnLocation["jsonString"]["name"] = "SpawnLocation";

							var oldPath = jo_spawnLocation["path"].ToString();
							var newPath = oldPath.Replace(oldName, "SpawnLocation");
							jo_spawnLocation["path"] = newPath;

							var oldJsonStringPath = jo_spawnLocation["jsonString"]["path"].ToString();
							var newJsonStringPath = oldJsonStringPath.Replace(oldName, "SpawnLocation");
							jo_spawnLocation["jsonString"]["path"] = newJsonStringPath;
						}
					}

					if (Map0_000000000["ladderRope"] is WzSubProperty Map0_000000000_ladderRope)
					{

					}

					string updatedJsonContent = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
					File.WriteAllText($"N:\\mswProjects\\Start4\\map\\{mapid}.map", updatedJsonContent);
				}
			}



			/*// Modify the JSON data
			jsonObject["name"] = "Bob";  // Modify the "name" field
			jsonObject["active"] = false;  // Modify the "active" field
			jsonObject["details"]["email"] = "bob@example.com";  // Modify nested "email" field

			// Save modified JSON back to a file
			string modifiedJsonPath = "modified_data.json";
			File.WriteAllText(modifiedJsonPath, jsonObject.ToString(Formatting.Indented));

			Console.WriteLine("JSON file modified and saved successfully.");*/
		}

		private int TileTypeNameToInt(string typeName)
		{
			switch (typeName)
			{
				case "slLU":
					return 1;
				case "slRU":
					return 3;
				case "slLD":
					return 2;
				case "slRD":
					return 4;
				case "bsc":
					return 5;
				case "enH1":
					return 7;
				case "enH0":
					return 9;
				case "enV0":
				case "enV1":
					return 8;
				case "edD":
					return 0;
				case "edU":
					return 11;
				default:
					return 5;
			}

		}
	}

	public static class Settings
	{
		/// <summary>
		/// wz文件夹
		/// </summary>
		public static string Path { set; get; }

		/// <summary>
		/// 导出路径
		/// </summary>
		public static string ExportPath { set; get; }

		/// <summary>
		/// 游戏版本 
		/// </summary>
		public static short GameVersion { set; get; } = 85;

		/// <summary>
		/// 游戏区域版本
		/// </summary>
		public static WzMapleVersion Version { set; get; } = WzMapleVersion.EMS;

		/// <summary>
		/// 是否带扩展名 (.mp3 和 .png)
		/// </summary>
		public static bool Extensions { set; get; } = false;

		/// <summary>
		/// 是否输出全路径
		/// </summary>
		public static bool FullPath { set; get; } = false;

		/// <summary>
		/// 保留UOL
		/// </summary>
		public static bool UseUol { set; get; } = false;

		/// <summary>
		/// 是否只导出json
		/// </summary>
		public static bool OnlyJSON { set; get; } = false;

		/// <summary>
		/// 是否导出到一起
		/// </summary>
		public static bool ResAll { set; get; } = false;
	}
}