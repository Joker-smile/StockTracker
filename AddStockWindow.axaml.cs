using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StockTracker;

public partial class AddStockWindow : Window
{
    public string? Result { get; private set; }

    public AddStockWindow()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        Result = this.FindControl<TextBox>("InputBox")?.Text;
        this.Close();
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        this.Close();
    }
}
