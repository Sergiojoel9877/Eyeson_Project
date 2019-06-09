using System;
using Android.App;
using Android.Content;
using Android.Icu.Util;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Hikvision.Netsdk;
using EyesonApp.Controls;
using Org.MediaPlayer.PlayM4;

namespace EyesonApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, IExceptionCallBack
    {
        private Button m_oLoginBtn = null;
        private Button m_oPreviewBtn = null;
        private Button m_oPlaybackBtn = null;
        private Button m_oParamCfgBtn = null;
        private Button m_oCaptureBtn = null;
        private Button m_oRecordBtn = null;
        private Button m_oTalkBtn = null;
        private Button m_oPTZBtn = null;
        private Button m_oOtherBtn = null;
        private EditText m_oIPAddr = null;
        private EditText m_oPort = null;
        private EditText m_oUser = null;
        private EditText m_oPsd = null;
        private EditText m_oCam = null;
        private EditText m_oDate = null;
        private EditText m_oTime = null;
        private TextView m_IPAdrs = null;
        private TimePicker timePicker;
        private DatePicker datePicker;
        private Calendar calendar;
        private NET_DVR_DEVICEINFO_V30 m_oNetDvrDeviceInfoV30 = null;

        private int m_iLogID = -1; // return by NET_DVR_Login_v30
        private int m_iPlayID = -1; // return by NET_DVR_RealPlay_V30
        private int m_iPlaybackID = -1; // return by NET_DVR_PlayBackByTime

        private int m_iPort = -1; // play port

        private bool m_bTalkOn = false;
        private bool m_bPTZL = false;
        private bool m_bMultiPlay = false;

        private bool m_bNeedDecode = true;
        private bool m_bSaveRealData = false;
        private bool m_bStopPlayback = false;

        public int getM_iStartChan()
        {
            return m_iStartChan;
        }

        public void setM_iStartChan(int m_iStartChan)
        {
            this.m_iStartChan = m_iStartChan;
        }

        private int m_iStartChan = 0; // start channel no
        private int m_iChanNum = 0; // channel number
        private static PlaySurfaceView[] playView = new PlaySurfaceView[4];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            if (!InitSDK())
            {
                Finish();
            }

            if (!InitActivity())
            {
                Finish();
            }
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
            catch (Exception er)
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
            catch (Exception er)
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
    }
}

