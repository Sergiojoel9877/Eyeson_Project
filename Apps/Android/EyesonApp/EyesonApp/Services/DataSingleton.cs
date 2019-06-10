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
using EyesonApp.Models;

namespace EyesonApp.Services
{
    public class DataSingleton
    {
        static Data _Data { get; set; }
  
        public static Data Instance()
        {
            if (_Data == null)
            {
                _Data = new Data();
            }
            return _Data;
        } 

        public static Data SetInstance(Data _data) => _Data = _data;

        public static void SetInstanceToNull()
        {
            _Data = null;
        }
    }
}