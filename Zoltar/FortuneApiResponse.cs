namespace Zoltar;

public class FortuneApiResponse
{
    public int? MaxTokens { get; set; }
    public double? CostLimit { get; set; }
    public string Context { get; set; }
    public double? Luck { get; set; }
    public string LuckText { get; set; }
    public string Fortune { get; set; }
}