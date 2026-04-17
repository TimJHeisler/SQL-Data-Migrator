using System.Windows.Forms.Design;

namespace SQLDataMigrator
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            //dark mode
            Application.SetColorMode(SystemColorMode.Dark);
            Application.Run(new Form1());
        }
    }
}
