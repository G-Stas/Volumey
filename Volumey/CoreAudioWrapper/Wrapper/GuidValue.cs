using System;

namespace Volumey.CoreAudioWrapper.Wrapper
{
    //Contains external & internal guid values
    public static class GuidValue
    {
        /// <summary>
        /// Contains CoreAudio interfaces guid values
        /// </summary>
        public static class External
        {
            public const string ISimpleAudioVolume = "87CE5498-68D6-44E5-9215-6DA47EF883D8";
            public const string IAudioSessionManager = "BFA971F1-4D5E-40BB-935E-967039BFBEE4";
            public const string IAudioSessionManager2 = "77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F";
            public const string IAudioSessionNotification = "641DD20B-4D41-49CC-ABA3-174B9477BB08";
            public const string IAudioSessionControl = "F4B1A599-7266-4319-A8CA-E70ACB11E8CD";
            public const string IAudioSessionControl2 = "bfb7ff88-7239-4fc9-8fa2-07c950be9c6d";
            public const string IAudioSessionEvents = "24918ACC-64B3-37C1-8CA9-74A66E9957A8";
            public const string IAudioEndpointVolume = "5CDF2C82-841E-4546-9722-0CF74078229A";
            public const string IAudioEndpointVolumeCallback = "657804FA-D6AD-4496-8A60-352752AF4F89";
            public const string IAudioSessionEnumerator = "E2F5BB11-0570-40CA-ACDD-3AA01277DEE8";
            public const string IMMDevice = "D666063F-1587-4E43-81F1-B948E807363F";
            public const string IMMDeviceEnumerator = "A95664D2-9614-4F35-A746-DE8DB63617E6";
            public const string IMMDeviceCollection = "0BD7A1BE-7A1A-44DB-8397-CC5392387B5E";
            public const string IMMNotificationClient = "7991EEC9-7E89-4D85-8390-6C703CEC60C0";
            public const string IPropertyStore = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99";
            public const string IMMEndpoint = "1BE09788-6894-4089-8586-9A2A6C265AC5";
            public const string IPolicyConfig = "F8679F50-850A-41CF-9C72-430F290290C8";
            public const string PolicyConfigCOM = "870AF99C-171D-4F9E-AF0D-E63DF40C2BC9";
        }

        /// <summary>
        /// Contains application guid values
        /// </summary>
        public static class Internal
        {
            /// <summary>
            /// Used to distinguish volume change sources between internal changes and external in volume change callbacks
            /// </summary>
            public static Guid VolumeGUID = new Guid("1DD0B43C-EE90-47BE-8E12-DE3BB4CF92F0");
            public static Guid Empty = Guid.Empty;
        }
    }
}
