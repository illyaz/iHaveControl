namespace iHaveControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public bool Updated = false;
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length == 2)
            {
                switch (e.Args[0])
                {
                    case "--update":
                        try
                        {
                            var sp = e.Args[1].Split(',', 2);
                            var p = Process.GetProcessById(int.Parse(sp[0]));
                            p.Kill();
                            p.WaitForExit();
                            File.Copy(Environment.ProcessPath!, sp[1], true);
                            Process.Start(new ProcessStartInfo(sp[1], $"--updated {Environment.ProcessId}")
                            {
                                WorkingDirectory = Path.GetDirectoryName(sp[1]),
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"{ex.Message}\nStackTrace:\n{ex.StackTrace}", "iHaveControl - Update failed",
                                MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }
                        finally
                        {
                            Shutdown();
                        }
                        break;

                    case "--updated":
                        Updated = true;
                        break;
                    default:
                        Shutdown();
                        break;
                }
            }

            base.OnStartup(e);
        }
    }
}
