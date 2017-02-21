using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Net;
using System.Web;
using CsQuery;

namespace ModuleOneProject
{
	class Program
	{
		private static string pathRootDir = "d:\\TempSite\\";
		private static string pathFileDir = "d:\\TempSite\\file\\";
		private static string uriString = "https://habrahabr.ru/";
		private static readonly HttpClient Client = new HttpClient();

		private class FileFullInfo
		{
			public object File { get; set; }

			public string FileName { get; set; }
		}

		static void Main(string[] args)
		{

			WorkingWithDirectory(pathRootDir);
			WorkingWithDirectory(pathFileDir);

			Task t = new Task(async() => await DownloadFileAsync(uriString));
			t.Start();

			Console.ReadLine();
			Client.Dispose();
		}

		private static async Task<FileFullInfo> DownloadFileAsync(string uriString)
		{
			Uri uri = NormalizeUri(uriString);
			try
			{
				using (HttpResponseMessage response = await Client.GetAsync(uri.AbsoluteUri))
				{
					WriteConsole(string.Format("Downloading {0}", uri.AbsoluteUri));
					if (response.StatusCode != HttpStatusCode.OK)
					{
						WriteConsole("HttpStatusCode != OK", true);
					}
					using (HttpContent content = response.Content)
					{
						var streamContent = await content.ReadAsStreamAsync();

						var result = GetFileNameAsync(streamContent, content.Headers.ContentType.MediaType);

						SaveFile(result);
						return result.Result;
					}
				}
			}
			catch (Exception e)
			{
				WriteConsole(e.Message, true);
			}
			return new FileFullInfo();
		}

		private static void SaveFile(Task<FileFullInfo> task)
		{
			string path = string.Format("{0}\\{1}", pathRootDir, task.Result.FileName);

			if (task.Result.File is Stream)
			{
				using (var fileStream = File.Create(string.Format("{0}\\{1}", pathRootDir, task.Result.FileName)))
				{
					Stream result = task.Result.File as Stream;
					result.Seek(0, SeekOrigin.Begin);
					result.CopyTo(fileStream);
				}
			}
			if (task.Result.File is CQ)
			{
				CQ result = task.Result.File as CQ;
				result.Save(path);
			}
		}

		private static string StreamToString(Stream stream)
		{
			stream.Position = 0;
			using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
			{
				return reader.ReadToEnd();
			}
		}

		public static Stream StringToStream(string src)
		{
			byte[] byteArray = Encoding.UTF8.GetBytes(src);
			return new MemoryStream(byteArray);
		}

		private static Uri NormalizeUri(string uri)
		{
			uri = HttpUtility.UrlDecode(uri);

			if (string.IsNullOrEmpty(uri)) throw new ArgumentException(string.Format("Uri {0} is not recognized", uri));

			if (!uri.StartsWith("http://") && !uri.StartsWith("https://"))
			{
				uri = uri.StartsWith("//") ? string.Format("http:{0}", uri) : string.Format("{0}{1}", uriString, uri);
			}
			return new Uri(uri);
		}

		private static async Task<CQ> ParseHTMLAsync(Stream resultStream)
		{
			var result = StreamToString(resultStream);
			var root = CQ.CreateDocument(result);
			var imgElements = root.Select("img");
			var linkElements = root.Select("link");
			var aElements = root.Select("a");

			List<Tuple<IDomElement, string>> elementsList =
				imgElements.Elements.Select(x => new Tuple<IDomElement, string>(x, "src")).ToList();
			elementsList.AddRange(linkElements.Elements.Select(x => new Tuple<IDomElement, string>(x, "href")));
			elementsList.AddRange(aElements.Elements.Select(x => new Tuple<IDomElement, string>(x, "href")));


			await Task.WhenAll(elementsList.Select(x =>
			{
				return Task.Run(async () =>
				{
					try
					{
						//await Task.Delay(random.Next(100, 3000)); //looks like some sites can consider such active work as dangerous behavior, let's add some times
						var src = x.Item1.Attributes[x.Item2];
						if (!string.IsNullOrEmpty(src))
						{
							var newVal = await DownloadFileAsync(src);
							//lock (ModifyLock)
							{
								x.Item1.SetAttribute(x.Item2, newVal.FileName);
							}
						}
					}
					catch (Exception e)
					{
						WriteConsole(e.Message, true);
					}
				});
			}));

			return root;
		}


		private static void WorkingWithDirectory(string path)
		{
			try
			{
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
					WriteConsole("Directory created");
				}
				else
				{
					DirectoryInfo di = new DirectoryInfo(path);
					foreach (FileInfo file in di.GetFiles())
					{
						file.Delete();
					}
					foreach (DirectoryInfo dir in di.GetDirectories())
					{
						dir.Delete(true);
					}

					WriteConsole("Directory cleared");
				}
			}
			catch (Exception ex)
			{
				WriteConsole("Error in working with directory", true);
			}
		}

		private static void WriteConsole(string text, bool error = false)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			if (error)
				Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(text);
			
		}

		//private static FileFullInfo GetFileFullInfo(object stream, string path)
		//{
		//	fileFullInfo.File = stream;
		//	fileFullInfo.FileName = path;
		//	return fileFullInfo;
		//}

		private static async Task<FileFullInfo> GetFileNameAsync(Stream stream, string type)
		{
			switch (type)
			{
				case "text/html":
					return new FileFullInfo() { File = await ParseHTMLAsync(stream), FileName = "index.html" };
				default:
					return new FileFullInfo(){File = stream, FileName = Guid.NewGuid().ToString()};

			}
		}
	}
}
