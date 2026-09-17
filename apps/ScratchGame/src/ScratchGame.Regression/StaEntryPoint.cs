namespace ScratchGame.Regression;

internal static class StaEntryPoint
{
    [STAThread]
    public static Task<int> Main(string[] args)
    {
        GameType2Regression.Run();
        return Program.Main(args);
    }
}
