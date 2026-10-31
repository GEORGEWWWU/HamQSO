using System;
using Android.Content;
using Android.Graphics;

namespace HamJi.Core
{
    /// <summary>内置主题色：只换强调色，明暗仍跟随系统。</summary>
    public static class AppTheme
    {
        public static readonly string[] Names = { "蓝", "石墨黑", "银白", "森林绿", "琥珀黄", "砖红", "深紫" };

        // 浅色 / 深色两套强调色（纯色，不做渐变）
        private static readonly int[] LightAccent =
        {
            unchecked((int)0xFF1F6BFF), unchecked((int)0xFF23272E), unchecked((int)0xFF6B7280), unchecked((int)0xFF17924F), unchecked((int)0xFFB5760A), unchecked((int)0xFFD93A3A), unchecked((int)0xFF6D4AD8)
        };

        private static readonly int[] DarkAccent =
        {
            unchecked((int)0xFF5B9CFF), unchecked((int)0xFFB9C4D2), unchecked((int)0xFFC3CBDA), unchecked((int)0xFF4CC98A), unchecked((int)0xFFE8B23C), unchecked((int)0xFFFF7373), unchecked((int)0xFFA98CFF)
        };

        private const string PrefFile = "hamji_prefs";
        private const string PrefKey = "theme";

        public static int Current { get; private set; }

        public static void Load(Context c)
        {
            try
            {
                var sp = c.GetSharedPreferences(PrefFile, FileCreationMode.Private);
                int v = sp.GetInt(PrefKey, 0);
                Current = Math.Max(0, Math.Min(Names.Length - 1, v));
            }
            catch { Current = 0; }
        }

        public static void Set(Context c, int idx)
        {
            Current = Math.Max(0, Math.Min(Names.Length - 1, idx));
            try
            {
                var sp = c.GetSharedPreferences(PrefFile, FileCreationMode.Private);
                // 必须同步落盘：apply() 是异步的，用户选完色马上划掉 App 会丢
                sp.Edit().PutInt(PrefKey, Current).Commit();
            }
            catch { }
        }

        /// <summary>当前明暗下的强调色（给设置页的色点用）。</summary>
        public static int Swatch(int idx)
        {
            idx = Math.Max(0, Math.Min(Names.Length - 1, idx));
            return Dui.Dark ? DarkAccent[idx] : LightAccent[idx];
        }

        /// <summary>把主题色写进 Dui 的调色板（必须在 Dui.Init 之后调用）。</summary>
        public static void Apply()
        {
            int idx = Math.Max(0, Math.Min(Names.Length - 1, Current));
            if (Dui.Dark)
                Dui.ApplyAccent(DarkAccent[idx], Dui.Mix(LightAccent[idx], Android.Graphics.Color.White, 0.10f));
            else
                Dui.ApplyAccent(LightAccent[idx], LightAccent[idx]);
        }
    }
}
