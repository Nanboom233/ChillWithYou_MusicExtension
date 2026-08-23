using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MusicBridge;

internal static class UiaNative
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnPtrOut(IntPtr self, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnHwndOut(IntPtr self, IntPtr hwnd, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnElemOut(IntPtr self, IntPtr elem, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnIntOut(IntPtr self, out int o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnPatternOut(IntPtr self, int patternId, out IntPtr o);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnVoid(IntPtr self);

	public struct ForegroundLock : IDisposable
	{
		private bool _entered;

		public static ForegroundLock Acquire()
		{
			ForegroundLock result = new ForegroundLock
			{
				_entered = true
			};
			try
			{
				Enter();
			}
			catch
			{
			}
			return result;
		}

		public void Dispose()
		{
			if (!_entered)
			{
				return;
			}
			_entered = false;
			try
			{
				Exit();
			}
			catch
			{
			}
		}
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnFindItem(IntPtr self, IntPtr startAfter, int propId, IntPtr variant, out IntPtr found);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnCreateCond(IntPtr self, int propId, IntPtr variant, out IntPtr cond);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int FnFindFirst(IntPtr self, int scope, IntPtr cond, out IntPtr found);

	private static Guid CLSID_CUIAutomation = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");

	private static Guid IID_IUIAutomation = new Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");

	private const uint CLSCTX_INPROC_SERVER = 1u;

	private const int UIA_GetRootElement = 5;

	private const int UIA_ElementFromHandle = 6;

	private const int UIA_ControlViewWalker = 14;

	private const int EL_GetCurrentPattern = 16;

	private const int EL_CurrentControlType = 21;

	private const int EL_CurrentName = 23;

	private const int EL_CurrentAutomationId = 29;

	private const int EL_CurrentClassName = 30;

	private const int EL_CurrentIsOffscreen = 38;

	private const int TW_GetFirstChild = 4;

	private const int TW_GetNextSibling = 6;

	private const int INV_Invoke = 3;

	private const int EC_Expand = 3;

	private const int EC_Collapse = 4;

	private const int EC_CurrentState = 5;

	private const int SI_Select = 3;

	private const int SCI_ScrollIntoView = 3;

	public const int PatternInvoke = 10000;

	public const int PatternExpandCollapse = 10005;

	public const int PatternSelectionItem = 10010;

	public const int PatternToggle = 10015;

	public const int PatternScrollItem = 10017;

	public const int PatternItemContainer = 10019;

	public const int PatternVirtualizedItem = 10020;

	private static IntPtr _uia;

	private static IntPtr _walker;

	private static int _findVisited;

	private const int SI_IsSelected = 6;

	private const uint LSFW_LOCK = 1u;

	private const uint LSFW_UNLOCK = 2u;

	private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

	private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_ZOrderOnly = 19u;

	private static int _shieldDepth;

	private static volatile bool _shieldWanted;

	private static volatile bool _shieldActive;

	private static IntPtr _shieldedWindow = IntPtr.Zero;

	private static DateTime _shieldSince = DateTime.MinValue;

	private static IntPtr _gameWindow = IntPtr.Zero;

	private static bool _lockFailureLogged;

	private static readonly TimeSpan ShieldApplyWait = TimeSpan.FromMilliseconds(120.0);

	private static readonly TimeSpan ShieldMaximumHold = TimeSpan.FromSeconds(90.0);

	private const int ForegroundRestoreAttemptCount = 2;

	private const int ForegroundRestoreVerificationDelayMilliseconds = 20;

	private const int TOG_Toggle = 3;

	private const int TOG_State = 4;

	private const int IC_FindItemByProperty = 3;

	private const int VI_Realize = 3;

	private const int UIA_CreatePropertyCondition = 23;

	private const int EL_FindFirst = 5;

	private const int TreeScope_Descendants = 4;

	private const int UIA_AutomationIdPropertyId = 30011;

	private const int UIA_NamePropertyId = 30005;

	private static int _nativeFindState;

	public static bool Ready
	{
		get
		{
			if (_uia != IntPtr.Zero)
			{
				return _walker != IntPtr.Zero;
			}
			return false;
		}
	}

	public static bool AllowForegroundForRefresh { get; set; }

	public static bool NativeFindAvailable => _nativeFindState >= 0;

	[DllImport("ole32.dll")]
	private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr ppv);

	public static bool Initialize()
	{
		if (Ready)
		{
			return true;
		}
		try
		{
			int num = CoCreateInstance(ref CLSID_CUIAutomation, IntPtr.Zero, 1u, ref IID_IUIAutomation, out _uia);
			if (num != 0 || _uia == IntPtr.Zero)
			{
				BridgeLog.Error("[AM] UIA 初始化失败 hr=0x" + num.ToString("X8"));
				_uia = IntPtr.Zero;
				return false;
			}
			num = Call(_uia, 14, out _walker);
			if (num != 0 || _walker == IntPtr.Zero)
			{
				BridgeLog.Error("[AM] 获取 ControlViewWalker 失败 hr=0x" + num.ToString("X8"));
				return false;
			}
			BridgeLog.Info("[AM] UIA 已就绪。");
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] UIA 初始化异常：" + ex.Message);
			return false;
		}
	}

	public static void Shutdown()
	{
		if (_walker != IntPtr.Zero)
		{
			Marshal.Release(_walker);
			_walker = IntPtr.Zero;
		}
		if (_uia != IntPtr.Zero)
		{
			Marshal.Release(_uia);
			_uia = IntPtr.Zero;
		}
	}

	private static IntPtr Slot(IntPtr obj, int index)
	{
		return Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size);
	}

	private static int Call(IntPtr obj, int slot, out IntPtr result)
	{
		return ((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnPtrOut)))(obj, out result);
	}

	private static int CallInt(IntPtr obj, int slot, out int result)
	{
		return ((FnIntOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnIntOut)))(obj, out result);
	}

	private static string CallBstr(IntPtr obj, int slot)
	{
		if (((FnPtrOut)Marshal.GetDelegateForFunctionPointer(Slot(obj, slot), typeof(FnPtrOut)))(obj, out var o) != 0 || o == IntPtr.Zero)
		{
			return "";
		}
		try
		{
			return Marshal.PtrToStringBSTR(o) ?? "";
		}
		finally
		{
			Marshal.FreeBSTR(o);
		}
	}

	public static IntPtr ElementFromHandle(IntPtr hwnd)
	{
		if (!Ready)
		{
			return IntPtr.Zero;
		}
		try
		{
			if (((FnHwndOut)Marshal.GetDelegateForFunctionPointer(Slot(_uia, 6), typeof(FnHwndOut)))(_uia, hwnd, out var o) != 0)
			{
				return IntPtr.Zero;
			}
			return o;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 附着窗口失败（Apple Music 可能已退出）：" + ex.GetType().Name);
			return IntPtr.Zero;
		}
	}

	public static UiaNode Snapshot(IntPtr el)
	{
		if (el == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			UiaNode obj = new UiaNode
			{
				Handle = el,
				Name = CallBstr(el, 23),
				AutomationId = CallBstr(el, 29),
				ClassName = CallBstr(el, 30)
			};
			CallInt(el, 21, out var result);
			obj.ControlType = result;
			return obj;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 读取元素属性失败（Apple Music 可能已退出）：" + ex.GetType().Name);
			return null;
		}
	}

	public static bool IsOffscreen(IntPtr el)
	{
		if (el == IntPtr.Zero)
		{
			return true;
		}
		if (CallInt(el, 38, out var result) == 0)
		{
			return result != 0;
		}
		return false;
	}

	private static IntPtr WalkStep(IntPtr el, int slot)
	{
		if (!Ready || el == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		if (((FnElemOut)Marshal.GetDelegateForFunctionPointer(Slot(_walker, slot), typeof(FnElemOut)))(_walker, el, out var o) != 0)
		{
			return IntPtr.Zero;
		}
		return o;
	}

	public static IntPtr FirstChild(IntPtr el)
	{
		return WalkStep(el, 4);
	}

	public static IntPtr NextSibling(IntPtr el)
	{
		return WalkStep(el, 6);
	}

	public static List<UiaNode> Children(IntPtr parent, int max = 500)
	{
		List<UiaNode> list = new List<UiaNode>();
		IntPtr intPtr = FirstChild(parent);
		while (intPtr != IntPtr.Zero && list.Count < max)
		{
			UiaNode uiaNode = Snapshot(intPtr);
			if (uiaNode != null)
			{
				list.Add(uiaNode);
			}
			intPtr = NextSibling(intPtr);
		}
		return list;
	}

	public static IntPtr FindByAutomationId(IntPtr root, string automationId, int maxDepth = 30)
	{
		return FindByAutomationId(root, automationId, maxDepth, logMiss: true);
	}

	public static IntPtr FindByAutomationId(IntPtr root, string automationId, int maxDepth, bool logMiss)
	{
		IntPtr intPtr = FindFirstByAutomationId(root, automationId);
		if (intPtr != IntPtr.Zero)
		{
			return intPtr;
		}
		_findVisited = 0;
		IntPtr intPtr2 = FindBy(root, automationId, null, maxDepth, 0);
		if (intPtr2 == IntPtr.Zero && logMiss)
		{
			BridgeLog.Info("[AM] 未找到 aid=" + automationId + "（遍历 " + _findVisited + " 个节点，深度上限 " + maxDepth + "）");
		}
		return intPtr2;
	}

	public static IntPtr FindByName(IntPtr root, string name, int maxDepth = 12)
	{
		return FindBy(root, null, name, maxDepth, 0);
	}

	public static IntPtr FindByClassName(IntPtr root, string className, int maxDepth = 14)
	{
		return FindBy(root, null, null, maxDepth, 0, className);
	}

	private static IntPtr FindBy(IntPtr root, string aid, string name, int maxDepth, int depth, string cls = null)
	{
		if (!Ready || root == IntPtr.Zero || depth > maxDepth)
		{
			return IntPtr.Zero;
		}
		IntPtr intPtr = FirstChild(root);
		while (intPtr != IntPtr.Zero)
		{
			_findVisited++;
			if ((cls != null) ? (CallBstr(intPtr, 30) == cls) : ((aid != null) ? (CallBstr(intPtr, 29) == aid) : (CallBstr(intPtr, 23) == name)))
			{
				return intPtr;
			}
			IntPtr intPtr2 = FindBy(intPtr, aid, name, maxDepth, depth + 1, cls);
			if (intPtr2 != IntPtr.Zero)
			{
				Marshal.Release(intPtr);
				return intPtr2;
			}
			IntPtr intPtr3 = NextSibling(intPtr);
			Marshal.Release(intPtr);
			intPtr = intPtr3;
		}
		return IntPtr.Zero;
	}

	private static IntPtr GetPattern(IntPtr el, int patternId)
	{
		if (el == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		if (((FnPatternOut)Marshal.GetDelegateForFunctionPointer(Slot(el, 16), typeof(FnPatternOut)))(el, patternId, out var o) != 0)
		{
			return IntPtr.Zero;
		}
		return o;
	}

	public static bool Invoke(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10000);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		IntPtr originalWindow = CaptureForegroundWindow();
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				RestoreForegroundAfterUia("Invoke()", originalWindow);
				Marshal.Release(pattern);
			}
		}
	}

	public static int IsSelected(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10010);
		if (pattern == IntPtr.Zero)
		{
			return -1;
		}
		try
		{
			int result;
			return (CallInt(pattern, 6, out result) == 0) ? result : (-1);
		}
		finally
		{
			Marshal.Release(pattern);
		}
	}

	public static bool Select(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10010);
		if (pattern == IntPtr.Zero)
		{
			BridgeLog.Warn("[AM] Select：目标没有 SelectionItem 模式。");
			return false;
		}
		IntPtr originalWindow = CaptureForegroundWindow();
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				RestoreForegroundAfterUia("Select()", originalWindow);
				Marshal.Release(pattern);
			}
		}
	}

	public static bool ScrollIntoView(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10017);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				Marshal.Release(pattern);
			}
		}
	}

	public static int ExpandState(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10005);
		if (pattern == IntPtr.Zero)
		{
			return -1;
		}
		try
		{
			int result;
			return (CallInt(pattern, 5, out result) == 0) ? result : (-1);
		}
		finally
		{
			Marshal.Release(pattern);
		}
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool LockSetForegroundWindow(uint lockCode);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	private static IntPtr GameWindow()
	{
		if (_gameWindow != IntPtr.Zero && IsWindow(_gameWindow))
		{
			return _gameWindow;
		}
		try
		{
			using Process process = Process.GetCurrentProcess();
			_gameWindow = process.MainWindowHandle;
		}
		catch
		{
			_gameWindow = IntPtr.Zero;
		}
		return _gameWindow;
	}

	private static void Enter()
	{
		if (Interlocked.Increment(ref _shieldDepth) <= 1 && !AllowForegroundForRefresh)
		{
			bool flag = false;
			try
			{
				flag = LockSetForegroundWindow(1u);
			}
			catch
			{
			}
			if (!flag && !_lockFailureLogged)
			{
				_lockFailureLogged = true;
				BridgeLog.Warn("[AM] LockSetForegroundWindow 加锁失败，改由窗口置顶屏蔽兜底。");
			}
			_shieldWanted = true;
			DateTime dateTime = DateTime.UtcNow + ShieldApplyWait;
			while (!_shieldActive && DateTime.UtcNow < dateTime)
			{
				Thread.Sleep(4);
			}
		}
	}

	private static void Exit()
	{
		if (Interlocked.Decrement(ref _shieldDepth) > 0)
		{
			return;
		}
		if (_shieldDepth < 0)
		{
			Interlocked.Exchange(ref _shieldDepth, 0);
		}
		_shieldWanted = false;
		try
		{
			LockSetForegroundWindow(2u);
		}
		catch
		{
		}
	}

	public static void WatchdogTick()
	{
		try
		{
			bool flag = _shieldWanted;
			if (flag && _shieldActive && DateTime.UtcNow - _shieldSince >= ShieldMaximumHold)
			{
				BridgeLog.Warn("[AM] 前台屏蔽持有超过 " + ShieldMaximumHold.TotalSeconds + "s，强制撤销（深度=" + _shieldDepth + "）。");
				Interlocked.Exchange(ref _shieldDepth, 0);
				_shieldWanted = false;
				flag = false;
			}
			if (flag == _shieldActive)
			{
				return;
			}
			if (flag)
			{
				IntPtr intPtr = GameWindow();
				if (!(intPtr == IntPtr.Zero) && !(CaptureForegroundWindow() != intPtr) && SetWindowPos(intPtr, HWND_TOPMOST, 0, 0, 0, 0, 19u))
				{
					_shieldedWindow = intPtr;
					_shieldSince = DateTime.UtcNow;
					_shieldActive = true;
				}
			}
			else
			{
				IntPtr shieldedWindow = _shieldedWindow;
				_shieldedWindow = IntPtr.Zero;
				_shieldActive = false;
				if (shieldedWindow != IntPtr.Zero && IsWindow(shieldedWindow))
				{
					SetWindowPos(shieldedWindow, HWND_NOTOPMOST, 0, 0, 0, 0, 19u);
				}
			}
		}
		catch
		{
		}
	}

	public static void ReleaseForegroundShield()
	{
		Interlocked.Exchange(ref _shieldDepth, 0);
		_shieldWanted = false;
		try
		{
			LockSetForegroundWindow(2u);
		}
		catch
		{
		}
		IntPtr shieldedWindow = _shieldedWindow;
		_shieldedWindow = IntPtr.Zero;
		_shieldActive = false;
		if (shieldedWindow == IntPtr.Zero || !IsWindow(shieldedWindow))
		{
			return;
		}
		try
		{
			SetWindowPos(shieldedWindow, HWND_NOTOPMOST, 0, 0, 0, 0, 19u);
		}
		catch
		{
		}
	}

	public static IntPtr CaptureForegroundWindow()
	{
		try
		{
			return GetForegroundWindow();
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	private static uint WindowProcessId(IntPtr window)
	{
		try
		{
			GetWindowThreadProcessId(window, out var pid);
			return pid;
		}
		catch
		{
			return 0u;
		}
	}

	private static bool TryRestoreForegroundWindow(IntPtr originalWindow)
	{
		if (originalWindow == IntPtr.Zero || !IsWindow(originalWindow))
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			SetForegroundWindow(originalWindow);
			Thread.Sleep(20);
			if (CaptureForegroundWindow() == originalWindow)
			{
				return true;
			}
		}
		return false;
	}

	private static void RestoreForegroundAfterUia(string operation, IntPtr originalWindow)
	{
		IntPtr intPtr = CaptureForegroundWindow();
		if (!(originalWindow == IntPtr.Zero) && !(intPtr == originalWindow))
		{
			uint num = WindowProcessId(originalWindow);
			uint num2 = WindowProcessId(intPtr);
			if (TryRestoreForegroundWindow(originalWindow))
			{
				bool shieldActive = _shieldActive;
				BridgeLog.Info("[AM] " + operation + " 曾激活进程 " + num2 + "，已立即恢复原前台进程 " + num + (shieldActive ? "（屏蔽生效，画面无变化）" : "（**未屏蔽，用户可见**）") + "。");
			}
			else
			{
				BridgeLog.Warn("[AM] " + operation + " 改变了前台（" + num + " -> " + num2 + "），恢复失败。");
			}
		}
	}

	public static void RestoreForegroundIfTargetOwnsIt(IntPtr originalWindow, IntPtr targetWindow, string operation)
	{
		if (originalWindow == IntPtr.Zero || targetWindow == IntPtr.Zero)
		{
			return;
		}
		IntPtr intPtr = CaptureForegroundWindow();
		if (!(intPtr == originalWindow) && WindowProcessId(intPtr) == WindowProcessId(targetWindow))
		{
			if (TryRestoreForegroundWindow(originalWindow))
			{
				BridgeLog.Info("[AM] " + operation + " 结束时已恢复原前台窗口。");
			}
			else
			{
				BridgeLog.Warn("[AM] " + operation + " 结束时 Apple Music 仍在前台，恢复失败。");
			}
		}
	}

	public static bool Expand(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10005);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		IntPtr originalWindow = CaptureForegroundWindow();
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				RestoreForegroundAfterUia("Expand()", originalWindow);
				Marshal.Release(pattern);
			}
		}
	}

	public static bool Collapse(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10005);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 4), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				Marshal.Release(pattern);
			}
		}
	}

	public static bool Toggle(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10015);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		IntPtr originalWindow = CaptureForegroundWindow();
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			finally
			{
				RestoreForegroundAfterUia("Toggle()", originalWindow);
				Marshal.Release(pattern);
			}
		}
	}

	public static int ToggleState(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10015);
		if (pattern == IntPtr.Zero)
		{
			return -1;
		}
		try
		{
			int result;
			return (CallInt(pattern, 4, out result) == 0) ? result : (-1);
		}
		finally
		{
			Marshal.Release(pattern);
		}
	}

	public static bool SupportsItemContainer(IntPtr container)
	{
		IntPtr pattern = GetPattern(container, 10019);
		if (pattern == IntPtr.Zero)
		{
			return false;
		}
		Marshal.Release(pattern);
		return true;
	}

	public static IntPtr NextItem(IntPtr container, IntPtr startAfter)
	{
		IntPtr pattern = GetPattern(container, 10019);
		if (pattern == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		IntPtr intPtr = Marshal.AllocHGlobal(24);
		using (ForegroundLock.Acquire())
		{
			try
			{
				for (int i = 0; i < 24; i++)
				{
					Marshal.WriteByte(intPtr, i, 0);
				}
				if (((FnFindItem)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnFindItem)))(pattern, startAfter, 0, intPtr, out var found) != 0)
				{
					return IntPtr.Zero;
				}
				return found;
			}
			catch
			{
				return IntPtr.Zero;
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
				Marshal.Release(pattern);
			}
		}
	}

	public static IntPtr FindFirstByAutomationId(IntPtr root, string automationId)
	{
		if (_nativeFindState < 0)
		{
			return IntPtr.Zero;
		}
		IntPtr intPtr = FindFirstByProperty(root, 30011, automationId);
		if (_nativeFindState == 0 && intPtr != IntPtr.Zero)
		{
			UiaNode uiaNode = Snapshot(intPtr);
			if (uiaNode == null || !(uiaNode.AutomationId == automationId))
			{
				_nativeFindState = -1;
				BridgeLog.Warn("[AM] 原生查找自检未通过（回读的 AutomationId 对不上），本次会话停用原生查找，改用遍历。");
				Release(intPtr);
				return IntPtr.Zero;
			}
			_nativeFindState = 1;
			BridgeLog.Info("[AM] 原生查找自检通过（能穿透虚拟化，扫描和起播都走它）。");
		}
		return intPtr;
	}

	public static IntPtr FindFirstByName(IntPtr root, string name)
	{
		if (_nativeFindState < 0)
		{
			return IntPtr.Zero;
		}
		return FindFirstByProperty(root, 30005, name);
	}

	private static IntPtr FindFirstByProperty(IntPtr root, int propertyId, string value)
	{
		if (!Ready || root == IntPtr.Zero || string.IsNullOrEmpty(value))
		{
			return IntPtr.Zero;
		}
		IntPtr intPtr = Marshal.AllocHGlobal(24);
		IntPtr intPtr2 = IntPtr.Zero;
		IntPtr cond = IntPtr.Zero;
		try
		{
			intPtr2 = Marshal.StringToBSTR(value);
			for (int i = 0; i < 24; i++)
			{
				Marshal.WriteByte(intPtr, i, 0);
			}
			Marshal.WriteInt16(intPtr, 0, 8);
			Marshal.WriteIntPtr(intPtr, 8, intPtr2);
			if (((FnCreateCond)Marshal.GetDelegateForFunctionPointer(Slot(_uia, 23), typeof(FnCreateCond)))(_uia, propertyId, intPtr, out cond) != 0 || cond == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (((FnFindFirst)Marshal.GetDelegateForFunctionPointer(Slot(root, 5), typeof(FnFindFirst)))(root, 4, cond, out var found) != 0)
			{
				return IntPtr.Zero;
			}
			return found;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 原生查找失败：" + ex.Message);
			return IntPtr.Zero;
		}
		finally
		{
			if (cond != IntPtr.Zero)
			{
				Marshal.Release(cond);
			}
			if (intPtr2 != IntPtr.Zero)
			{
				Marshal.FreeBSTR(intPtr2);
			}
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static bool Realize(IntPtr el)
	{
		IntPtr pattern = GetPattern(el, 10020);
		if (pattern == IntPtr.Zero)
		{
			return true;
		}
		using (ForegroundLock.Acquire())
		{
			try
			{
				return ((FnVoid)Marshal.GetDelegateForFunctionPointer(Slot(pattern, 3), typeof(FnVoid)))(pattern) == 0;
			}
			catch
			{
				return false;
			}
			finally
			{
				Marshal.Release(pattern);
			}
		}
	}

	public static void Release(IntPtr p)
	{
		if (p != IntPtr.Zero)
		{
			Marshal.Release(p);
		}
	}

	public static void ReleaseAll(List<UiaNode> nodes)
	{
		if (nodes == null)
		{
			return;
		}
		foreach (UiaNode node in nodes)
		{
			if (node.Handle != IntPtr.Zero)
			{
				Marshal.Release(node.Handle);
			}
		}
	}
}
