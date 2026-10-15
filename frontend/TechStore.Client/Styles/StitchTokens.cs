using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TechStore.Client.Styles;

public static class StitchTokens
{
    // Primary brand colors from Google Stitch
    public static readonly Color Primary = Color.FromArgb(0xFF, 0x00, 0x66, 0xFF);         // #0066FF
    public static readonly Color PrimaryHover = Color.FromArgb(0xFF, 0x00, 0x52, 0xCC);    // #0052CC
    public static readonly Color PrimaryPressed = Color.FromArgb(0xFF, 0x00, 0x40, 0xA8);  // #0040A8
    public static readonly Color PrimaryLight = Color.FromArgb(0xFF, 0xEB, 0xF3, 0xFF);    // #EBF3FF

    // Surfaces & Backgrounds
    public static readonly Color SurfaceCanvas = Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC);   // #F8FAFC
    public static readonly Color SurfaceWhite = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);    // #FFFFFF
    public static readonly Color SurfaceBorder = Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0);   // #E2E8F0
    public static readonly Color SurfaceBorderDark = Color.FromArgb(0xFF, 0x33, 0x41, 0x55);

    // Text & Content colors
    public static readonly Color ContentTitle = Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A);    // #0F172A
    public static readonly Color ContentBody = Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B);     // #1E293B
    public static readonly Color ContentMuted = Color.FromArgb(0xFF, 0x64, 0x74, 0x8B);    // #64748B

    // Status colors
    public static readonly Color SuccessGreen = Color.FromArgb(0xFF, 0x05, 0x96, 0x69);
    public static readonly Color SuccessBg = Color.FromArgb(0xFF, 0xEC, 0xFD, 0xF5);       // #ECFDF5
    public static readonly Color SuccessBorder = Color.FromArgb(0xFF, 0xA7, 0xF3, 0xD0);   // #A7F3D0
    public static readonly Color SuccessText = Color.FromArgb(0xFF, 0x06, 0x5F, 0x46);     // #065F46

    public static readonly Color ErrorRed = Color.FromArgb(0xFF, 0xDC, 0x26, 0x26);
    public static readonly Color ErrorBg = Color.FromArgb(0xFF, 0xFE, 0xF2, 0xF2);        // #FEF2F2
    public static readonly Color ErrorBorder = Color.FromArgb(0xFF, 0xFE, 0xCD, 0xCD);
    public static readonly Color ErrorText = Color.FromArgb(0xFF, 0x99, 0x1B, 0x1B);

    // Brushes
    public static SolidColorBrush PrimaryBrush => new(Primary);
    public static SolidColorBrush SurfaceCanvasBrush => new(SurfaceCanvas);
    public static SolidColorBrush SurfaceWhiteBrush => new(SurfaceWhite);
    public static SolidColorBrush SurfaceBorderBrush => new(SurfaceBorder);
    public static SolidColorBrush ContentTitleBrush => new(ContentTitle);
    public static SolidColorBrush ContentBodyBrush => new(ContentBody);
    public static SolidColorBrush ContentMutedBrush => new(ContentMuted);
    public static SolidColorBrush SuccessBgBrush => new(SuccessBg);
    public static SolidColorBrush SuccessBorderBrush => new(SuccessBorder);
    public static SolidColorBrush SuccessTextBrush => new(SuccessText);
    public static SolidColorBrush ErrorBgBrush => new(ErrorBg);
    public static SolidColorBrush ErrorBorderBrush => new(ErrorBorder);
    public static SolidColorBrush ErrorTextBrush => new(ErrorText);
}
