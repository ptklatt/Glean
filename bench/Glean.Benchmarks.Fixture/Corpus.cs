using System.Runtime.InteropServices;

namespace Glean.Benchmarks.Fixture;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class SampleAttribute(string label, int[] values) : Attribute
{
    public string Label { get; } = label;
    public int[] Values { get; } = values;
    public string? Note { get; set; }
}

public interface IBase<T>
{
    T Echo(T value);
}

public interface IChild<T> : IBase<T>
{
    int Value { get; }
}

public abstract class Root
{
    public abstract int Abstract(int value);
    public virtual int Virtual(int value) => value;
}

[Sample("base", new[] { 1, 2, 3 }, Note = "named")]
public class Base<T> : Root, IChild<T> where T : struct, IComparable<T>
{
    public T Field;
    public static int StaticField;
    public const int Constant = 17;
    public virtual int Value { get; protected set; }
    public T this[int index] { get => Field; set => Field = value; }
    public event EventHandler? Changed;

    [Sample("method", new[] { 4, 5 })]
    public T Echo([In] T value)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        return value;
    }

    private long Echo(long value) => value;
    public static int Static(int value) => value;
    public override int Abstract(int value) => value;
    public sealed override int Virtual(int value) => value;
    public U Generic<U>(T value, U other) where U : class, new() => other;
    public int[] Arrays(ref int value, int[] vector, int[,] matrix, in long number) => vector;
    public unsafe delegate* unmanaged[Cdecl]<int, int> FunctionPointer(delegate* unmanaged[Cdecl]<int, int> callback) => callback;
    public void Defaults(int number = 42, string text = "value") { }

    public sealed class Nested<U> where U : IDisposable
    {
        public U? Value;
    }
}

public class Middle : Base<int> { }
public sealed class Leaf : Middle
{
    int Explicit() => ((IBase<int>)this).Echo(Field);
    public int Use()
    {
        Field = Echo(3);
        Value = Static(Field);
        Changed += Handler;
        Changed -= Handler;
        return Virtual(Value) + Explicit();
    }

    private void Handler(object? sender, EventArgs args) { }
}
