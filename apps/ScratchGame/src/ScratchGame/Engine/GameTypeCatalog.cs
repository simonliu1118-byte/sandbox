namespace ScratchGame.Engine;

public static class GameTypeCatalog
{
    public static string GetDisplayName(string ruleId)
        => ruleId switch
        {
            "1" => "星星連線",
            "ThreeLine" => "三星連線（舊版）",
            "LuckyNumberMatch" => "幸運號碼",
            "MatchThree" => "三個相同",
            _ => $"未知玩法（{ruleId}）"
        };
}
