// 生成 src/app.ico: 与托盘同款"蓝底圆角白译"图标, 多尺寸 BMP-DIB 条目 (16/24/32/48/64/128)
// 用法: csc makeicon.cs && makeicon.exe <输出路径>
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIcon
{
    static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        float k = size / 32f;
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            int x = (int)Math.Round(1 * k), w = (int)Math.Round(30 * k);
            // 圆角半径按比例 (32px 时 =8)
            using (GraphicsPath path = new GraphicsPath())
            {
                int rad = (int)Math.Round(8 * k), d = rad * 2;
                path.AddArc(x, x, d, d, 180, 90);
                path.AddArc(x + w - d, x, d, d, 270, 90);
                path.AddArc(x + w - d, x + w - d, d, d, 0, 90);
                path.AddArc(x, x + w - d, d, d, 90, 90);
                path.CloseFigure();
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 130, 210))) g.FillPath(b, path);
            }
            using (Font f = new Font("Microsoft YaHei UI", 13f * 96f / 72f * k, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush fg = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("译", f, fg, new RectangleF(0, 0, size, size), sf);
            }
        }
        return bmp;
    }

    static byte[] DibEntry(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((uint)40); bw.Write(w); bw.Write(h * 2); bw.Write((ushort)1); bw.Write((ushort)32);
            bw.Write((uint)0); bw.Write((uint)0); bw.Write(0); bw.Write(0); bw.Write((uint)0); bw.Write((uint)0);
            byte[] px = new byte[w * h * 4];
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)   // 自底向上
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0 + (h - 1 - y) * bd.Stride, px, y * w * 4, w * 4);
            bmp.UnlockBits(bd);
            bw.Write(px);
            int maskRow = ((w + 31) / 32) * 4;
            for (int y = 0; y < h; y++) bw.Write(new byte[maskRow]);   // AND 全 0 = 用 alpha
            bw.Flush();
            return ms.ToArray();
        }
    }

    static void Main(string[] argv)
    {
        string outPath = argv.Length > 0 ? argv[0] : "app.ico";
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128 };
        using (FileStream fs = new FileStream(outPath, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)sizes.Length);
            byte[][] dibs = new byte[sizes.Length][];
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap b = Render(sizes[i])) dibs[i] = DibEntry(b);
                bw.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                bw.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write((uint)dibs[i].Length); bw.Write((uint)offset);
                offset += dibs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) bw.Write(dibs[i]);
        }
        Console.WriteLine("ICO OK -> " + outPath);
    }
}
