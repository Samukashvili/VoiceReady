using System.Windows.Forms;

namespace VoiceReady.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, eventArgs) => ShowStartupError(eventArgs.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    ShowStartupError(exception);
                }
            };

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        MessageBox.Show(
            exception.Message,
            "VoiceReady error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
