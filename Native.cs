using System;
using System.Runtime.InteropServices;
namespace SeatSheet.Widget;
internal static class Native
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr handle, int index, int value);
    public static void MakeToolWindow(IntPtr handle) => SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x80);
}
