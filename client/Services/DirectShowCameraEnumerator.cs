using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace LanChat.Client.Services
{
    internal static class DirectShowCameraEnumerator
    {
        private static readonly Guid SystemDeviceEnumerator = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
        private static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");

        public static IReadOnlyList<MediaDeviceInfo> GetVideoInputDevices()
        {
            var devices = new List<MediaDeviceInfo>();
            ICreateDevEnum? devEnum = null;
            IEnumMoniker? enumMoniker = null;

            try
            {
                var devEnumType = Type.GetTypeFromCLSID(SystemDeviceEnumerator);
                if (devEnumType == null)
                {
                    return devices;
                }

                devEnum = Activator.CreateInstance(devEnumType) as ICreateDevEnum;
                if (devEnum == null)
                {
                    return devices;
                }

                Guid category = VideoInputDeviceCategory;
                int hr = devEnum.CreateClassEnumerator(ref category, out enumMoniker, 0);
                if (hr != 0 || enumMoniker == null)
                {
                    return devices;
                }

                var monikers = new IMoniker[1];
                int index = 0;
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var moniker = monikers[0];
                    string displayName = ReadProperty(moniker, "FriendlyName") ?? $"Camera {index}";
                    string devicePath = ReadProperty(moniker, "DevicePath") ?? string.Empty;

                    devices.Add(new MediaDeviceInfo
                    {
                        DeviceIndex = index,
                        DisplayName = displayName,
                        DevicePath = devicePath
                    });

                    Marshal.ReleaseComObject(moniker);
                    index++;
                }
            }
            catch
            {
                return devices;
            }
            finally
            {
                if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
                if (devEnum != null) Marshal.ReleaseComObject(devEnum);
            }

            return devices;
        }

        private static string? ReadProperty(IMoniker moniker, string propertyName)
        {
            object? bagObject = null;
            Guid propertyBagId = typeof(IPropertyBag).GUID;

            try
            {
#pragma warning disable CS8625
                moniker.BindToStorage(null, null, ref propertyBagId, out bagObject);
#pragma warning restore CS8625
                if (bagObject is not IPropertyBag propertyBag)
                {
                    return null;
                }

                object value = string.Empty;
                int hr = propertyBag.Read(propertyName, ref value, IntPtr.Zero);
                return hr == 0 ? value?.ToString() : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bagObject != null) Marshal.ReleaseComObject(bagObject);
            }
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
        private interface ICreateDevEnum
        {
            [PreserveSig]
            int CreateClassEnumerator(
                ref Guid clsidDeviceClass,
                out IEnumMoniker? enumMoniker,
                int flags);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
        private interface IPropertyBag
        {
            [PreserveSig]
            int Read(
                [MarshalAs(UnmanagedType.LPWStr)] string propertyName,
                [MarshalAs(UnmanagedType.Struct)] ref object value,
                IntPtr errorLog);

            [PreserveSig]
            int Write(
                [MarshalAs(UnmanagedType.LPWStr)] string propertyName,
                [MarshalAs(UnmanagedType.Struct)] ref object value);
        }
    }
}
