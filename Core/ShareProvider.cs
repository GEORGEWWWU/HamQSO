using System;
using System.IO;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;

namespace HamJi.Core
{
    /// <summary>
    /// 极简 FileProvider：把缓存目录里的分享图片以 content:// 暴露给别的 App。
    ///
    /// 为什么不用 file://：Android 7.0（API 24）起直接抛 FileUriExposedException；
    /// 为什么自己写：项目没有引入 AndroidX，用不了 androidx.core.content.FileProvider。
    ///
    /// 关键点（跨版本分享能不能成功全看这几条）：
    /// 1) Exported=false + GrantUriPermissions=true：别人只有在拿到临时授权时才读得到，
    ///    靠 Intent 上的 FLAG_GRANT_READ_URI_PERMISSION 生效（API 21 起就支持）。
    /// 2) Query 必须返回 _display_name / _size：微信、QQ、部分相册在拿流之前会先查这两个字段，
    ///    返回 null 时它们会直接判定"文件无效"。
    /// 3) GetType 返回 image/png：接收方据此决定用图片模式打开。
    /// 4) 路径只认缓存目录下的 share/ 一层，禁止 .. 穿越。
    /// </summary>
    [ContentProvider(new[] { Authority }, Exported = false, GrantUriPermissions = true)]
    public class ShareProvider : ContentProvider
    {
        public const string Authority = "com.hamji.app.shareprovider";
        public const string Folder = "share";

        /// <summary>分享图片落盘的目录（缓存，系统可随时回收）。</summary>
        public static string DirPath(Context c)
        {
            var dir = new Java.IO.File(c.CacheDir, Folder);
            if (!dir.Exists()) dir.Mkdirs();
            return dir.AbsolutePath;
        }

        /// <summary>文件名 → content:// 地址。</summary>
        public static Android.Net.Uri UriFor(string fileName)
            => Android.Net.Uri.Parse("content://" + Authority + "/" + Android.Net.Uri.Encode(fileName));

        public override bool OnCreate() => true;

        private static Java.IO.File Resolve(Android.Net.Uri uri)
        {
            if (uri == null) return null;
            string name = uri.LastPathSegment;
            if (string.IsNullOrEmpty(name)) return null;
            name = Android.Net.Uri.Decode(name);
            // 只允许纯文件名，挡掉 ../ 之类的穿越
            if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.Contains("..")) return null;
            // 注意：静态方法里的 Context 是类型名，取不到实例属性，只能用 Application 的
            var dir = new Java.IO.File(Android.App.Application.Context.CacheDir, Folder);
            var file = new Java.IO.File(dir, name);
            if (!file.Exists()) return null;
            return file;
        }

        public override ParcelFileDescriptor OpenFile(Android.Net.Uri uri, string mode)
        {
            var file = Resolve(uri);
            if (file == null) throw new Java.IO.FileNotFoundException("share file not found: " + uri);
            // 分享出去的永远是只读，接收方拿不到写权限
            return ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
        }

        public override string GetType(Android.Net.Uri uri) => "image/png";

        public override ICursor Query(Android.Net.Uri uri, string[] projection,
            string selection, string[] selectionArgs, string sortOrder)
        {
            var file = Resolve(uri);
            if (file == null) return null;

            // 只回 OpenableColumns 这一组，够微信 / QQ / 系统相册用
            var cols = new[] { OpenableColumns.DisplayName, OpenableColumns.Size };
            var cursor = new MatrixCursor(cols, 1);
            cursor.AddRow(new Java.Lang.Object[] { file.Name, (long)file.Length() });
            return cursor;
        }

        public override Android.Net.Uri Insert(Android.Net.Uri uri, ContentValues values) => null;

        public override int Delete(Android.Net.Uri uri, string selection, string[] selectionArgs) => 0;

        public override int Update(Android.Net.Uri uri, ContentValues values, string selection, string[] selectionArgs) => 0;
    }
}
