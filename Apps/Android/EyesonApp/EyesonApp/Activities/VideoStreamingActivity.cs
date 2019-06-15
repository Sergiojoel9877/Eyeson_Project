using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using EyesonApp.Controls;

namespace EyesonApp.Activities
{
    [Activity(Label = "VideoStreamingActivity")]
    public class VideoStreamingActivity : Activity
    {

        private static PlaySurfaceView[] playView = new PlaySurfaceView[4];

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your application here
        }
    }
}