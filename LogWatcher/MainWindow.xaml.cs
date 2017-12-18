using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
			if (settings.winX != -1)
			{
				Left = settings.winX;
				Top = settings.winY;
				Width = settings.winWidth;
				Height = settings.winHeight;
				if (settings.winMaximized) WindowState = WindowState.Maximized;
			}

			if (settings.tabs == null) settings.tabs = new List<string>();
			foreach (string tab in settings.tabs) AddTab(tab);

			// start watch timer
			timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, CheckFileSizeCallback, Dispatcher);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			// dispose and add open tabs to settings
			timer.Stop();
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
			if (stream.Length < lastPosition || stream.Position < lastPosition)
			{
				stream.Position = 0;
				didRefresh = true;
			}

			lastStreamLength = stream.Length;
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
			tab.Header = System.IO.Path.GetFileName(filename);

			// create text block
			var scrollBar = new ScrollViewer();
			var textBox = new TextBox();
			scrollBar.Content = textBox;
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
			var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(filename), System.IO.Path.GetFileName(filename));
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
								watchedFile.watcher.Path = System.IO.Path.GetDirectoryName(newFileName);
								watchedFile.watcher.Filter = System.IO.Path.GetFileName(newFileName);
								watchedFile.DisposeStream();
								textBox.Text = OpenFile(newFileName, out watchedFile.stream, out watchedFile.reader, out watchedFile.lastPosition, out watchedFile.lastStreamLength);
								tab.Header = System.IO.Path.GetFileName(newFileName);
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
	}
}
