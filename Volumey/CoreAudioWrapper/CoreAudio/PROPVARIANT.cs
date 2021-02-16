using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Volumey.CoreAudioWrapper.CoreAudio
{
    /// <summary>
    /// The structure is used by the GetValue and SetValue methods of IPropertyStore
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)] private ushort varType;
        [FieldOffset(2)] private ushort wReserved1;
        [FieldOffset(4)] private ushort wReserved2;
        [FieldOffset(6)] private ushort wReserved3;
        [FieldOffset(8)] private byte bVal;
        [FieldOffset(8)] private sbyte cVal;
        [FieldOffset(8)] private ushort uiVal;
        [FieldOffset(8)] private short iVal;
        [FieldOffset(8)] private UInt32 uintVal;
        [FieldOffset(8)] private Int32 intVal;
        [FieldOffset(8)] private UInt64 ulVal;
        [FieldOffset(8)] private Int64 lVal;
        [FieldOffset(8)] private float fltVal;
        [FieldOffset(8)] private double dblVal;
        [FieldOffset(8)] private short boolVal;
        [FieldOffset(8)] private IntPtr pclsidVal;
        [FieldOffset(8)] private IntPtr pszVal;
        [FieldOffset(8)] private IntPtr pwszVal; 
        [FieldOffset(8)] private IntPtr punkVal;
        [FieldOffset(8)] private IntPtr ca;
        [FieldOffset(8)] private FILETIME filetime;

        /// <summary>
        /// PwszVal - A pointer to a null-terminated Unicode string in the user default locale.
        /// </summary>
        /// <returns></returns>
        public string GetPwszValAsString()
            => Marshal.PtrToStringUni(this.pwszVal);
    }
}