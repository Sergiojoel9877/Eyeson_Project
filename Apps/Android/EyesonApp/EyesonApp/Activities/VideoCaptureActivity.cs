using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Icu.Util;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Com.Hikvision.Netsdk;
using EyesonApp.Controls;

namespace EyesonApp.Activities
{
    public class VideoCaptureActivity : AppCompatActivity, IExceptionCallBack
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

        public VideoCaptureActivity()
        {

        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (!InitSDK())
            {
                Finish();
            }

            if (!InitActivity())
            {
                Finish();
            }
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

        public void FExceptionCallBack(int p0, int p1, int p2)
        {
            throw new NotImplementedException();
        }
    }
}