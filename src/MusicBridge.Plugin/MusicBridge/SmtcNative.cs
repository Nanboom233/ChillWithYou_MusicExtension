using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MusicBridge;

internal static class SmtcNative
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnPtrOut(IntPtr self, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnIntOut(IntPtr self, out int o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnLongOut(IntPtr self, out long o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnByteOut(IntPtr self, out byte o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnLongIn(IntPtr self, long v, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnQI(IntPtr self, ref Guid iid, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnUIntIn(IntPtr self, uint v, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnReadAsync(IntPtr self, IntPtr buf, uint count, int opts, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnGetAt(IntPtr self, uint index, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnUIntOut(IntPtr self, out uint o);

	private const string ClassSessionManager = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";

	private static Guid IID_ManagerStatics = new Guid("2050c4ee-11a0-57de-aed7-c97c70338245");

	private static Guid IID_IAsyncInfo = new Guid("00000036-0000-0000-C000-000000000046");

	private const int INSP_GetRuntimeClassName = 4;

	private const int AI_get_Status = 7;

	private const int AO_GetResults = 8;

	private const int STAT_RequestAsync = 6;

	private const int MGR_GetCurrentSession = 6;

	private const int MGR_GetSessions = 7;

	private const int VV_GetAt = 6;

	private const int VV_GetSize = 7;

	private const int SES_get_SourceAppUserModelId = 6;

	private const int SES_TryGetMediaPropertiesAsync = 7;

	private const int SES_GetTimelineProperties = 8;

	private const int SES_GetPlaybackInfo = 9;

	private const int SES_TryPlayAsync = 10;

	private const int SES_TryPauseAsync = 11;

	private const int SES_TryStopAsync = 12;

	private const int SES_TrySkipNextAsync = 16;

	private const int SES_TrySkipPreviousAsync = 17;

	private const int SES_TryTogglePlayPause = 20;

	private const int SES_TryChangePosition = 24;

	private const int MP_get_Title = 6;

	private const int MP_get_AlbumArtist = 8;

	private const int MP_get_Artist = 9;

	private const int MP_get_AlbumTitle = 10;

	private const int MP_get_Thumbnail = 15;

	private static Guid IID_IRandomAccessStreamRef = new Guid("33ee3134-1dd6-4e3a-8067-d1c162e8642b");

	private static Guid IID_IRandomAccessStream = new Guid("905a0fe1-bc53-11df-8c49-001e4fc686da");

	private static Guid IID_IBufferFactory = new Guid("71af914d-c10f-484b-bc50-14bc623b3a27");

	private static Guid IID_IBufferByteAccess = new Guid("905a0fef-bc53-11df-8c49-001e4fc686da");

	private const int RASR_OpenReadAsync = 6;

	private const int RAS_get_Size = 6;

	private const int RAS_GetInputStreamAt = 8;

	private const int IS_ReadAsync = 6;

	private const int BUFFAC_Create = 6;

	private const int BBA_Buffer = 3;

	private const int AOWP_GetResults = 10;

	private const int PBI_get_Controls = 6;

	private const int PBI_get_PlaybackStatus = 7;

	private const int PBC_get_IsPauseEnabled = 7;

	private const int PBC_get_IsNextEnabled = 12;

	private const int PBC_get_IsPreviousEnabled = 13;

	private const int PBC_get_IsPlaybackPositionEnabled = 20;

	private const int TL_get_EndTime = 7;

	private const int TL_get_Position = 10;

	private static IntPtr _statics;

	public static bool Ready => _statics != IntPtr.Zero;

	[DllImport("combase.dll", CharSet = CharSet.Unicode)]
	private static extern int WindowsCreateString(string src, int length, out IntPtr hstring);

	[DllImport("combase.dll")]
	private static extern int WindowsDeleteString(IntPtr hstring);

	[DllImport("combase.dll")]
	private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

	[DllImport("combase.dll")]
	private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);

	[DllImport("combase.dll")]
	private static extern int RoInitialize(int initType);

	private static IntPtr Slot(IntPtr obj, int index)
	{
		return Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size);
	}

	private static IntPtr CallObj(IntPtr obj, int slot)
	{
		if (obj == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		if (((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnPtrOut)))(obj, out var o) != 0)
		{
			return IntPtr.Zero;
		}
		return o;
	}

	private static int CallInt(IntPtr obj, int slot)
	{
		if (obj == IntPtr.Zero)
		{
			return 0;
		}
		if (((FnIntOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnIntOut)))(obj, out var o) != 0)
		{
			return 0;
		}
		return o;
	}

	private static long CallLong(IntPtr obj, int slot)
	{
		if (obj == IntPtr.Zero)
		{
			return 0L;
		}
		if (((FnLongOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnLongOut)))(obj, out var o) != 0)
		{
			return 0L;
		}
		return o;
	}

	private static bool CallBool(IntPtr obj, int slot)
	{
		if (obj == IntPtr.Zero)
		{
			return false;
		}
		if (((FnByteOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnByteOut)))(obj, out var o) == 0)
		{
			return o != 0;
		}
		return false;
	}

	private static string CallHString(IntPtr obj, int slot)
	{
		if (obj == IntPtr.Zero)
		{
			return "";
		}
		IntPtr o = IntPtr.Zero;
		try
		{
			if (((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnPtrOut)))(obj, out o) != 0 || o == IntPtr.Zero)
			{
				return "";
			}
			uint length;
			IntPtr intPtr = WindowsGetStringRawBuffer(o, out length);
			if (intPtr == IntPtr.Zero || length == 0)
			{
				return "";
			}
			return Marshal.PtrToStringUni(intPtr, (int)length);
		}
		catch
		{
			return "";
		}
		finally
		{
			if (o != IntPtr.Zero)
			{
				WindowsDeleteString(o);
			}
		}
	}

	private static int QI(IntPtr obj, ref Guid iid, out IntPtr result)
	{
		return ((FnQI)Marshal.GetDelegateForFunctionPointer(Slot(obj, 0), typeof(FnQI)))(obj, ref iid, out result);
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

	public static string RuntimeClassName(IntPtr obj)
	{
		if (obj == IntPtr.Zero)
		{
			return "(null)";
		}
		return CallHString(obj, 4);
	}

	public static bool Initialize()
	{
		if (Ready)
		{
			return true;
		}
		IntPtr hstring = IntPtr.Zero;
		try
		{
			RoInitialize(1);
			if (WindowsCreateString("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager", "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager".Length, out hstring) != 0)
			{
				return false;
			}
			int num = RoGetActivationFactory(hstring, ref IID_ManagerStatics, out _statics);
			if (num != 0 || _statics == IntPtr.Zero)
			{
				BridgeLog.Error("[AM] SMTC 激活工厂获取失败 hr=0x" + num.ToString("X8"));
				_statics = IntPtr.Zero;
				return false;
			}
			BridgeLog.Info("[AM] SMTC 已就绪。");
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] SMTC 初始化异常：" + ex.Message);
			return false;
		}
		finally
		{
			if (hstring != IntPtr.Zero)
			{
				WindowsDeleteString(hstring);
			}
		}
	}

	public static void Shutdown()
	{
		Rel(_statics);
		_statics = IntPtr.Zero;
	}

	private static IntPtr AwaitOperation(IntPtr asyncOp, int timeoutMs = 8000, int resultSlot = 8)
	{
		if (asyncOp == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		IntPtr result = IntPtr.Zero;
		try
		{
			if (QI(asyncOp, ref IID_IAsyncInfo, out result) != 0 || result == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			FnIntOut fnIntOut = (FnIntOut)Marshal.GetDelegateForFunctionPointer(Slot(result, 7), typeof(FnIntOut));
			int i;
			for (i = 0; i < timeoutMs; i += 15)
			{
				if (fnIntOut(result, out var o) != 0)
				{
					return IntPtr.Zero;
				}
				if (o == 1)
				{
					break;
				}
				if (o >= 2)
				{
					return IntPtr.Zero;
				}
				Thread.Sleep(15);
			}
			if (i >= timeoutMs)
			{
				BridgeLog.Warn("[AM] SMTC 异步操作超时。");
				return IntPtr.Zero;
			}
			return CallObj(asyncOp, resultSlot);
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] 等待异步操作异常：" + ex.Message);
			return IntPtr.Zero;
		}
		finally
		{
			Rel(result);
			Rel(asyncOp);
		}
	}

	private static IntPtr GetManager()
	{
		if (!Ready)
		{
			return IntPtr.Zero;
		}
		return AwaitOperation(CallObj(_statics, 6));
	}

	private static IntPtr GetCurrentSession()
	{
		IntPtr manager = GetManager();
		if (manager == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			intPtr = CallObj(manager, 7);
			if (intPtr != IntPtr.Zero)
			{
				uint o = 0u;
				((FnUIntOut)Marshal.GetDelegateForFunctionPointer(Slot(intPtr, 7), typeof(FnUIntOut)))(intPtr, out o);
				FnGetAt fnGetAt = (FnGetAt)Marshal.GetDelegateForFunctionPointer(Slot(intPtr, 6), typeof(FnGetAt));
				for (uint num = 0u; num < o; num++)
				{
					if (fnGetAt(intPtr, num, out var o2) == 0 && !(o2 == IntPtr.Zero))
					{
						string text = CallHString(o2, 6);
						if (!string.IsNullOrEmpty(text) && text.IndexOf("AppleMusic", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							return o2;
						}
						Rel(o2);
					}
				}
			}
			return IntPtr.Zero;
		}
		finally
		{
			Rel(intPtr);
			Rel(manager);
		}
	}

	public static SmtcSnapshot ReadSnapshot()
	{
		SmtcSnapshot smtcSnapshot = new SmtcSnapshot();
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		IntPtr intPtr3 = IntPtr.Zero;
		IntPtr intPtr4 = IntPtr.Zero;
		IntPtr intPtr5 = IntPtr.Zero;
		try
		{
			intPtr = GetCurrentSession();
			if (intPtr == IntPtr.Zero)
			{
				return smtcSnapshot;
			}
			smtcSnapshot.AppId = CallHString(intPtr, 6);
			intPtr3 = CallObj(intPtr, 9);
			if (intPtr3 != IntPtr.Zero)
			{
				smtcSnapshot.Status = CallInt(intPtr3, 7);
				intPtr4 = CallObj(intPtr3, 6);
				if (intPtr4 != IntPtr.Zero)
				{
					smtcSnapshot.CanPause = CallBool(intPtr4, 7);
					smtcSnapshot.CanNext = CallBool(intPtr4, 12);
					smtcSnapshot.CanPrev = CallBool(intPtr4, 13);
					smtcSnapshot.CanSeek = CallBool(intPtr4, 20);
				}
			}
			intPtr5 = CallObj(intPtr, 8);
			if (intPtr5 != IntPtr.Zero)
			{
				smtcSnapshot.DurationSeconds = (double)CallLong(intPtr5, 7) / 10000000.0;
				smtcSnapshot.PositionSeconds = (double)CallLong(intPtr5, 10) / 10000000.0;
			}
			intPtr2 = AwaitOperation(CallObj(intPtr, 7));
			if (intPtr2 != IntPtr.Zero)
			{
				smtcSnapshot.Title = CallHString(intPtr2, 6);
				smtcSnapshot.Artist = CallHString(intPtr2, 9);
				smtcSnapshot.AlbumTitle = CallHString(intPtr2, 10);
				smtcSnapshot.AlbumArtist = CallHString(intPtr2, 8);
			}
			smtcSnapshot.Valid = true;
			return smtcSnapshot;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] 读取 SMTC 状态异常：" + ex.Message);
			return smtcSnapshot;
		}
		finally
		{
			Rel(intPtr5);
			Rel(intPtr4);
			Rel(intPtr3);
			Rel(intPtr2);
			Rel(intPtr);
		}
	}

	public static byte[] ReadThumbnail()
	{
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		IntPtr intPtr3 = IntPtr.Zero;
		IntPtr intPtr4 = IntPtr.Zero;
		IntPtr result = IntPtr.Zero;
		IntPtr o = IntPtr.Zero;
		IntPtr factory = IntPtr.Zero;
		IntPtr o2 = IntPtr.Zero;
		IntPtr intPtr5 = IntPtr.Zero;
		IntPtr result2 = IntPtr.Zero;
		IntPtr hstring = IntPtr.Zero;
		try
		{
			intPtr = GetCurrentSession();
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			intPtr2 = AwaitOperation(CallObj(intPtr, 7));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			intPtr3 = CallObj(intPtr2, 15);
			if (intPtr3 == IntPtr.Zero)
			{
				return null;
			}
			intPtr4 = AwaitOperation(CallObj(intPtr3, 6));
			if (intPtr4 == IntPtr.Zero)
			{
				return null;
			}
			if (QI(intPtr4, ref IID_IRandomAccessStream, out result) != 0 || result == IntPtr.Zero)
			{
				return null;
			}
			ulong num = (ulong)CallLong(result, 6);
			if (num == 0L || num > 8388608)
			{
				return null;
			}
			if (((FnLongIn)Marshal.GetDelegateForFunctionPointer(Slot(result, 8), typeof(FnLongIn)))(result, 0L, out o) != 0 || o == IntPtr.Zero)
			{
				return null;
			}
			if (WindowsCreateString("Windows.Storage.Streams.Buffer", "Windows.Storage.Streams.Buffer".Length, out hstring) != 0)
			{
				return null;
			}
			if (RoGetActivationFactory(hstring, ref IID_IBufferFactory, out factory) != 0 || factory == IntPtr.Zero)
			{
				return null;
			}
			if (((FnUIntIn)Marshal.GetDelegateForFunctionPointer(Slot(factory, 6), typeof(FnUIntIn)))(factory, (uint)num, out o2) != 0 || o2 == IntPtr.Zero)
			{
				return null;
			}
			if (((FnReadAsync)Marshal.GetDelegateForFunctionPointer(Slot(o, 6), typeof(FnReadAsync)))(o, o2, (uint)num, 0, out var o3) != 0 || o3 == IntPtr.Zero)
			{
				return null;
			}
			intPtr5 = AwaitOperation(o3, 8000, 10);
			if (intPtr5 == IntPtr.Zero)
			{
				return null;
			}
			if (QI(intPtr5, ref IID_IBufferByteAccess, out result2) != 0 || result2 == IntPtr.Zero)
			{
				return null;
			}
			if (((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(result2, 3), typeof(FnPtrOut)))(result2, out var o4) != 0 || o4 == IntPtr.Zero)
			{
				return null;
			}
			uint num2 = (uint)CallInt(intPtr5, 7);
			if (num2 == 0 || num2 > num)
			{
				num2 = (uint)num;
			}
			byte[] array = new byte[num2];
			Marshal.Copy(o4, array, 0, (int)num2);
			return array;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 读取封面失败：" + ex.Message);
			return null;
		}
		finally
		{
			Rel(result2);
			Rel(intPtr5);
			Rel(o2);
			Rel(factory);
			Rel(o);
			Rel(result);
			Rel(intPtr4);
			Rel(intPtr3);
			Rel(intPtr2);
			Rel(intPtr);
			if (hstring != IntPtr.Zero)
			{
				WindowsDeleteString(hstring);
			}
		}
	}

	private static bool Transport(int slot, string what)
	{
		IntPtr currentSession = GetCurrentSession();
		if (currentSession == IntPtr.Zero)
		{
			BridgeLog.Warn("[AM] " + what + "：当前没有 SMTC 会话。");
			return false;
		}
		try
		{
			bool flag = AwaitOperation(CallObj(currentSession, slot)) != IntPtr.Zero;
			BridgeLog.Info("[AM] " + what + " -> " + (flag ? "已发送" : "未成功"));
			return flag;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] " + what + " 异常：" + ex.Message);
			return false;
		}
		finally
		{
			Rel(currentSession);
		}
	}

	public static bool Stop()
	{
		return Transport(12, "停止");
	}

	public static bool Play()
	{
		return Transport(10, "播放");
	}

	public static bool Pause()
	{
		return Transport(11, "暂停");
	}

	public static bool TogglePlayPause()
	{
		return Transport(20, "播放/暂停");
	}

	public static bool Next()
	{
		return Transport(16, "下一首");
	}

	public static bool Previous()
	{
		return Transport(17, "上一首");
	}

	public static bool Seek(double seconds)
	{
		IntPtr currentSession = GetCurrentSession();
		if (currentSession == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			long v = (long)(seconds * 10000000.0);
			if (((FnLongIn)Marshal.GetDelegateForFunctionPointer(Slot(currentSession, 24), typeof(FnLongIn)))(currentSession, v, out var o) != 0 || o == IntPtr.Zero)
			{
				return false;
			}
			bool flag = AwaitOperation(o) != IntPtr.Zero;
			BridgeLog.Info("[AM] 跳转到 " + seconds.ToString("0.0") + "s -> " + (flag ? "已发送" : "未成功"));
			return flag;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] 跳转异常：" + ex.Message);
			return false;
		}
		finally
		{
			Rel(currentSession);
		}
	}
}
