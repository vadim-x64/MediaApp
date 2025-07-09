using System.Windows;

namespace MediaApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Виникла непередбачена помилка: {e.Exception.Message}", 
            "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        
        Console.WriteLine($"Dispatcher Exception: {e.Exception}");
            
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show($"Критична помилка: {exception?.Message}", 
            "Критична помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        
        Console.WriteLine($"Unhandled Exception: {exception}");
    }
}