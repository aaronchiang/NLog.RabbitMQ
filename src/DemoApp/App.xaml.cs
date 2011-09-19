using System.Windows;
using NLog;

namespace DemoApp
{
	/// <summary>
	/// 	Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);

			var allTargets = LogManager.Configuration.AllTargets;

			foreach (var target in allTargets)
				target.Dispose();
		}
	}
}