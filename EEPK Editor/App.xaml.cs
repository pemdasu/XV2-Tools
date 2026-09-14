using System;
using System.Threading.Tasks;
using System.Windows;
using LB_Common.Forms;
using Xv2CoreLib.Resource.App;

namespace EEPK_Organiser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            SettingsManager.Instance.CurrentApp = Xv2CoreLib.Resource.App.Application.EepkOrganiser;
#if !DEBUG
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#endif
        }

#if !DEBUG
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowException(e.Exception);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            ShowException(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowException(e.ExceptionObject as Exception);
        }

        private void ShowException(Exception ex)
        {
            //Create a detailed exception message to display in the rich text box of the message prompt.
            string richText = ex.Message;

            if (ex.InnerException != null)
                richText += $"\n\nInner Exception: {ex.InnerException?.Message}";

            richText += $"\n\nStack Trace:\n{ex.StackTrace}";

            MessagePromptResult result = MessagePrompt.Show("The program has encountered an exception with the following error message.",
                "Exception Thrown",
                MessagePromptButtons.OK, MessagePromptIcon.Error, richText, "OK", null, "Copy Message", null);

            if (result == MessagePromptResult.Negative)
            {
                Clipboard.SetText(ex.ToString());
            }
        }
#endif
    }
}
