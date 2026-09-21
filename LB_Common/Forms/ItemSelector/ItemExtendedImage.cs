using System;
using System.Windows.Media.Imaging;

namespace LB_Common.Forms
{
    public class ItemExtendedImage : ItemExtended
    {
        private readonly BitmapSource _bitmapDirect;
        private readonly Func<BitmapSource> bitmapFunc;

        public BitmapSource Bitmap => _bitmapDirect != null ? _bitmapDirect : bitmapFunc?.Invoke();
        public bool HasBitmap => _bitmapDirect != null || bitmapFunc != null;

        public ItemExtendedImage(int id, string name, string preNameStr, string postNameStr, Func<BitmapSource> bitmap) : base(id, name, preNameStr, postNameStr)
        {
            bitmapFunc = bitmap;
        }

        public ItemExtendedImage(int id, string name, string preNameStr, string postNameStr, BitmapSource bitmap) : base(id, name, preNameStr, postNameStr)
        {
            _bitmapDirect = bitmap;
        }
    }
}
