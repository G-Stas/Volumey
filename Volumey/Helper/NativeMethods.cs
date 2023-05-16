using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace Volumey.Helper
{
    static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, IntPtr phiconSmall, int nIcons);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern int ExtractIconEx(string lpszFile, int nIconIndex, IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);
        
        /// <summary>
        /// Use with LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020 flag to extract resources
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <param name="handle"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibraryExA(
            [MarshalAs(UnmanagedType.LPStr)] string lpFileName,
            IntPtr handle, uint flag);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int LoadString(IntPtr hInstance, int ID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// Registers the active instance of an application for restart.
        /// </summary>
        /// <param name="commandLineArgs">Command line args</param>
        /// <param name="Flags"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int RegisterApplicationRestart([MarshalAs(UnmanagedType.LPWStr)] string commandLineArgs, int Flags);

        /// <summary>
        /// Removes the active instance of an application from the restart list.
        /// </summary>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int UnregisterApplicationRestart();

        /// <summary>
        /// Enables the application to access the window menu (also known as the system menu or the control menu) for copying and modifying.
        /// </summary>
        /// <param name="hWnd">A handle to the window that will own a copy of the window menu.</param>
        /// <param name="bRevert">If this parameter is FALSE, GetSystemMenu returns a handle to the copy of the window menu currently in use.
        /// The copy is initially identical to the window menu, but it can be modified.
        /// If this parameter is TRUE, GetSystemMenu resets the window menu back to the default state. </param>
        /// <returns>If the bRevert parameter is FALSE, the return value is a handle to a copy of the window menu. If the bRevert parameter is TRUE, the return value is NULL.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        /// <summary>
        /// Enables, disables, or grays the specified menu item.
        /// </summary>
        /// <param name="menuPtr">A handle to the menu.</param>
        /// <param name="uIDEnableItem">The menu item to be enabled, disabled, or grayed, as determined by the uEnable parameter. This parameter specifies an item in a menu bar, menu, or submenu.</param>
        /// <param name="uEnable">Controls the interpretation of the uIDEnableItem parameter and indicate whether the menu item is enabled, disabled, or grayed.</param>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int EnableMenuItem(IntPtr menuPtr, long uIDEnableItem, long uEnable);

        /// <summary>
        /// Extracts string from system DLL
        /// </summary>
        /// <param name="file">System DLL name without it's path</param>
        /// <param name="number">Non-negative index</param>
        /// <returns></returns>
        internal static string ExtractStringFromSystemDLL(string file, int number) 
        {
            IntPtr lib = LoadLibraryExA(file, IntPtr.Zero, 0x00000020);
            StringBuilder result = new StringBuilder(64);
            LoadString(lib, number, result, result.Capacity);
            FreeLibrary(lib);
            return result.ToString();
        }
        
        /// <summary>
        /// Defines a system-wide hot key.
        /// </summary>
        /// <param name="hwnd">A handle to the window that will receive WM_HOTKEY messages generated by the hot key.</param>
        /// <param name="id">The identifier of the hot key. If the hWnd parameter is NULL,
        /// then the hot key is associated with the current thread rather than with a particular window. </param>
        /// <param name="modifiers">The keys that must be pressed in combination with the key specified by the vk parameter in order to generate the WM_HOTKEY message. </param>
        /// <param name="vk">The virtual-key code of the hot key.</param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, uint vk);

        /// <summary>
        /// Frees a hot key previously registered by the calling thread.
        /// </summary>
        /// <param name="hwnd">A handle to the window associated with the hot key to be freed.</param>
        /// <param name="id">The identifier of the hot key to be freed.</param>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        
        private const UInt32 FLASHW_ALL = 3; //Flash both the window caption and taskbar button.        
        private const UInt32 FLASHW_TIMERNOFG = 12; //Flash continuously until the window comes to the foreground.  

        /// <summary>
        /// Flashes the specified window. It does not change the active state of the window.
        /// </summary>
        /// <param name="pwfi"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        /// <summary>
        /// Flashes the specified window.
        /// </summary>
        /// <param name="handle">A handle to the window to be flashed.</param>
        internal static void FlashWindow(IntPtr handle)
        {
            //Don't flash if the window is active            
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = UInt32.MaxValue,
                dwTimeout = 0
            };
            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        /// <summary>
        /// Contains the flash status for a window and the number of times the system should flash the window.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            /// <summary>
            /// The size of the structure, in bytes.
            /// </summary>
            internal UInt32 cbSize;

            /// <summary>
            /// A handle to the window to be flashed. The window can be either opened or minimized.
            /// </summary>
            internal IntPtr hwnd;

            /// <summary>
            /// The flash status. 
            /// </summary>
            internal UInt32 dwFlags;

            /// <summary>
            /// The number of times to flash the window.  
            /// </summary>
            internal UInt32 uCount;

            /// <summary>
            /// The rate at which the Window is to be flashed, in milliseconds. If Zero, the function uses the default cursor blink rate.  
            /// </summary>
            internal UInt32 dwTimeout;
        }
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
    
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags,
                                                     [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpExeName,
                                                     ref uint lpdwSize);
        
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }
    
        public const uint MAX_PATH = 256;
        
        [DllImport("user32.dll")]
        public static extern  uint MapVirtualKey(uint uCode, MapType uMapType);
		
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetKeyNameText(int lParam, [MarshalAs(UnmanagedType.LPWStr), Out] StringBuilder str, int size);
		
        public enum MapType : uint
        {
            MAPVK_VK_TO_VSC = 0x0,
            MAPVK_VSC_TO_VK = 0x1,
            MAPVK_VK_TO_CHAR = 0x2,
            MAPVK_VSC_TO_VK_EX = 0x3,
        }
        
    /// <summary>
    /// Special window handles.
    /// </summary>
    internal enum SpecialWindowHandles
    {
        /// <summary>
        /// Places the window at the top of the Z order.
        /// </summary>
        Top = 0,
        
        /// <summary>
        /// Places the window at the bottom of the Z order.
        /// If the hWnd parameter identifies a topmost window, the window loses its topmost status and is placed at the bottom of all other windows.
        /// </summary>
        Bottom = 1,
        
        /// <summary>
        /// Places the window above all non-topmost windows.
        /// The window maintains its topmost position even when it is deactivated.
        /// </summary>
        TopMost = -1,
        
        /// <summary>
        /// Places the window above all non-topmost windows (that is, behind all topmost windows).
        /// This flag has no effect if the window is already a non-topmost window.
        /// </summary>
        NoTopMost = -2
    }

    [Flags]
    internal enum SetWindowPosFlags : uint
    {
        /// <summary>
        /// If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window.
        /// This prevents the calling thread from blocking its execution while other threads process the request.
        /// </summary>
        AsyncWindowPositioning = 0x4000,

        /// <summary>
        /// Prevents generation of the WM_SYNCPAINT message.
        /// </summary>
        DeferErase = 0x2000,

        /// <summary>
        /// Draws a frame (defined in the window's class description) around the window.
        /// </summary>
        DrawFrame = 0x0020,

        /// <summary>
        /// Applies new frame styles set using the SetWindowLong function.
        /// Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed.
        /// If this flag is not specified, WM_NCCALCSIZE is sent only when the window's size is being changed.
        /// </summary>
        FrameChanged = 0x0020,

        /// <summary>
        /// Hides the window.
        /// </summary>
        HideWindow = 0x0080,

        /// <summary>
        /// Does not activate the window.
        /// If this flag is not set, the window is activated and moved to the top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter parameter).
        /// </summary>
        NoActivate = 0x0010,

        /// <summary>
        /// Discards the entire contents of the client area.
        /// If this flag is not specified, the valid contents of the client area are saved and copied back into the client area after the window is sized or repositioned.
        /// </summary>
        NoCopyBits = 0x0100,

        /// <summary>
        /// Retains the current position (ignores X and Y parameters).
        /// </summary>
        NoMove = 0x0002,

        /// <summary>
        /// Does not change the owner window's position in the Z order.
        /// </summary>
        NoOwnerZOrder = 0x0200,

        /// <summary>
        /// Does not redraw changes. If this flag is set, no repainting of any kind occurs.
        /// This applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent window uncovered as a result of the window being moved.
        /// When this flag is set, the application must explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
        /// </summary>
        NoRedraw = 0x0008,

        /// <summary>
        /// Same as the NoOwnerZOrder flag.
        /// </summary>
        NoReposition = 0x0200,

        /// <summary>
        /// Prevents the window from receiving the WM_WINDOWPOSCHANGING message.
        /// </summary>
        NoSendChanging = 0x0400,

        /// <summary>
        /// Retains the current size (ignores the cx and cy parameters).
        /// </summary>
        NoSize = 0x0001,

        /// <summary>
        /// Retains the current Z order (ignores the hWndInsertAfter parameter).
        /// </summary>
        NoZOrder = 0x0004,

        /// <summary>
        /// Displays the window.
        /// </summary>
        ShowWindow = 0x0040
    }


        /// <summary>
        /// Installs an application-defined hook procedure into a hook chain.
        /// </summary>
        /// <param name="id">The type of hook procedure to be installed.</param>
        /// <param name="callback">A pointer to the hook procedure.</param>
        /// <param name="dllHwnd">A handle to the DLL containing the hook procedure.</param>
        /// <param name="threadId">The identifier of the thread with which the hook procedure is to be associated.</param>
        /// <returns>Handle to the hook procedure.</returns>
        [DllImport("User32.dll")][PreserveSig]
        public static extern IntPtr SetWindowsHookExA(int id, HotkeyHookManager.WinHookCallback callback, IntPtr dllHwnd, int threadId);
        
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags uFlags);

        /// <summary>
        /// Removes a hook procedure installed in a hook chain by the <see cref="SetWindowsHookExA"/> function.
        /// </summary>
        /// <param name="hookPtr">A handle to the hook to be removed.</param>
        /// <returns>If the function succeeds, the return value is nonzero.</returns>
        [DllImport("User32.dll")][PreserveSig]
        public static extern bool UnhookWindowsHookEx(IntPtr hookPtr);

        /// <summary>
        /// Passes the hook information to the next hook procedure in the current hook chain.
        /// </summary>
        /// <param name="hook">This parameter is ignored.</param>
        /// <param name="nCode">The hook code passed to the current hook procedure.</param>
        /// <param name="wParam">The wParam value passed to the current hook procedure. </param>
        /// <param name="lParam">The lParam value passed to the current hook procedure.</param>
        /// <returns></returns>
        [DllImport("User32.dll")][PreserveSig]
        public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, ref IntPtr lParam);
        
        /// <summary>
        /// Retrieves a handle to the foreground window (the window with which the user is currently working).
        /// </summary>
        /// <returns>The return value is a handle to the foreground window. The foreground window can be NULL in certain circumstances, such as when a window is losing activation.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the process that created the window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="lpdwProcessId">A pointer to a variable that receives the process identifier.</param>
        /// <returns>The return value is the identifier of the thread that created the window.</returns>
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        internal static uint GetForegroundWindowProcessId()
        {
            uint procId;
            IntPtr handle = GetForegroundWindow();
            if(GetWindowThreadProcessId(handle, out procId) > 0)
                return procId;
            return uint.MinValue;
        }

        [DllImport("user32.dll")]
        public static extern uint SendInput(uint nInputs, INPUT [] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            internal InputType type;
            internal InputUnion U;
            internal static int Size
            {
                get { return Marshal.SizeOf(typeof(INPUT)); }
            }
        }

        public enum InputType : uint
        {
            INPUT_MOUSE,
            INPUT_KEYBOARD,
            INPUT_HARDWARE
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            internal MOUSEINPUT mi;
            [FieldOffset(0)]
            internal KEYBDINPUT ki;
            [FieldOffset(0)]
            internal HARDWAREINPUT hi;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal int mouseData;
            internal int dwFlags;
            internal uint time;
            internal UIntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            internal byte wVk;
            internal byte wScan;
            internal int dwFlags;
            internal int time;
            internal UIntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            internal int uMsg;
            internal short wParamL;
            internal short wParamH;
        }
        
        /// <summary>
        /// If specified, the key is being released. If not specified, the key is being pressed.
        /// </summary>
        private const int KEYEVENTF_KEYUP = 0x0002;

        internal static void SimulateKeyPress(Key keyToSimulate)
        {
            INPUT[] inputs = new INPUT[2];
		          
            var pressInput = new INPUT();
            pressInput.type = InputType.INPUT_KEYBOARD;
            pressInput.U.ki.wVk = (byte)KeyInterop.VirtualKeyFromKey(keyToSimulate);
            inputs[0] = pressInput;
		          
            var releaseInput = pressInput;
            releaseInput.U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1] = releaseInput;

            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }
        
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATA pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
        internal struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=14)]
            public string cAlternateFileName;
            public uint dwFileType;
            public uint dwCreatorType;
            public uint wFinderFlags;
        }
    }
}
