using System.Threading;

namespace TsAudio.Utils.Threading;
public static class ArithmeticalExtensions
{
    public static void SetGreater(ref long oldValue, long value)
    {
        var currentValue = oldValue;

        if (currentValue > value)
        {
            return;
        }

        while(value > currentValue)
        {
            var previousValue = currentValue;
            currentValue = Interlocked.CompareExchange(ref oldValue, value, previousValue);
        }
    }
}
