using Velopack;
using Velopack.Sources;

namespace Updater
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            VelopackApp.Build().Run();

            var mgr = new UpdateManager(new GithubSource("https://github.com/id-pm/test", null, false));

            var newVersion = mgr.CheckForUpdates();

            if (newVersion != null)
            {
                
                mgr.DownloadUpdates(newVersion);

            }

        }
    }
}