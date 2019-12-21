using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Support.Constraints;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Com.Hikvision.Netsdk;
using EyesonApp.Controls;
using EyesonApp.Services;
using Java.IO;
using Java.Lang;
using Org.Aviran.Cookiebar2;
using Org.MediaPlayer.PlayM4;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using AlertDialog = Android.Support.V7.App.AlertDialog;
using Console = System.Console;

namespace EyesonApp
{
    [Preserve(AllMembers = true)]
    [Activity(Label = "@string/app_name", Theme = "@style/ThemeSplash", MainLauncher = true, LaunchMode = Android.Content.PM.LaunchMode.SingleInstance, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class MainActivity : AppCompatActivity, IExceptionCallBack, ITextWatcher
    {
        static Button m_oPreviewBtn = null;
        static Button m_oPlaybackBtn = null;
        static Button m_oCaptureBtn = null;
        static Button m_oRecordBtn = null;
        static TextView m_IPAdrs = null;
        static SurfaceView m_surface;
        static TimePicker timePicker;
        static DatePicker datePicker;
        static ScrollView scrollView = null;
        NET_DVR_DEVICEINFO_V30 m_oNetDvrDeviceInfoV30;

        delegate void LoginListenerDelegate(EyesonApp.Models.Data data);
        static LoginListenerDelegate _LoginListenerDelegate;

        delegate void PlaybackListenerDelegate(object obj, EventArgs E);
        static PlaybackListenerDelegate _PlaybackListenerDelegate;

        delegate void StopPlaybackDelegate();
        static StopPlaybackDelegate _StopPlaybackDelegate;

        delegate void SetTimerDelegate();
        static SetTimerDelegate _SetTimerDelegate;

        delegate void PreviewListenerDelegate(object obj, EventArgs e);
        static PreviewListenerDelegate _PreviewListenerDelegate;

        delegate void LaunchAppFromBackgroundDelegate(Activity context);
        static LaunchAppFromBackgroundDelegate _LaunchAppFromBackgroundDelegate;

        static MainActivity main { get; set; }

        public static Activity MContext { get; set; }

        private static int m_iLogID = -1; // return by NET_DVR_Login_v30
        private static int m_iPlayID = -1; // return by NET_DVR_RealPlay_V30
        private static int m_iPlaybackID = -1; // return by NET_DVR_PlayBackByTime

        private static int m_iPort = -1; // play port

        private static bool m_bTalkOn = false;
        private static bool m_bPTZL = false;
        private static bool m_bMultiPlay = false;

        private static bool m_bNeedDecode = true;
        private static bool m_bSaveRealData = false;
        private static bool m_bStopPlayback = false;
        private static bool _OnResume = false;
        private static int nProgress = -1;

        private static int Year { get; set; }
        private static int Month { get; set; }
        private static int Day { get; set; }

        private static int Hour { get; set; }
        private static int Minute { get; set; }
        private static int Second { get; set; }

        private static bool ValueForSingleStreamChannel { get; set; } = false;

        private static bool AllowedToStream { get; set; } = true;

        private static int m_iStartChan { get; set; } = 0; // start channel no
        private static int m_iChanNum { get; set; } = 0; // channel number
        public bool CanShowDateDialog { get; private set; }

        private double StreamTimeElapsedOnRuntime { get; set; } = 0;

        private double SecondsElapsed { get; set; } = 0;

        private static Stopwatch _Timer = null;

        private static Stopwatch _RuntimeStreamTimer = null;

        private static PlaySurfaceView[] playView = new PlaySurfaceView[4];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);

            base.SetTheme(Resource.Style.AppTheme_NoActionBar);

            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            StartSocketListening();

            if (!InitSDK())
            {
                Finish();
            }

            if (!InitActivity())
            {
                Finish();
            }

            SetGlobalAppContext();

            ShowAlertAboutApp();

            CheckInternetConnection();

            SetTimers();

            ScrollScrollViewToTheBottom();

            //CalculateOnTheFlySurfaceViewTopMargin();

            Xamarin.Essentials.Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
        }

        private void CalculateOnTheFlySurfaceViewTopMargin()
        {
            ConstraintLayout.LayoutParams _params = (ConstraintLayout.LayoutParams)m_surface.LayoutParameters;
            _params.SetMargins(0, (m_surface.Height / 4), 0, 0);
            m_surface.LayoutParameters = _params;
        }

        //public static void SetMargins(this View view, int l, int t, int r, int b)
        //{
        //    if (view.LayoutParameters.GetType() == typeof(ViewGroup.MarginLayoutParams))
        //    {
        //        ViewGroup.MarginLayoutParams p = (ViewGroup.MarginLayoutParams)view.LayoutParameters;
        //        p.SetMargins(l, t, r, b);
        //        view.RequestLayout();
        //    }
        //}

        private void ScrollScrollViewToTheBottom()
        {
            scrollView.Post(() =>
            {
                scrollView.FullScroll(FocusSearchDirection.Down);
            });
        }

        private void SetTimers()
        {
            if (_Timer is null || _RuntimeStreamTimer is null)
            {
                _Timer = new Stopwatch();
                _RuntimeStreamTimer = new Stopwatch();
            }
        }

        private void CheckInternetConnection()
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.Internet)
            {
                SetIPAddressToIPLabel();
            }
            SetIPAddressToIPLabel();
        }

        private void ShowAlertAboutApp()
        {
            var alert = new AlertDialog.Builder(GetApplicationContext()).SetTitle("Eyeson APP BETA Phase 0")
                .SetMessage("This is the preview of the Phase 0, some details are waiting to be treated, those will get fixed, patched and/or refactored completely [Like the UI/UX, when more phases of the app get finished] on future updates, thanks. Sergio Joel Ferreras Batista | Mobile Developer, Eyeson Digital LLC.").SetPositiveButton("Yes", (a, s) => { }).SetIcon(Resource.Drawable.notification_bg);
            alert.Show();
        }

        private void SetGlobalAppContext()
        {
            MainActivity.MContext = this;
        }

        public static Activity GetApplicationContext() => MainActivity.MContext;

        private void StartSocketListening()
        {
            new System.Threading.Thread(new ThreadStart(() => {
                AsynchronousSocketListener.StartListening();
            })).Start();
        }

        private void Connectivity_ConnectivityChanged(object sender, Xamarin.Essentials.ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == Xamarin.Essentials.NetworkAccess.Internet)
            {
                //SetIPAddressToIPLabel();
                StartSocketListening();
            }
            else
            {
                ShowFancyMessage(GetApplicationContext(), "No internet connection", Color: Resource.Color.error_color_material_light, Duration: 3500);
                //SetIPAddressToIPLabel();
                StartSocketListening();
            }
        }

        private void SetIPAddressToIPLabel()
        {
            //var IP = GetIP();
            var IP = "127.0.0.1";
            //var emptyIp = IP == "127.0.0.1" ? true : false;
            //var message = emptyIp == false ? " " + IP + " listening on Port 7555" : " " + IP + " there's a network issue";
            var message = IP + " listening on Port 3200";
            m_IPAdrs.Text = message;
            m_IPAdrs.Selected = true;
        }

        private string GetIP()
        {
            WifiManager manager = (WifiManager)GetSystemService(Service.WifiService);
            int ip = manager.ConnectionInfo.IpAddress;

            string ipaddress = Android.Text.Format.Formatter.FormatIpAddress(ip);
            return ipaddress;
        }

        public static void SetDataToControls()
        {
            InvokeAppLaunch(GetApplicationContext());

            object locker = new object();
            var _result = DataSingleton.Instance();

            InvokeTimerInit();

            if (!SaveLoginSessionSingleton.SessionLoggedIn())
            {
                ParseDate(_result.Date);
                lock (locker)
                {
                    InvokeStopPlaybackDelegate();

                    new System.Threading.Thread(() =>
                    {
                        using (var h = new Handler(Looper.MainLooper))
                        {
                            h.PostAtFrontOfQueue(() => InvokePlaybackListener(null, null));
                        }
                    }).Start();

                    CookieBar.Dismiss(GetApplicationContext());

                    SaveOnResumeStateSingleton.SaveOnResumeState(true);

                    OnPauseStateSingleton.SaveOnPauseState(true);

                    InvokeLoginListener(_result);
                }
            }
            else
            {
                lock (locker)
                {
                    InvokePlaybackListener(null, null);
                }
            }
        }
        
        public void LaunchApp(Activity context)
        {
            var intent = new Intent(context, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.UpdateCurrent);

            var mgr = (AlarmManager)context.GetSystemService(Context.AlarmService);
            mgr.Set(AlarmType.Rtc, 5, pendingIntent);

            AllowedToStream = false;
        }

        private static void InvokeStopPlaybackDelegate()
        {
            main = new MainActivity();
            MainActivity._StopPlaybackDelegate = main.StopPlayback;
            MainActivity._StopPlaybackDelegate?.Invoke();
        }

        private static void InvokePlaybackListener(object obj, EventArgs e)
        {
            main = new MainActivity();
            MainActivity._PlaybackListenerDelegate = main.M_oPlaybackBtn_Click;
            MainActivity._PlaybackListenerDelegate?.Invoke(null, null);
        }

        private static void InvokeLoginListener(EyesonApp.Models.Data data)
        {
            main = new MainActivity();
            MainActivity._LoginListenerDelegate = main.Login_Listener;
            MainActivity._LoginListenerDelegate?.Invoke(data);
        }

        private static void InvokeTimerInit()
        {
            main = new MainActivity();
            MainActivity._SetTimerDelegate = main.SetTimers;
            MainActivity._SetTimerDelegate?.Invoke();
        }

        private static void InitPreviewListener(object sender, EventArgs e)
        {
            main = new MainActivity();
            MainActivity._PreviewListenerDelegate = main.Preview_Listener;
            MainActivity._PreviewListenerDelegate?.Invoke(null, null);
        }

        private static void InvokeAppLaunch(Activity context)
        {
            main = new MainActivity();
            MainActivity._LaunchAppFromBackgroundDelegate = main.LaunchApp;
            MainActivity._LaunchAppFromBackgroundDelegate?.Invoke(context);
        }

        private static void ParseDate(DateTimeOffset date)
        {
            Second = date.Second;
            Hour = date.Hour;
            Minute = date.Minute;
            Month = date.Month;
            Year = date.Year;
            Day = date.Day;
        }

        static MainActivity()
        {
            bool _falseFlag = false;
            if (_falseFlag)
            {
                var ignore1 = typeof(HCNetSDK);
                var ignore2 = typeof(Player);
            }
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            ShowFancyMessage(GetApplicationContext(), "Eyeson App Beta 1.0.0", Position: CookieBar.Bottom, Color:Resource.Color.material_blue_grey_800);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                ShowFancyMessage(GetApplicationContext(), "Compatible With", message: "HikVision SDK", Position: CookieBar.Bottom, Color: Resource.Color.material_blue_grey_800, Duration: 3500);
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        
        private bool InitSDK()
        {
            //init net SDK
            if (!HCNetSDK.Instance.NET_DVR_Init())
            {
                System.Console.WriteLine("HCNetSDK init is failed");
                ShowFancyMessage(GetApplicationContext(), "The HCNetSDK has failed to init", Position:CookieBar.Top, Color: Resource.Color.material_blue_grey_800, Duration:1500);
                return false;
            }
            HCNetSDK.Instance.NET_DVR_SetLogToFile(3, "/mnt/sdcard/sdklog/", true);
            return true;
        }

        private bool InitActivity()
        {
            FindViews();
            SetListeners();
            return true;
        }

        private void FindViews()
        {
            m_oPreviewBtn = (Button)FindViewById(Resource.Id.btn_Preview);
            m_oPlaybackBtn = (Button)FindViewById(Resource.Id.btn_Playback);
            m_oCaptureBtn = (Button)FindViewById(Resource.Id.btn_Capture);
            m_oRecordBtn = (Button)FindViewById(Resource.Id.btn_Record);
            m_IPAdrs = (TextView)FindViewById(Resource.Id.ipPlaceHolder);
            m_surface = (SurfaceView)FindViewById(Resource.Id.Sur_Player);
            scrollView = (ScrollView)FindViewById(Resource.Id.scrollView2);
        }

        private void Record_Listener(object sender, EventArgs e)
        {
            if (!IsThereInternetOnDevice())
                return;

            var random = new System.Random(12000);
           
            if (!m_bSaveRealData)
            {
               
                // Documents folder
                string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos) + $"_{random.Next(12000)}.mp4";
                //string documentsPath = GetApplicationContext().GetExternalFilesDir($"_{random.Next(12000)}").AbsolutePath;
                //string documentsPath = $"/storage/emulated/0/" + $"_{random.Next()}.mp4";
                if (!HCNetSDK.Instance.NET_DVR_SaveRealData(m_iPlayID, documentsPath))
                {
                    Console.WriteLine("NET_DVR_SaveRealData failed! error: " + HCNetSDK.Instance.NET_DVR_GetLastError());
                    ShowFancyMessage(GetApplicationContext(), $"There's an error: Code: {HCNetSDK.Instance.NET_DVR_GetLastError()}", SwipeToDismissEnabled: true, message: "Try again, there was an error when trying to start saving the video", Position:CookieBar.Top, Color:Resource.Color.error_color_material_light, Duration: 23000);
                    return;
                }
                else
                {
                    Console.WriteLine("NET_DVR_SaveRealData success!");
                    ShowFancyMessage(GetApplicationContext(), "Saving data in realtime", SwipeToDismissEnabled: true, Position: CookieBar.Top, Duration: 2000);
                }
                m_bSaveRealData = true;
            }
            else
            {
                if (!HCNetSDK.Instance.NET_DVR_StopSaveRealData(m_iPlayID))
                {
                    Console.WriteLine("NET_DVR_StopSaveRealData failed! error: "
                                    + HCNetSDK.Instance
                                            .NET_DVR_GetLastError());
                    ShowFancyMessage(GetApplicationContext(), $"There's an error at: {HCNetSDK.Instance.NET_DVR_GetLastError()}", SwipeToDismissEnabled: true, message: "Try again, there was an error when trying to stop saving the video", Position: CookieBar.Top, Color: Resource.Color.error_color_material_light, Duration: 23000);

                }
                else
                {
                    Console.WriteLine("NET_DVR_SaveRealData success!");
                    ShowFancyMessage(GetApplicationContext(), "Realtime data saved.", SwipeToDismissEnabled: true, Position: CookieBar.Top, Duration: 2000);
                }
                m_bSaveRealData = false;
            }
        }

        private bool IsThereInternetOnDevice()
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None || Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.Unknown)
            {
                ShowFancyMessage(GetApplicationContext(), "No internet connection", Color: Resource.Color.error_color_material_light, Duration: 3500);
                return false;
            }
            return true;
        }

        private void SetListeners()
        {
            m_oPreviewBtn.Click += Preview_Listener;
            m_oRecordBtn.Click += Record_Listener;
            m_oPlaybackBtn.Click += PlaybackButtomCliked;
            m_oCaptureBtn.Click += Capture_Listener;
        }


        private void DisposeListeners()
        {
            m_oPreviewBtn.Click -= Preview_Listener;
            m_oRecordBtn.Click -= Record_Listener;
            m_oPlaybackBtn.Click -= PlaybackButtomCliked;
            m_oCaptureBtn.Click -= Capture_Listener;
        }

        private void PlaybackButtomCliked(object sender, EventArgs e)
        {
            object locker = new object();
            if (m_oPlaybackBtn.Text == "Stop")
            {
                InvokeStopPlaybackDelegate();
            }
            else
            {
                lock (locker)
                {
                    InvokePlaybackListener(null, null);
                }
            }
        }

        private void Capture_Listener(object sender, EventArgs e)
        {
            if (!IsThereInternetOnDevice())
                return;

            try
            {
                var port = Player.Instance.Port;
                if (/*m_iPort < 0*/ port < 0)
                {
                    Log.Error("EYESON APP", "please start preview first");
                    ShowFancyMessage(GetApplicationContext(), "Please start previewing first", Color: Resource.Color.error_color_material_light, Duration: 2000);
                    return;
                }

                Player.MPInteger stWidth = new Player.MPInteger();
                Player.MPInteger stHeight = new Player.MPInteger();

                if (!Player.Instance.GetPictureSize(port, stWidth, stHeight))
                {
                    Log.Error("EYESON", "please start preview first");
                    ShowFancyMessage(GetApplicationContext(), "Please start previewing first", Color:Resource.Color.error_color_material_light, Duration:2000);
                    return;
                }

                int nSize = 5 * stWidth.Value * stHeight.Value;
                byte[] picBuf = new byte[nSize];

                Player.MPInteger stSize = new Player.MPInteger();

                if (!Player.Instance.GetBMP(port, picBuf, nSize, stSize))
                {
                    var error = Player.Instance.GetLastError(port);
                    Log.Error("EYESON APP", $"getBMP failed with error code: {error}");
                    ShowFancyMessage(GetApplicationContext(), $"Capturing function failed: {error}", Color: Resource.Color.error_color_material_light, Duration: 2000);
                    return;
                }
                //var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonPictures);
                var path = FileSystem.AppDataDirectory;

                FileOutputStream file = new FileOutputStream(path + System.DateTime.Now.Date + ".bmp");
                file.Write(picBuf, 0, stSize.Value);
                file.Close();

            }   
            catch (System.Exception er)
            {
                Log.Error("EYESON APP", "Error at: " + er.ToString());
            }
        }

        private async void M_oPlaybackBtn_Click(object sender, EventArgs e)
        {
            await Task.Delay(10);

            //StopPlayback();

            InvokeStopPlaybackDelegate();

            if (SaveOnResumeStateSingleton.ResumeStarted())
            {
                ShowFancyMessage(GetApplicationContext(), "Starting Playback, please Wait", message: "Please wait. Swipe this message to the right or left to dismiss it.", Duration: 2000);
                CookieBar.Dismiss(GetApplicationContext());
            }

            try
            {
                if (m_iLogID < 0)
                {
                    Log.Error("EYESON APP", "please login on a device first");
                    ShowFancyMessage(GetApplicationContext(), "Please login first", Color: Resource.Color.error_color_material_light);
                    await Task.Delay(2000);
                    CookieBar.Dismiss(GetApplicationContext());
                    return;
                }

                if (m_iPlaybackID < 0)
                {
                    if (m_iPlayID >= 0)
                    {
                        Log.Info("EYESON APP", "Please stop preview first");
                        ShowFancyMessage(GetApplicationContext(), "Please stop Preview function first", Color: Resource.Color.error_color_material_light);
                        await Task.Delay(2000);
                        CookieBar.Dismiss(GetApplicationContext());
                        return;
                    }

                    ChangeSingleSurFace(true);

                    ValueForSingleStreamChannel = true;

                    NET_DVR_TIME struBegin = new NET_DVR_TIME();
                    NET_DVR_TIME struEnd = new NET_DVR_TIME();

                    struBegin.DwYear = Year;
                    struBegin.DwMonth = Month;
                    struBegin.DwDay = Day;
                    struBegin.DwHour = Hour;
                    struBegin.DwMinute = Minute;
                    struBegin.DwSecond = Second;

                    struEnd.DwYear = System.DateTime.UtcNow.Year;
                    struEnd.DwMonth = System.DateTime.UtcNow.Month;
                    struEnd.DwDay = System.DateTime.UtcNow.Day;
                    struEnd.DwHour = System.DateTime.Now.Hour;
                    struEnd.DwMinute = System.DateTime.UtcNow.Minute;
                    struEnd.DwSecond = System.DateTime.UtcNow.Second;

                    NET_DVR_VOD_PARA struVod = new NET_DVR_VOD_PARA();
                    struVod.StruBeginTime = struBegin;
                    struVod.StruEndTime = struEnd;
                    struVod.ByStreamType = 0;

                    if (SaveLoginSessionSingleton.SessionLoggedIn())
                    {
                        struVod.StruIDInfo.DwChannel = Convert.ToInt32(DataSingleton.Instance().Camera) - 1;
                    }
                    else
                    {
                        struVod.StruIDInfo.DwChannel = m_iStartChan == 0 ? Integer.ParseInt(m_iStartChan.ToString()) - 1 : Integer.ParseInt(m_iStartChan.ToString()) - 1;
                    }
                    struVod.HWnd = playView[0].Holder.Surface;

                    m_iPlaybackID = HCNetSDK.Instance.NET_DVR_PlayBackByTime_V40(m_iLogID, struVod);

                    if (m_iPlaybackID >= 0)
                    {
                        NET_DVR_PLAYBACK_INFO struPlaybackInfo = null;
                        if (!HCNetSDK.Instance.NET_DVR_PlayBackControl_V40(m_iPlaybackID, PlaybackControlCommand.NetDvrPlaystart, null, 0, struPlaybackInfo))
                        {
                            Log.Error("EYESON APP", "net sdk playback start failed!");
                            ShowFancyMessage(GetApplicationContext(), "NET_SDK_Playback start failed, try again.");
                            return;
                        }
                        m_bStopPlayback = false;

                        RunOnUiThread(() => m_oPlaybackBtn.Text = "Stop");
                      
                        AllowedToStream = true;

                        new System.Threading.Thread(new ThreadStart(() =>
                        {
                            while (true)
                            {
                                nProgress = HCNetSDK.Instance.NET_DVR_GetPlayBackPos(m_iPlaybackID);

                                System.Console.WriteLine("NET_DVR_GetPlayBackPos:" + nProgress);

                                if (nProgress < 0 || nProgress >= 100)
                                {
                                    break;
                                }

                                try
                                {
                                    System.Threading.Thread.Sleep(1000);
                                }
                                catch (InterruptedException er)
                                {
                                    er.PrintStackTrace();
                                }
                            }
                        })).Start();

                        if (!_RuntimeStreamTimer.IsRunning)
                        {
                            _RuntimeStreamTimer.Start();
                        }

                        DataSingleton.Instance().DisableOnResumeRendering = false;
                    }
                    else
                    {
                        Log.Info("EYESON APP", "NET_DVR_PlayBackByTime failed, error code: " + HCNetSDK.Instance.NET_DVR_GetLastError());
                        var code = HCNetSDK.Instance.NET_DVR_GetLastError();
                        var msg = code == 10 ? "Connection Time out, try again" : "";
                        ShowFancyMessage(GetApplicationContext(), "NET_DVR_PlayBackByTime failed, error code: " + code, message: msg, Color:Resource.Color.error_color_material_light, Duration:3000);
                    }
                }
                else
                {
                    StopPlayback();
                }
            }
            catch (System.Exception er)
            {
                Log.Error("EYESON APP", "Error: " + er.StackTrace);
                ShowFancyMessage(GetApplicationContext(), "Please login first", message: "Fatal error: " + er.StackTrace, Color: Resource.Color.error_color_material_light);
            }
        }

        private async void ResumePlaybackAfterOnResume(double ellapsedStremTimeOnRuntime, double ellapsedSeconds)
        {
            await Task.Delay(10);

            var minutes = ellapsedSeconds > 59 ? ellapsedSeconds / 60 : 0;
            var seconds = ellapsedSeconds <= 59 ? ellapsedSeconds : 0;

            var minutesOnRuntime = ellapsedStremTimeOnRuntime > 59 ? ellapsedStremTimeOnRuntime / 60 : 0;
            var secondsOnRuntime = ellapsedStremTimeOnRuntime <= 59 ? ellapsedStremTimeOnRuntime : 0 ;

            var totalMinutes = Convert.ToInt32(minutes + minutesOnRuntime);
            var totalSeconds = Convert.ToInt32(seconds + secondsOnRuntime);

            var secondsOverflow = 0;

            try
            {
                if (m_iLogID < 0)
                {
                    Log.Error("EYESON APP", "please login on a device first");
                    ShowFancyMessage(GetApplicationContext(), "Please login first", Color: Resource.Color.error_color_material_light);
                    return;
                }

                if (m_iPlaybackID < 0)
                {
                    if (m_iPlayID >= 0)
                    {
                        Log.Info("EYESON APP", "Please stop preview first");
                        ShowFancyMessage(GetApplicationContext(), "Please stop Preview function first", Color: Resource.Color.error_color_material_light);
                        return;
                    }

                    ChangeSingleSurFace(true);

                    ValueForSingleStreamChannel = true;

                    NET_DVR_TIME struBegin = new NET_DVR_TIME();
                    NET_DVR_TIME struEnd = new NET_DVR_TIME();

                    struBegin.DwYear = Year;
                    struBegin.DwMonth = Month;
                    struBegin.DwDay = Day;
                    struBegin.DwHour = Hour;

                    if (totalSeconds < 1)
                    {
                        struBegin.DwSecond = totalSeconds;
                    }
                    else
                    {
                        secondsOverflow = totalSeconds + Second;
                        if (secondsOverflow >= 60)
                        {
                            totalMinutes++;
                        }
                        struBegin.DwSecond = totalSeconds + Second;
                    }

                    if (totalMinutes < 1)
                    {
                        struBegin.DwMinute = Minute;
                    }
                    else
                    {
                        struBegin.DwMinute = totalMinutes + Minute;
                    }

                    Minute += totalMinutes;
                    Second += totalSeconds;

                    seconds = 0;
                    secondsOnRuntime = 0;
                    totalMinutes = 0;
                    totalSeconds = 0;

                    struEnd.DwYear = System.DateTime.UtcNow.Year;
                    struEnd.DwMonth = System.DateTime.UtcNow.Month;
                    struEnd.DwDay = System.DateTime.UtcNow.Day;
                    struEnd.DwHour = System.DateTime.Now.Hour;
                    struEnd.DwMinute = System.DateTime.UtcNow.Minute;
                    struEnd.DwSecond = System.DateTime.UtcNow.Second;

                    NET_DVR_VOD_PARA struVod = new NET_DVR_VOD_PARA();
                    struVod.StruBeginTime = struBegin;
                    struVod.StruEndTime = struEnd;
                    struVod.ByStreamType = 0;

                    if (SaveLoginSessionSingleton.SessionLoggedIn())
                    {
                        struVod.StruIDInfo.DwChannel = Convert.ToInt32(DataSingleton.Instance().Camera) - 1;
                    }
                    else
                    {
                        struVod.StruIDInfo.DwChannel = m_iStartChan == 0 ? Integer.ParseInt(m_iStartChan.ToString()) - 1 : Integer.ParseInt(m_iStartChan.ToString()) - 1;
                    }

                    struVod.HWnd = playView[0].Holder.Surface;
                 
                    m_iPlaybackID = HCNetSDK.Instance.NET_DVR_PlayBackByTime_V40(m_iLogID, struVod);

                    if (m_iPlaybackID >= 0)
                    {
                        NET_DVR_PLAYBACK_INFO struPlaybackInfo = null;
                        if (!HCNetSDK.Instance.NET_DVR_PlayBackControl_V40(m_iPlaybackID, PlaybackControlCommand.NetDvrPlaystart, null, 0, struPlaybackInfo))
                        {
                            Log.Error("EYESON APP", "net sdk playback start failed!");
                            ShowFancyMessage(GetApplicationContext(), "NET_SDK_Playback start failed, try again.");
                            return;
                        }
                        
                        m_bStopPlayback = false;

                        RunOnUiThread(() => m_oPlaybackBtn.Text = "Stop");

                        new System.Threading.Thread(new ThreadStart(() =>
                        {
                            while (true)
                            {
                                nProgress = HCNetSDK.Instance.NET_DVR_GetPlayBackPos(m_iPlaybackID);

                                System.Console.WriteLine("NET_DVR_GetPlayBackPos:" + nProgress);

                                if (nProgress < 0 || nProgress >= 100)
                                {
                                    break;
                                }

                                try
                                {
                                    System.Threading.Thread.Sleep(1000);
                                }
                                catch (InterruptedException er)
                                {
                                    er.PrintStackTrace();
                                }
                            }
                        })).Start();

                        if (!_RuntimeStreamTimer.IsRunning)
                        {
                            _RuntimeStreamTimer.Start();
                        }
                    }
                    else
                    {
                        Log.Info("EYESON APP", "NET_DVR_PlayBackByTime failed, error code: " + HCNetSDK.Instance.NET_DVR_GetLastError());
                        var code = HCNetSDK.Instance.NET_DVR_GetLastError();
                        var msg = code == 10 ? "Connection Time out, try again" : "";
                        ShowFancyMessage(GetApplicationContext(), "NET_DVR_PlayBackByTime failed, error code: " + code, message: msg, Color: Resource.Color.error_color_material_light, Duration: 3000);
                    }
                }
                else
                {
                    StopPlayback();

                    //InvokePlaybackListener(null, null);
                }
            }
            catch (System.Exception er)
            {
                Log.Error("EYESON APP", "Error: " + er.StackTrace);
                ShowFancyMessage(GetApplicationContext(), "Please login first", message: "Fatal error: " + er.StackTrace, Color: Resource.Color.error_color_material_light);
            }
        }

        private void Preview_Listener(object sender, EventArgs e)
        {
            if (!IsThereInternetOnDevice())
                return;

            InvokeStopPlaybackDelegate();

            //Task.Run(async () =>
            //{
            //    await Task.Delay(100);

            try
            {
                //await Task.Run(() =>
                //{
                //    using (var h = new Handler(Looper.MainLooper))
                //    {
                //        h.Post(() => ;
                //    }
                //});

                ChangeSingleSurFace(false);

                ValueForSingleStreamChannel = false;

                if (m_iLogID < 0)
                {
                    Log.Error("", "Please login in device first");
                    ShowFancyMessage(GetApplicationContext(), "Please log in first", Color: Resource.Color.error_color_material_light);
                    return;
                }
                if (m_iPlaybackID >= 0)
                {
                    Log.Info("", "Please stop palyback first");
                    ShowFancyMessage(GetApplicationContext(), "Please stop playback first", Color: Resource.Color.error_color_material_light, Duration: 23000);
                    return;
                }

                if (m_bNeedDecode)
                {
                    if (m_iChanNum > 1) //Preview more than a channel
                    {
                        //CAMERA INDEX
                        if (!m_bMultiPlay)
                        {
                            StartMultiplePreview(4);

                            m_bMultiPlay = true;

                            using (var h = new Handler(Looper.MainLooper))
                            {
                                h.Post(()=>
                                {
                                    m_oPreviewBtn.Text = "Stop";
                                });
                            }
                            OnResumePreviewStateSingleton.SetState(true);
                        }
                        else
                        {
                            StopmultiPreview();
                            m_bMultiPlay = false;
                            using (var h = new Handler(Looper.MainLooper))
                            {
                                h.Post(() =>
                                {
                                    m_oPreviewBtn.Text = "Preview";
                                });
                            }
                            OnResumePreviewStateSingleton.SetState(false);
                        }
                    }
                    else
                    {
                        if (m_iPlayID < 0)
                        {
                            StartSinglePreview();
                        }
                        else
                        {
                            StopSinglePreview();

                            using (var h = new Handler(Looper.MainLooper))
                            {
                                h.Post(() =>
                                {
                                    m_oPreviewBtn.Text = "Preview";
                                });
                            }
                            OnResumePreviewStateSingleton.SetState(false);
                        }
                    }
                }
            }
            catch (System.Exception er)
            {
                Log.Error("", "Error: " + er.ToString());
            }
            //});
        }

        private void ResumePreviewAfterOnResume()
        {
            if (!IsThereInternetOnDevice())
                return;

            StopPlayback();

            Task.Run(() =>
            {
                try
                {
                    ValueForSingleStreamChannel = false;

                    using (var h = new Handler(Looper.MainLooper))
                    {
                        h.Post(() => ChangeSingleSurFace(false));
                    }

                    if (m_iLogID < 0)
                    {
                        Log.Error("", "Please login in device first");
                        ShowFancyMessage(GetApplicationContext(), "Please log in first", Color: Resource.Color.error_color_material_light);
                        return;
                    }
                    if (m_iPlaybackID >= 0)
                    {
                        Log.Info("", "Please stop palyback first");
                        ShowFancyMessage(GetApplicationContext(), "Please stop playback first", Color: Resource.Color.error_color_material_light, Duration: 23000);
                        return;
                    }

                    if (m_bNeedDecode)
                    {
                        if (m_iChanNum > 1) //Preview more than a channel
                        {
                            //CAMERA INDEX
                            if (!m_bMultiPlay)
                            {
                                StartMultiplePreview(4);

                                m_bMultiPlay = true;

                                using (var h = new Handler(Looper.MainLooper))
                                {
                                    h.Post(() =>
                                    {
                                        m_oPreviewBtn.Text = "Stop";
                                    });
                                }
                            }
                            else
                            {
                                StopmultiPreview();
                                m_bMultiPlay = false;
                                using (var h = new Handler(Looper.MainLooper))
                                {
                                    h.Post(() =>
                                    {
                                        m_oPreviewBtn.Text = "Preview";
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (m_iPlayID < 0)
                            {
                                StartSinglePreview();
                            }
                            else
                            {
                                StopSinglePreview();

                                using (var h = new Handler(Looper.MainLooper))
                                {
                                    h.Post(() =>
                                    {
                                        m_oPreviewBtn.Text = "Preview";
                                    });
                                }
                            }
                        }
                    }

                    OnResumePreviewStateSingleton.SetState(true);
                }
                catch (System.Exception er)
                {
                    Log.Error("", "Error: " + er.ToString());
                }
            });
        }

        private void StopPlayback()
        {
            m_bStopPlayback = true;
            if (!HCNetSDK.Instance.NET_DVR_StopPlayBack(m_iPlaybackID))
            {
                Log.Error("EYESON APP", "net sdk stop playback failed");
            }

            RunOnUiThread(() => m_oPlaybackBtn.Text = "Play");

            m_iPlaybackID = -1;
        }

        private async void Login_Listener(EyesonApp.Models.Data data)
        {
            if (!IsThereInternetOnDevice())
                return;

            try
            {
                if (data.Ip.Trim().Length == 0 || data.Port <= 0 || data.Username.Trim().Length == 0 || data.Password.Trim().Length == 0)
                {
                    ShowFancyMessage(main, "Fill every field", Color:Resource.Color.error_color_material_light);
                    return;
                }
                if (m_iLogID < 0)
                {
                    //login in the device
                    m_iLogID = LoginDevice(data);
                    if (m_iLogID < 0)
                    {
                        Console.WriteLine("This device logins failed!");
                        ShowFancyMessage(GetApplicationContext(), "Login failed, try again", Position:CookieBar.Top, Color:Resource.Color.error_color_material_light);
                        return;
                    }
                    else
                    {
                        Console.WriteLine("m_iLogID=" + m_iLogID);
                    }

                    Log.Info("", "Login sucess ********************************************************");

                    ShowFancyMessage(GetApplicationContext(), "Logged in successfully");

                    m_iStartChan = Convert.ToInt32(data.Camera);

                    SaveLoginSessionSingleton.SaveUserLoggedSession(true);

                    await Task.Delay(300);

                    InvokePlaybackListener(null, null);
                }
                else
                {
                    if (!HCNetSDK.Instance.NET_DVR_Logout_V30(m_iLogID))
                    {
                        Log.Error("", "NET_DVR_Logout is failed!");
                        ShowFancyMessage(GetApplicationContext(), "NET_DVR_Logout is failed");
                        return;
                    }
                    m_iLogID = -1;
                }
            }
            catch (System.Exception er)
            {
                Log.Error("", "error: " + er.ToString());
            }
        }

        public static void ShowFancyMessage(Activity Activity, string Title, bool SwipeToDismissEnabled = true, string message = "", int Position = CookieBar.Top, int Color = Resource.Color.material_blue_grey_900, int Duration = 1000)
        {
            CookieBar.Build(Activity).SetTitle(Title)
                .SetSwipeToDismiss(SwipeToDismissEnabled)
                .SetCookiePosition(Position)
                .SetMessage(message)
                .SetIcon(Resource.Mipmap.ic_launcher)
                .SetBackgroundColor(Color)
                .SetDuration(Duration)
                .Show();
        }

        private void StopSinglePreview()
        {
            if (m_iPlayID < 0)
            {
                Log.Error("", "m_iPlayID < 0");
                return;
            }

            // net sdk stop preview
            if (!HCNetSDK.Instance.NET_DVR_StopRealPlay(m_iPlayID))
            {
                Log.Error("", "StopRealPlay is failed!Err:"
                        + HCNetSDK.Instance.NET_DVR_GetLastError());
                ShowFancyMessage(GetApplicationContext(), "StopRealPlay failed", Color:Resource.Color.error_color_material_light, Duration: 23000);
                return;
            }

            m_iPlayID = -1;
        }

        private void StartSinglePreview()
        {
            if (m_iPlaybackID >= 0)
            {
                Log.Info("", "Please stop plaback first");
                ShowFancyMessage(GetApplicationContext(), "Please stop playback first", Color: Resource.Color.error_color_material_light);
                return;
            }

            Log.Info("", "m_iStartChan: " + m_iStartChan);

            NET_DVR_PREVIEWINFO previewinfo = new NET_DVR_PREVIEWINFO()
            {
                LChannel = m_iStartChan,
                DwStreamType = 0,
                BBlocked = 1,
                HHwnd = playView[0].Holder
            };

            m_iPlaybackID = HCNetSDK.Instance.NET_DVR_RealPlay_V40(m_iLogID, previewinfo, null);
            if (m_iPlaybackID < 0)
            {
                Log.Error("", "NET_DVR_RealPlay is failed!Err: " + HCNetSDK.Instance.NET_DVR_GetLastError());
                return;
            }

            Log.Info("", "NetSdk Play sucess ***************************************************");
            
            m_oPreviewBtn.Text = "Stop";
        }

        private void StopmultiPreview()
        {
            for (int i = 0; i < 4; i++)
            {
                playView[i].StopPreview();
            }
            m_iPlayID = -1;
        }

        private void StartMultiplePreview(int index)
        {
            for (int i = 0; i < index; i++)
            {
                playView[i].StartPreview(m_iLogID, m_iStartChan + i);
            }

            m_iPlayID = playView[0].M_iPreviewHandle;
        }
        private int LoginDevice(EyesonApp.Models.Data data)
        {
            int iLogID = -1;

            iLogID = LoginNormalDevice(data);

            return iLogID;
        }

        private int LoginNormalDevice(EyesonApp.Models.Data data)
        {
            m_oNetDvrDeviceInfoV30 = new NET_DVR_DEVICEINFO_V30
            {
                ByAlarmInPortNum = default
            };

            if (null == m_oNetDvrDeviceInfoV30)
            {
                Console.WriteLine("HKNetDvrDeviceInfoV30 new is failed!");
                return -1;
            }

            string StrIP = data.Ip.Trim().ToString();
            int nPort = Convert.ToInt32(data.Port);
            string StrUser = data.Username.ToString();
            string StrPsd = data.Password.ToString();

            // call NET_DVR_Login_v30 to login on, port 8000 as default
            int iLogID = HCNetSDK.Instance.NET_DVR_Login_V30(StrIP, nPort, StrUser, StrPsd, m_oNetDvrDeviceInfoV30);

            if (iLogID < 0)
            {
                Console.WriteLine("NET_DVR_Login is failed!Err:" + HCNetSDK.Instance.NET_DVR_GetLastError());
                return -1;
            }

            if (m_oNetDvrDeviceInfoV30.ByChanNum > 0)
            {
                m_iStartChan = m_oNetDvrDeviceInfoV30.ByStartChan;
                m_iChanNum = m_oNetDvrDeviceInfoV30.ByChanNum;
            }
            else if (m_oNetDvrDeviceInfoV30.ByIPChanNum > 0)
            {
                m_iStartChan = m_oNetDvrDeviceInfoV30.ByStartDChan;
                m_iChanNum = 1 /* m_oNetDvrDeviceInfoV30.ByIPChanNum
                    + m_oNetDvrDeviceInfoV30.byHighDChanNum * 256*/;
            }

            /*if (m_iChanNum > 1)
            {
                ChangeSingleSurFace(false);
            }
            else
            {
                ChangeSingleSurFace(true);
            }*/

            Log.Info("", "NET_DVR_Login is Successful!");

            return iLogID;
        }
      
        //TODO Tablet devices Support..
        private void ChangeSingleSurFace(bool bSingle)
        {
            bool IsTabletDevice = DeviceInfo.Idiom == DeviceIdiom.Tablet ? true : false;
            var DisplayMetrics = new DisplayMetrics();
            GetApplicationContext().WindowManager.DefaultDisplay.GetMetrics(DisplayMetrics);

            void SetParamsForButtonsOnMultiViewMode(int i)
            {
                ConstraintLayout.LayoutParams _params = (ConstraintLayout.LayoutParams)m_oPlaybackBtn.LayoutParameters;
                _params.SetMargins(8, Convert.ToInt32(DisplayMetrics.WidthPixels + DisplayMetrics.WidthPixels * 0.5f), 8, 8);
                m_oPlaybackBtn.LayoutParameters = _params;

                ConstraintLayout.LayoutParams __params = (ConstraintLayout.LayoutParams)m_oCaptureBtn.LayoutParameters;
                __params.SetMargins(8, Convert.ToInt32(DisplayMetrics.WidthPixels + DisplayMetrics.WidthPixels * 0.5f), 8, 8);
                m_oCaptureBtn.LayoutParameters = __params;

                ConstraintLayout.LayoutParams ___params = (ConstraintLayout.LayoutParams)m_oRecordBtn.LayoutParameters;
                ___params.SetMargins(8, Convert.ToInt32(DisplayMetrics.WidthPixels + DisplayMetrics.WidthPixels * 0.5f), 8, 8);
                m_oRecordBtn.LayoutParameters = ___params;

                ConstraintLayout.LayoutParams ____params = (ConstraintLayout.LayoutParams)m_oPreviewBtn.LayoutParameters;
                ____params.SetMargins(8, Convert.ToInt32(DisplayMetrics.WidthPixels + DisplayMetrics.WidthPixels * 0.5f), 8, 8);
                m_oPreviewBtn.LayoutParameters = ____params;
            }

            var iteration_gen = 0;

            for (int i = 0; i < 4; i++)
            {
                if (playView[i] == null)
                {
                    playView[i] = new PlaySurfaceView(GetApplicationContext().ApplicationContext);
                   
                    #region PhoneResolution..

                    FrameLayout.LayoutParams @params = null;

                    if (!IsTabletDevice)
                    {
                        playView[i].SetParam(DisplayMetrics.WidthPixels);
                        DefineParams(out @params, i);
                        SetParamsForButtonsOnMultiViewMode(i);
                    }
                    else
                    {
                        iteration_gen++;
                        switch (iteration_gen)
                        {
                            case 1:
                                playView[i].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels - DisplayMetrics.WidthPixels * 0.2f));
                                @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                                @params.TopMargin = Convert.ToInt32(playView[i].M_iWidth + playView[i].M_iWidth * 0.2f);
                                @params.LeftMargin = Convert.ToInt32((DisplayMetrics.WidthPixels * 0.1f));
                                @params.RightMargin = 0;
                                break;
                            case 2:
                                playView[i].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels - DisplayMetrics.WidthPixels * 0.2f));
                                @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                                @params.TopMargin = Convert.ToInt32(playView[i].M_iWidth + playView[i].M_iWidth * 0.2f);
                                @params.RightMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.1f);
                                @params.LeftMargin = Convert.ToInt32(playView[i].M_iWidth + DisplayMetrics.WidthPixels * 0.1f);
                                break;
                            case 3:
                                playView[i].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels - DisplayMetrics.WidthPixels * 0.2f));
                                @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                                @params.TopMargin = 0;
                                @params.RightMargin = 0;
                                @params.LeftMargin = Convert.ToInt32((DisplayMetrics.WidthPixels * 0.1f));
                                break;
                            case 4:
                                playView[i].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels - DisplayMetrics.WidthPixels * 0.2f));
                                @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                                @params.RightMargin = Convert.ToInt32(playView[i].M_iWidth + DisplayMetrics.WidthPixels * 0.1f);
                                @params.TopMargin = 0;
                                @params.LeftMargin = Convert.ToInt32(playView[i].M_iWidth + DisplayMetrics.WidthPixels * 0.1f);
                                break;
                        }

                        @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.35f) + playView[i].M_iHeight - (i / 2) * playView[i].M_iHeight + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.1f);
                        @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;

                        //SetParamsForButtonsOnMultiViewMode(i);
                    }

                    #endregion TabletResolution..

                    GetApplicationContext().AddContentView(playView[i], @params);
                    playView[i].Visibility = ViewStates.Invisible;
                }
            }

            void DefineParams(out FrameLayout.LayoutParams @params, int i)
            {
                @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);

                @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.35f) + playView[i].M_iHeight - (i / 2) * playView[i].M_iWidth + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.24f);
                @params.LeftMargin = (i % 2) * playView[i].M_iWidth;
                @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;
            }

            void SetParamsForButtons()
            {
                ConstraintLayout.LayoutParams _params = (ConstraintLayout.LayoutParams)m_oPlaybackBtn.LayoutParameters;
                _params.SetMargins(8, Convert.ToInt32(playView[3].M_iHeight * 0.3f), 8, 8);
                m_oPlaybackBtn.LayoutParameters = _params;

                ConstraintLayout.LayoutParams __params = (ConstraintLayout.LayoutParams)m_oCaptureBtn.LayoutParameters;
                __params.SetMargins(8, Convert.ToInt32(playView[3].M_iHeight * 0.3f), 8, 8);
                m_oCaptureBtn.LayoutParameters = __params;

                ConstraintLayout.LayoutParams ___params = (ConstraintLayout.LayoutParams)m_oRecordBtn.LayoutParameters;
                ___params.SetMargins(8, Convert.ToInt32(playView[3].M_iHeight * 0.3f), 8, 8);
                m_oRecordBtn.LayoutParameters = ___params;

                ConstraintLayout.LayoutParams ____params = (ConstraintLayout.LayoutParams)m_oPreviewBtn.LayoutParameters;
                ____params.SetMargins(8, Convert.ToInt32(playView[3].M_iHeight * 0.3f), 8, 8);
                m_oPreviewBtn.LayoutParameters = ____params;

                ScrollScrollViewToTheBottom();
            }

            if (bSingle)
            {
                for (int i = 0; i < 4; ++i)
                {
                    playView[i].Visibility = ViewStates.Invisible;
                }

                FrameLayout.LayoutParams @params;

                if (!IsTabletDevice)
                {
                    playView[0].SetParam(DisplayMetrics.WidthPixels * 2);

                    @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                    @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.35f) + playView[3].M_iHeight - (3 / 2) * playView[3].M_iHeight + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.24f);
                    @params.LeftMargin = 0;
                    @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;
                    SetParamsForButtons();
                }
                else
                {
                    playView[0].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels + DisplayMetrics.WidthPixels * 0.2f));
                    @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                    @params.TopMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.2f);
                    @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.1f) + playView[3].M_iHeight - (3 / 2) * playView[3].M_iHeight + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.1f);
                    @params.Gravity = GravityFlags.Top | GravityFlags.Center;

                    SetParamsForButtons();
                }

                playView[0].LayoutParameters = @params;

                playView[0].Visibility = ViewStates.Visible;
            }
            else
            {
                for (int i = 0; i < 4; ++i)
                {
                    playView[i].Visibility = ViewStates.Visible;
                }

                playView[0].SetParam(DisplayMetrics.WidthPixels);

                FrameLayout.LayoutParams @params;

                if (!IsTabletDevice)
                {
                    @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                    @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.35f) + playView[0].M_iHeight - (0 / 2) * playView[0].M_iHeight + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.24f);
                    @params.LeftMargin = (0 % 2) * playView[0].M_iWidth;
                    @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;
                    SetParamsForButtons();
                }
                else
                {
                    playView[0].SetParam(Convert.ToInt32(DisplayMetrics.WidthPixels - DisplayMetrics.WidthPixels * 0.2f));
                    @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);
                    @params.TopMargin = Convert.ToInt32(playView[0].M_iWidth + playView[0].M_iWidth * 0.2f);
                    @params.LeftMargin = Convert.ToInt32((DisplayMetrics.WidthPixels * 0.1f));
                    @params.RightMargin = 0;
                    @params.BottomMargin = Convert.ToInt32(DisplayMetrics.WidthPixels * 0.35f) + playView[0].M_iHeight - (0 / 2) * playView[0].M_iHeight + Convert.ToInt32(DisplayMetrics.WidthPixels * 0.1f);
                    @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;

                    SetParamsForButtons();
                }

                playView[0].LayoutParameters = @params;

                SetParamsForButtonsOnMultiViewMode(0);
            }
        }

        public void FExceptionCallBack(int p0, int p1, int p2)
        {
            throw new System.Exception("Not Implemented");
        }

        public void AfterTextChanged(IEditable s)
        {
            throw new System.Exception("Not Implemented");
        }

        public void BeforeTextChanged(ICharSequence s, int start, int count, int after)
        {
            throw new System.Exception("Not Implemented");
        }

        public void OnTextChanged(ICharSequence s, int start, int before, int count)
        {
            if (s.Length() > 0)
            {
                m_iStartChan = Convert.ToInt32(s.ToString()) - 1;
            }
        }

        protected override void OnPause()
        {
            base.OnPause();

            Xamarin.Essentials.Connectivity.ConnectivityChanged -= Connectivity_ConnectivityChanged;

            //DisposeListeners();

            if (m_oPlaybackBtn.Text != "Stop")
                return;

            AllowedToStream = true;

            StopmultiPreview();

            InvokeStopPlaybackDelegate();

            if (OnPauseStateSingleton.PauseStarted())
            {
                if (!_Timer.IsRunning)
                {
                    _Timer.Start();
                }

                if (_RuntimeStreamTimer.IsRunning)
                {
                    _RuntimeStreamTimer.Stop();
                    StreamTimeElapsedOnRuntime = _RuntimeStreamTimer.Elapsed.TotalSeconds;
                }
                OnPauseStateSingleton.SaveOnPauseState(false);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            Xamarin.Essentials.Connectivity.ConnectivityChanged -= Connectivity_ConnectivityChanged;
            Xamarin.Essentials.Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;

            //SetListeners();

            if (AllowedToStream)
            {
                if (SaveOnResumeStateSingleton.ResumeStarted() /*&& DataSingleton.Instance().DisableOnResumeRendering != true*/)
                {
                    StopPlayback();

                    _Timer.Stop();

                    SecondsElapsed = _Timer.Elapsed.TotalSeconds;

                    ResumePlaybackAfterOnResume(StreamTimeElapsedOnRuntime, SecondsElapsed);

                    SecondsElapsed = 0;

                    StreamTimeElapsedOnRuntime = 0;

                    AllowedToStream = false;
                }

                if (m_oPreviewBtn.Text == "Stop" && OnResumePreviewStateSingleton.ResumeStarted())
                {
                    m_bMultiPlay = false;

                    InitPreviewListener(null, null);

                    AllowedToStream = false;
                }

                OnPauseStateSingleton.SaveOnPauseState(false);
            }

            Xamarin.Essentials.Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            if(newConfig.Orientation == Android.Content.Res.Orientation.Portrait)
            {
                using (var h = new Handler(Looper.MainLooper))
                {
                    h.Post(() =>
                    {
                        ChangeSingleSurFace(ValueForSingleStreamChannel);   
                    });
                }
            }else if(newConfig.Orientation == Android.Content.Res.Orientation.Landscape)
            {
                using (var h = new Handler(Looper.MainLooper))
                {
                    h.Post(() =>
                    {
                        ChangeSingleSurFace(ValueForSingleStreamChannel);
                    });
                }
            }
        }
    }
}

