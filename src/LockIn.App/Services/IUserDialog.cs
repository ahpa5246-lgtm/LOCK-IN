using System.Windows;

namespace LockIn.App.Services;

internal interface IUserDialog
{
    MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image);
}

internal sealed class WpfUserDialog : IUserDialog
{
    public MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image)
    {
        return MessageBox.Show(message, title, buttons, image);
    }
}
