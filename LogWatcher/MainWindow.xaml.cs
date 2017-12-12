﻿using Microsoft.Win32;
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

namespace LogWatcher
{
	class WatchedFile
	{
		// IO
		public string filename;
		public FileSystemWatcher watcher;
		public FileStream stream;
		public StreamReader reader;
		public long lastPosition;

		// UI
		public ScrollViewer scrollBar;
		public TextBlock textBlock;
	}

	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			foreach (TabItem tab in fileTabControl.Items)
			{
				var watchedFile = (WatchedFile)tab.Tag;
				if (watchedFile.watcher != null)
				{
					watchedFile.watcher.Dispose();
					watchedFile.watcher = null;
				}
			}

			base.OnClosing(e);
		}

		private string OpenFile(string filename, out FileStream stream, out StreamReader reader, out long lastPosition)
		{
			stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			reader = new StreamReader(stream);
			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private string ReadFile(FileStream stream, StreamReader reader, ref long lastPosition, out bool isRefresh)
		{
			isRefresh = false;
			if (stream.Length < lastPosition || stream.Position < lastPosition)
			{
				stream.Position = 0;
				isRefresh = true;
			}

			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private string RefreshReadFile(FileStream stream, StreamReader reader, ref long lastPosition)
		{
			stream.Position = 0;
			string result = reader.ReadToEnd();
			lastPosition = stream.Position;
			return result;
		}

		private void openButton_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == true)
			{
				string filename = dlg.FileName;

				// create tab
				var tab = new TabItem();
				tab.ContextMenu = new ContextMenu();
				tab.Header = System.IO.Path.GetFileName(filename);

				// create text block
				var scrollBar = new ScrollViewer();
				var textBlock = new TextBlock();
				scrollBar.Content = textBlock;
				tab.Content = scrollBar;

				// add close context menu
				var closeMenuItem = new MenuItem();
				closeMenuItem.Tag = tab;
				closeMenuItem.Header = "Close";
				closeMenuItem.Click += CloseMenuItem_Click;
				tab.ContextMenu.Items.Add(closeMenuItem);

				// add refresh context menu
				var refreshMenuItem = new MenuItem();
				refreshMenuItem.Tag = tab;
				refreshMenuItem.Header = "Refresh";
				refreshMenuItem.Click += RefreshMenuItem_Click;
				tab.ContextMenu.Items.Add(refreshMenuItem);

				// load file content into tab
				FileStream stream;
				StreamReader reader;
				long lastPosition;
				try
				{
					textBlock.Text = OpenFile(filename, out stream, out reader, out lastPosition);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "ERROR");
					return;
				}

				// add file watcher
				var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(filename), System.IO.Path.GetFileName(filename));
				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.CreationTime;
				watcher.Changed += new FileSystemEventHandler(OnChanged);
				watcher.Created += new FileSystemEventHandler(OnChanged);
				watcher.Deleted += new FileSystemEventHandler(OnChanged);
				watcher.Renamed += new RenamedEventHandler(OnRenamed);
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

					// UI
					scrollBar = scrollBar,
					textBlock = textBlock
				};

				tab.Tag = watchedFile;

				// finish
				fileTabControl.Items.Add(tab);
				fileTabControl.Items.Refresh();
				scrollBar.ScrollToEnd();
			}
		}

		private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var tab = (TabItem)menuItem.Tag;
			var scrollBar = (ScrollViewer)tab.Content;
			var textBlock = (TextBlock)scrollBar.Content;
			var watchedFile = (WatchedFile)tab.Tag;
			
			try
			{
				textBlock.Text = RefreshReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition);
				scrollBar.ScrollToEnd();
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
			watchedFile.watcher.Dispose();
			fileTabControl.Items.Remove(tab);
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			Dispatcher.Invoke(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					if (watchedFile.watcher == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBlock = (TextBlock)scrollBar.Content;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Deleted) != 0)
							{
								textBlock.Text = "<< DELETED >>";
							}

							if ((e.ChangeType & WatcherChangeTypes.Created) != 0)
							{
								textBlock.Text = RefreshReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition);
								scrollBar.ScrollToEnd();
							}

							if ((e.ChangeType & WatcherChangeTypes.Changed) != 0)
							{
								if (File.Exists(e.FullPath))
								{
									string result = ReadFile(watchedFile.stream, watchedFile.reader, ref watchedFile.lastPosition, out bool isRefresh);
									if (isRefresh) textBlock.Text = result;
									else textBlock.Text += result;
									scrollBar.ScrollToEnd();
								}
							}
						}
						catch (Exception ex)
						{
							textBlock.Text = "ERROR: " + ex.Message;
						}

						return;
					}
				}
			});
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			Dispatcher.Invoke(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					var watchedFile = (WatchedFile)tab.Tag;
					if (watchedFile.watcher == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBlock = (TextBlock)scrollBar.Content;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Renamed) != 0)
							{
								string newFileName = e.FullPath;
								watchedFile.watcher.Path = System.IO.Path.GetDirectoryName(newFileName);
								watchedFile.watcher.Filter = System.IO.Path.GetFileName(newFileName);
								tab.Header = System.IO.Path.GetFileName(newFileName);
							}
						}
						catch (Exception ex)
						{
							textBlock.Text = "ERROR: " + ex.Message;
						}

						return;
					}
				}
			});
		}
	}
}
