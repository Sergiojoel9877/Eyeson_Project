using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Hikvision.Netsdk;
using EyesonApp;

namespace EyesonApp.Controls
{
    public class PlaySurfaceView : SurfaceView, ISurfaceHolderCallback
    {
        private const string TAG = "PlaySurfaceView";

        public int M_iWidth { get; set; } = 0;
        public int M_iHeight { get; set; } = 0;

        public int M_iPreviewHandle = -1;

        private ISurfaceHolder M_hHolder;

        public bool BCreate = false;

        public int M_lUserID = -1;
        public int M_iChan = 0;

        public PlaySurfaceView(MainActivity context) : base(context)
        {
            M_hHolder = this.Holder;
            Holder.AddCallback(this);
        }

        public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
        {
            SetZOrderOnTop(true);
            Holder.SetFormat(Format.Translucent);
            Console.WriteLine("SurfaceChanged");
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            BCreate = true;
            Console.WriteLine("SurfaceCreated");
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            Console.WriteLine("SurfaceDestroyed");
            BCreate = false;
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.SetMeasuredDimension(M_iWidth - 1, M_iHeight - 1);
        }

        public void SetParam(int NScreenSize)
        {
            M_iWidth = NScreenSize / 2;
            M_iHeight = (M_iWidth * 3) / 4;
        }

        public void StartPreview(int IUserID, int IChan)
        {
            Console.WriteLine(TAG, "Preview channel:" + IChan);

            while (!BCreate)
            {
                try
                {
                    Task.Delay(100);
                    Console.WriteLine("Wait for the surface create");
                }
                catch (Java.Lang.InterruptedException e)
                {
                    e.PrintStackTrace();
                }
            }

            NET_DVR_PREVIEWINFO previewinfo = new NET_DVR_PREVIEWINFO()
            {
                LChannel = IChan,
                DwStreamType = 0, //Substream
                BBlocked = 1,
                HHwnd = M_hHolder
            };

            M_iPreviewHandle = HCNetSDK.Instance.NET_DVR_RealPlay_V40(IUserID, previewinfo, null);

            if (M_iPreviewHandle < 0)
            {
                Console.WriteLine(TAG, "NET_DVR_RealPlay is failed!Err: " + HCNetSDK.Instance.NET_DVR_GetLastError());
                using (var h = new Handler(Looper.MainLooper))
                {
                    h.Post(()=>
                    {
                        MainActivity.ShowFancyMessage(MainActivity.GetApplicationContext(), "Preview has failed", message: $"Error code: {HCNetSDK.Instance.NET_DVR_GetLastError()} ");
                    });
                }
                MainActivity.ShowFancyMessage(MainActivity.GetApplicationContext(), "Preview has failed", message:$"Error code: {HCNetSDK.Instance.NET_DVR_GetLastError()} ");
            }
        }

        public void StopPreview()
        {
            HCNetSDK.Instance.NET_DVR_StopRealPlay(M_iPreviewHandle);
        }
    }
}