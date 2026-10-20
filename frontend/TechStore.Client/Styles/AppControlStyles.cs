namespace TechStore.Client.Styles;

public static partial class AppControlStyles
{
    public const string Panel = "TechStorePanelStyle";
    public const string Button = "TechStoreButtonStyle";
    public const string PrimaryButton = "TechStorePrimaryButtonStyle";
    public const string DangerButton = "TechStoreDangerButtonStyle";
    public const string QuietButton = "TechStoreQuietButtonStyle";
    public const string TextBox = "TechStoreTextBoxStyle";
    public const string AutoSuggestBox = "TechStoreAutoSuggestBoxStyle";
    public const string ComboBox = "TechStoreComboBoxStyle";
    public const string FieldError = "TechStoreFieldErrorStyle";
    public const string TableRow = "TechStoreTableRowStyle";
    public const string TableHeader = "TechStoreTableHeaderStyle";

    public static IReadOnlyList<string> Keys { get; } =
    [
        Panel,
        Button,
        PrimaryButton,
        DangerButton,
        QuietButton,
        TextBox,
        AutoSuggestBox,
        ComboBox,
        FieldError,
        TableRow,
        TableHeader,
    ];
}
