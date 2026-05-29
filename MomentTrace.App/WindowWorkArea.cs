using System.Windows;
using System.Windows.Interop;

namespace MomentTrace.App;

internal static class WindowWorkArea
{
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = handle == IntPtr.Zero
            ? System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position)
            : System.Windows.Forms.Screen.FromHandle(handle);

        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var workArea = screen.WorkingArea;
        var topLeft = transform.Transform(new System.Windows.Point(workArea.Left, workArea.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(workArea.Right, workArea.Bottom));

        window.MaxWidth = bottomRight.X - topLeft.X;
        window.MaxHeight = bottomRight.Y - topLeft.Y;

        if (window.WindowState == WindowState.Maximized)
        {
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }
    }
}
