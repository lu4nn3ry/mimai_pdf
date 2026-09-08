using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace TradutorPdfOllama
{
    public class TranslationCache
    {
        private readonly string _cacheFilePath;
        private readonly Dictionary<string, string> _cacheData;
        private readonly JavaScriptSerializer _serializer;

        public TranslationCache()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "TradutorPdfOllama");
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }
            _cacheFilePath = Path.Combine(appDir, "translation_cache.json");
            _serializer = new JavaScriptSerializer();
            _cacheData = LoadCache();
        }

        private Dictionary<string, string> LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath, Encoding.UTF8);
                    var data = _serializer.Deserialize<Dictionary<string, string>>(json);
                    if (data != null) return data;
                }
            }
            catch
            {
                // Fallback to empty cache on read error
            }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private void SaveCache()
        {
            try
            {
                string json = _serializer.Serialize(_cacheData);
                File.WriteAllText(_cacheFilePath, json, Encoding.UTF8);
            }
            catch
            {
                // Ignore save errors
            }
        }

        public string GetKey(string fileName, int pageNum, string targetLang, string model)
        {
            string cleanFile = Path.GetFileName(fileName ?? "untitled");
            return string.Format("{0}::p{1}::{2}::{3}", cleanFile, pageNum, targetLang, model);
        }

        public string GetTranslation(string fileName, int pageNum, string targetLang, string model)
        {
            string key = GetKey(fileName, pageNum, targetLang, model);
            if (_cacheData.ContainsKey(key))
            {
                return _cacheData[key];
            }
            return null;
        }

        public void SetTranslation(string fileName, int pageNum, string targetLang, string model, string translation)
        {
            if (string.IsNullOrWhiteSpace(translation)) return;
            string key = GetKey(fileName, pageNum, targetLang, model);
            _cacheData[key] = translation;
            SaveCache();
        }

        public void ClearDocumentCache(string fileName)
        {
            string cleanFile = Path.GetFileName(fileName ?? "untitled");
            string prefix = cleanFile + "::";
            var keysToRemove = new List<string>();
            foreach (var k in _cacheData.Keys)
            {
                if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(k);
                }
            }
            foreach (var k in keysToRemove)
            {
                _cacheData.Remove(k);
            }
            SaveCache();
        }
    }
}
