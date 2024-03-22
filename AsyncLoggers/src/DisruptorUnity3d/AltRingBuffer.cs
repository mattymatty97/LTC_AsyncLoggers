using System.Threading;

namespace DisruptorUnity3d
{
    /*
    Code Took from https://github.com/dave-hillier/disruptor-unity3d
    */
    
    public class AltRingBuffer<T>
    {
        private T[] _entries;
        private uint _modMask;
        private uint _capacity;
        private long _consumerCursor = 0;
        private long _producerCursor = 0;

        public AltRingBuffer(uint capacity)
        {
            _capacity = NextPowerOfTwo(capacity);
            _modMask = _capacity - 1;
            _entries = new T[_capacity];
        }

        public T this[long index]
        {
            get { unchecked { return _entries[index & _modMask]; } }
            set { unchecked { _entries[index & _modMask] = value; } }
        }

        public bool TryDequeue(out T obj)
        {
            Interlocked.MemoryBarrier();
            var next = _consumerCursor;

            if (_producerCursor <= next)
            {
                obj = default(T);
                Interlocked.Exchange(ref _producerCursor, _producerCursor & _modMask);
                Interlocked.Exchange(ref _consumerCursor, _consumerCursor & _modMask);
                return false;
            }
            obj = this[next];
            Interlocked.CompareExchange(ref _consumerCursor, next + 1, next);
            return true;
        }


        public void Enqueue(T item)
        {
            var next = Interlocked.Increment(ref _producerCursor);
    
            long wrapPoint = next - _capacity;
            Interlocked.CompareExchange(ref _consumerCursor, wrapPoint + 1, wrapPoint); 
            
            this[next] = item;
        }

        public int Count { get { Interlocked.MemoryBarrier(); return (int)(_producerCursor - _consumerCursor); } }

        private static uint NextPowerOfTwo(uint x)
        {
            var result = 2U;
            while (result < x)
            {
                result <<= 1;
            }
            return result;
        }

        public void Clear()
        {
            Interlocked.MemoryBarrier();
            if (_producerCursor == 0 && _consumerCursor == 0)
                return;
            Interlocked.Exchange(ref _producerCursor, 0);
            Interlocked.Exchange(ref _consumerCursor, 0);
        }
    }
}
