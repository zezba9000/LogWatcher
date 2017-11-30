using System;
using System.Collections.Generic;
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
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		FileSystemWatcher watcher;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void openButton_Click(object sender, RoutedEventArgs e)
		{
			const string filename = @"D:\Downloads\test.txt";
			textBlock.Text = File.ReadAllText(filename);

			watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(filename), "*.txt");
			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);
			watcher.EnableRaisingEvents = true;
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			Console.WriteLine("File: " +  e.FullPath + " " + e.ChangeType);
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
		}
	}
}
