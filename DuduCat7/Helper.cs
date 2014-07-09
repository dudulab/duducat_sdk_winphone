using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;

namespace DuduCat
{
    internal static class Helper
    {
        public static void SafeInvoke(Action action)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            });
        }

        public static DateTime? ConvertTime(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            else
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddSeconds(long.Parse(s));
            }
        }
    }
}
