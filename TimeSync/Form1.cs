using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace TimeSync
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public bool syncSuccessful = false;
        public bool formClosing = false;

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Millisecond;
        };

        [DllImport("kernel32.dll", EntryPoint = "GetSystemTime", SetLastError = true)]
        public extern static void Win32GetSystemTime(ref SystemTime sysTime);

        [DllImport("kernel32.dll", EntryPoint = "SetSystemTime", SetLastError = true)]
        public extern static bool Win32SetSystemTime(ref SystemTime sysTime);

        public string HttpGet(string url)
        {
            WebClient client = new WebClient();
            string content = string.Empty;
            Stream stream;

            try
            {
                stream = client.OpenRead(url);
                StreamReader reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                // MessageBox.Show(ex.Message);
                return null;
            }

            return content;
        }

        private void SetDateTime(DateTime dt)
        {
            SystemTime updatedTime = new SystemTime
            {
                Year = (ushort)dt.Year,
                Month = (ushort)dt.Month,
                Day = (ushort)dt.Day,
                Hour = (ushort)dt.Hour,
                Minute = (ushort)dt.Minute,
                Second = (ushort)dt.Second
            };

            Win32SetSystemTime(ref updatedTime);
        }

        public void AsyncStatusLabel(string text)
        {
            try
            {
                label1.Invoke((MethodInvoker)delegate
                {
                    // Running on the UI thread
                    label1.Text = text;
                });
            }
            catch (Exception) { }
        }

        public void GetApiTime()
        {
            if (formClosing) return;

            AsyncStatusLabel($"Status: starting [{DateTime.Now.ToLongTimeString()}]");

            string resp = HttpGet(Properties.Settings.Default.ApiPath);

            if (resp != null)
            {
                try
                {
                    JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
                    var result = jsSerializer.DeserializeObject(resp);
                    Dictionary<string, object> json = new Dictionary<string, object>();
                    json = (Dictionary<string, object>)(result);

                    string timestamp = json["response"].ToString();

                    var respMatch = Regex.Matches(timestamp, @"\d+");

                    if (respMatch.Count > 0)
                    {
                        DateTime dt = UnixTimeStampToDateTime(Convert.ToDouble(timestamp));

                        AsyncStatusLabel($"Date/Time: {dt.ToString()}");

                        syncSuccessful = true;

                        SetDateTime(dt);
                    }
                }
                catch (Exception)
                {
                    AsyncStatusLabel($"Status: bad json [{DateTime.Now.ToLongTimeString()}]");

                    Thread.Sleep(Properties.Settings.Default.ErrorDelay);
                    GetApiTime();
                }
            }
            else
            {
                AsyncStatusLabel($"Status: bad response [{DateTime.Now.ToLongTimeString()}]");

                Thread.Sleep(Properties.Settings.Default.ErrorDelay);
                GetApiTime();
            }
        }

        public void StartSync()
        {
            label1.Text = $"Status: starting sync [{DateTime.Now.ToLongTimeString()}]";

            ThreadStart ts = new ThreadStart(GetApiTime);
            ts += () =>
            {
                if (!formClosing)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        if (syncSuccessful)
                        {
                            Close();
                        }
                    });
                }
            };

            Thread thread = new Thread(ts);
            thread.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StartSync();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            StartSync();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            formClosing = true;
        }
    }
}
