using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MusicBridge;

internal static class AppleMusicVolume
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnEndpoint(IntPtr self, int flow, int role, out IntPtr dev);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnActivate(IntPtr self, ref Guid iid, uint ctx, IntPtr act, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnPtrOut(IntPtr self, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnIntOut(IntPtr self, out int o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnUIntOut(IntPtr self, out uint o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnIndexOut(IntPtr self, int i, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnSetVol(IntPtr self, float v, ref Guid ctx);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnGetVol(IntPtr self, out float v);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnQI(IntPtr self, ref Guid iid, out IntPtr o);

	private static Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

	private static Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

	private static Guid IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

	private static Guid IID_IAudioSessionControl2 = new Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d");

	private static Guid IID_ISimpleAudioVolume = new Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8");

	private static Guid EventContext = Guid.Empty;

	private const uint CLSCTX_ALL = 23u;

	private const int eRender = 0;

	private const int eMultimedia = 1;

	private const int MMDE_GetDefaultAudioEndpoint = 4;

	private const int MMD_Activate = 3;

	private const int ASM2_GetSessionEnumerator = 5;

	private const int ASE_GetCount = 3;

	private const int ASE_GetSession = 4;

	private const int ASC_GetState = 3;

	private const int ASC2_GetProcessId = 14;

	private const int SAV_SetMasterVolume = 3;

	private const int SAV_GetMasterVolume = 4;

	private static readonly string[] AudioProcNames = new string[2] { "AMPLibraryAgent", "AppleMusic" };

	[DllImport("ole32.dll")]
	private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr ppv);

	private static IntPtr Slot(IntPtr o, int i)
	{
		return Marshal.ReadIntPtr(Marshal.ReadIntPtr(o), i * IntPtr.Size);
	}

	private static void Rel(IntPtr p)
	{
		if (p != IntPtr.Zero)
		{
			try
			{
				Marshal.Release(p);
			}
			catch
			{
			}
		}
	}

	private static int QI(IntPtr o, ref Guid iid, out IntPtr r)
	{
		return ((FnQI)Marshal.GetDelegateForFunctionPointer(Slot(o, 0), typeof(FnQI)))(o, ref iid, out r);
	}

	private static IntPtr FindSessionVolume(int[] targetPids)
	{
		IntPtr ppv = IntPtr.Zero;
		IntPtr dev = IntPtr.Zero;
		IntPtr o = IntPtr.Zero;
		IntPtr o2 = IntPtr.Zero;
		try
		{
			if (CoCreateInstance(ref CLSID_MMDeviceEnumerator, IntPtr.Zero, 23u, ref IID_IMMDeviceEnumerator, out ppv) != 0 || ppv == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (((FnEndpoint)Marshal.GetDelegateForFunctionPointer(Slot(ppv, 4), typeof(FnEndpoint)))(ppv, 0, 1, out dev) != 0 || dev == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (((FnActivate)Marshal.GetDelegateForFunctionPointer(Slot(dev, 3), typeof(FnActivate)))(dev, ref IID_IAudioSessionManager2, 23u, IntPtr.Zero, out o) != 0 || o == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(o, 5), typeof(FnPtrOut)))(o, out o2) != 0 || o2 == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (((FnIntOut)Marshal.GetDelegateForFunctionPointer(Slot(o2, 3), typeof(FnIntOut)))(o2, out var o3) != 0)
			{
				return IntPtr.Zero;
			}
			IntPtr intPtr = IntPtr.Zero;
			for (int i = 0; i < o3; i++)
			{
				IntPtr o4 = IntPtr.Zero;
				IntPtr r = IntPtr.Zero;
				try
				{
					if (((FnIndexOut)Marshal.GetDelegateForFunctionPointer(Slot(o2, 4), typeof(FnIndexOut)))(o2, i, out o4) != 0 || o4 == IntPtr.Zero || QI(o4, ref IID_IAudioSessionControl2, out r) != 0 || r == IntPtr.Zero || ((FnUIntOut)Marshal.GetDelegateForFunctionPointer(Slot(r, 14), typeof(FnUIntOut)))(r, out var o5) != 0)
					{
						continue;
					}
					bool flag = false;
					foreach (int num in targetPids)
					{
						if (o5 == (uint)num)
						{
							flag = true;
							break;
						}
					}
					if (flag && QI(o4, ref IID_ISimpleAudioVolume, out var r2) == 0 && !(r2 == IntPtr.Zero))
					{
						int o6 = 0;
						((FnIntOut)Marshal.GetDelegateForFunctionPointer(Slot(o4, 3), typeof(FnIntOut)))(o4, out o6);
						if (o6 == 1)
						{
							Rel(intPtr);
							return r2;
						}
						if (intPtr == IntPtr.Zero)
						{
							intPtr = r2;
						}
						else
						{
							Rel(r2);
						}
					}
				}
				finally
				{
					Rel(r);
					Rel(o4);
				}
			}
			return intPtr;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 查找音频会话失败：" + ex.Message);
			return IntPtr.Zero;
		}
		finally
		{
			Rel(o2);
			Rel(o);
			Rel(dev);
			Rel(ppv);
		}
	}

	private static int[] FindPids()
	{
		List<int> list = new List<int>();
		string[] audioProcNames = AudioProcNames;
		foreach (string processName in audioProcNames)
		{
			try
			{
				Process[] processesByName = Process.GetProcessesByName(processName);
				foreach (Process process in processesByName)
				{
					try
					{
						list.Add(process.Id);
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}
		return list.ToArray();
	}

	public static bool SetVolume(float v)
	{
		int[] array = FindPids();
		if (array.Length == 0)
		{
			return false;
		}
		IntPtr intPtr = FindSessionVolume(array);
		if (intPtr == IntPtr.Zero)
		{
			BridgeLog.Warn("[AM] 找不到 Apple Music 的音频会话（它可能还没出过声）。");
			return false;
		}
		try
		{
			if (v < 0f)
			{
				v = 0f;
			}
			if (v > 1f)
			{
				v = 1f;
			}
			int num = ((FnSetVol)Marshal.GetDelegateForFunctionPointer(Slot(intPtr, 3), typeof(FnSetVol)))(intPtr, v, ref EventContext);
			if (num != 0)
			{
				BridgeLog.Warn("[AM] 设置音量失败 hr=0x" + num.ToString("X8"));
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 设置音量异常：" + ex.Message);
			return false;
		}
		finally
		{
			Rel(intPtr);
		}
	}

	public static float GetVolume()
	{
		int[] array = FindPids();
		if (array.Length == 0)
		{
			return -1f;
		}
		IntPtr intPtr = FindSessionVolume(array);
		if (intPtr == IntPtr.Zero)
		{
			return -1f;
		}
		try
		{
			float v;
			return (((FnGetVol)Marshal.GetDelegateForFunctionPointer(Slot(intPtr, 4), typeof(FnGetVol)))(intPtr, out v) == 0) ? v : (-1f);
		}
		catch
		{
			return -1f;
		}
		finally
		{
			Rel(intPtr);
		}
	}
}
