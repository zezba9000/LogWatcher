using LogWatcher.XML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace LogWatcher
{
	namespace XML
	{
		public class CustomErrorCodes
		{
			[XmlElement("ErrorCode")] public List<string> errorCodes = new List<string>();
		}

		[XmlRoot("AppSettings")]
		public class AppSettings
		{
			[XmlAttribute("WinMaximized")] public bool winMaximized = false;
			[XmlAttribute("WinX")] public int winX = -1;
			[XmlAttribute("WinY")] public int winY = -1;
			[XmlAttribute("WinWidth")] public int winWidth = -1;
			[XmlAttribute("WinHeight")] public int winHeight = -1;
			[XmlElement("Tab")] public List<string> tabs;
		}
	}
	
	public static class Settings
	{
		private static readonly string appSettingsFilename;

		static Settings()
		{
			var dataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			appSettingsFilename = Path.Combine(dataFolder, "LogWatcher", "Settings.xml");
		}

		public static AppSettings Load()
		{
			if (!File.Exists(appSettingsFilename))
			{
				var settings = new AppSettings();
				Save(settings);
				return settings;
			}

			try
			{
				var xml = new XmlSerializer(typeof(AppSettings));
				using (var stream = new FileStream(appSettingsFilename, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					return (AppSettings)xml.Deserialize(stream);
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(MainWindow.singleton, "Load Settings Error: " + e.Message);
				return new AppSettings();
			}
		}

		public static bool Save(AppSettings settings)
		{
			string path = Path.GetDirectoryName(appSettingsFilename);
			if (!Directory.Exists(path)) Directory.CreateDirectory(path);

			try
			{
				var xml = new XmlSerializer(typeof(AppSettings));
				using (var stream = new FileStream(appSettingsFilename, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					xml.Serialize(stream, settings);
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(MainWindow.singleton, "Save Settings Error: " + e.Message);
				return false;
			}

			return true;
		}
	}
}
