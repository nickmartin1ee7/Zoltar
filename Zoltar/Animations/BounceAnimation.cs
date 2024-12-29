namespace Zoltar.Animations;

public class BounceAnimation : TriggerAction<VisualElement>
{
    public uint Duration { get; set; }

    protected override void Invoke(VisualElement sender)
    {
        var bounceHeight = 15; // Set the height for the bounce effect
        var animation = new Animation
        {
            // Create the bounce animation sequence
            { 0, 0.5, new Animation(v => sender.TranslationY = v, 0, -bounceHeight, Easing.CubicOut) },
            { 0.5, 1, new Animation(v => sender.TranslationY = v, -bounceHeight, 0, Easing.CubicIn) }
        };

        // Commit the animation to the view
        animation.Commit(sender, "Bounce", 16, Duration, Easing.Linear, (v, c) => sender.TranslationY = 0);
    }
}
