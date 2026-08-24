using System.Configuration;
using System.Data;
using System.Windows;

using System;
using System.IO;
using System.Windows.Threading;

namespace VideoSelector;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
            File.AppendAllText(logPath, $"{DateTime.Now}: {ex.ToString()}\n\n");
            System.Windows.MessageBox.Show($"Ocorreu um erro fatal. O erro foi salvo em: {logPath}\n\n{ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}

