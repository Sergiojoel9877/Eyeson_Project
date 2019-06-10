using System;
using System.Net;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Icu.Text;
using Android.Icu.Util;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Hikvision.Netsdk;
using EyesonApp.Controls;
using EyesonApp.Services;
using Java.Lang;
using Java.Util;
using Org.MediaPlayer.PlayM4;

namespace EyesonApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, IExceptionCallBack, ITextWatcher
    {
        private static Button m_oLoginBtn = null;
        private static Button m_oPreviewBtn = null;
        private static Button m_oPlaybackBtn = null;
        private static Button m_oParamCfgBtn = null;
        private static Button m_oCaptureBtn = null;
        private static Button m_oRecordBtn = null;
        private static Button m_oTalkBtn = null;
        private static Button m_oPTZBtn = null;
        private static Button m_oOtherBtn = null;
        private static EditText m_oIPAddr = null;
        private static EditText m_oPort = null;
        private static EditText m_oUser = null;
        private static EditText m_oPsd = null;
        private static EditText m_oCam = null;
        private static EditText m_oDate = null;
        private static EditText m_oTime = null;
        private static TextView m_IPAdrs = null;
        private static TimePicker timePicker;
        private static DatePicker datePicker;
        private Android.Icu.Util.Calendar calendar;
        private NET_DVR_DEVICEINFO_V30 m_oNetDvrDeviceInfoV30 = null;

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

        private static int Year { get; set; }
        private static int Month { get; set; }
        private static int Day { get; set; }
                
        private static int Hour { get; set; }
        private static int Minute { get; set; }

        private static int m_iStartChan { get; set; } = 0; // start channel no
        private static int m_iChanNum { get; set; } = 0; // channel number

        private static PlaySurfaceView[] playView = new PlaySurfaceView[4];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
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

            SetIPAddressToIPLabel();
        }

        private void StartSocketListening()
        {
            new System.Threading.Thread(new ThreadStart(() => {
                AsynchronousSocketListener.StartListening();
            })).Start();
        }

        private void SetIPAddressToIPLabel()
        {
            var IP = GetIP();
            m_IPAdrs.Text = m_IPAdrs.Text + ":" + IP + " Port: 7555";
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
            var _result = DataSingleton.Instance();

            using (var h = new Handler(Looper.MainLooper))
            {
                h.Post(()=>
                {
                    m_oCam.Text = Convert.ToString(_result.Camera);
                    m_oIPAddr.Text = _result.Ip;
                    m_oPort.Text = Convert.ToString(_result.Port);
                    m_oPsd.Text = _result.Password;
                    m_oUser.Text = _result.Username;
                    ParseDate(_result.Date);
                    m_oTime.Text = new System.Text.StringBuilder().Append(Hour).Append(":").Append(Minute).ToString();
                    m_oDate.Text = new System.Text.StringBuilder().Append(Day).Append("/").Append(Month).Append("/").Append(Year).ToString();

                    m_iStartChan = Convert.ToInt32(m_oCam.Text) - 1;
                });
            }
        }

        private static void ParseDate(DateTimeOffset date)
        {
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
            View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
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
            m_oLoginBtn = (Button)FindViewById(Resource.Id.btn_Login);
            m_oPreviewBtn = (Button)FindViewById(Resource.Id.btn_Preview);
            m_oPlaybackBtn = (Button)FindViewById(Resource.Id.btn_Playback);
            //m_oParamCfgBtn = (Button)FindViewById(Resource.Id.btn_ParamCfg);
            m_oCaptureBtn = (Button)FindViewById(Resource.Id.btn_Capture);
            m_oRecordBtn = (Button)FindViewById(Resource.Id.btn_Record);
            //m_oTalkBtn = (Button)FindViewById(Resource.Id.btn_Talk);
            //m_oPTZBtn = (Button)FindViewById(Resource.Id.btn_PTZ);
            //m_oOtherBtn = (Button)FindViewById(Resource.Id.btn_OTHER);
            m_oIPAddr = (EditText)FindViewById(Resource.Id.EDT_IPAddr);
            m_oPort = (EditText)FindViewById(Resource.Id.EDT_Port);
            m_oUser = (EditText)FindViewById(Resource.Id.EDT_User);
            m_oPsd = (EditText)FindViewById(Resource.Id.EDT_Psd);
            m_oCam = (EditText)FindViewById(Resource.Id.EDT_Cam);
            m_oDate = (EditText)FindViewById(Resource.Id.EDT_Date);
            m_oTime = (EditText)FindViewById(Resource.Id.EDT_Hr);
            m_IPAdrs = (TextView)FindViewById(Resource.Id.ipPlaceHolder);

            m_oDate.FocusChange += M_oDate_FocusChange;
            m_oTime.FocusChange += M_oTime_FocusChange;

            m_oCam.AddTextChangedListener(this);
        }

        private void M_oCam_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {

        }

        private void M_oTime_FocusChange(object sender, View.FocusChangeEventArgs e)
        {
            ShowDialog(998);
        }

        private void M_oDate_FocusChange(object sender, View.FocusChangeEventArgs e)
        {
            ShowDialog(999);
        }

        protected override Dialog OnCreateDialog(int id)
        {
            if (id == 999)
            {
                return new DatePickerDialog(this, DateListener, Year, Month, Day);
            }
            if (id == 998)
            {
                return new TimePickerDialog(this, TimeListener, Hour, Minute, false);
            }
            return base.OnCreateDialog(id); 
        }

        private void TimeListener(object sender, TimePickerDialog.TimeSetEventArgs e)
        {
            ShowTime(e.HourOfDay, e.Minute);
        }

        private void ShowTime(int hourOfDay, int minute)
        {
            Hour = hourOfDay;
            Minute = minute;

            m_oTime.Text = new System.Text.StringBuilder().Append(hourOfDay).Append(":")
                    .Append(minute).ToString();
        }

        private void DateListener(object sender, DatePickerDialog.DateSetEventArgs e)
        {
            ShowDate(e.Year, e.Month + 1, e.DayOfMonth);
        }

        private void ShowDate(int year, int v, int dayOfMonth)
        {
            Year = year;
            Month = v;
            Day = dayOfMonth;

            m_oDate.Text = new System.Text.StringBuilder().Append(dayOfMonth).Append("/").Append(v).Append("/").Append(year).ToString();
        }

        private void SetListeners()
        {
            m_oLoginBtn.Click += Login_Listener;
            m_oPreviewBtn.Click += Preview_Listener;
        }

        private void Preview_Listener(object sender, EventArgs e)
        {
            try
            {
                if (m_iLogID < 0)
                {
                    Log.Error("", "Please login in device first");
                    return;
                }
                if (m_iPlaybackID >= 0)
                {
                    Log.Info("", "Please stop palyback first");
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
                            m_oPreviewBtn.Text = "Stop";
                        }
                        else
                        {
                            StopmultiPreview();
                            m_bMultiPlay = false;
                            m_oPreviewBtn.Text = "Preview";
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
                            m_oPreviewBtn.Text = "Preview";
                        }
                    }
                }
            }
            catch (System.Exception er)
            {
                Log.Error("", "Error: " + er.ToString());
            }
        }

        private void Login_Listener(object sender, EventArgs e)
        {
            try
            {
                if (m_iLogID < 0)
                {
                    //login in the device
                    m_iLogID = LoginDevice();
                    if (m_iLogID < 0)
                    {
                        Console.WriteLine("This device logins failed!");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("m_iLogID=" + m_iLogID);
                    }

                    m_oLoginBtn.Text = "Logout";
                    Log.Info("", "Login sucess ****************************1***************************");

                }
                else
                {
                    if (!HCNetSDK.Instance.NET_DVR_Logout_V30(m_iLogID))
                    {
                        Log.Error("", "NET_DVR_Logout is failed!");
                        return;
                    }
                    m_oLoginBtn.Text = "Login";
                    m_iLogID = -1;
                }
            }
            catch (System.Exception er)
            {
                Log.Error("", "error: " + er.ToString());
            }
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
                return;
            }

            m_iPlayID = -1;
        }

        private void StartSinglePreview()
        {
            if (m_iPlaybackID >= 0)
            {
                Log.Info("", "Please stop plaback first");
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

            Log.Info("", "NetSdk Play sucess ***********************3***************************");
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
        private int LoginDevice()
        {
            int iLogID = -1;

            iLogID = LoginNormalDevice();

            // iLogID = JNATest.TEST_EzvizLogin();
            // iLogID = loginEzvizDevice();

            return iLogID;
        }

        private int LoginNormalDevice()
        {
            m_oNetDvrDeviceInfoV30 = new NET_DVR_DEVICEINFO_V30();

            if (null == m_oNetDvrDeviceInfoV30)
            {
                Console.WriteLine("HKNetDvrDeviceInfoV30 new is failed!");
                return -1;
            }

            string StrIP = m_oIPAddr.Text.ToString();
            int nPort = int.Parse(m_oPort.Text.ToString());
            string StrUser = m_oUser.Text.ToString();
            string StrPsd = m_oPsd.Text.ToString();

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

            if (m_iChanNum > 1)
            {
                ChangeSingleSurFace(false);
            }
            else
            {
                ChangeSingleSurFace(true);
            }

            Log.Info("", "NET_DVR_Login is Successful!");

            return iLogID;
        }

        private void ChangeSingleSurFace(bool bSingle)
        {
            DisplayMetrics metric = new DisplayMetrics();
            WindowManager.DefaultDisplay.GetMetrics(metric);

            for (int i = 0; i < 4; i++)
            {
                if (playView[i] == null)
                {
                    playView[i] = new PlaySurfaceView(this);
                    playView[i].SetParam(metric.WidthPixels);

                    FrameLayout.LayoutParams @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);

                    @params.BottomMargin = playView[i].M_iHeight - (i / 2) * playView[i].M_iWidth;
                    @params.LeftMargin = (i % 2) * playView[i].M_iWidth;
                    @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;

                    playView[0].LayoutParameters = @params;

                    AddContentView(playView[i], @params);
                    playView[i].Visibility = ViewStates.Invisible;
                }
            }

            if (bSingle)
            {
                for (int i = 0; i < 4; i++)
                {
                    playView[i].Visibility = ViewStates.Invisible;
                }

                playView[0].SetParam(metric.WidthPixels * 2);

                FrameLayout.LayoutParams @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);

                @params.BottomMargin = playView[3].M_iHeight - (3 / 2) * playView[3].M_iHeight;
                @params.LeftMargin = 0;
                @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;

                playView[0].LayoutParameters = @params;

                playView[0].Visibility = ViewStates.Visible;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    playView[i].Visibility = ViewStates.Visible;
                }

                playView[0].SetParam(metric.WidthPixels);
                FrameLayout.LayoutParams @params = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WrapContent, FrameLayout.LayoutParams.WrapContent);

                @params.BottomMargin = playView[0].M_iHeight - (0 / 2) * playView[0].M_iHeight;
                @params.LeftMargin = (0 % 2) * playView[0].M_iWidth;
                @params.Gravity = GravityFlags.Bottom | GravityFlags.Left;

                playView[0].LayoutParameters = @params;
            }
        }

        public void FExceptionCallBack(int p0, int p1, int p2)
        {
            throw new NotImplementedException();
        }

        public void AfterTextChanged(IEditable s)
        {
            throw new NotImplementedException();
        }

        public void BeforeTextChanged(ICharSequence s, int start, int count, int after)
        {
            throw new NotImplementedException();
        }

        public void OnTextChanged(ICharSequence s, int start, int before, int count)
        {
            if (s.Length() > 0)
            {
                m_iStartChan = Convert.ToInt32(s.ToString()) - 1;
            }
        }
    }
}

