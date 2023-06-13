﻿using Microsoft.Win32;
using SharpDX.XInput;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Universal_x86_Tuning_Utility.Scripts;
using Universal_x86_Tuning_Utility.Scripts.AMD_Backend;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Universal_x86_Tuning_Utility_Handheld.Properties;
using Universal_x86_Tuning_Utility_Handheld.Scripts.Misc;
using Universal_x86_Tuning_Utility_Handheld.Services;
using Windows.Devices.Power;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Application = System.Windows.Application;

namespace Universal_x86_Tuning_Utility_Handheld.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public ViewModels.MainWindowViewModel ViewModel
        {
            get; set;
        }
        public ViewModels.AdvancedViewModel AdViewModel
        {
            get; set;
        }

        private DispatcherTimer timer;

        private readonly INavigationService _navigationService;
        public MainWindow(ViewModels.MainWindowViewModel viewModel, ViewModels.AdvancedViewModel adViewModel, IPageService pageService, INavigationService navigationService)
        {
            ViewModel = viewModel;
            AdViewModel = adViewModel;
            DataContext = this;

            InitializeComponent();
            SetPageService(pageService);

            navigationService.SetNavigationControl(RootNavigation);
            SetWindowPosition();
            Loaded += MainWindow_Loaded;
            Global._mainWindowNav = RootNavigation;

            //set up timer for key combo system
            DispatcherTimer checkKeyInput = new DispatcherTimer();
            checkKeyInput.Interval = TimeSpan.FromSeconds(0.14);
            checkKeyInput.Tick += KeyShortCuts_Tick;
            checkKeyInput.Start();

            DispatcherTimer _timer = new DispatcherTimer();
            _timer.Tick += Mouse_Tick;
            _timer.Interval = TimeSpan.FromMilliseconds(1000 / MouseControl.RefreshRate);
            _timer.Start();

            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            UpdateInfo();
            getBatteryTime();

            _navigationService = navigationService;

            UpdatePreset("Default");
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {      
            this.WindowState = WindowState.Minimized;
            SetWindowPosition();
            this.UpdateLayout();
            SetWindowPosition();
            this.UpdateLayout();
            this.WindowState = WindowState.Normal;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.WorkArea.Width - Width;
        }
        int i = 0;
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (timer.Interval == TimeSpan.FromSeconds(1))
            {
                timer.Stop();
                HideFromTaskbar();
                //MessageBox.Show(RootNavigation.Current.PageTag);
                this.Topmost = true;
                Garbage.Garbage_Collect();
                timer.Interval = TimeSpan.FromSeconds(2.2);
                timer.Start();
            }
            else
            {
                UpdateInfo();
                SetWindowPosition();
                getBatteryTime();
                ApplySettings();
                i++;
                if(i >= 9)
                {
                    Garbage.Garbage_Collect();
                    i = 0;
                }
            }
        }

        private void ApplySettings()
        {
            string commandString = "";

            if(Family.TYPE == Family.ProcessorType.Amd_Apu)
            {
                if (AdViewModel.IsTemp == true) commandString = $"--tctl-temp={AdViewModel.TempLimit} --skin-temp-limit={AdViewModel.TempLimit} ";
                if (AdViewModel.IsPower == true) commandString = commandString + $"--stapm-limit={AdViewModel.PowerLimit * 1000} --slow-limit={AdViewModel.PowerLimit * 1000} --fast-limit={AdViewModel.PowerLimit * 1000} --vrm-current={(AdViewModel.PowerLimit * 1000) * 2} --vrmmax-current={(AdViewModel.PowerLimit * 1000) * 2} ";
                if (AdViewModel.IsUndervolt == true)
                {
                    if (AdViewModel.UnderVolt >= 0) commandString = commandString + $"--set-coall={AdViewModel.UnderVolt} ";
                    if (AdViewModel.UnderVolt < 0) commandString = commandString + $"--set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * AdViewModel.UnderVolt))} ";
                }
                if (AdViewModel.IsIGPUClock == true) commandString = commandString + $"--gfx-clk={AdViewModel.IGPUClock} ";

                if(commandString != null && commandString != "") RyzenAdj_To_UXTU.Translate(commandString);
            }

            if (AdViewModel.IsMaxClock == true)
            {
                // Set the AC and DC values for PROCFREQMAX
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX", (uint)AdViewModel.MaxClock, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX", (uint)AdViewModel.MaxClock, false);

                // Set the AC and DC values for PROCFREQMAX1
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX1", (uint)AdViewModel.MaxClock, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX1", (uint)AdViewModel.MaxClock, false);

                // Activate the current power scheme
                PowerPlans.SetActiveScheme("scheme_current");
            }
            else
            {
                // Set the AC and DC values for PROCFREQMAX
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX", 0, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX", 0, false);

                // Set the AC and DC values for PROCFREQMAX1
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX1", 0, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PROCFREQMAX1", 0, false);

                // Activate the current power scheme
                PowerPlans.SetActiveScheme("scheme_current");
            }

            if (AdViewModel.IsEPP == true)
            {

                // Set the AC and DC values for PROCFREQMAX
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP", (uint)AdViewModel.EPP, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP", (uint)AdViewModel.EPP, false);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", (uint)AdViewModel.EPP, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", (uint)AdViewModel.EPP, false);

                // Activate the current power scheme
                PowerPlans.SetActiveScheme("scheme_current");
            }
            else
            {

                // Set the AC and DC values for EPP
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP ", 40, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP ", 40, false);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", 25, true);
                PowerPlans.SetPowerValue("scheme_current", "sub_processor", "PERFEPP1", 25, false);

                // Activate the current power scheme
                PowerPlans.SetActiveScheme("scheme_current");
            }

            if (AdViewModel.IsRSR == true)
            {
                ADLXBackend.SetRSR(true);
                ADLXBackend.SetRSRSharpness(AdViewModel.RSR);
            } else ADLXBackend.SetRSR(false);
        }

        private void UpdateInfo()
        {
            PowerStatus powerStatus = SystemInformation.PowerStatus;
            int batteryLifePercent = (int)(powerStatus.BatteryLifePercent * 100);
            ViewModel.Battery = batteryLifePercent;
            ViewModel.Time = DateTime.Now;
            bool isBatteryCharging = powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online && powerStatus.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging);
            if (!isBatteryCharging)
            {
                if (batteryLifePercent >= 100) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery1024;
                else if (batteryLifePercent >= 90) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery924;
                else if (batteryLifePercent >= 80) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery824;
                else if (batteryLifePercent >= 70) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery724;
                else if (batteryLifePercent >= 60) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery624;
                else if (batteryLifePercent >= 50) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery524;
                else if (batteryLifePercent >= 40) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery424;
                else if (batteryLifePercent >= 30) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery324;
                else if (batteryLifePercent >= 20) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery224;
                else if (batteryLifePercent >= 10) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery124;
                else if (batteryLifePercent >= 0) ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery024;
                else ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.Battery024;
            }
            else ViewModel.BatteryIcon = Wpf.Ui.Common.SymbolRegular.BatteryCharge24;

            GetWifi();

            Global.wifi = ViewModel.WifiIcon;
            Global.battery = ViewModel.BatteryIcon;
            Global.batteryPer = ViewModel.Battery;
        }

        private void HideFromTaskbar()
        {
            ShowInTaskbar = false;
        }

        private void SetWindowPosition()
        {
            this.Topmost = true;
            this.MaxWidth = 512;
            this.MinWidth = 512;
            this.Width = 512;

            // Get the primary screen dimensions
            var primaryScreen = System.Windows.SystemParameters.WorkArea;
            var screenWidth = primaryScreen.Width;
            var screenHeight = primaryScreen.Height;

            this.Left = screenWidth - this.Width - 12;
            this.Top = primaryScreen.Top + 12;
            this.Height = screenHeight - 22;
            this.MaxHeight = screenHeight - 22;
            this.MinHeight = screenHeight - 22;

            this.WindowStyle = WindowStyle.None;

            this.InvalidateVisual();
            this.UpdateLayout();
            this.Activate();
            this.Focus();
            Wpf.Ui.Appearance.Watcher.Watch(this, Wpf.Ui.Appearance.BackgroundType.Mica, true);
            Global._appVis = this.Visibility;
        }

        #region INavigationWindow methods

        public Frame GetFrame()
            => RootFrame;

        public INavigation GetNavigation()
            => RootNavigation;

        public bool Navigate(Type pageType)
            => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService)
            => RootNavigation.PageService = pageService;

        public void ShowWindow()
            => Show();

        public void CloseWindow()
            => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        private void UiWindow_LocationChanged(object sender, EventArgs e)
        {
            SetWindowPosition();
        }

        private IntPtr Handle => new WindowInteropHelper(this).Handle;

        private void NotifyIcon_LeftClick(Wpf.Ui.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
            }
            else
            {
                Visibility = Visibility.Visible;
                this.Activate();
            }
            SetWindowPosition();
            UpdateLayout();

            Global._appVis = this.Visibility;
        }

        private void UiWindow_StateChanged(object sender, EventArgs e)
        {

        }

        private void TitleBar_MinimizeClicked(object sender, RoutedEventArgs e)
        {
            this.WindowStyle = WindowStyle.None;
        }

        async void KeyShortCuts_Tick(object sender, EventArgs e)
        {
            ControllerInput(UserIndex.One);
            ControllerInput(UserIndex.Two);
        }

        private static Controller controller;
        bool visible = true;
        private void ControllerInput(UserIndex controllerNo)
        {
            try
            {
                controller = new Controller(controllerNo);

                bool connected = controller.IsConnected;

                SetWindowPosition();
                this.UpdateLayout();
                SetWindowPosition();
                this.UpdateLayout();

                if (connected)
                {
                    //get controller state
                    var state = controller.GetState();

                    //detect if keyboard or controller combo is being activated
                    if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) && state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                    {
                        Global.shortCut = true;
                    }
                    else if (Global.shortCut == true) Global.shortCut = false;


                    if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) && state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                    {
                        if (Visibility == Visibility.Visible)
                        {
                            Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            Visibility = Visibility.Visible;
                            this.Activate();
                            SetWindowPosition();
                            UpdateLayout();
                        }

                        UpdateLayout();
                    }

                    if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) && this.WindowState != WindowState.Minimized)
                    {
                        int current = RootNavigation.SelectedPageIndex;
                        current--;
                        if(current == 0) _navigationService.Navigate(typeof(Views.Pages.DashboardPage));
                        else if(current == 1) _navigationService.Navigate(typeof(Views.Pages.AdvancedPage));
                    }

                    if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder) && this.WindowState != WindowState.Minimized)
                    {
                        int current = RootNavigation.SelectedPageIndex;
                        current++;
                        if (current == 0) _navigationService.Navigate(typeof(Views.Pages.DashboardPage));
                        else if (current == 1) _navigationService.Navigate(typeof(Views.Pages.AdvancedPage));
                    }
                }

                if (App.mbo.Contains("aya") && controllerNo == UserIndex.One)
                {
                    //detect if keyboard or controller combo is being activated
                    if ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.F12) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.F12) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0 && (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0)
                    {
                        if (Visibility == Visibility.Visible)
                        {
                            Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            Visibility = Visibility.Visible;
                            this.Activate();
                            SetWindowPosition();
                            UpdateLayout();
                        }

                        UpdateLayout();
                    }
                }

                Global._appVis = this.Visibility;
            }
            catch { }
        }

        async void Mouse_Tick(object sender, EventArgs e)
        {
            try
            {
                controller = new Controller(UserIndex.One);

                bool connected = controller.IsConnected;

                if (connected)
                {
                    if (controller.IsConnected && Settings.Default.isMouse == true && this.WindowState == WindowState.Minimized)
                    {
                        controller.GetState(out var state);
                        MouseControl.Movement(state);
                        MouseControl.Scroll(state);
                        MouseControl.LeftButton(state);
                        MouseControl.RightButton(state);
                    }
                }
            }
            catch { }
        }
        static double lastWifi = 0;
        public static async Task<double> RetrieveSignalStrengthAsync()
        {
            try
            {
                var adapters = await WiFiAdapter.FindAllAdaptersAsync();
                foreach (var adapter in adapters)
                {
                    foreach (var network in adapter.NetworkReport.AvailableNetworks)
                    {
                        lastWifi = network.SignalBars;
                        return network.SignalBars;
                    }
                    return 0;
                }
                return 0;
            }
            catch
            {
                return lastWifi;
            }
        }

        public async void GetWifi()
        {
            string wifiURL = "";
            double wifi = await Task.Run(() => RetrieveSignalStrengthAsync().Result);
            wifi = await Task.Run(() => RetrieveSignalStrengthAsync().Result);

            var wifiRadios = await Radio.GetRadiosAsync();
            var wifiRadio = await Task.Run(() => wifiRadios.FirstOrDefault(r => r.Kind == RadioKind.WiFi));
            var internetConnectionProfile = await Task.Run(() => NetworkInformation.GetInternetConnectionProfile());

            if (wifiRadio != null && wifiRadio.State == RadioState.On)
            {
                if (wifi >= 4) ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.Wifi124;
                else if (wifi >= 3) ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.Wifi224;
                else if (wifi >= 2) ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.Wifi324;
                else if (wifi >= 1) ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.Wifi424;
                else ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.GlobeProhibited20;
            }
            else if (wifiRadio != null && wifiRadio.State == RadioState.Off) ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.WifiOff24;
            else if (internetConnectionProfile != null)
            {
                var interfaceType = internetConnectionProfile.NetworkAdapter.IanaInterfaceType;

                if (interfaceType == 71)
                {
                    ViewModel.WifiIcon = Wpf.Ui.Common.SymbolRegular.UsbPlug24;
                }
            }

        }

        public async void getBatteryTime()
        {
            await Task.Run(() =>
            {
                PowerStatus pwr = System.Windows.Forms.SystemInformation.PowerStatus;
                //Get battery life

                var batTime = (float)pwr.BatteryLifeRemaining;

                bool isCharging = false;

                if (ViewModel.BatteryIcon == Wpf.Ui.Common.SymbolRegular.BatteryCharge24)
                {
                    batTime = 0;
                    isCharging = true;
                }
                var time = TimeSpan.FromSeconds(batTime);

                AdViewModel.BatteryTime = $"{time:%h} Hours {time:%m} Minutes Remaining";

                if (AdViewModel.BatteryTime == "0 Hours 0 Minutes Remaining" && isCharging == true) AdViewModel.BatteryTime = "Battery Charging";
                if (AdViewModel.BatteryTime == "0 Hours 0 Minutes Remaining" && isCharging == false) AdViewModel.BatteryTime = "Calculating";

                PerfCounters.ReadSensors();
                float dischargeRate = (float)PerfCounters.BatteryDischarge;

                if (dischargeRate != 0)
                {
                    AdViewModel.IsDischarge = true;
                    AdViewModel.ChargeRate = $"-{dischargeRate.ToString("0.00")}W Charge Rate";
                }
                else AdViewModel.IsDischarge = false;
            });
        }

        private static AdaptivePresetManager adaptivePresetManager = new AdaptivePresetManager(Settings.Default.Path + "adaptivePresets.json");
        private void UpdatePreset(string presetName)
        {
            Global.updatingPreset = true;
            try
            {
                Global.presetName = presetName;

                adaptivePresetManager = new AdaptivePresetManager(Settings.Default.Path + "adaptivePresets.json");
                AdaptivePreset myPreset = adaptivePresetManager.GetPreset(presetName);

                if (myPreset != null)
                {
                    AdViewModel.IsTemp = myPreset._isTemp;
                    AdViewModel.TempLimit = myPreset.tempLimit;
                    AdViewModel.IsPower = myPreset._isPower;
                    AdViewModel.PowerLimit = myPreset.powerLimit;
                    AdViewModel.IsUndervolt = myPreset._isUndervolt;
                    AdViewModel.IsMaxClock = myPreset._isMaxClock;
                    AdViewModel.MaxClock = myPreset.maxClock;
                    AdViewModel.IsIGPUClock = myPreset._isIGPUClock;
                    AdViewModel.IGPUClock = myPreset.iGPUClock;
                    AdViewModel.IsEPP = myPreset._isEPP;
                    AdViewModel.IsRSR = myPreset._isRSR;
                    AdViewModel.RSR = myPreset._RSR;
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString()); }

            Global.updatingPreset = false;
        }
    }
}