using System.Runtime.InteropServices;
using System.Text;

namespace Valency.Shell.Platform;

public static class WindowsConsole
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint Utf8CodePage = 65001;

    public static void Configure()
    {
        if (!OperatingSystem.IsWindows())
            return;

        EnableVirtualTerminal();
        EnableUtf8();
    }

    private static void EnableVirtualTerminal()
    {
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return;

        if (GetConsoleMode(handle, out var mode))
            SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    private static void EnableUtf8()
    {
        SetConsoleOutputCP(Utf8CodePage);
        SetConsoleCP(Utf8CodePage);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = utf8;
        Console.InputEncoding = utf8;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint codePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint codePageID);
}
