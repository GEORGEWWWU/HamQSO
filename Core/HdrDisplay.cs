using System;
using System.IO;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;

namespace HamJi.Core
{
    /// <summary>
    /// HDR / 广色域显示支持。
    ///
    /// 能力探测与开关全部走系统标准接口（HdrCapabilities / Window.setColorMode），
    /// 不判断厂商：ColorOS / HyperOS / One UI / OriginOS / HarmonyOS 只要底层上报了 HDR 类型就能用。
    /// 这些接口在 .NET for Android 的绑定里名字/可见性各版本不一致，统一用 Java 反射调用，
    /// 老系统上方法不存在就安静地跳过（返回 false），不会崩。
    ///
    /// 解码优先 ImageDecoder（API 28+）：认 EXIF 方向、保留 Ultra HDR 的 gainmap（API 34+），
    /// 拿到的 Bitmap 画到 HDR 窗口上就是真 HDR；不支持时回退 BitmapFactory。
    /// 只有「图片本身是 HDR」时才把窗口切成 HDR 色彩模式，SDR 卡片留在默认模式，
    /// 避免个别 ROM 的色调映射把普通图片弄暗或发灰。
    /// </summary>
    public static class HdrDisplay
    {
        private const string Tag = "HamJiHdr";

        /// <summary>解码长边上限：再大也看不出差别，只会吃内存。</summary>
        public const int MaxSide = 2560;

        /// <summary>ActivityInfo.COLOR_MODE_HDR</summary>
        private const int ColorModeHdr = 2;

        /// <summary>当前屏幕/系统是否支持 HDR（API 26+ 且上报了 HDR 类型）。</summary>
        public static bool Supported { get; private set; }

        /// <summary>给日志看的一行能力摘要。</summary>
        public static string Summary { get; private set; } = "未探测";

        private static bool _probed;

        public static void Probe(Context c)
        {
            if (_probed) return;
            _probed = true;

            try
            {
                int sdk = (int)Build.VERSION.SdkInt;
                if (sdk < 24)
                {
                    Supported = false;
                    Summary = "sdk=" + sdk + "（低于 24，无 HDR 接口）";
                    Log.Info(Tag, Summary);
                    return;
                }

                var display = GetDefaultDisplay(c);
                var caps = Invoke(display, "getHdrCapabilities");
                var typesObj = Invoke(caps, "getSupportedHdrTypes");
                int typeCount = 0;
                if (typesObj != null)
                {
                    try { typeCount = Java.Lang.Reflect.Array.GetLength(typesObj); } catch { }
                }

                float maxLum = 0f;
                var lum = Invoke(caps, "getMaxLuminance");
                if (lum is Java.Lang.Float f) maxLum = f.FloatValue();

                bool wide = false;
                var w = Invoke(display, "isWideColorGamut");
                if (w is Java.Lang.Boolean b) wide = b.BooleanValue();

                Supported = sdk >= 26 && typeCount > 0;
                Summary = "sdk=" + sdk + " vendor=" + Build.Manufacturer
                          + " hdrTypes=" + (typeCount == 0 ? "none" : typeCount.ToString())
                          + " maxLum=" + maxLum + " wide=" + wide + " supported=" + Supported;
                Log.Info(Tag, Summary);
            }
            catch (Exception ex)
            {
                Supported = false;
                Summary = "探测失败：" + ex.GetType().Name + " " + ex.Message;
                Log.Info(Tag, Summary);
            }
        }

