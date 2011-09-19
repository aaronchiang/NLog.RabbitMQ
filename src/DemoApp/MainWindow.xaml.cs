using System.Windows;
using System.Windows.Controls;
using NLog;

namespace DemoApp
{
	/// <summary>
	/// 	Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var b = (Button) sender;

			switch (b.Name)
			{
				case "Trace":
					logger.Trace("This is a sample trace message");
					break;
				case "Debug":
					logger.Debug("This is a sample debug message");
					break;
				case "Info":
					logger.Info("This is a sample info message");
					break;
				case "Warn":
					logger.Warn("This is a sample warn message");
					break;
				case "Error":
					logger.Error("This is a sample error message");
					break;
				case "Fatal":
					logger.Fatal("This is a sample fatal message");
					break;
			}
		}

		protected override void OnClosed(System.EventArgs e)
		{
			base.OnClosed(e);
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			base.OnClosing(e);
		}
	}
}