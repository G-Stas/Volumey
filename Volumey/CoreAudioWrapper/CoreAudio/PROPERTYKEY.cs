using System;

namespace Volumey.CoreAudioWrapper.CoreAudio
{
    /// <summary>
    /// Specifies the FMTID/PID identifier that programmatically identifies a property.
    /// </summary>
    public readonly struct PROPERTYKEY
    {
        /// <summary>
        /// A unique GUID for the property.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// A property identifier.
        /// </summary>
        public int Id { get; }

        public PROPERTYKEY(Guid guid, int id)
        {
            this.Guid = guid;
            this.Id = id;
        }

        /// <summary>
        /// All audio endpoint devices have these properties.
        /// </summary>
        public static class DeviceProperties
        {
            /// <summary>
            /// The device description of the endpoint device (for example, "Speakers").
            /// </summary>
            public static PROPERTYKEY Description =>
                 new PROPERTYKEY(new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
                    id: 2);

            /// <summary>
            /// The friendly name of the endpoint device (for example, "Speakers (XYZ Audio Adapter)")
            /// </summary>
            public static PROPERTYKEY FriendlyName =>
                new PROPERTYKEY(new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
                    id: 14);

            /// <summary>
            /// The icon resource specifier of the endpoint device which
            /// contains full path to the file that contains the icon
            /// and the resource identifier (for example, "%SystemRoot%\system32\DLL1.dll,-12") 
            /// </summary>
            public static PROPERTYKEY IconPath =>
                new PROPERTYKEY(new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), 
                    id: 12);

            /// <summary>
            /// Specifies the device format, which is the format that the user has selected for the stream
            /// that flows between the audio engine and the audio endpoint device when the device operates in shared mode.
            /// </summary>
            public static PROPERTYKEY DeviceFormat =>
                new PROPERTYKEY(new Guid(0xf19f064d, 0x82c, 0x4e27, 0xbc, 0x73, 0x68, 0x82, 0xa1, 0xbb, 0x8e, 0x4c), id: 0);
        }
    }
}