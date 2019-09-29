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
}