namespace Mh2TexEditor;

internal sealed class EditorColorTable : ProfessionalColorTable
{
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private static readonly Color BlueHover = Color.FromArgb(29, 78, 216);

    public override Color ToolStripGradientBegin => Color.White;
    public override Color ToolStripGradientMiddle => Color.White;
    public override Color ToolStripGradientEnd => Color.White;
    public override Color MenuStripGradientBegin => Color.White;
    public override Color MenuStripGradientEnd => Color.White;
    public override Color ToolStripBorder => Color.FromArgb(216, 221, 230);
    public override Color ButtonSelectedHighlight => Color.FromArgb(219, 234, 254);
    public override Color ButtonSelectedGradientBegin => Color.FromArgb(219, 234, 254);
    public override Color ButtonSelectedGradientEnd => Color.FromArgb(219, 234, 254);
    public override Color ButtonPressedGradientBegin => Color.FromArgb(191, 219, 254);
    public override Color ButtonPressedGradientEnd => Color.FromArgb(191, 219, 254);
    public override Color MenuItemSelected => Color.FromArgb(219, 234, 254);
    public override Color MenuItemBorder => Blue;
    public override Color ImageMarginGradientBegin => Color.White;
    public override Color ImageMarginGradientMiddle => Color.White;
    public override Color ImageMarginGradientEnd => Color.White;
    public Color Primary => Blue;
    public Color PrimaryHover => BlueHover;
}
