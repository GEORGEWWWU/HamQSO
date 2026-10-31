using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HamJi.Core
{
    [JsonSerializable(typeof(List<QsoRecord>))]
    internal partial class AppJson : JsonSerializerContext
    {
    }

    /// <summary>本地存储：单文件 JSON，原子写入，无外部依赖、无需权限。</summary>
    public static class Store
    {
        private static readonly object Gate = new object();
        private static List<QsoRecord> _items = new List<QsoRecord>();
        private static bool _loaded;
        private static string _file;

        public static event Action Changed;
        public static event Action<string> Error;

        public static string FilePath
        {
            get
            {
                if (_file == null)
                {
                    var dir = Android.App.Application.Context.FilesDir.AbsolutePath;
                    _file = Path.Combine(dir, "records.json");
                }
                return _file;
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Gate)
            {
                if (_loaded) return;
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var json = File.ReadAllText(FilePath);
                        var list = JsonSerializer.Deserialize(json, AppJson.Default.ListQsoRecord);
                        if (list != null) _items = list;
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke("读取数据失败：" + ex.Message);
                    _items = new List<QsoRecord>();
                }
                _loaded = true;
            }
            Normalize();
        }

        private static void Normalize()
        {
            _items = _items.Where(x => x != null).ToList();
            foreach (var it in _items)
            {
                if (string.IsNullOrEmpty(it.Id)) it.Id = Guid.NewGuid().ToString("N");
                it.Mode = it.Mode ?? "";
                it.Call = it.Call ?? "";
                it.Notes = it.Notes ?? "";
                it.Qth = it.Qth ?? "";
                it.Rig = it.Rig ?? "";
                it.Power = it.Power ?? "";
                it.Ant = it.Ant ?? "";
                it.RstTx = it.RstTx ?? "";
                it.RstRx = it.RstRx ?? "";
            }
        }

        public static IReadOnlyList<QsoRecord> All
        {
            get { EnsureLoaded(); return _items; }
        }

        public static List<QsoRecord> Of(int kind)
        {
            EnsureLoaded();
            return _items.Where(x => x.Kind == kind).OrderByDescending(x => x.Time).ThenByDescending(x => x.Created).ToList();
        }

        public static int CountOf(int kind) => _items.Count(x => x.Kind == kind);

        public static void Add(QsoRecord rec)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(rec.Id)) rec.Id = Guid.NewGuid().ToString("N");
            if (rec.Created == 0) rec.Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (Gate) { _items.Add(rec); }
            Persist();
            Changed?.Invoke();
        }

        public static void Remove(string id)
        {
            EnsureLoaded();
            lock (Gate) { _items.RemoveAll(x => x.Id == id); }
            Persist();
            Changed?.Invoke();
        }

        public static void ClearAll()
        {
            EnsureLoaded();
            lock (Gate) { _items.Clear(); }
            Persist();
            Changed?.Invoke();
        }

        public static string ToJson()
        {
            EnsureLoaded();
            var sorted = _items.OrderByDescending(x => x.Time).ToList();
            return JsonSerializer.Serialize(sorted, AppJson.Default.ListQsoRecord);
        }

        private static void Persist()
        {
            try
            {
                var json = JsonSerializer.Serialize(_items, AppJson.Default.ListQsoRecord);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex)
            {
                Error?.Invoke("保存失败：" + ex.Message);
            }
        }
    }
}
