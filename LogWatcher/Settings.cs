using CredentialManagement;
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
		/*public class CustomErrorCodes
		{
			[XmlElement("ErrorCode")] public List<string> errorCodes = new List<string>();
		}*/

		[XmlRoot("AppSettings")]
		public class AppSettings
		{
			// window placement
			[XmlAttribute("WinMaximized")] public bool winMaximized = false;
			[XmlAttribute("WinX")] public int winX = -1;
			[XmlAttribute("WinY")] public int winY = -1;
			[XmlAttribute("WinWidth")] public int winWidth = -1;
			[XmlAttribute("WinHeight")] public int winHeight = -1;

			// email
			[XmlAttribute("EmailTo")] public string emailTo;
			[XmlAttribute("EmailFrom")] public string emailFrom;
			[XmlAttribute("EmailSmtpHost")] public string emailSmtpHost;
			[XmlAttribute("EmailSmtpPort")] public int emailSmtpPort;

			// tabs
			[XmlElement("Tab")] public List<string> tabs;
		}
	}
	
	public static class Settings
	{
		private static readonly string appSettingsFilename;
		private const string emailCredentialTarget = "LogWatcherCreds";

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

		public static bool SetWindowsEmailCredentials(string username, string password)
		{
			using (var creds = new Credential())
			{
				creds.Target = emailCredentialTarget;
				creds.Username = username;
				creds.Password = password;
				creds.PersistanceType = PersistanceType.LocalComputer;
				return creds.Save();
			}
		}

		public static bool GetWindowsEmailCredentials(out string username, out string password)
		{
			using (var creds = new Credential())
			{
				creds.Target = emailCredentialTarget;
				creds.PersistanceType = PersistanceType.LocalComputer;
				if (creds.Load())
				{
					username = creds.Username;
					password = creds.Password;
					return true;
				}
				else
				{
					username = null;
					password = null;
					return false;
				}
			}
		}

		public static bool RemoveWindowsEmailCredentials()
		{
			using (var creds = new Credential())
			{
				creds.Target = emailCredentialTarget;
				return creds.Delete();
			}
		}
	}
}
