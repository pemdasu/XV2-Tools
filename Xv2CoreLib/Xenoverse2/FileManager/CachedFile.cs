using System;

namespace Xv2CoreLib
{
    internal class CachedFile
    {
        internal string Path { get; private set; }
        internal WeakReference ObjectReference { get; private set; }
        private object StrongReference { get; set; }

        internal CachedFile(string path, object file, bool strongReference)
        {
            Path = path;
            ObjectReference = new WeakReference(file);

            if (strongReference)
                StrongReference = file;
        }

        internal void ClearStrongReference()
        {
            StrongReference = null;
        }

    }
}
