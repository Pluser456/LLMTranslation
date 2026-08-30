// 自动拆分自 Program.cs - 模块: Dpi
using System;

namespace LLMTranslation
{
    internal static class Dpi
    {
        private static float _s = 0f;
        public static float Scale
        {
            get
            {
                if (_s <= 0f)
                {
                    try
                    {
                        IntPtr dc = NativeMethods.GetDC(IntPtr.Zero);
                        int d = NativeMethods.GetDeviceCaps(dc, 88); // LOGPIXELSX
                        NativeMethods.ReleaseDC(IntPtr.Zero, dc);
                        _s = d > 0 ? d / 96f : 1f;
                    }
                    catch { _s = 1f; }
                }
                return _s;
            }
        }

        // 强制重算 (屏幕缩放可能已变, 缓存失效)
        public static float Recompute()
        {
            _s = 0f;
            return Scale;
        }
    }

}
