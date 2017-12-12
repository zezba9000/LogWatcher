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

namespace LogWatcher
{
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
				var watcher = (FileSystemWatcher)tab.Tag;
				if (watcher != null)
				{
					watcher.Dispose();
					watcher = null;
				}
			}

			base.OnClosing(e);
		}

		private string ReadAllText(string filename)
		{
			using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
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
				try
				{
					textBlock.Text = ReadAllText(filename);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "ERROR");
					return;
				}

				// add file watcher
				var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(filename), System.IO.Path.GetFileName(filename));
				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
				watcher.Changed += new FileSystemEventHandler(OnChanged);
				watcher.Created += new FileSystemEventHandler(OnChanged);
				watcher.Deleted += new FileSystemEventHandler(OnChanged);
				watcher.Renamed += new RenamedEventHandler(OnRenamed);
				watcher.EnableRaisingEvents = true;
				tab.Tag = watcher;

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
			var watcher = (FileSystemWatcher)tab.Tag;
			
			try
			{
				string filename = System.IO.Path.Combine(watcher.Path, watcher.Filter);
				textBlock.Text = ReadAllText(filename);
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
			var watcher = (FileSystemWatcher)tab.Tag;
			watcher.Dispose();
			fileTabControl.Items.Remove(tab);
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			Dispatcher.Invoke(delegate ()
			{
				foreach (TabItem tab in fileTabControl.Items)
				{
					if (tab.Tag == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBlock = (TextBlock)scrollBar.Content;
						var watcher = (FileSystemWatcher)tab.Tag;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Deleted) != 0)
							{
								textBlock.Text = "<< DELETED >>";
							}

							if ((e.ChangeType & WatcherChangeTypes.Changed) != 0 || (e.ChangeType & WatcherChangeTypes.Created) != 0)
							{
								if (File.Exists(e.FullPath))
								{
									textBlock.Text = ReadAllText(e.FullPath);
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
					if (tab.Tag == sender)
					{
						var scrollBar = (ScrollViewer)tab.Content;
						var textBlock = (TextBlock)scrollBar.Content;
						var watcher = (FileSystemWatcher)tab.Tag;

						try
						{
							if ((e.ChangeType & WatcherChangeTypes.Renamed) != 0)
							{
								string newFileName = e.FullPath;
								watcher.Path = System.IO.Path.GetDirectoryName(newFileName);
								watcher.Filter = System.IO.Path.GetFileName(newFileName);
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
