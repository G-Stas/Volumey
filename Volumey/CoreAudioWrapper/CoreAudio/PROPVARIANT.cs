using System;
using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio
{
    /// <summary>
    /// The structure is used by the GetValue and SetValue methods of IPropertyStore
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public readonly struct PROPVARIANT
    {
        [FieldOffset(0)] public readonly ushort varType;
        [FieldOffset(2)] public readonly ushort wReserved1;
        [FieldOffset(4)] public readonly ushort wReserved2;
        [FieldOffset(6)] public readonly ushort wReserved3;
        [FieldOffset(8)] public readonly IntPtr pwszVal; 
        [FieldOffset(8)] public readonly BLOB blob;

        /// <summary>
        /// PwszVal - A pointer to a null-terminated Unicode string in the user default locale.
        /// </summary>
        /// <returns></returns>
        public string GetPwszValAsString()
            => Marshal.PtrToStringUni(this.pwszVal);
    }
}