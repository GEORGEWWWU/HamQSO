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
        private static bool _blockPersist;
        private static string _lastError;

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

        public static string BackupPath => FilePath + ".bak";

        public static bool IsBlocked
        {
            get { EnsureLoaded(); return _blockPersist; }
        }

        public static string LastError
        {
            get { EnsureLoaded(); return _lastError; }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Gate)
            {
                if (_loaded) return;
                if (!TryLoad(FilePath))
                {
                    // 主文件读不了就回退到备份，仍不行则保留现场并停止写入，绝不覆盖
                    if (TryLoad(BackupPath))
                    {
                        _lastError = "主数据文件损坏，已从备份恢复";
                        TryQuarantine(FilePath);
                        _blockPersist = false;
                    }
                    else
                    {
                        TryQuarantine(FilePath);
                        _blockPersist = true;
                        _lastError = "数据文件无法读取，已停止写入以免覆盖原始数据";
                        Error?.Invoke(_lastError);
                        _items = new List<QsoRecord>();
                    }
                }
                _loaded = true;
            }
            Normalize();
        }

        private static bool TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize(json, AppJson.Default.ListQsoRecord);
                if (list == null) return false;
                _items = list;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryQuarantine(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var target = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, target, true);
            }
            catch { }
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

        /// <summary>按 Id 查找记录。</summary>
        public static QsoRecord Find(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;
            return _items.FirstOrDefault(x => x.Id == id);
        }

        /// <summary>最后保存时间（数据文件写入时间）。</summary>
        public static DateTime LastUpdated
        {
            get
            {
                EnsureLoaded();
                try
                {
                    if (File.Exists(FilePath)) return File.GetLastWriteTime(FilePath);
                }
                catch { }
                return DateTime.Now;
            }
        }

        /// <summary>更新已有记录；不存在则插入。用于长按编辑后保存。</summary>
        public static void Update(QsoRecord rec)
        {
            EnsureLoaded();
            if (rec == null) return;
            if (string.IsNullOrEmpty(rec.Id)) rec.Id = Guid.NewGuid().ToString("N");
            lock (Gate)
            {
                int i = _items.FindIndex(x => x.Id == rec.Id);
                if (i >= 0)
                {
                    if (rec.Created == 0) rec.Created = _items[i].Created;
                    _items[i] = rec;
                }
                else
                {
                    if (rec.Created == 0) rec.Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _items.Add(rec);
                }
            }
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
            if (_blockPersist)
            {
                Error?.Invoke("数据文件异常，已阻止写入以免覆盖原始数据");
                return;
            }
            try
            {
                var json = JsonSerializer.Serialize(_items, AppJson.Default.ListQsoRecord);

                // 写新内容前，把上一份完好的数据留作 .bak，任何一次写入失败都还能回滚
                if (File.Exists(FilePath))
                {
                    try { File.Copy(FilePath, BackupPath, true); } catch { }
                }

                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, FilePath, true);
                _lastError = null;
            }
            catch (Exception ex)
            {
                _lastError = "保存失败：" + ex.Message;
                Error?.Invoke(_lastError);
            }
        }
    }
}
