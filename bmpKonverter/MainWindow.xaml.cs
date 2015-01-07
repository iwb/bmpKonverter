using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;

namespace bmpKonverter
{
	/// <summary>
	///    Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private CancellationTokenSource _cts;
		private FileSystemWatcher _watcher;
		private int _filecount;
		private int _index;
		private DateTime _start;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void SearchInputBtn_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new VistaFolderBrowserDialog {Description = "Eingabepfad auswählen"};

			if (Directory.Exists(InputTxt.Text))
				dialog.SelectedPath = InputTxt.Text;
			if (dialog.ShowDialog(this) ?? false)
				InputTxt.Text = dialog.SelectedPath;
		}

		private void SearchOutputBtn_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new VistaFolderBrowserDialog {Description = "Ausgabepfad auswählen"};

			if (Directory.Exists(OutputTxt.Text))
				dialog.SelectedPath = OutputTxt.Text;
			if (dialog.ShowDialog(this) ?? false)
				OutputTxt.Text = dialog.SelectedPath;
		}

		private void InputTxt_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (Directory.Exists(InputTxt.Text))
			{
				_filecount = Directory.EnumerateFiles(InputTxt.Text, "*.bmp").Count();
				InputinfoLbl.Content = string.Format("{0} Dateien gefunden.", _filecount);
				StartBtn.IsEnabled = true;
			}
			else
				StartBtn.IsEnabled = false;
		}

		private void StartBtn_Click(object sender, RoutedEventArgs e)
		{
			if (_cts == null) // Berechnung starten
			{
				_index = 0;
				StatusPb.Value = 0;
				StatusPb.Maximum = _filecount;
				StartBtn.Content = "Abbrechen";
				WatchBtn.IsEnabled = false;
				EnableGUI(false);
				_cts = new CancellationTokenSource();
				_start = DateTime.Now;

				string inputdir = InputTxt.Text;
				if (!Directory.Exists(inputdir))
				{
					throw new DirectoryNotFoundException(
						"Source directory does not exist or could not be found: "
						+ inputdir);
				}

				string outputdir = OutputTxt.Text;
				if (!Directory.Exists(outputdir))
					Directory.CreateDirectory(outputdir);

				var options = new ParallelOptions();
				options.MaxDegreeOfParallelism = 2;
				options.CancellationToken = _cts.Token;

				var task = Task.Factory.StartNew(() =>
				{
					var files = Directory.EnumerateFiles(inputdir, "*.bmp");
					Parallel.ForEach(files, options, filepath =>
					{
						string filename = Path.GetFileNameWithoutExtension(filepath);
						string dest = Path.Combine(outputdir, filename + ".png");
						if (!File.Exists(dest))
							Convert(filepath, dest);
					});
				}, _cts.Token);
				task.ContinueWith(t => { Dispatcher.BeginInvoke(new Action<Task>(ResetGUI), t); });
			}
			else
				_cts.Cancel();
		}

		private void EnableGUI(bool value)
		{
			OutputTxt.IsEnabled = value;
			InputTxt.IsEnabled = value;
			SearchInputBtn.IsEnabled = value;
			SearchOutputBtn.IsEnabled = value;
		}

		private void ResetGUI(Task t)
		{
			EnableGUI(true);
			WatchBtn.IsEnabled = true;
			StartBtn.Content = "Start";
			WatchBtn.Content = "Überwachen";
			_cts = null;
			StatusPb.Value = 0;
			_index = 0;
			var seconds = (DateTime.Now - _start).TotalSeconds;
			if (t.Status == TaskStatus.RanToCompletion)
			{
				StatusLbl.Content = string.Format("{0} Dateien wurden erfolgreich konvertiert. ({1:0.00 Bilder/s})", _filecount,
					_filecount / seconds);
			}
			else
				StatusLbl.Content = string.Format("Es ist ein Fehler aufgetreten.");
		}

		private void UpdateGUI()
		{
			StatusLbl.Content = string.Format("{0} von {1} Dateien verarbeitet.", ++_index, _filecount);
			StatusPb.Value = _index;
		}

		private void Convert(string source, string destination)
		{
			Bitmap bmp = new Bitmap(source);
			bmp.Save(destination, ImageFormat.Png);
			bmp.Dispose();

			Dispatcher.BeginInvoke((Action)UpdateGUI);
		}

		private void WatchBtn_Click(object sender, RoutedEventArgs e)
		{
			if (_watcher == null)
			{
				WatchBtn.Content = "Anhalten";
				StartBtn.IsEnabled = false;
				EnableGUI(false);
				string outputdir = OutputTxt.Text;

				_watcher = new FileSystemWatcher
				{
					Filter = "*.bmp",
					IncludeSubdirectories = true,
					Path = InputTxt.Text
				};
				_watcher.Created += (send, eargs) => { BmpCreatedCallback(outputdir, eargs); };
				_watcher.Renamed += (send, eargs) => { BmpCreatedCallback(outputdir, eargs); };
				_watcher.EnableRaisingEvents = true;
			}
			else
			{
				_watcher.EnableRaisingEvents = false;
				_watcher.Dispose();
				_watcher = null;

				WatchBtn.Content = "Überwachen";
				EnableGUI(true);
				StartBtn.IsEnabled = true;
			}
		}

		private void BmpCreatedCallback(string outputdir, FileSystemEventArgs e)
		{
			string name = Path.ChangeExtension(e.Name, ".png");
			string outpath = Path.Combine(outputdir, name);
			var outdir = Path.GetDirectoryName(outpath);
			if (outdir != null)
				Directory.CreateDirectory(outdir);
			Task.Factory.StartNew(() => { if (WaitForFile(e.FullPath, 20)) Convert(e.FullPath, outpath); });
		}

		private bool WaitForFile(string filePath, int timeout)
		{
			int readAttempt = 0;
			while (readAttempt < timeout)
			{
				readAttempt++;
				try
				{
					using (new StreamReader(filePath))
					{
						return true;
					}
				}
				catch
				{
					Thread.Sleep(500);
				}
			}
			return false;
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			char[] splitter = {'\\', '/'};
			string[] dirs = InputTxt.Text.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
			if (dirs.Length > 1)
			{
				var zahl = int.Parse(dirs[dirs.Length - 1]) + 1;
				dirs[dirs.Length - 1] = zahl.ToString();
				InputTxt.Text = string.Join("\\", dirs);

				dirs = OutputTxt.Text.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
				dirs[dirs.Length - 1] = zahl.ToString();
				OutputTxt.Text = string.Join("\\", dirs);
			}
		}
	}
}