﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Diagnostics;
using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Universal_x86_Tuning_Utility_Handheld.Models;
using Universal_x86_Tuning_Utility_Handheld.Services;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;
using System.Net.NetworkInformation;
using Universal_x86_Tuning_Utility_Handheld.Properties;
using Universal_x86_Tuning_Utility.Scripts;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using System.Management;
using System.Security.Cryptography;

namespace Universal_x86_Tuning_Utility_Handheld
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)); })
            .ConfigureServices((context, services) =>
            {
                // App Host
                services.AddHostedService<ApplicationHostService>();

                // Page resolver service
                services.AddSingleton<IPageService, PageService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddScoped<INavigationWindow, Views.Windows.MainWindow>();
                services.AddScoped<ViewModels.MainWindowViewModel>();

                // Views and ViewModels
                services.AddScoped<Views.Pages.DashboardPage>();
                services.AddScoped<ViewModels.DashboardViewModel>();
                services.AddScoped<Views.Pages.AdvancedPage>();
                services.AddScoped<ViewModels.AdvancedViewModel>();
                services.AddScoped<Views.Pages.DataPage>();
                services.AddScoped<ViewModels.DataViewModel>();
                services.AddScoped<Views.Pages.SettingsPage>();
                services.AddScoped<ViewModels.SettingsViewModel>();

                // Configuration
                services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));
            }).Build();

        /// <summary>
        /// Gets registered service.
        /// </summary>
        /// <typeparam name="T">Type of the service to get.</typeparam>
        /// <returns>Instance of the service or <see langword="null"/>.</returns>
        public static T GetService<T>()
            where T : class
        {
            return _host.Services.GetService(typeof(T)) as T;
        }

        public static string version = "0.0.1";

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static AdaptivePresetManager adaptivePresetManager = new AdaptivePresetManager(Settings.Default.Path + "adaptivePresets.json");

        public static string mbo = "";

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            if (!App.IsAdministrator())
            {
                // Restart and run as admin
                var exeName = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
                startInfo.Arguments = "restart";
                Process.Start(startInfo);
                Environment.Exit(0);
            }
            else
            {

                _ = Tablet.TabletDevices;
                bool firstBoot = false;

                Family.setCpuFamily();
                Family.setCpuFamily();

                try
                {
                    if (Settings.Default.SettingsUpgradeRequired)
                    {
                        try
                        {
                            Settings.Default.Upgrade();
                            Settings.Default.SettingsUpgradeRequired = false;
                            Settings.Default.Save();
                        }
                        catch { }
                    }

                    firstBoot = Settings.Default.FirstBoot;
                }
                catch (ConfigurationErrorsException ex)
                {
                    string filename = ((ConfigurationErrorsException)ex.InnerException).Filename;
                    File.Delete(filename);
                    Settings.Default.Reload();
                }

                if (File.Exists("C:\\Universal.x86.Tuning.Utility.Handheld.msi")) File.Delete("C:\\Universal.x86.Tuning.Utility.Handheld.msi");

                if (firstBoot)
                {
                    string path = System.Reflection.Assembly.GetEntryAssembly().Location;
                    path = path.Replace("Universal x86 Tuning Utility Handheld.dll", null);
                    Settings.Default.Path = path;
                    Settings.Default.FirstBoot = false;
                    if (Family.FAM > Family.RyzenFamily.Rembrandt || Family.FAM == Family.RyzenFamily.Mendocino) Settings.Default.Polling = 3;
                    Settings.Default.Save();

                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFAUTONOMOUS", 1, true);
                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFAUTONOMOUS", 1, false);
                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP", 50, true);
                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP", 50, false);
                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", 50, true);
                    PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", 50, false);
                }

                //Detect if an AYA Neo is being used
                ManagementObjectSearcher baseboardSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
                foreach (ManagementObject queryObj in baseboardSearcher.Get())
                {
                    mbo = queryObj["Manufacturer"].ToString();
                    mbo = mbo.ToLower();
                }

                //if (IsInternetAvailable()) if (Settings.Default.UpdateCheck) CheckForUpdate();

                AdaptivePreset myPreset = adaptivePresetManager.GetPreset("Default");

                if (myPreset == null)
                {
                    AdaptivePreset preset = new AdaptivePreset
                    {
                        _isTemp = false,
                        tempLimit = 95,
                        _isPower = false,
                        powerLimit = 15,
                        _isUndervolt = false,
                        underVolt = 0,
                        _isMaxClock = false,
                        maxClock = 3000,
                        _isIGPUClock = false,
                        iGPUClock = 1500,
                        _isEPP = false,
                        _EPP = 50
                    };
                    adaptivePresetManager.SavePreset("Default", preset);
                }

                await _host.StartAsync();
            }
        }

        private static bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var result = ping.Send("8.8.8.8", 2000); // ping Google DNS server
                    return result.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}