using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace DuduCat
{
    internal static class Http
    {
        public static IAsyncResult GetStringAsync(string url, Action<string, Exception> callback)
        {
            HttpWebRequest request = HttpWebRequest.CreateHttp(url);
            IAsyncResult asyncResult = null;
            asyncResult = request.BeginGetResponse(new AsyncCallback((a) =>
            {
                string body = null;
                Exception error = null;
                try
                {
                    var response = request.EndGetResponse(asyncResult);

                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        body = reader.ReadToEnd();
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                callback(body, error);

            }), null);

            return asyncResult;
        }

        public static IAsyncResult GetBytesAsync(string url, Action<byte[], Exception> callback)
        {
            HttpWebRequest request = HttpWebRequest.CreateHttp(url);
            IAsyncResult asyncResult = null;
            asyncResult = request.BeginGetResponse(new AsyncCallback((a) =>
            {
                byte[] content = null;
                Exception e = null;

                try
                {
                    var response = request.EndGetResponse(a);

                    using (var mem = new MemoryStream())
                    using (var reader = response.GetResponseStream())
                    {
                        reader.CopyTo(mem);
                        content = mem.ToArray();
                    }
                }
                catch (Exception err)
                {
                    e = err;
                }

                callback(content, e);

            }), null);

            return asyncResult;
        }

        public static IAsyncResult PostStringAsync(string url, string content, Action<string, Exception> callback)
        {
            HttpWebRequest request = HttpWebRequest.CreateHttp(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            return request.BeginGetRequestStream(new AsyncCallback((a) =>
            {
                using (var requestStream = request.EndGetRequestStream(a))
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    requestStream.Write(bytes, 0, bytes.Length);
                }

                request.BeginGetResponse(new AsyncCallback((rspState) =>
                {
                    string body = null;
                    Exception error = null;
                    try
                    {
                        var response = request.EndGetResponse(rspState);

                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            body = reader.ReadToEnd();
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    callback(body, error);

                }), null);
            }), null);
        }
    }
}
