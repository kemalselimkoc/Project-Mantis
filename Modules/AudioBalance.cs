// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Runtime.InteropServices;

namespace ProjectMantis
{
    // Uses the public Windows Core Audio endpoint-volume API. No driver, service,
    // audio capture, process injection, or third-party component is involved.
    internal static class AudioBalance
    {
        private static readonly Guid EndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        public static float[] GetChannelLevels()
        {
            object deviceObject = null; object endpointObject = null; MMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (MMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device; Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); deviceObject = device;
                Guid iid = EndpointVolumeId; Check(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
                IAudioEndpointVolume endpoint = (IAudioEndpointVolume)endpointObject;
                uint count; Check(endpoint.GetChannelCount(out count));
                float[] result = new float[count];
                for (uint i = 0; i < count; i++) Check(endpoint.GetChannelVolumeLevelScalar(i, out result[i]));
                return result;
            }
            finally
            {
                if (endpointObject != null && Marshal.IsComObject(endpointObject)) Marshal.FinalReleaseComObject(endpointObject);
                if (deviceObject != null && Marshal.IsComObject(deviceObject)) Marshal.FinalReleaseComObject(deviceObject);
                if (enumerator != null && Marshal.IsComObject(enumerator)) Marshal.FinalReleaseComObject(enumerator);
            }
        }

        public static void SetChannelLevels(float left, float right)
        {
            left = Math.Max(0f, Math.Min(1f, left)); right = Math.Max(0f, Math.Min(1f, right));
            object deviceObject = null; object endpointObject = null; MMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (MMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device; Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); deviceObject = device;
                Guid iid = EndpointVolumeId; Check(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
                IAudioEndpointVolume endpoint = (IAudioEndpointVolume)endpointObject;
                uint count; Check(endpoint.GetChannelCount(out count));
                if (count < 2) throw new InvalidOperationException("The active output does not expose stereo channels.");
                Guid context = Guid.Empty;
                Check(endpoint.SetChannelVolumeLevelScalar(0, left, ref context));
                Check(endpoint.SetChannelVolumeLevelScalar(1, right, ref context));
            }
            finally
            {
                if (endpointObject != null && Marshal.IsComObject(endpointObject)) Marshal.FinalReleaseComObject(endpointObject);
                if (deviceObject != null && Marshal.IsComObject(deviceObject)) Marshal.FinalReleaseComObject(deviceObject);
                if (enumerator != null && Marshal.IsComObject(enumerator)) Marshal.FinalReleaseComObject(enumerator);
            }
        }

        private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    internal enum EDataFlow { Render = 0, Capture = 1, All = 2 }
    internal enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface MMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }
}
