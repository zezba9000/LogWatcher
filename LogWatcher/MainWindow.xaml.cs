using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LogWatcher
{
	class WatchedFile : IDisposable
	{
		// IO
		public string filename;
		public FileSystemWatcher watcher;
		public FileStream stream;
		public StreamReader reader;
		public long lastPosition, lastStreamLength;

		// UI
		public ScrollViewer scrollBar;
		public TextBox textBox;

		public void Dispose()
		{
			if (watcher != null)
			{
				watcher.Dispose();
				watcher = null;
			}

			DisposeStream();
		}

		public void DisposeStream()
		{
			if (reader != null)
			{
				reader.Dispose();
				reader = null;
			}

			if (stream != null)
			{
				stream.Dispose();
				stream = null;
			}
		}
	}

	public partial class MainWindow : Window
	{
		public static MainWindow singleton;
		private XML.AppSettings settings;
		private DispatcherTimer timer;

		public MainWindow()
		{
			singleton = this;
			InitializeComponent();

			// load app settings
			settings = Settings.Load();

			// set arg setting overrides
			bool settingOverrideFound = false;
			string emailUsername = null, emailPassword = null;
			foreach (string arg in Environment.GetCommandLineArgs())
			{
				var parts = arg.Split('=');
				if (parts == null || parts.Length != 2) continue;

				settingOverrideFound = true;
				switch (parts[0].ToLower())
				{
					case "-emailto": settings.emailTo = parts[1]; break;
					case "-emailfrom": settings.emailFrom = parts[1]; break;
					case "-emailsmtphost": settings.emailSmtpHost = parts[1]; break;
					case "-emailsmtpport": int.TryParse(parts[1], out settings.emailSmtpPort); break;

					case "-emailusername": emailUsername = parts[1]; break;
					case "-emailpassword": emailPassword = parts[1]; break;

					case "-tab":
						if (settings.tabs == null) settings.tabs = new List<string>();
						settings.tabs.Add(parts[1]);
						break;
				}
			}

			if (!string.IsNullOrEmpty(emailUsername) && !string.IsNullOrEmpty(emailUsername))
			{
				Settings.SetWindowsEmailCredentials(emailUsername, emailPassword);
			}

			if (settingOverrideFound)
			{
				Settings.Save(settings);
				Close();
				return;
			}

			// apply settings
			if (settings.winX != -1)
			{
				Left = settings.winX;
				Top = settings.winY;
				Width = settings.winWidth;
				Height = settings.winHeight;
				if (settings.winMaximized) WindowState = WindowState.Maximized;
			}

			emailButton.IsEnabled = !string.IsNullOrEmpty(settings.emailTo);

			if (settings.tabs == null) settings.tabs = new List<string>();
			foreach (string tab in settings.tabs) AddTab(tab);

			// start watch timer
			timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, CheckFileSizeCallback, Dispatcher);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			// dispose and add open tabs to settings
			timer.Stop();
			settings.tabs.Clear();
			foreach (TabItem tab in fileTabControl.Items)
			{
				var watchedFile = (WatchedFile)tab.Tag;
				if (!settings.tabs.Contains(watchedFile.filename)) settings.tabs.Add(watchedFile.filename);
				watchedFile.Dispose();
			}

			// save settings
			settings.winX = (int)Left;
			settings.winY = (int)Top;
			settings.winWidth = (int)Width;
			settings.winHeight = (int)Height;
			settings.winMaximized = WindowState == WindowState.Maximized;
			Settings.Save(settings);

			base.OnClosing(e);
		}

		private void CheckFileSizeCallback(object sender, EventArgs e)
		{
			foreach (TabItem tab in fileTabControl.Items)
			{
				var watchedFile = (WatchedFile)tab.Tag;
				var scrollBar = (ScrollViewer)tab.Content;
				var textBox = (TextBox)scrollBar.Content;

				watchedFile.stream.Flush();
				long length = watchedFile.stream.Length;
				if (length != watchedFile.lastStreamLength)
				{
					watchedFile.lastStreamLength = length;

					string result = ReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition, ref watchedFile.lastStreamLength, out bool didRefresh);
					if (didRefresh) textBox.Text = result;
					else textBox.AppendText(result);
					if (autoScroll.IsChecked == true) scrollBar.ScrollToEnd();
				}
			}
		}

		private string OpenFile(string filename, out FileStream stream, out StreamReader reader, out long lastPosition, out long lastStreamLength)
		{
			stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			reader = new StreamReader(stream);
			lastStreamLength = stream.Length;
			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private string ReadFile(FileStream stream, StreamReader reader, ref long lastPosition, ref long lastStreamLength, out bool didRefresh)
		{
			didRefresh = false;
			long streamLength = stream.Length;
			if (streamLength < lastPosition || stream.Position < lastPosition)
			{
				lastStreamLength = 0;
				stream.Position = 0;
				didRefresh = true;
			}
			else
			{
				lastStreamLength = streamLength;
			}

			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private string RefreshReadFile(FileStream stream, StreamReader reader, ref long lastPosition, ref long lastStreamLength)
		{
			stream.Position = 0;
			lastStreamLength = stream.Length;
			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private void AddTab(string filename)
		{
			// create tab
			var tab = new TabItem();
			tab.ContextMenu = new ContextMenu();
			tab.Header = Path.GetFileName(filename);

			// create text block
			var scrollBar = new ScrollViewer();
			var textBox = new TextBox();
			textBox.IsReadOnly = true;
			scrollBar.Content = textBox;
			scrollBar.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
			tab.Content = scrollBar;

			// add close context menu
			var closeMenuItem = new MenuItem();
			closeMenuItem.Tag = tab;
			closeMenuItem.Header = "Close";
			closeMenuItem.Click += CloseMenuItem_Click;
			tab.ContextMenu.Items.Add(closeMenuItem);

			// add close context menu
			var closeAllMenuItem = new MenuItem();
			closeAllMenuItem.Tag = tab;
			closeAllMenuItem.Header = "Close All";
			closeAllMenuItem.Click += CloseAllMenuItem_Click;
			tab.ContextMenu.Items.Add(closeAllMenuItem);

			// add refresh context menu
			var refreshMenuItem = new MenuItem();
			refreshMenuItem.Tag = tab;
			refreshMenuItem.Header = "Refresh";
			refreshMenuItem.Click += RefreshMenuItem_Click;
			tab.ContextMenu.Items.Add(refreshMenuItem);

			// add seperator
			tab.ContextMenu.Items.Add(new Separator());

			// add open context menu
			var openMenuItem = new MenuItem();
			openMenuItem.Tag = tab;
			openMenuItem.Header = "Open";
			openMenuItem.Click += OpenMenuItem_Click;
			tab.ContextMenu.Items.Add(openMenuItem);

			// add open location context menu
			var openLocationMenuItem = new MenuItem();
			openLocationMenuItem.Tag = tab;
			openLocationMenuItem.Header = "Open Location";
			openLocationMenuItem.Click += OpenLocationMenuItem_Click;
			tab.ContextMenu.Items.Add(openLocationMenuItem);

			// load file content into tab
			FileStream stream;
			StreamReader reader;
			long lastPosition, lastStreamLength;
			try
			{
				textBox.Text = OpenFile(filename, out stream, out reader, out lastPosition, out lastStreamLength);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "ERROR");
				return;
			}

			// create file watcher
			var watcher = new FileSystemWatcher(Path.GetDirectoryName(filename), Path.GetFileName(filename));
			watcher.NotifyFilter = NotifyFilters.FileName;// NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size;
			//watcher.Changed += new FileSystemEventHandler(OnChanged);
			//watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);
			watcher.Error += new ErrorEventHandler(OnError);
			watcher.EnableRaisingEvents = true;

			// create watched file tag
			var watchedFile = new WatchedFile()
			{
				// IO
				filename = filename,
				watcher = watcher,
				stream = stream,
				reader = reader,
				lastPosition = lastPosition,
				lastStreamLength = lastStreamLength,

				// UI
				scrollBar = scrollBar,
				textBox = textBox
			};

			tab.Tag = watchedFile;

			// finish
			fileTabControl.Items.Add(tab);
			fileTabControl.SelectedItem = tab;
			fileTabControl.Items.Refresh();
			if (autoScroll.IsChecked == true) scrollBar.ScrollToEnd();
		}

		private void openButton_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == true)
			{
				AddTab(dlg.FileName);
			}
		}

		private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var tab = (TabItem)menuItem.Tag;
			var scrollBar = (ScrollViewer)tab.Content;
			var textBox = (TextBox)scrollBar.Content;
			var watchedFile = (WatchedFile)tab.Tag;
			Process.Start("explorer.exe", watchedFile.filename);
		}

		private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var tab = (TabItem)menuItem.Tag;
			var scrollBar = (ScrollViewer)tab.Content;
			var textBox = (TextBox)scrollBar.Content;
			var watchedFile = (WatchedFile)tab.Tag;
			Process.Start("explorer.exe", "/select, " + watchedFile.filename);
		}

		private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var tab = (TabItem)menuItem.Tag;
			var scrollBar = (ScrollViewer)tab.Content;
			var textBox = (TextBox)scrollBar.Content;
			var watchedFile = (WatchedFile)tab.Tag;
			
			try
			{
				textBox.Text = RefreshReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition, ref watchedFile.lastStreamLength);
				if (autoScroll.IsChecked == true) scrollBar.ScrollToEnd();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "ERROR");
				return;
			}
		}

		private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var tab = (TabItem)menuItem.Tag;
			var watchedFile = (WatchedFile)tab.Tag;
			watchedFile.Dispose();
			fileTabControl.Items.Remove(tab);
		}

		private void CloseAllMenuItem_Click(object sender, RoutedEventArgs e)
		{
			foreach (TabItem tab in fileTabControl.Items)
			{
				var watchedFile = (WatchedFile)tab.Tag;
				watchedFile.Dispose();
			}

			fileTabControl.Items.Clear();
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			Dispatcher.InvokeAsync(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					if (watchedFile.watcher == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBox = (TextBox)scrollBar.Content;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Deleted) != 0)
							{
								textBox.Text = "<< DELETED >>";
							}

							if ((e.ChangeType & WatcherChangeTypes.Created) != 0)
							{
								textBox.Text = RefreshReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition, ref watchedFile.lastStreamLength);
								if (autoScroll.IsChecked == true) scrollBar.ScrollToEnd();
							}

							/*if ((e.ChangeType & WatcherChangeTypes.Changed) != 0)
							{
								if (File.Exists(e.FullPath))
								{
									string result = ReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition, ref watchedFile.lastStreamLength, out bool didRefresh);
									if (didRefresh) textBox.Text = result;
									else textBox.Text += result;
									if (autoScroll.IsChecked == true) scrollBar.ScrollToEnd();
								}
							}*/
						}
						catch (Exception ex)
						{
							textBox.Text = "ERROR: " + ex.Message;
						}

						return;
					}
				}
			});
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			Dispatcher.InvokeAsync(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					if (watchedFile.watcher == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBox = (TextBox)scrollBar.Content;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Renamed) != 0)
							{
								string newFileName = e.FullPath;
								watchedFile.filename = newFileName;
								watchedFile.watcher.Path = Path.GetDirectoryName(newFileName);
								watchedFile.watcher.Filter = Path.GetFileName(newFileName);
								watchedFile.DisposeStream();
								textBox.Text = OpenFile(newFileName, out watchedFile.stream, out watchedFile.reader, out watchedFile.lastPosition, out watchedFile.lastStreamLength);
								tab.Header = Path.GetFileName(newFileName);
							}
						}
						catch (Exception ex)
						{
							textBox.Text = "ERROR: " + ex.Message;
						}

						return;
					}
				}
			});
		}

		private void OnError(object sender, ErrorEventArgs e)
		{
			Dispatcher.InvokeAsync(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					if (watchedFile.watcher == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBox = (TextBox)scrollBar.Content;

						var ex = e.GetException();
						if (ex != null) textBox.Text = "ERROR: " + ex.Message;
						else textBox.Text = "UNKNOWN ERROR";

						return;
					}
				}
			});
		}

		private void settingsButton_Click(object sender, RoutedEventArgs e)
		{
			var settingsWindow = new SettingsWindow(settings);
			settingsWindow.Owner = this;
			settingsWindow.ShowDialog();
			emailButton.IsEnabled = !string.IsNullOrEmpty(settings.emailTo);
		}

		private void emailButton_Click(object sender, RoutedEventArgs e)
		{
			// get stmp user / pass
			if (!Settings.GetWindowsEmailCredentials(out string username, out string password))
			{
				MessageBox.Show(this, "Failed to get stmp user/pass", "Error");
				return;
			}

			try
			{
				// copy logs to temp path
				string tmpFolder = Path.Combine(Path.GetTempPath(), "LogWatcherLogs");
				if (!Directory.Exists(tmpFolder)) Directory.CreateDirectory(tmpFolder);
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					string dst = Path.Combine(tmpFolder, Path.GetFileName(watchedFile.filename));
					File.Copy(watchedFile.filename, dst, true);
				}

				// zip logs
				string zipFilePath = Path.Combine(Path.GetTempPath(), "LogWatcherLogs.zip");
				if (File.Exists(zipFilePath))
				{
					File.Delete(zipFilePath);
					System.Threading.Thread.Sleep(1000);
				}

				ZipFile.CreateFromDirectory(tmpFolder, zipFilePath);

				// setup sender
				var message = new MailMessage();
				message.To.Add(settings.emailTo);
				message.From = new MailAddress(settings.emailFrom);
				message.Subject = "LogWatcher Logs";
				var now = DateTime.Now;
				message.Body = string.Format("Logs sent on LOCAL '{0}' -- UTC '{1}'", now, now.ToUniversalTime());

				// add attachment
				var attachment = new Attachment(zipFilePath);
				message.Attachments.Add(attachment);

				var smtp = new SmtpClient(settings.emailSmtpHost, settings.emailSmtpPort);
				smtp.EnableSsl = true;
				smtp.Credentials = new NetworkCredential(username, password);
				smtp.Send(message);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Failed to email logs: " + ex.Message, "Error");
				return;
			}

			MessageBox.Show(this, "Logs sent!");
		}
	}
}