        /// <summary>把窗口切到 HDR 色彩模式。返回是否真的切了。</summary>
        public static bool EnableWindow(Android.Views.Window win)
        {
            if (win == null) return false;
            Probe(win.Context);
            if (!Supported) return false;

            try
            {
                var m = win.Class.GetMethod("setColorMode", Java.Lang.Integer.Type);
                m.Invoke(win, new Java.Lang.Integer(ColorModeHdr));
                Log.Info(Tag, "window color mode → HDR");
                return true;
            }
            catch (Exception ex)
            {
                Log.Info(Tag, "setColorMode(HDR) 失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>这张位图是不是 HDR（自带 gainmap，或色彩空间本身就是 HDR）。</summary>
        public static bool IsHdr(Bitmap bmp)
        {
            if (bmp == null) return false;
            try
            {
                if ((int)Build.VERSION.SdkInt >= 34)
                {
                    var g = Invoke(bmp, "hasGainmap");
                    if (g is Java.Lang.Boolean gb && gb.BooleanValue()) return true;
                }
                var cs = bmp.ColorSpace;
                if (cs == null) return false;
                var h = Invoke(cs, "isHdr");
                return h is Java.Lang.Boolean hb && hb.BooleanValue();
            }
            catch { return false; }
        }

        /// <summary>
        /// 解码图片：API 28+ 用 ImageDecoder（保留 gainmap / 广色域 / EXIF 方向），
        /// 失败或老系统回退 BitmapFactory + EXIF。hdr 表示解出来的图是否 HDR。
        /// </summary>
        public static Bitmap Decode(string path, out bool hdr)
        {
            hdr = false;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            if ((int)Build.VERSION.SdkInt >= 28)
            {
                try
                {
                    var src = ImageDecoder.CreateSource(new Java.IO.File(path));
                    var bmp = ImageDecoder.DecodeBitmap(src, new HeaderListener());
                    if (bmp != null)
                    {
                        hdr = IsHdr(bmp);
                        LogDecoded(path, bmp, hdr);
                        return bmp;
                    }
                }
                catch (Exception ex)
                {
                    Log.Info(Tag, "ImageDecoder 回退：" + ex.GetType().Name + " " + ex.Message);
                }
            }

            var legacy = LegacyDecode(path);
            if (legacy != null)
            {
                hdr = IsHdr(legacy);
                LogDecoded(path, legacy, hdr);
            }
            return legacy;
        }

        private static void LogDecoded(string path, Bitmap bmp, bool hdr)
        {
            string cs = "null";
            try { if (bmp.ColorSpace != null) cs = bmp.ColorSpace.Name; } catch { }
            Log.Info(Tag, "decoded " + System.IO.Path.GetFileName(path) + " " + bmp.Width + "x" + bmp.Height
                          + " cs=" + cs + " hdr=" + hdr);
        }

        // ---- Java 反射小工具：绑定缺失的接口靠它兜底 ----

        private static Java.Lang.Object GetDefaultDisplay(Context c)
        {
            try
            {
                var dm = c?.GetSystemService(Context.DisplayService);
                if (dm == null) return null;
                var m = dm.Class.GetMethod("getDisplay", Java.Lang.Integer.Type);
                return m.Invoke(dm, new Java.Lang.Integer(0)) as Java.Lang.Object;
            }
            catch { return null; }
        }

        private static Java.Lang.Object Invoke(Java.Lang.Object target, string name, params Java.Lang.Object[] args)
        {
            if (target == null) return null;
            try
            {
                Java.Lang.Reflect.Method m;
                if (args == null || args.Length == 0)
                {
                    m = target.Class.GetMethod(name);
                }
                else
                {
                    var types = new Java.Lang.Class[args.Length];
                    for (int i = 0; i < args.Length; i++)
                        types[i] = args[i] is Java.Lang.Integer ? Java.Lang.Integer.Type : args[i].Class;
                    m = target.Class.GetMethod(name, types);
                }
                return m?.Invoke(target, args) as Java.Lang.Object;
            }
            catch { return null; }
        }

        /// <summary>老路径：BitmapFactory + 采样 + EXIF 旋转（HDR 信息会丢，但至少能看）。</summary>
        private static Bitmap LegacyDecode(string path)
        {
            try
            {
                var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
                BitmapFactory.DecodeFile(path, bounds);
                if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;

                int sample = 1;
                int maxSide = Math.Max(bounds.OutWidth, bounds.OutHeight);
                while (maxSide / (sample * 2) >= MaxSide) sample *= 2;

                var opt = new BitmapFactory.Options { InSampleSize = sample };
                var bmp = BitmapFactory.DecodeFile(path, opt);
                if (bmp == null) return null;
                return ApplyExif(bmp, path);
            }
            catch { return null; }
        }

        private static Bitmap ApplyExif(Bitmap bmp, string path)
        {
            int deg = 0;
            bool mirror = false;
            try
            {
#pragma warning disable CA1422
                var exif = new Android.Media.ExifInterface(path);
                int o = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1);
#pragma warning restore CA1422
                switch (o)
                {
                    case 3: deg = 180; break;
                    case 6: deg = 90; break;
                    case 8: deg = 270; break;
                    case 2: mirror = true; break;
                    case 5: deg = 90; mirror = true; break;
                    case 7: deg = 270; mirror = true; break;
                }
            }
            catch { return bmp; }

            if (deg == 0 && !mirror) return bmp;

            try
            {
                var m = new Android.Graphics.Matrix();
                if (deg != 0) m.PostRotate(deg);
                if (mirror) m.PostScale(-1f, 1f);
                var rotated = Bitmap.CreateBitmap(bmp, 0, 0, bmp.Width, bmp.Height, m, true);
                if (rotated != null && rotated != bmp) bmp.Recycle();
                return rotated ?? bmp;
            }
            catch { return bmp; }
        }

        /// <summary>解码前按长边采样，避免上亿像素直接 OOM。</summary>
        private sealed class HeaderListener : Java.Lang.Object, ImageDecoder.IOnHeaderDecodedListener
        {
            public void OnHeaderDecoded(ImageDecoder decoder, ImageDecoder.ImageInfo info, ImageDecoder.Source source)
            {
                try
                {
                    int w = info.Size.Width, h = info.Size.Height;
                    int max = Math.Max(w, h);
                    int sample = 1;
                    while (max / (sample * 2) >= MaxSide) sample *= 2;
                    // SetTargetSampleSize 是 API 30 才有的；更老的系统交给整图解码
                    if (sample > 1 && (int)Build.VERSION.SdkInt >= 30) decoder.SetTargetSampleSize(sample);
                }
                catch { }
            }
        }
    }
}
