global using System.Collections.Specialized;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections;
global using System.Threading.Tasks;
global using System.Linq;
global using System.Text;
global using System.IO;
global using System;


// global using MessageBox = ModernWpf.MessageBox;


using System.Diagnostics.CodeAnalysis;

namespace TabbedExplorer.WPF;


public sealed class StackPointer<T>
    : ICollection<T?>
{
    private readonly object _mutex;
    private T?[] _array;


    private int End { get; set; }

    public int Size { get; private set; }

    public int EndOffset => Size - End;

    public T? CurrentElement => Peek();

    public int Count { get; }

    public bool IsReadOnly { get; } = false;


    public StackPointer()
        : this(0)
    {
    }

    public StackPointer(int capacity)
    {
        _mutex = new();
        _array = new T[capacity];
        End = 0;
        Size = 0;
    }

    public void Add(T? value) => Push(value);

    [return: NotNullIfNotNull("value")]
    public T? Push(T? value)
    {
        lock (_mutex)
        {
            if (End >= _array.Length - 1)
                Array.Resize(ref _array, Math.Max(End + 1, _array.Length * 2));

            _array[End++] = value;
            Size = End;
        }

        return value;
    }

    public T? Peek() => TryPeek(out T? value) ? value : throw new IndexOutOfRangeException();

    public T? Pop()
    {
        lock (_mutex)
        {
            if (End * 2 < _array.Length - 1)
                Array.Resize(ref _array, _array.Length / 2);

            T? value = _array[End--];

            Size = End;
            _array[End + 1] = default;

            return value;
        }
    }

    public bool TryPop([NotNullWhen(true), MaybeNullWhen(false)] out T? value)
    {
        bool popped;

        lock (_mutex)
        {
            if (End * 2 < _array.Length - 1)
                Array.Resize(ref _array, _array.Length / 2);

            value = (popped = End > 0) ? _array[End--] : default;
            Size = End;

            if (popped)
                _array[End + 1] = default;
        }

        return popped;
    }

    public bool TryPeek([NotNullWhen(true), MaybeNullWhen(false)] out T? value)
    {
        bool peeked;

        lock (_mutex)
            value = (peeked = End > 0) ? _array[End - 1] : default;

        return peeked;
    }

    public bool MovePointerDown()
    {
        lock (_mutex)
        {
            if (End == 0)
                return false;

            --End;
        }

        return true;
    }

    public bool MovePointerUp()
    {
        lock (_mutex)
        {
            if (End >= Size)
                return false;

            ++End;
        }

        return true;
    }

    public void Clear()
    {
        Array.Resize(ref _array, 0);

        End = 0;
        Size = 0;
    }

    public IEnumerator<T?> GetEnumerator()
    {
        lock (_mutex)
        {
            int i = End;

            while (i-- > 0)
                yield return _array[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(T? item)
    {
        lock (_mutex)
            for (int i = 0; i <= End; ++i)
                if (Equals(_array[i], item))
                    return true;

        return false;
    }

    public void CopyTo(T?[] array, int index)
    {
        IEnumerator<T?> enumerator = GetEnumerator();

        for (; index < array.Length && enumerator.MoveNext(); ++index)
            array[index] = enumerator.Current;
    }

    public bool Remove(T? item) => throw new NotImplementedException();
}

