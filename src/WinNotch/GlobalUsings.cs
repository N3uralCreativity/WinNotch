// Global using directives to resolve WPF vs WinForms ambiguities.
// WinForms is included only for Screen enumeration; all UI types should be WPF.

global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Rect = System.Windows.Rect;
global using Color = System.Windows.Media.Color;
global using MessageBox = System.Windows.MessageBox;
global using Application = System.Windows.Application;
global using Control = System.Windows.Controls.Control;
global using UserControl = System.Windows.Controls.UserControl;
