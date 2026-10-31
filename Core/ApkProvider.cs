using System.IO;
using Android.Content;
using Android.Database;
using Android.OS;

namespace HamJi.Core
{
    /// <summary>
    /// 极简 FileProvider：把缓存里的更新包以 content:// 暴露给系统安装器。
    /// 项目没有引入 AndroidX，所以自己实现一个，只服务固定的 update.apk。
    /// </summary>
    [ContentProvider(new[] { Authority }, Exported = false, GrantUriPermissions = true)]
    public class ApkProvider : ContentProvider
    {
        public const string Authority = "com.hamji.app.apkprovider";
        public const string ApkName = "update.apk";

        public static string FilePathFor(Context c) => Path.Combine(c.CacheDir.AbsolutePath, ApkName);

        public static Android.Net.Uri UriFor() => Android.Net.Uri.Parse("content://" + Authority + "/" + ApkName);

        public override bool OnCreate() => true;

        public override ParcelFileDescriptor OpenFile(Android.Net.Uri uri, string mode)
        {
            var file = new Java.IO.File(FilePathFor(Context));
            return ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
        }

        public override string GetType(Android.Net.Uri uri) => "application/vnd.android.package-archive";

        public override ICursor Query(Android.Net.Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder) => null;

        public override Android.Net.Uri Insert(Android.Net.Uri uri, ContentValues values) => null;

        public override int Delete(Android.Net.Uri uri, string selection, string[] selectionArgs) => 0;

        public override int Update(Android.Net.Uri uri, ContentValues values, string selection, string[] selectionArgs) => 0;
    }
}
