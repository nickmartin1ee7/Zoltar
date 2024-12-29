namespace Zoltar.Animations;

public class ShakeAnimation : TriggerAction<VisualElement>
{
    public uint Duration { get; set; }

    protected override void Invoke(VisualElement sender)
    {
        var animation = new Animation();
        double translation = 15;

        for (int i = 0; i < 6; i++)
        {
            double start = (i % 2 == 0) ? translation : -translation;
            double end = (i % 2 == 0) ? -translation : translation;

            animation.Add((double)i / 6, (double)(i + 1) / 6, new Animation(v => sender.TranslationX = v, start, end));
        }

        animation.Commit(sender, "Shake", 16, Duration, Easing.Linear, (v, c) => sender.TranslationX = 0);
    }
}
