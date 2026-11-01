namespace Mh2TexEditor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" }))
        {
            try
            {
                using var handle = NativeMethods.Open(Path.Combine(AppContext.BaseDirectory, "__missing_self_test__.tex"));
                return handle.IsInvalid && NativeMethods.LastError().Length > 0 ? 0 : 2;
            }
            catch { return 2; }
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
            MessageBox.Show(
                eventArgs.Exception.Message,
                "未处理的错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
