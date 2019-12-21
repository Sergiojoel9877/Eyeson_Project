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
using Newtonsoft.Json;

namespace EyesonApp.Models
{
    public class Data
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("port")]
        public long Port { get; set; }

        [JsonProperty("camera")]
        public long Camera { get; set; }

        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }

        [JsonIgnore]
        public bool DisableOnResumeRendering { get; set; }
    }
}