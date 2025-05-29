using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FileTransparencyTest
{
    internal class Program
    {
        // Windows API constants
        private const int GWL_EXSTYLE = -20;    // Index for extended window styles in GetWindowLong/SetWindowLong
        private const int WS_EX_LAYERED = 0x80000;  // Extended window style that enables layered windows (required for transparency)
        private const int LWA_ALPHA = 0x2;  // Flag that tells SetLayeredWindowAttributes to use alpha blending for transparency

        // Windows API functions - these are external functions from Windows DLLs that we can call from C#
        // [DllImport] is an attribute that tells C# how to call unmanaged code (Windows API functions)

        // Windows API functions needed to get updated windows for fileeplorer

        // Finds a window by its class name and/or window title
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // Enumerates (loops through) all top-level windows on the desktop
        // Takes a callback function that will be called for each window found
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        // Gets the text (title) of a window - used to identify windows
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        // Gets the class name of a window - different types of windows have different class names
        // File Explorer windows have the class name "CabinetWClass"
        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        // Gets the process ID that owns a specific window
        // We use this to verify that the window belongs to explorer.exe
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Gets window style information - we use this to check current extended styles
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        // Sets window style information - we use this to add the WS_EX_LAYERED style
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // This is the key function for transparency - sets the alpha (transparency) value for a layered window
        // crKey: color key (not used for alpha transparency)
        // bAlpha: transparency value (0 = fully transparent, 255 = fully opaque)
        // dwFlags: specifies which parameters to use (we use LWA_ALPHA for alpha blending)
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        // Gets the handle of the currently active (foreground) window
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Checks if a window handle is still valid (window hasn't been closed)
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        // Delegate (function pointer) type for the EnumWindows callback
        // This defines the signature of the function that will be called for each window
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        static void Main(string[] args)
        {
            byte alpha = 230;      //change this to change across the board

            Console.WriteLine("File Explorer Transparency Controller"+
            "\n====================================\n"+
            "\nChoose an option:"+
            "\n1. Set all File Explorer windows to 80% transparency"+
            "\n2. Dynamic transparency control (follows active window)"+
            "\n3. Reset transparency to 100% (opaque)\n"+
            "\nEnter your choice (1, 2, or 3): ");

            string choice = Console.ReadLine();     //not doing checks yet

            switch (choice)     //simple switch for handling functions
            {
                case "1":
                    SetFileExplorerTransparency(alpha); // 50% transparency (255 * 0.5 = 127.5)       //just do math and change to desired preset
                    Console.WriteLine($"Set all currently open File Explorer windows to {quickMath(alpha)}% transparency.");
                    break;
                case "2":
                    StartDynamicTransparencyControl(alpha);
                    break;
                case "3":
                    SetFileExplorerTransparency(255); // 100% opaque
                    Console.WriteLine("Reset all File Explorer windows to 100% opacity.");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Exiting...");
                    break;
            }

            if (choice != "2")
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void SetFileExplorerTransparency(byte alpha)
        {
            var explorerWindows = GetFileExplorerWindows();     //set var using api call

            if (explorerWindows.Count == 0)      // Check if any File Explorer windows are open
            {
                Console.WriteLine("No File Explorer windows found.");
                return;
            }

            foreach (IntPtr hwnd in explorerWindows)        // Apply transparency to each File Explorer window found
            {
                SetWindowTransparency(hwnd, alpha);     //used to set hwnd of current file explorers to alpha
            }

            Console.WriteLine($"Applied transparency to {explorerWindows.Count} File Explorer window(s).");
        }

        static int quickMath(byte alpha)
        {
            int result = (int)((alpha / 255.0) * 100);      //int cast to avoid integer division with byte and int
            return result;
        }

        static void StartDynamicTransparencyControl(byte alpha)
        {
            //int quickMath = (alpha / 255) * 100;
            Console.WriteLine("Starting dynamic transparency control...");
            Console.WriteLine($"File Explorer windows will become {quickMath(alpha)}% transparent when active.");
            Console.WriteLine("Press 'Q' to quit.");
            Console.WriteLine();

            var explorerWindows = GetFileExplorerWindows();
            if (explorerWindows.Count == 0)
            {
                Console.WriteLine("No File Explorer windows found. Please open File Explorer first.");
                return;
            }
            // Keep track of the last active window to avoid unnecessary API calls
            IntPtr lastActiveWindow = IntPtr.Zero;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        // Reset all windows to opaque before exiting
                        foreach (IntPtr hwnd in explorerWindows)
                        {
                            if (IsWindow(hwnd))
                            {
                                SetWindowTransparency(hwnd, 255);
                            }
                        }
                        Console.WriteLine("Exiting and resetting transparency...");
                        break;
                    }
                }

                IntPtr activeWindow = GetForegroundWindow();

                if (activeWindow != lastActiveWindow)
                {
                    // Reset previous window to opaque  //optional may keep 
                    /*removing for now
                    if (lastActiveWindow != IntPtr.Zero && IsWindow(lastActiveWindow))
                    {
                        if (IsFileExplorerWindow(lastActiveWindow))
                        {
                            SetWindowTransparency(lastActiveWindow, 255);       //255 to reset
                        }
                    }
                    */
                    // Set current window to transparent if it's File Explorer
                    if (IsFileExplorerWindow(activeWindow))
                    {
                        SetWindowTransparency(activeWindow, alpha); // 70% transparency
                        Console.WriteLine($"Made File Explorer window transparent (Handle: {activeWindow})");
                    }

                    lastActiveWindow = activeWindow;
                }

                Thread.Sleep(100); // Check every 100ms
            }
        }
        //todo add different applications such as chorme perhaps?
        static System.Collections.Generic.List<IntPtr> GetFileExplorerWindows()
        {
            // Create a list to store window handles (IntPtr = "Int Pointer" - represents a handle to a Windows object
            var windows = new System.Collections.Generic.List<IntPtr>();

            // EnumWindows calls our callback function for every top-level window on the desktop
            // The callback function (lambda expression) will be called once for each window
            EnumWindows((hWnd, lParam) =>
            {
                // For each window, check if it's a File Explorer window
                if (IsFileExplorerWindow(hWnd))
                {
                    windows.Add(hWnd); // Add the window handle to our list
                }
                return true;    // Return true to continue enumerating more windows
            }, IntPtr.Zero);     // Second parameter is not used in our case

            return windows;
        }

        static bool IsFileExplorerWindow(IntPtr hWnd)
        {
            var className = new System.Text.StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            // Check if it's a File Explorer window
            string classNameStr = className.ToString();
            if (classNameStr != "CabinetWClass" && classNameStr != "Chrome_WidgetWin_1")      //class names for fileexplorer and chrome      //chrome isnt working atm coz it uses OPENGL and DIRECTX --nvrm i fixxed with wingetwin_1
                return false;

            // Verify it's actually explorer.exe and chrome process
            GetWindowThreadProcessId(hWnd, out uint processId);
            try
            {
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                       process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase);       
                /*
                if (process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    return process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
                }
                else if(process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                {
                    return process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return false;
                }*/
            }
            catch
            {
                return false;
            }
        }

        static void SetWindowTransparency(IntPtr hwnd, byte alpha)
        {
            // Get current window style
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Add layered window style if not present
            if ((extendedStyle & WS_EX_LAYERED) == 0)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
            }

            // Set transparency
            SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
        }
    }
}