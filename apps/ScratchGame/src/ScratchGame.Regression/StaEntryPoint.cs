namespace ScratchGame.Regression;

internal static class StaEntryPoint
{
    [STAThread]
    public static Task<int> Main(string[] args)
        => Program.Main(args);
}
