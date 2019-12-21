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

namespace EyesonApp.Services
{
    [Preserve(AllMembers = true)]
    public class SaveLoginSessionSingleton
    {
        static bool IsLoggedIn { get; set; } = false;

        public static bool SessionLoggedIn()
        {
            if (IsLoggedIn)
	        {
                IsLoggedIn = true;
	        }
            return IsLoggedIn;
        }

        public static bool SaveUserLoggedSession(bool save) => IsLoggedIn = save;
    }

    [Preserve(AllMembers = true)]
    public class SaveOnResumeStateSingleton
    {
        static bool OnResume { get; set; } = false;

        public static bool ResumeStarted()
        {
            if (OnResume)
            {
                OnResume = true;
            }
            return OnResume;
        }

        public static bool SaveOnResumeState(bool save) => OnResume = save;
    }

    [Preserve(AllMembers = true)]
    public class OnPauseStateSingleton
    {
        static bool OnPause { get; set; } = false;

        static int Progress { get; set; } = -1;

        public static bool PauseStarted()
        {
            if (OnPause)
            {
                OnPause = true;
            }
            return OnPause;
        }

        public static int ProgressValue()
        {
            if (Progress >= -1)
            {
                return Progress;
            }
            return -1;
        }

        public static bool SaveOnPauseState(bool save) => OnPause = save;

        public static int SaveProgressState(int progress) => Progress = progress;
    }

    public class OnResumePreviewStateSingleton
    {
        static bool OnResume { get; set; } = false;

        public static bool ResumeStarted()
        {
            if (OnResume)
            {
                return OnResume = true;
            }
            return OnResume;
        }

        public static bool SetState(bool state) => OnResume = state;
    }
}