namespace GDriveDownloader;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(248, 250, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(209, 213, 219);
    public static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
    public static readonly Color TextSecondary = Color.FromArgb(102, 112, 133);

    public static readonly Color Accent = Color.FromArgb(13, 148, 136);
    public static readonly Color AccentHover = Color.FromArgb(13, 128, 118);
    public static readonly Color AccentPressed = Color.FromArgb(15, 118, 110);
    public static readonly Color AccentSoft = Color.FromArgb(209, 242, 235);

    public static readonly Color Danger = Color.FromArgb(180, 55, 55);
    public static readonly Color DangerHover = Color.FromArgb(164, 47, 47);

    public const string FontFamily = "Microsoft JhengHei UI";

    public static Font BodyFont { get; } = new(FontFamily, 9.5f, FontStyle.Regular, GraphicsUnit.Point);

    public static Font SectionFont { get; } = new(FontFamily, 9.5f, FontStyle.Bold, GraphicsUnit.Point);

    public static void ApplyForm(Form form)
    {
        form.BackColor = Window;
        form.Font = BodyFont;
    }

    public static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentPressed;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = SectionFont;
        button.Cursor = Cursors.Hand;
        button.Height = Math.Max(button.Height, 32);
    }

    public static void StyleSecondaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = AccentSoft;
        button.BackColor = Surface;
        button.ForeColor = TextPrimary;
        button.Font = BodyFont;
        button.Cursor = Cursors.Hand;
        button.Height = Math.Max(button.Height, 32);
    }

    public static void StyleDangerButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = DangerHover;
        button.BackColor = Danger;
        button.ForeColor = Color.White;
        button.Font = BodyFont;
        button.Cursor = Cursors.Hand;
        button.Height = Math.Max(button.Height, 32);
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = BodyFont;
        textBox.BackColor = Surface;
        textBox.ForeColor = TextPrimary;
    }

    public static void StyleLabel(Label label, bool secondary = false)
    {
        label.Font = BodyFont;
        label.ForeColor = secondary ? TextSecondary : TextPrimary;
        label.BackColor = Color.Transparent;
    }

    public static void StyleListView(ListView listView)
    {
        listView.Font = BodyFont;
        listView.BackColor = Surface;
        listView.ForeColor = TextPrimary;
        listView.BorderStyle = BorderStyle.FixedSingle;
        listView.GridLines = true;
    }
}
