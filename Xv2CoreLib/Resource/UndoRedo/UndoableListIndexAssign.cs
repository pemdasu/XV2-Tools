using System.Collections.Generic;

namespace Xv2CoreLib.Resource.UndoRedo
{
    public class UndoableListIndexAssign<T> : IUndoRedo
    {
        public string Message { get; set; }
        public bool doLast { get; set; }

        private int idx;
        private IList<T> list;
        private T newItem;
        private T oldItem;

        public UndoableListIndexAssign(IList<T> list, int idx, T newItem, T oldItem, string message = null)
        {
            this.idx = idx;
            this.newItem = newItem;
            this.oldItem = oldItem;
            this.list = list;
            Message = message;
        }

        public void Undo()
        {
            if (idx >= 0 && idx <= list.Count - 1)
                list[idx] = oldItem;
        }

        public void Redo()
        {
            if (idx >= 0 && idx <= list.Count)
                list[idx] = newItem;
        }
    }
}