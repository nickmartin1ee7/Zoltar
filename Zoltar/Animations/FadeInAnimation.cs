namespace Zoltar.Animations;

public class FadeInAnimation : TriggerAction<VisualElement>
{
    public uint Duration { get; set; }

    protected override async void Invoke(VisualElement sender)
    {
        sender.Opacity = 0;
        await sender.FadeTo(1, Duration);
    }
}
