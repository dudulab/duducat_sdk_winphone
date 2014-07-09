using DuduCat.Config.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DuduCat.Config
{
    public enum ConfigType
    {
        Text,
        Image,
    }

    public class ConfigUpdatedEventArgs : EventArgs
    {
        public string Key { get; set; }
        public ConfigType Type { get; set; }
        public object Value { get; set; }
    }

    public static class ActiveConfig
    {
        private static string _appKey;
        private static string _appSecret;
        private static bool _isRegistered;
        private static Timer _updateTimer;
        private static TimeSpan _updateInterval = new TimeSpan(1, 0, 0);

        private static DBContext CacheManger = new DBContext();
        private static object _databaseLock = new object();

        public static event EventHandler<ConfigUpdatedEventArgs> ConfigUpdated;

        /// <summary>
        /// Gets or sets the interval indicating how often the config is updated.
        /// Default is 1 hour.
        /// </summary>
        public static TimeSpan UpdateInterval
        {
            get
            {
                return _updateInterval;
            }

            set
            {
                _updateInterval = value;
                _updateTimer.Change(_updateInterval, _updateInterval);
            }
        }

        public static bool Register(string appKey, string appSecret)
        {
            _appKey = appKey;
            _appSecret = appSecret;

            string url = new UrlBuilder()
                .SetPath("/ActiveConfig/v1/Register/")
                .AddParam("appid", _appKey)
                .AddParam("secretkey", _appSecret)
                .GetUrl();

            JSONClass deviceInfo = new JSONClass();
            deviceInfo.Add("sys", new JSONData("WindowsPhone"));
            deviceInfo.Add("version", new JSONData(Environment.OSVersion.Version.ToString()));
            deviceInfo.Add("language", new JSONData(System.Threading.Thread.CurrentThread.CurrentCulture.Name));
            deviceInfo.Add("resolution", new JSONData(Application.Current.Host.Content.ActualWidth + "*" + Application.Current.Host.Content.ActualHeight));
            deviceInfo.Add("carrier", new JSONData(Microsoft.Phone.Net.NetworkInformation.DeviceNetworkInformation.CellularMobileOperator ?? ""));

            string body = string.Format("appid={0}&secretkey={1}&info={2}", _appKey, _appSecret, deviceInfo.ToString());
            Http.PostStringAsync(url, body, (s, e) =>
            {
                if (e == null)
                {
                    var dict = JSON.Parse(s);
                    if (dict != null)
                    {
                        string code = dict["code"].Value;
                        string msg = dict["msg"].Value;
                        string data = dict["data"].Value;

                        if (code != "0")
                        {
                            Debug.WriteLine("Server return error {0}: {1}", code, msg);
                            return;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine(e.Message);
                }
            });

            _updateTimer = new Timer(OnTimer, null, _updateInterval, _updateInterval);

            _isRegistered = true;
            return false;
        }

        public static IAsyncResult SetTextBlockWithKey(TextBlock control, string key, string defaultText)
        {
            if (control == null || key == null)
            {
                throw new ArgumentNullException();
            }

            textControls[control.GetHashCode()] = key;
            return GetTextAsync(key, defaultText, (s) =>
            {
                lock (textControls)
                {
                    if (textControls[control.GetHashCode()] == key)
                    {
                        control.Text = s;
                    }
                }
            });
        }

        public static IAsyncResult GetTextAsync(string key, string defaultValue, Action<string> callBack, bool forceUpdate = false)
        {
            if (!_isRegistered)
            {
                throw new InvalidOperationException("Call Register first");
            }

            var hit = CacheManger.Configs.FirstOrDefault(x => x.ID == string.Format("{0}:{1}", (int)ConfigType.Text, key));
            var call = callBack;
            if (hit != null && hit.Status == ItemStatus.KeyNotFound)
            {
                Helper.SafeInvoke(() => call(defaultValue));
                return null;
            }
            else if (forceUpdate || hit == null || hit.ExpireTime < DateTime.Now)
            {
                return QueryItemFromServerAsync(key, ConfigType.Text, (s, e) =>
                    {
                        try
                        {
                            if (e == null)
                            {
                                if (hit == null)
                                {
                                    lock (_databaseLock)
                                    {
                                        hit = CacheManger.Configs.FirstOrDefault(x => x.ID == string.Format("{0}:{1}", (int)ConfigType.Text, key));
                                        if (hit == null)
                                        {
                                            CacheManger.Configs.InsertOnSubmit(s);
                                            CacheManger.SubmitChanges();
                                        }
                                    }
                                }
                                else
                                {
                                    hit.Value = s.Value;
                                    hit.ExpireTime = s.ExpireTime;
                                    hit.MD5 = s.MD5;

                                    lock (_databaseLock)
                                    {
                                        CacheManger.SubmitChanges();
                                    }
                                }

                                Helper.SafeInvoke(() => call(s.Value));
                            }
                            else  // e != null
                            {
                                if (e is KeyNotFoundException)
                                {
                                    if (hit == null)
                                    {
                                        lock (_databaseLock)
                                        {
                                            hit = CacheManger.Configs.FirstOrDefault(x => x.ID == string.Format("{0}:{1}", (int)ConfigType.Text, key));
                                            {
                                                if (hit == null)
                                                {
                                                    CacheManger.Configs.InsertOnSubmit(new ConfigItem() { ID = string.Format("{0}:{1}", (int)ConfigType.Text, key), Key = key, Type = ConfigType.Text, Status = ItemStatus.KeyNotFound });
                                                    CacheManger.SubmitChanges();
                                                }
                                            }
                                        }
                                    }
                                    else  // has cache
                                    {
                                        hit.Status = ItemStatus.KeyNotFound;
                                        lock (_databaseLock)
                                        {
                                            CacheManger.SubmitChanges();
                                        }
                                    }
                                }

                                Helper.SafeInvoke(() => call(defaultValue));
                            }
                        }
                        catch (Exception err)
                        {
                            Debug.WriteLine(err.Message);
                        }
                    });
            }
            else
            {
                string value = hit.Value;
                Helper.SafeInvoke(() => call(value));
                return null;
            }
        }

        static Dictionary<int, string> imageControls = new Dictionary<int, string>();
        static Dictionary<int, string> textControls = new Dictionary<int, string>();

        public static IAsyncResult SetImageControlWithKey(Image control, string key, BitmapImage defaultImage)
        {
            if (control == null || key == null)
            {
                throw new ArgumentNullException();
            }

            imageControls[control.GetHashCode()] = key;
            return GetImageAsync(key, defaultImage, (p) =>
            {
                lock (imageControls)
                {
                    if (imageControls[control.GetHashCode()] == key)
                    {
                        control.Source = p;
                    }
                }
            });
        }

        public static IAsyncResult GetImageAsync(string key, BitmapImage defaultValue, Action<BitmapImage> callBack, bool forceUpdate = false)
        {
            if (!_isRegistered)
            {
                throw new InvalidOperationException("Call Register first");
            }

            var hit = CacheManger.Configs.FirstOrDefault(x => x.ID == string.Format("{0}:{1}", (int)ConfigType.Image, key));
            var call = callBack;

            if (hit != null && hit.Status == ItemStatus.KeyNotFound)
            {
                Helper.SafeInvoke(() => call(defaultValue));
                return null;
            }
            else if (forceUpdate || hit == null || hit.ExpireTime < DateTime.Now)
            {
                return QueryItemFromServerAsync(key, ConfigType.Image, (s, e) =>
                {
                    try
                    {
                        if (e == null)
                        {
                            if (hit == null)
                            {
                                lock (_databaseLock)
                                {
                                    hit = CacheManger.Configs.FirstOrDefault(x => x.ID == string.Format("{0}:{1}", (int)ConfigType.Image, key));
                                    if (hit == null)
                                    {
                                        CacheManger.Configs.InsertOnSubmit(s);
                                        CacheManger.SubmitChanges();
                                    }
                                }

                                hit = s;
                            }
                            else
                            {
                                hit.Value = s.Value;
                                hit.ExpireTime = s.ExpireTime;
                                hit.MD5 = s.MD5;
                                hit.Blob = null;
                            }

                            DownloadImage(hit, call);
                        }
                        else
                        {
                            Helper.SafeInvoke(() =>
                            {
                                call(defaultValue);
                            });
                        }
                    }
                    catch (Exception err)
                    {
                        Debug.WriteLine(err.Message);
                    }
                });
            }
            else
            {
                if (hit.Blob == null)
                {
                    DownloadImage(hit, call);
                }
                else
                {
                    Helper.SafeInvoke(() =>
                    {
                        BitmapImage image = new BitmapImage();
                        image = GetImage(hit.Blob);
                        call(image);
                    });
                }

                return null;
            }
        }

        static BitmapImage GetImage(byte[] rawImageBytes)
        {
            BitmapImage imageSource = null;

            try
            {
                using (MemoryStream stream = new MemoryStream(rawImageBytes))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    BitmapImage b = new BitmapImage();
                    b.SetSource(stream);
                    imageSource = b;
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return imageSource;
        }

        private static void DownloadImage(ConfigItem hit, Action<BitmapImage> call)
        {
            Http.GetBytesAsync(hit.Value, (bytes, err) =>
            {
                try
                {
                    if (err == null)
                    {
                        hit.Blob = bytes;
                        lock (_databaseLock)
                        {
                            CacheManger.SubmitChanges();
                        }

                        Helper.SafeInvoke(() =>
                        {
                            BitmapImage image = new BitmapImage();
                            image = GetImage(bytes);
                            call(image);
                        });
                    }
                    else
                    {
                        if (err is WebException)
                        {
                            if ((err as WebException).Status == WebExceptionStatus.ConnectFailure)
                            {
                                hit.Status = ItemStatus.InvalidPath;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            });
        }

        public static void CheckUpdate()
        {
            if (!_isRegistered)
            {
                throw new InvalidOperationException("Call Register first");
            }

            if (CacheManger.Configs.Count() > 0)
            {
                QueryAll(CacheManger.Configs.ToArray(), (results, e) =>
                {
                    try
                    {
                        if (e == null)
                        {
                            foreach (var c in results)
                            {
                                ConfigItem hit = null;
                                lock (_databaseLock)
                                {
                                    hit = CacheManger.Configs.Single(x => x.ID == string.Format("{0}:{1}", (int)c.Type, c.Key));
                                }

                                if (c.MD5 != hit.MD5)
                                {
                                    hit.Value = c.Value;
                                    hit.ExpireTime = c.ExpireTime;
                                    hit.MD5 = c.MD5;
                                    hit.Blob = null;

                                    lock (_databaseLock)
                                    {
                                        CacheManger.SubmitChanges();
                                    }

                                    if (hit.Type == ConfigType.Image)
                                    {
                                        Http.GetBytesAsync(hit.Value, (bytes, err) =>
                                        {
                                            if (err == null)
                                            {
                                                hit.Blob = bytes;
                                                lock (_databaseLock)
                                                {
                                                    CacheManger.SubmitChanges();
                                                }

                                                if (ConfigUpdated != null)
                                                {
                                                    Helper.SafeInvoke(() =>
                                                    {
                                                        BitmapImage image = new BitmapImage();
                                                        image.SetSource(new MemoryStream(hit.Blob));

                                                        ConfigUpdated(null, new ConfigUpdatedEventArgs()
                                                        {
                                                            Key = hit.Key,
                                                            Type = ConfigType.Image,
                                                            Value = image
                                                        });
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine(err.Message);
                                            }
                                        });
                                    }
                                    else
                                    {
                                        if (ConfigUpdated != null)
                                        {
                                            Helper.SafeInvoke(() =>
                                            {
                                                ConfigUpdated(null, new ConfigUpdatedEventArgs()
                                                {
                                                    Key = hit.Key,
                                                    Type = ConfigType.Text,
                                                    Value = hit.Value
                                                });
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine(e.Message);
                        }
                    }
                    catch (Exception err)
                    {
                        Debug.WriteLine(err.Message);
                    }
                });
            }
        }

        public static void ClearCache()
        {
            CacheManger.Reset();
        }

        private static void OnTimer(object state)
        {
            try
            {
                CheckUpdate();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private class UrlBuilder
        {
            private UriBuilder ub = new UriBuilder();
            List<KeyValuePair<string, string>> param = new List<KeyValuePair<string, string>>();
            public UrlBuilder()
            {
                ub.Host = "api.duducat.com";
                ub.Scheme = "http";
            }

            public UrlBuilder SetHost(string host)
            {
                ub.Host = host;
                return this;
            }

            public UrlBuilder SetPath(string path)
            {
                ub.Path = path;
                return this;
            }

            public UrlBuilder AddParam(string key, string value = null)
            {
                param.Add(new KeyValuePair<string, string>(key, value));
                ub.Query = string.Join("&", param.Select(x => x.Key + (x.Value == null ? "" : ("=" + x.Value))));
                return this;
            }

            public string GetUrl()
            {
                return ub.Uri.ToString();
            }
        }

        private static IAsyncResult QueryItemFromServerAsync(string key, ConfigType type, Action<ConfigItem, Exception> callback)
        {
            string url = new UrlBuilder()
                .SetPath("/ActiveConfig/v1/GetKey")
                .AddParam("appid", _appKey)
                .AddParam("secretkey", _appSecret)
                .AddParam("key", key + ":" + (int)type)
                .GetUrl();

            return Http.GetStringAsync(url, (s, e) =>
            {
                ConfigItem result = null;
                if (e == null)
                {
                    result = null;

                    try
                    {
                        var dict = JSON.Parse(s);
                        if (dict != null)
                        {
                            string code = dict["code"].Value;
                            string msg = dict["msg"].Value;
                            string data = dict["data"].Value;

                            if (code != "0")
                            {
                                Debug.WriteLine("Server return error {0}: {1}", code, msg);
                                e = new ActiveConfigException(msg);
                            }
                            else
                            {
                                if (dict["data"].Count == 1)
                                {
                                    var item = dict["data"][0];
                                    result = new ConfigItem();
                                    result.Key = item["key"].Value;
                                    result.Value = item["value"].Value;
                                    result.Type = (ConfigType)Enum.Parse(typeof(ConfigType), item["type"].Value, true);
                                    result.ExpireTime = Helper.ConvertTime(item["endtime"].Value);
                                    result.MD5 = item["md5"].Value;
                                    result.ID = string.Format("{0}:{1}", (int)result.Type, result.Key);
                                    if (result.Status == ItemStatus.KeyNotFound)
                                    {
                                        e = new KeyNotFoundException(key);
                                    }
                                }
                                else
                                {
                                    e = new KeyNotFoundException(key);
                                }
                            }
                        }
                        else
                        {
                            e = new ActiveConfigException("Parsing json failed");
                        }
                    }
                    catch (Exception err)
                    {
                        e = new ActiveConfigException("Parsing json failed", err);
                    }
                }

                callback(result, e);
            });
        }

        internal enum ServerItemStatus
        {
            Success,
            Invalid,
            NoUpdate
        }

        private static IAsyncResult QueryAll(ConfigItem[] keys, Action<ConfigItem[], Exception> callback)
        {
            string url = new UrlBuilder()
                .SetPath("/ActiveConfig/v1/CheckUpdate")
                .AddParam("appid", _appKey)
                .AddParam("secretkey", _appSecret)
                .AddParam("key", string.Join(";", keys.Select(x => x.Key + "," + x.MD5 + "," + (int)x.Type)))
                .GetUrl();

            return Http.GetStringAsync(url, (s, e) =>
            {
                List<ConfigItem> results = null;

                if (e == null)
                {
                    results = new List<ConfigItem>();
                    var dict = JSON.Parse(s);
                    if (dict != null)
                    {
                        string code = dict["code"].Value;
                        string msg = dict["msg"].Value;

                        if (code != "0")
                        {
                            Debug.WriteLine("Server return error {0}: {1}", code, msg);
                            e = new ActiveConfigException(msg);
                        }
                        else
                        {
                            foreach (var item in dict["data"].Childs)
                            {
                                ConfigItem c = new ConfigItem();
                                c.Key = item["key"].Value;
                                c.Type = (ConfigType)Enum.Parse(typeof(ConfigType), item["type"].Value, true);

                                var serverStatus = item["status"].Value != "" ? (ServerItemStatus)Enum.Parse(typeof(ServerItemStatus), item["status"].Value, true) : ServerItemStatus.Success;

                                if (serverStatus == ServerItemStatus.NoUpdate)
                                {
                                    continue;
                                }
                                else if (serverStatus == ServerItemStatus.Invalid)
                                {
                                    c.Status = ItemStatus.KeyNotFound;
                                }
                                else
                                {
                                    c.Value = item["value"].Value;
                                    c.ExpireTime = Helper.ConvertTime(item["endtime"].Value);
                                    c.MD5 = item["md5"].Value;
                                    c.Status = ItemStatus.OK;
                                }

                                results.Add(c);
                            }
                        }
                    }
                    else
                    {
                        e = new ActiveConfigException("Parsing json failed");
                    }
                }

                callback(results.ToArray(), e);
            });
        }
    }
}
