using System.Windows.Forms;

namespace VoiceReady.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
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

            // --voice-test: listen and parse commands without attaching to Ready or Not or sending input.
            var voiceTestMode = args.Any(arg => arg.Equals("--voice-test", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(voiceTestMode));
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
