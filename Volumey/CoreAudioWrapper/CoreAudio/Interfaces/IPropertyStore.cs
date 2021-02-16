using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.CoreAudioWrapper.CoreAudio.Interfaces
{
    /// <summary>
    /// This interface exposes methods used to enumerate and manipulate property values.
    /// </summary>
    [Guid(GuidValue.External.IPropertyStore), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        public int GetCount(out int propertiesCount);

        /// <summary>
        /// Not implemented
        /// </summary>
        public int GetAt();

        /// <summary>
        /// This method retrieves the data for a specific property.
        /// </summary>
        /// <param name="propertyKey"></param>
        /// <param name="propvariant">PROPVARIANT member vt is set to VT_LPWSTR and member pwszVal points to a null-terminated, wide-character string that contains the required audio device property</param>
        /// <returns></returns>
        public int GetValue(ref PROPERTYKEY propertyKey, out PROPVARIANT propvariant);

        /// <summary>
        /// Not implemented
        /// </summary>
        public int SetValue();

        /// <summary>
        /// Not implemented 
        /// </summary>
        public int Commit();
    }
}