using CoreGraphics;
using Foundation;
using UIKit;

namespace CrashBandicoot.IOSRuntime;

internal sealed class PauseOverlay : UIView
{
    readonly UIStackView _stack;

    public PauseOverlay(CGRect frame) : base(frame)
    {
        BackgroundColor = UIColor.FromWhiteAlpha(0f, 0.78f);
        UserInteractionEnabled = true;

        var title = new UILabel
        {
            Font = UIFont.BoldSystemFontOfSize(34),
            Text = "PAUSED",
            TextAlignment = UITextAlignment.Center,
            TextColor = UIColor.White,
        };
        var resume = Button("RESUME");
        var map = Button("RETURN TO MAP");
        resume.TouchUpInside += (_, _) => ResumeRequested?.Invoke();
        map.TouchUpInside += (_, _) => MapRequested?.Invoke();

        _stack = new UIStackView(new UIView[] { title, resume, map })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 18,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        AddSubview(_stack);
        _stack.CenterXAnchor.ConstraintEqualTo(CenterXAnchor).Active = true;
        _stack.CenterYAnchor.ConstraintEqualTo(CenterYAnchor).Active = true;
        _stack.WidthAnchor.ConstraintEqualTo(320).Active = true;
    }

    public event Action? ResumeRequested;
    public event Action? MapRequested;

    static UIButton Button(string text)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle(text, UIControlState.Normal);
        button.SetTitleColor(UIColor.White, UIControlState.Normal);
        button.TitleLabel.Font = UIFont.BoldSystemFontOfSize(22);
        button.BackgroundColor = UIColor.FromWhiteAlpha(1f, 0.14f);
        button.Layer.CornerRadius = 10;
        button.HeightAnchor.ConstraintEqualTo(48).Active = true;
        return button;
    }
}
