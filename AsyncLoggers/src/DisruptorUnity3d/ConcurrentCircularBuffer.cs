using System;

namespace DisruptorUnity3d
{
    public class ConcurrentCircularBuffer<T>
    {
        private readonly object _lock = new object();
        private readonly T[] _entries;
        private readonly uint _modMask;
        private readonly uint _capacity;
        private uint _consumerCursor = 0U;
        private uint _producerCursor = 0U;

        public ConcurrentCircularBuffer(uint capacity)
        {
            _capacity = NextPowerOfTwo(capacity);
            _modMask = _capacity - 1;
            _entries = new T[_capacity];
        }

        public T this[uint index]
        {
            get
            {
                unchecked
                {
                    lock (_lock)
                    {
                        return _entries[index & _modMask];
                    }
                }
            }
            set
            {
                unchecked
                {
                    lock (_lock)
                    {
                        _entries[index & _modMask] = value;
                    }
                }
            }
        }

        public bool TryDequeue(out T obj)
        {
            lock (_lock)
            {
                var next = _consumerCursor + 1;

                if (_producerCursor < next)
                {
                    obj = default(T);
                    //reset the cursors so they do not reach the max values
                    Clear();
                    return false;
                }

                obj = this[next];
                _consumerCursor = next;
                return true;
            }
        }

        public void Enqueue(T item)
        {
            lock (_lock)
            {
                var next = _producerCursor + 1;

                var wrapPoint = (long)next - _capacity;
                var min = _consumerCursor;

                if (wrapPoint >= min)
                {
                    //overwrite old values if full
                    _consumerCursor = (uint)Math.Max(0, wrapPoint + 1);
                }

                this[next] = item;
                _producerCursor = next;
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return (int)(_producerCursor - _consumerCursor);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (_producerCursor == 0 && _consumerCursor == 0)
                    return;
                _producerCursor = 0;
                _consumerCursor = 0;
            }
        }


        private static uint NextPowerOfTwo(uint x)
        {
            var result = 2U;
            while (result < x)
            {
                result <<= 1;
            }

            return result;
        }
    }
}