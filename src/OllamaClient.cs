using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TradutorPdfOllama
{
    public class OllamaClient
    {
        private string _baseUrl;
        private readonly JavaScriptSerializer _serializer;

        public OllamaClient(string baseUrl = "http://localhost:11434")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _serializer = new JavaScriptSerializer();
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
            set { _baseUrl = value.TrimEnd('/'); }
        }

        public bool CheckConnection()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/api/tags");
                request.Method = "GET";
                request.Timeout = 3000;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetModels()
        {
            var list = new List<string>();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/api/tags");
                request.Method = "GET";
                request.Timeout = 5000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var dict = _serializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("models"))
                    {
                        var models = dict["models"] as ArrayList;
                        if (models != null)
                        {
                            foreach (Dictionary<string, object> m in models)
                            {
                                if (m.ContainsKey("name"))
                                {
                                    list.Add(m["name"].ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Return empty list if Ollama is not reachable
            }
            return list;
        }

        public virtual void StreamGenerate(
            string model,
            string prompt,
            string systemPrompt,
            Action<string> onChunkReceived,
            Action onCompleted,
            Action<Exception> onError,
            CancellationTokenSource cts,
            List<string> images = null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                CancellationTokenRegistration cancellation = new CancellationTokenRegistration();
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/api/generate");
                    request.Method = "POST";
                    request.ContentType = "application/json; charset=utf-8";
                    request.SendChunked = false;
                    request.KeepAlive = true;
                    request.Timeout = 300000; // 5 min
                    if (cts != null) cancellation = cts.Token.Register(() => request.Abort());

                    var payload = new Dictionary<string, object>
                    {
                        { "model", model },
                        { "prompt", prompt },
                        { "stream", true }
                    };

                    if (images != null && images.Count > 0)
                    {
                        payload["images"] = images;
                    }

                    if (!string.IsNullOrEmpty(systemPrompt))
                    {
                        payload["system"] = systemPrompt;
                    }

                    string jsonPayload = _serializer.Serialize(payload);
                    byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
                    request.ContentLength = payloadBytes.Length;

                    using (var reqStream = request.GetRequestStream())
                    {
                        reqStream.Write(payloadBytes, 0, payloadBytes.Length);
                    }

                    if (cts != null && cts.IsCancellationRequested)
                    {
                        request.Abort();
                        return;
                    }

                    bool finished = false;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var respStream = response.GetResponseStream())
                    using (var reader = new StreamReader(respStream, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (cts != null && cts.IsCancellationRequested)
                            {
                                return;
                            }

                            if (string.IsNullOrWhiteSpace(line)) continue;

                                var chunkObj = _serializer.Deserialize<Dictionary<string, object>>(line);
                                if (chunkObj != null && chunkObj.ContainsKey("error"))
                                    throw new InvalidOperationException(Convert.ToString(chunkObj["error"]));
                                if (chunkObj != null && chunkObj.ContainsKey("response"))
                                {
                                    string text = chunkObj["response"] as string;
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        if (onChunkReceived != null) onChunkReceived(text);
                                    }
                                }
                                if (chunkObj != null && chunkObj.ContainsKey("done"))
                                {
                                    bool done = Convert.ToBoolean(chunkObj["done"]);
                                    if (done) { finished = true; break; }
                                }
                        }
                    }

                    if (cts != null && cts.IsCancellationRequested) return;
                    if (!finished) throw new IOException("A resposta do Ollama foi interrompida antes de terminar.");
                    if (onCompleted != null) onCompleted();
                }
                catch (ThreadAbortException)
                {
                    // Thread canceled
                }
                catch (Exception ex)
                {
                    if (cts != null && cts.IsCancellationRequested)
                    {
                        return;
                    }
                    else
                    {
                        if (onError != null) onError(ex);
                    }
                }
                finally { cancellation.Dispose(); }
            });
        }
    }
}
