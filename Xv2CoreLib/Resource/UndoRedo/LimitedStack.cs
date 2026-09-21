using System.Collections.Generic;

namespace Xv2CoreLib.Resource.UndoRedo
{
    public class LimitedStack<T> : LinkedList<T>
    {
        private int _maxSize;
        public int Capacity => _maxSize;

        public LimitedStack(int maxSize)
        {
            _maxSize = maxSize;
        }

        public T Push(T item)
        {
            this.AddFirst(item);


            if (this.Count > _maxSize)
            {
                T last = this.Last.Value;
                this.RemoveLast();
                return last;
            }

            return default;
        }

        public T Pop()
        {
            var item = this.First.Value;
            this.RemoveFirst();
            return item;
        }

        public void Resize(int newSize)
        {
            _maxSize = newSize;

            while (Count > newSize)
                RemoveLast();
        }
    }
}
