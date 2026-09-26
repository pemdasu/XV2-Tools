using System;
using System.Collections.Generic;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.UndoRedo;

namespace Xv2CoreLib
{
    public partial class FileManager
    {
        private int _strongReferenceLimit = 0;

        /// <summary>
        /// Use direct references when caching loaded files, preventing them from being removed by the garbage collector.
        /// </summary>
        /// <remarks>These files can later be freed up by calling <see cref="ClearStrongReferences"/>.</remarks>
        public bool UseStrongReferences { get; set; }
        /// <summary>
        /// Gets or sets the maximum number of strong references to retain.
        /// </summary>
        /// <remarks>Default is 0, which will be treated as no limit.</remarks>
        public int StrongReferenceLimit
        {
            get => _strongReferenceLimit;
            set
            {
                _strongReferenceLimit = value;
                InitStrongReferenceStack();
            }
        }
        /// <summary>
        /// When enabled, the file cache is ignored and files are always reloaded from loose files or CPK, with the cache entry being overwritten.
        /// </summary>
        public bool ForceReloadFiles { get; set; }
        public bool UseCpkCache { get; set; }

        private Dictionary<ValueTuple<string, Type>, CachedFile> CachedFiles;
        private Dictionary<ValueTuple<string, Type>, CachedFile> CpkCachedFiles;
        private LimitedStack<CachedFile> StrongReferenceStack;

        private void InitStrongReferenceStack()
        {
            StrongReferenceLimit = MathHelpers.Clamp(0, int.MaxValue, StrongReferenceLimit);

            if (StrongReferenceStack == null)
            {
                StrongReferenceStack = new LimitedStack<CachedFile>(StrongReferenceLimit);
            }
            else
            {
                if (StrongReferenceLimit < StrongReferenceStack.Capacity)
                {
                    for (int i = 0; i < StrongReferenceStack.Capacity - StrongReferenceLimit; i++)
                    {
                        StrongReferenceStack.Pop().ClearStrongReference();
                    }
                }

                StrongReferenceStack.Resize(StrongReferenceLimit);
            }
        }

        private T GetCachedFile<T>(string path, bool fromCpk) where T : class
        {
            RemoveDeadReferences();
            Type type = typeof(T);
            var key = (path, type);

            if (fromCpk && CpkCachedFiles.TryGetValue(key, out CachedFile cpkFile))
            {
                return (T)(cpkFile.ObjectReference.IsAlive ? cpkFile.ObjectReference.Target : null);
            }
            else if (CachedFiles.TryGetValue(key, out CachedFile file))
            {
                return (T)(file.ObjectReference.IsAlive ? file.ObjectReference.Target : null);
            }

            return null;
        }

        private void AddCachedFile<T>(string path, object data, bool fromCpk) where T : class
        {
            path = Utils.SanitizePath(path);

            if (fromCpk)
            {
                AddCachedFileInternal(CpkCachedFiles, path, typeof(T), data);
            }
            else
            {
                AddCachedFileInternal(CachedFiles, path, typeof(T), data);
            }
        }

        private void AddCachedFileInternal(Dictionary<ValueTuple<string, Type>, CachedFile> cache, string path, Type type, object data)
        {
            var key = (path, type);

            //If replacing a cached file clear out possible strong references
            if (cache.TryGetValue(key, out CachedFile file))
            {
                StrongReferenceStack.Remove(file);
                file?.ClearStrongReference();
            }

            CachedFile newCachedFile = new CachedFile(path, data, UseStrongReferences);
            cache[key] = newCachedFile;

            if (UseStrongReferences && StrongReferenceLimit > 0)
            {
                var outCachedObject = StrongReferenceStack.Push(newCachedFile);
                outCachedObject?.ClearStrongReference();
            }
        }

        public void ClearStrongReferences()
        {
            foreach (var file in CachedFiles)
            {
                file.Value?.ClearStrongReference();
            }

            foreach (var file in CpkCachedFiles)
            {
                file.Value?.ClearStrongReference();
            }

            StrongReferenceStack?.Clear();
        }

        private void RemoveDeadReferences()
        {
            CachedFiles.RemoveAll((k, v) => !v.ObjectReference.IsAlive);
            CpkCachedFiles.RemoveAll((k, v) => !v.ObjectReference.IsAlive);
        }

    }
}
