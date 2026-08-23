using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MusicBridge;

internal static class AppleMusicClient
{
	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	private const string ProcName = "AppleMusic";

	private const uint WM_KEYDOWN = 256u;

	private const uint WM_KEYUP = 257u;

	private const int VK_CONTROL = 17;

	private const int VK_SPACE = 32;

	private const string AidHamburger = "Hamburger_Button";

	private const string AidPlaylists = "Sidebar_Header_Playlists";

	private const string AidLibrary = "Sidebar_Header_Library";

	private const string AidAccount = "lowerAccountName";

	private const int CtButton = 50000;

	private const int CtListItem = 50007;

	private const int CtSlider = 50015;

	private const int CtText = 50020;

	public static string LastPaneProblem;

	private static bool _hamburgerUsedThisSession;

	private static string _lastOpenedPlaylistId;

	private static bool _structureScanFailed;

	public static bool LastInteractionOpenedPane { get; private set; }

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lp);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassNameW(IntPtr hWnd, StringBuilder buf, int n);

	[DllImport("user32.dll")]
	private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr hWnd);

	private static IntPtr FindInputSite(IntPtr mainWindow)
	{
		IntPtr found = IntPtr.Zero;
		StringBuilder sb = new StringBuilder(256);
		EnumChildWindows(mainWindow, delegate(IntPtr h, IntPtr l)
		{
			sb.Length = 0;
			GetClassNameW(h, sb, sb.Capacity);
			if (sb.ToString() == "InputSiteWindowClass")
			{
				found = h;
				return false;
			}
			return true;
		}, IntPtr.Zero);
		return found;
	}

	private static void PostCtrlSpace(IntPtr inputSite)
	{
		PostMessageW(inputSite, 256u, (IntPtr)17, IntPtr.Zero);
		Thread.Sleep(MusicBridgeOptions.Current.Apple.KeyChordStepDelay);
		PostMessageW(inputSite, 256u, (IntPtr)32, IntPtr.Zero);
		Thread.Sleep(MusicBridgeOptions.Current.Apple.KeyChordHoldDelay);
		PostMessageW(inputSite, 257u, (IntPtr)32, IntPtr.Zero);
		PostMessageW(inputSite, 257u, (IntPtr)17, IntPtr.Zero);
	}

	public static IntPtr FindWindow()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("AppleMusic");
			foreach (Process process in processesByName)
			{
				try
				{
					if (process.MainWindowHandle != IntPtr.Zero)
					{
						return process.MainWindowHandle;
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 查找窗口失败：" + ex.Message);
		}
		return IntPtr.Zero;
	}

	public static bool IsWindowMinimized(IntPtr hwnd)
	{
		try
		{
			return hwnd != IntPtr.Zero && IsIconic(hwnd);
		}
		catch
		{
			return false;
		}
	}

	public static void DumpTopLevel(IntPtr winEl)
	{
		List<UiaNode> list = UiaNative.Children(winEl, 20);
		try
		{
			BridgeLog.Info("[AM诊断] 窗口元素直接子节点 " + list.Count + " 个：");
			foreach (UiaNode item in list)
			{
				BridgeLog.Info("[AM诊断]   [" + item.ControlType + "] cls=" + item.ClassName + " aid=" + item.AutomationId);
				List<UiaNode> list2 = UiaNative.Children(item.Handle, 12);
				try
				{
					foreach (UiaNode item2 in list2)
					{
						BridgeLog.Info("[AM诊断]      └ [" + item2.ControlType + "] cls=" + item2.ClassName + " aid=" + item2.AutomationId);
					}
				}
				finally
				{
					UiaNative.ReleaseAll(list2);
				}
			}
		}
		finally
		{
			UiaNative.ReleaseAll(list);
		}
	}

	public static string ReadAccountName(IntPtr winEl)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "lowerAccountName");
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			UiaNode uiaNode = UiaNative.Snapshot(intPtr);
			return (uiaNode == null || string.IsNullOrEmpty(uiaNode.Name)) ? null : uiaNode.Name;
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	public static void AllowPaneOpenAgain()
	{
		_hamburgerUsedThisSession = false;
	}

	private static bool TryOpenPaneOnce(IntPtr winEl)
	{
		if (_hamburgerUsedThisSession)
		{
			return false;
		}
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Hamburger_Button");
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		_hamburgerUsedThisSession = true;
		LastInteractionOpenedPane = true;
		BridgeLog.Info("[AM] 侧栏是紧凑模式，点一次汉堡键撑开（本次游戏会话仅此一次，会短暂闪到前台）。");
		try
		{
			UiaNative.Invoke(intPtr);
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		Thread.Sleep(MusicBridgeOptions.Current.Apple.PaneOpenSettleDelay);
		return true;
	}

	public static void ForgetOpenedPlaylist()
	{
		_lastOpenedPlaylistId = null;
	}

	private static bool EnsurePaneOpen(IntPtr winEl)
	{
		return EnsurePaneOpen(winEl, allowHamburger: true);
	}

	private static bool EnsurePaneOpen(IntPtr winEl, bool allowHamburger)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
		if (intPtr == IntPtr.Zero)
		{
			LastPaneProblem = "读不到 Apple Music 的侧栏，请确认它没有被最小化。";
			BridgeLog.WarnThrottled("am-pane-missing", "[AM] 找不到侧栏「播放列表」节点。", TimeSpan.FromSeconds(30.0));
			DumpTopLevel(winEl);
			return false;
		}
		try
		{
			if (CountChildListItems(intPtr) > 0)
			{
				LastPaneProblem = null;
				return true;
			}
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.InitialPlaylistRootExpandDelay);
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
				if (intPtr == IntPtr.Zero)
				{
					LastPaneProblem = "侧栏读不到";
					return false;
				}
				if (CountChildListItems(intPtr) > 0)
				{
					LastPaneProblem = null;
					return true;
				}
			}
			if (allowHamburger && TryOpenPaneOnce(winEl))
			{
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
				if (intPtr == IntPtr.Zero)
				{
					return false;
				}
				if (UiaNative.ExpandState(intPtr) != 1)
				{
					UiaNative.Expand(intPtr);
					Thread.Sleep(MusicBridgeOptions.Current.Apple.StandardPlaylistRootExpandDelay);
					UiaNative.Release(intPtr);
					intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
					if (intPtr == IntPtr.Zero)
					{
						return false;
					}
				}
				if (CountChildListItems(intPtr) > 0)
				{
					LastPaneProblem = null;
					return true;
				}
			}
			LastPaneProblem = "Apple Music 的侧栏收起了，暂时无法起播。把 Apple Music 窗口拉宽可以避免。";
			BridgeLog.WarnThrottled("am-pane-compact", "[AM] 侧栏是紧凑模式" + (allowHamburger ? "，且本次会话已用过一次汉堡键，不再重复撑开。" : "，本次调用不允许动汉堡键。"), TimeSpan.FromSeconds(30.0));
			return false;
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	private static int CountChildListItems(IntPtr node)
	{
		List<UiaNode> list = UiaNative.Children(node);
		try
		{
			int num = 0;
			foreach (UiaNode item in list)
			{
				if (item.ControlType == 50007)
				{
					num++;
				}
			}
			return num;
		}
		finally
		{
			UiaNative.ReleaseAll(list);
		}
	}

	public static List<AmPlaylist> FullScan(IntPtr winEl)
	{
		UiaNative.AllowForegroundForRefresh = true;
		try
		{
			return FullScanCore(winEl);
		}
		finally
		{
			UiaNative.AllowForegroundForRefresh = false;
			UiaNative.ReleaseForegroundShield();
		}
	}

	private static List<AmPlaylist> FullScanCore(IntPtr winEl)
	{
		_hamburgerUsedThisSession = false;
		List<AmPlaylist> list = FullScanOnce(winEl);
		int num = 0;
		while (list.Count == 0 && num < MusicBridgeOptions.Current.Apple.EmptyLibraryMaximumRetryCount)
		{
			BridgeLog.Info("[AM] 侧栏还没有歌单，可能仍在加载资料库，等 6 秒再试（第 " + (num + 1) + " 次）。");
			Thread.Sleep(MusicBridgeOptions.Current.Apple.EmptyLibraryRetryDelay);
			list = FullScanOnce(winEl);
			num++;
		}
		if (list.Count > 0)
		{
			string text = AppleMusicCache.Fingerprint(list);
			bool flag = false;
			for (int i = 0; i < MusicBridgeOptions.Current.Apple.StabilityVerificationCount; i++)
			{
				if (flag)
				{
					break;
				}
				Thread.Sleep(MusicBridgeOptions.Current.Apple.StabilityVerificationInitialDelay + TimeSpan.FromTicks(MusicBridgeOptions.Current.Apple.StabilityVerificationIncrementDelay.Ticks * i));
				List<AmPlaylist> list2 = FullScanOnce(winEl);
				if (list2.Count != 0)
				{
					string text2 = AppleMusicCache.Fingerprint(list2);
					if (text2 == text)
					{
						list = list2;
						flag = true;
						continue;
					}
					BridgeLog.Warn("[AM] 侧栏结构两次读取不一致（" + text + " -> " + text2 + "），等待稳定后再读一次。");
					list = list2;
					text = text2;
				}
			}
			if (!flag)
			{
				LastPaneProblem = "Apple Music 侧栏仍在变化，本次未同步。等列表稳定后再点一次「更新播放列表」。";
				BridgeLog.Warn("[AM] 连续三次侧栏结构未得到相同指纹，本次拒绝提交。 ");
				list.Clear();
			}
		}
		LastPaneProblem = ((list.Count == 0) ? "没有读到任何歌单。Apple Music 可能还在加载资料库，等它侧栏显示出播放列表后再点一次「更新播放列表」。" : null);
		BridgeLog.Info("[AM] 完整扫描完成，顶层 " + list.Count + " 项。");
		return list;
	}

	private static List<AmPlaylist> FullScanOnce(IntPtr winEl)
	{
		List<AmPlaylist> list = new List<AmPlaylist>();
		_structureScanFailed = false;
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
		if (intPtr == IntPtr.Zero)
		{
			LastPaneProblem = "读不到 Apple Music 的侧栏，请确认它没有被最小化。";
			return list;
		}
		try
		{
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.StandardPlaylistRootExpandDelay);
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
				if (intPtr == IntPtr.Zero)
				{
					return list;
				}
			}
			ScanInto(winEl, intPtr, list, 0);
			if (list.Count == 0 && TryOpenPaneOnce(winEl))
			{
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
				if (intPtr != IntPtr.Zero)
				{
					if (UiaNative.ExpandState(intPtr) != 1)
					{
						UiaNative.Expand(intPtr);
						Thread.Sleep(MusicBridgeOptions.Current.Apple.StandardPlaylistRootExpandDelay);
						UiaNative.Release(intPtr);
						intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
					}
					if (intPtr != IntPtr.Zero)
					{
						ScanInto(winEl, intPtr, list, 0);
					}
				}
			}
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		if (_structureScanFailed)
		{
			LastPaneProblem = "Apple Music 侧栏节点在扫描中消失，本次结果已作废。";
			list.Clear();
		}
		return list;
	}

	private static void ScanInto(IntPtr winEl, IntPtr node, List<AmPlaylist> into, int depth)
	{
		ScanInto(winEl, node, into, depth, null);
	}

	private static void ScanInto(IntPtr winEl, IntPtr node, List<AmPlaylist> into, int depth, string parentId)
	{
		if (depth > 6)
		{
			_structureScanFailed = true;
			return;
		}
		List<UiaNode> list = UiaNative.Children(node, 400);
		List<AmPlaylist> list2 = new List<AmPlaylist>();
		try
		{
			int num = 0;
			foreach (UiaNode item in list)
			{
				if (item.ControlType == 50007 && item.AutomationId.IndexOf("ePlaylist", StringComparison.Ordinal) >= 0)
				{
					AmPlaylist amPlaylist = new AmPlaylist
					{
						Name = item.Name,
						PersistentId = item.AutomationId,
						IsFolder = (UiaNative.ExpandState(item.Handle) != -1),
						Depth = depth,
						Order = num++,
						ParentId = parentId
					};
					into.Add(amPlaylist);
					if (amPlaylist.IsFolder)
					{
						list2.Add(amPlaylist);
					}
				}
			}
		}
		finally
		{
			UiaNative.ReleaseAll(list);
		}
		foreach (AmPlaylist item2 in list2)
		{
			IntPtr intPtr = UiaNative.FindByAutomationId(winEl, item2.PersistentId);
			if (intPtr == IntPtr.Zero)
			{
				_structureScanFailed = true;
				continue;
			}
			try
			{
				if (UiaNative.ExpandState(intPtr) == 1)
				{
					goto IL_017e;
				}
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.FolderExpandDelay);
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, item2.PersistentId);
				if (!(intPtr == IntPtr.Zero))
				{
					goto IL_017e;
				}
				_structureScanFailed = true;
				goto end_IL_012a;
				IL_017e:
				ScanInto(winEl, intPtr, item2.Children, depth + 1, item2.PersistentId);
				item2.ChildrenLoaded = true;
				end_IL_012a:;
			}
			finally
			{
				UiaNative.Release(intPtr);
			}
		}
	}

	private static void ExpandSidebarTree(IntPtr winEl)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
		if (intPtr == IntPtr.Zero)
		{
			return;
		}
		try
		{
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.StructureRescanRootExpandDelay);
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
				if (intPtr == IntPtr.Zero)
				{
					return;
				}
			}
			ExpandFoldersUnder(winEl, intPtr, 0);
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	private static void ExpandFoldersUnder(IntPtr winEl, IntPtr node, int depth)
	{
		if (depth > 6)
		{
			return;
		}
		List<string> list = new List<string>();
		List<UiaNode> list2 = UiaNative.Children(node, 400);
		try
		{
			foreach (UiaNode item in list2)
			{
				if (item.ControlType == 50007 && item.AutomationId.IndexOf("ePlaylist", StringComparison.Ordinal) >= 0 && UiaNative.ExpandState(item.Handle) != -1)
				{
					list.Add(item.AutomationId);
				}
			}
		}
		finally
		{
			UiaNative.ReleaseAll(list2);
		}
		foreach (string item2 in list)
		{
			IntPtr intPtr = UiaNative.FindByAutomationId(winEl, item2);
			if (intPtr == IntPtr.Zero)
			{
				continue;
			}
			try
			{
				if (UiaNative.ExpandState(intPtr) == 1)
				{
					goto IL_00fd;
				}
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.StructureRescanFolderExpandDelay);
				UiaNative.Release(intPtr);
				intPtr = UiaNative.FindByAutomationId(winEl, item2);
				if (!(intPtr == IntPtr.Zero))
				{
					goto IL_00fd;
				}
				goto end_IL_00b4;
				IL_00fd:
				ExpandFoldersUnder(winEl, intPtr, depth + 1);
				end_IL_00b4:;
			}
			finally
			{
				UiaNative.Release(intPtr);
			}
		}
	}

	public static void FlattenPlaylists(List<AmPlaylist> tree, List<AmPlaylist> into)
	{
		FlattenPlaylists(tree, into, new List<string>());
	}

	private static void FlattenPlaylists(List<AmPlaylist> tree, List<AmPlaylist> into, List<string> path)
	{
		if (tree == null)
		{
			return;
		}
		foreach (AmPlaylist item in tree)
		{
			if (item.IsFolder)
			{
				path.Add(item.PersistentId);
				FlattenPlaylists(item.Children, into, path);
				path.RemoveAt(path.Count - 1);
			}
			else
			{
				item.AncestorIds = ((path.Count > 0) ? new List<string>(path) : null);
				into.Add(item);
			}
		}
	}

	public static int AdoptCompleted(List<AmPlaylist> tree, List<AmPlaylist> previous)
	{
		if (tree == null || previous == null)
		{
			return 0;
		}
		List<AmPlaylist> list = new List<AmPlaylist>();
		FlattenPlaylists(tree, list);
		List<AmPlaylist> list2 = new List<AmPlaylist>();
		FlattenPlaylists(previous, list2);
		if (list.Count == 0 || list.Count != list2.Count)
		{
			return 0;
		}
		Dictionary<string, AmPlaylist> dictionary = new Dictionary<string, AmPlaylist>();
		foreach (AmPlaylist item in list2)
		{
			if (!string.IsNullOrEmpty(item.PersistentId))
			{
				dictionary[item.PersistentId] = item;
			}
		}
		foreach (AmPlaylist item2 in list)
		{
			if (string.IsNullOrEmpty(item2.PersistentId) || !dictionary.ContainsKey(item2.PersistentId))
			{
				return 0;
			}
		}
		int num = 0;
		foreach (AmPlaylist item3 in list)
		{
			AmPlaylist amPlaylist = dictionary[item3.PersistentId];
			if ((amPlaylist.TrackState == AmTrackState.Loaded || amPlaylist.TrackState == AmTrackState.Empty) && amPlaylist.DeclaredCount >= 0 && (amPlaylist.TrackState != AmTrackState.Loaded || (amPlaylist.DeclaredCount != 0 && amPlaylist.Tracks.Count == amPlaylist.DeclaredCount)) && (amPlaylist.TrackState != AmTrackState.Empty || (amPlaylist.DeclaredCount == 0 && amPlaylist.Tracks.Count == 0)))
			{
				item3.Tracks.Clear();
				item3.Tracks.AddRange(amPlaylist.Tracks);
				item3.DeclaredCount = amPlaylist.DeclaredCount;
				item3.Summary = amPlaylist.Summary;
				item3.TracksComplete = amPlaylist.TracksComplete;
				item3.TrackState = amPlaylist.TrackState;
				num++;
			}
		}
		return num;
	}

	private static IntPtr FindSidebarNode(IntPtr winEl, string aid)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, aid, 30, logMiss: false);
		if (intPtr != IntPtr.Zero)
		{
			return intPtr;
		}
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				IntPtr intPtr2 = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists", 30, logMiss: false);
				if (intPtr2 == IntPtr.Zero)
				{
					return IntPtr.Zero;
				}
				List<UiaNode> list = UiaNative.Children(intPtr2, 400);
				try
				{
					if (list.Count == 0)
					{
						return IntPtr.Zero;
					}
					UiaNative.ScrollIntoView((i == 0) ? list[list.Count - 1].Handle : list[0].Handle);
				}
				finally
				{
					UiaNative.ReleaseAll(list);
					UiaNative.Release(intPtr2);
				}
				Thread.Sleep(MusicBridgeOptions.Current.Apple.SidebarScrollStepDelay);
				intPtr = UiaNative.FindByAutomationId(winEl, aid, 30, logMiss: false);
				if (intPtr != IntPtr.Zero)
				{
					BridgeLog.Info("[AM] 侧栏滚动 " + (j + 1) + " 次后找到了节点（方向 " + ((i == 0) ? "下" : "上") + "）。");
					return intPtr;
				}
			}
		}
		BridgeLog.Info("[AM] 侧栏里怎么滚都找不到 aid=" + aid);
		return IntPtr.Zero;
	}

	private static void ExpandPathTo(IntPtr winEl, AmPlaylist pl)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
		if (intPtr == IntPtr.Zero)
		{
			return;
		}
		try
		{
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				UiaNative.Expand(intPtr);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.AncestorRootExpandDelay);
			}
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		if (pl.AncestorIds == null)
		{
			return;
		}
		foreach (string ancestorId in pl.AncestorIds)
		{
			IntPtr intPtr2 = FindSidebarNode(winEl, ancestorId);
			if (intPtr2 == IntPtr.Zero)
			{
				continue;
			}
			try
			{
				if (UiaNative.ExpandState(intPtr2) != 1)
				{
					UiaNative.Expand(intPtr2);
					Thread.Sleep(MusicBridgeOptions.Current.Apple.AncestorFolderExpandDelay);
				}
			}
			finally
			{
				UiaNative.Release(intPtr2);
			}
		}
	}

	public static void ScanAllTracks(IntPtr winEl, List<AmPlaylist> tree, Action<int, int, string> onProgress, Action onCheckpoint, Func<bool> shouldStop)
	{
		List<AmPlaylist> list = new List<AmPlaylist>();
		FlattenPlaylists(tree, list);
		BridgeLog.Info("[AM] 开始扫描曲目，共 " + list.Count + " 个歌单。");
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		for (int i = 0; i < list.Count; i++)
		{
			if (shouldStop != null && shouldStop())
			{
				BridgeLog.Info("[AM] 曲目扫描被中止。");
				break;
			}
			AmPlaylist amPlaylist = list[i];
			if (amPlaylist.TrackState == AmTrackState.Loaded)
			{
				num++;
				continue;
			}
			if (amPlaylist.TrackState == AmTrackState.Empty)
			{
				num2++;
				continue;
			}
			if ((i & 3) == 0 && FindWindow() == IntPtr.Zero)
			{
				BridgeLog.Warn("[AM] Apple Music 窗口消失（多半是它自己崩了），扫描在第 " + (i + 1) + " / " + list.Count + " 个歌单处停止，已扫部分已保留。");
				break;
			}
			onProgress?.Invoke(i, list.Count, amPlaylist.Name);
			try
			{
				if (amPlaylist.AncestorIds != null && amPlaylist.AncestorIds.Count > 0)
				{
					ExpandPathTo(winEl, amPlaylist);
				}
				OpenPlaylistAndReadTracks(winEl, amPlaylist, checkPane: false);
				for (int j = 0; j < 2; j++)
				{
					if (amPlaylist.TrackState != AmTrackState.Failed && amPlaylist.TrackState != AmTrackState.Incomplete)
					{
						break;
					}
					IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists", 30, logMiss: false);
					if (intPtr == IntPtr.Zero)
					{
						LastPaneProblem = "Apple Music 侧栏在同步中消失，已停止无效重试。";
						amPlaylist.TracksError = LastPaneProblem;
						break;
					}
					int num4;
					try
					{
						num4 = CountChildListItems(intPtr);
					}
					finally
					{
						UiaNative.Release(intPtr);
					}
					if (num4 == 0)
					{
						LastPaneProblem = "Apple Music 侧栏已进入紧凑模式，已停止无效重试。";
						amPlaylist.TracksError = LastPaneProblem;
						break;
					}
					ExpandPathTo(winEl, amPlaylist);
					OpenPlaylistAndReadTracks(winEl, amPlaylist, checkPane: false);
				}
				if (amPlaylist.TrackState == AmTrackState.Loaded)
				{
					num++;
				}
				else if (amPlaylist.TrackState == AmTrackState.Empty)
				{
					num2++;
				}
				else
				{
					num3++;
				}
			}
			catch (Exception ex)
			{
				amPlaylist.TrackState = AmTrackState.Failed;
				amPlaylist.TracksComplete = false;
				amPlaylist.TracksError = "扫描异常：" + ex.Message;
				num3++;
				BridgeLog.Warn("[AM] 扫描『" + BridgeLog.Redact(amPlaylist.Name) + "』抛异常：" + ex.Message);
			}
			if (onCheckpoint != null && (i + 1) % 8 == 0)
			{
				onCheckpoint();
			}
		}
		BridgeLog.Info("[AM] 曲目扫描结束：成功 " + num + "，确认为空 " + num2 + "，失败 " + num3 + "，共 " + list.Count + " 个歌单，耗时 " + (int)stopwatch.Elapsed.TotalSeconds + " 秒。");
	}

	public static List<AmPlaylist> ReadPlaylists(IntPtr winEl)
	{
		List<AmPlaylist> list = new List<AmPlaylist>();
		if (!EnsurePaneOpen(winEl))
		{
			BridgeLog.Warn("[AM] 导航面板未能展开，读不到歌单。");
			return list;
		}
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Playlists");
		if (intPtr == IntPtr.Zero)
		{
			return list;
		}
		try
		{
			List<UiaNode> list2 = UiaNative.Children(intPtr);
			try
			{
				foreach (UiaNode item in list2)
				{
					if (item.ControlType == 50007 && item.AutomationId.IndexOf("ePlaylist", StringComparison.Ordinal) >= 0)
					{
						int num = UiaNative.ExpandState(item.Handle);
						AmPlaylist amPlaylist = new AmPlaylist
						{
							Name = item.Name,
							PersistentId = item.AutomationId,
							IsFolder = (num != -1),
							Depth = 0
						};
						if (num == 1)
						{
							ReadChildItems(item.Handle, amPlaylist);
							amPlaylist.ChildrenLoaded = true;
							amPlaylist.Expanded = true;
						}
						list.Add(amPlaylist);
					}
				}
				return list;
			}
			finally
			{
				UiaNative.ReleaseAll(list2);
			}
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	public static bool ReadFolderChildren(IntPtr winEl, AmPlaylist folder)
	{
		folder.Children.Clear();
		if (!EnsurePaneOpen(winEl))
		{
			return false;
		}
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, folder.PersistentId);
		if (intPtr == IntPtr.Zero)
		{
			BridgeLog.Warn("[AM] 文件夹『" + BridgeLog.Redact(folder.Name) + "』已不在侧栏。");
			return false;
		}
		try
		{
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				folder.TracksError = "这个文件夹在 Apple Music 里是折叠的。请在 Apple Music 中展开它，再回来点「刷新」。";
				BridgeLog.Info("[AM] 文件夹『" + BridgeLog.Redact(folder.Name) + "』在 Apple Music 里是折叠的，按约定不自动展开。");
				return false;
			}
			ReadChildItems(intPtr, folder);
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		folder.ChildrenLoaded = true;
		BridgeLog.Info("[AM] 文件夹『" + BridgeLog.Redact(folder.Name) + "』下有 " + folder.Children.Count + " 项。");
		return true;
	}

	private static void ReadChildItems(IntPtr node, AmPlaylist parent)
	{
		List<UiaNode> list = UiaNative.Children(node, 400);
		try
		{
			foreach (UiaNode item in list)
			{
				if (item.ControlType == 50007 && item.AutomationId.IndexOf("ePlaylist", StringComparison.Ordinal) >= 0)
				{
					int num = UiaNative.ExpandState(item.Handle);
					AmPlaylist amPlaylist = new AmPlaylist
					{
						Name = item.Name,
						PersistentId = item.AutomationId,
						IsFolder = (num != -1),
						Depth = parent.Depth + 1
					};
					if (num == 1)
					{
						ReadChildItems(item.Handle, amPlaylist);
						amPlaylist.ChildrenLoaded = true;
					}
					parent.Children.Add(amPlaylist);
				}
			}
		}
		finally
		{
			UiaNative.ReleaseAll(list);
		}
	}

	public static bool OpenPlaylistAndReadTracks(IntPtr winEl, AmPlaylist pl)
	{
		return OpenPlaylistAndReadTracks(winEl, pl, checkPane: true);
	}

	public static bool OpenPlaylistAndReadTracks(IntPtr winEl, AmPlaylist pl, bool checkPane)
	{
		pl.ResetTracks();
		pl.TracksLoading = true;
		pl.TrackState = AmTrackState.Unknown;
		if (checkPane && !EnsurePaneOpen(winEl))
		{
			pl.TracksError = "侧栏未能展开";
			pl.TrackState = AmTrackState.Failed;
			pl.TracksLoading = false;
			return false;
		}
		IntPtr intPtr = FindSidebarNode(winEl, pl.PersistentId);
		if (intPtr == IntPtr.Zero)
		{
			pl.TracksError = "歌单已不在侧栏（可能已被删除或重命名）";
			pl.TrackState = AmTrackState.Failed;
			pl.TracksLoading = false;
			return false;
		}
		bool result;
		try
		{
			UiaNative.Realize(intPtr);
			UiaNative.ScrollIntoView(intPtr);
			Thread.Sleep(MusicBridgeOptions.Current.Apple.ItemRealizeDelay);
			bool flag = UiaNative.Select(intPtr);
			Thread.Sleep(MusicBridgeOptions.Current.Apple.SelectionConfirmationDelay);
			int num = UiaNative.IsSelected(intPtr);
			if (!flag || num != 1)
			{
				string[] obj = new string[8]
				{
					"[AM] 『",
					BridgeLog.Redact(pl.Name),
					"』选中异常：Select=",
					flag.ToString(),
					" IsSelected=",
					num.ToString(),
					" Offscreen=",
					null
				};
				result = UiaNative.IsOffscreen(intPtr);
				obj[7] = result.ToString();
				BridgeLog.Warn(string.Concat(obj));
			}
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		IntPtr intPtr2 = IntPtr.Zero;
		bool flag2 = false;
		bool flag3 = false;
		for (int i = 0; i < MusicBridgeOptions.Current.Apple.PageReadyMaximumPollCount; i++)
		{
			if (flag2)
			{
				break;
			}
			Thread.Sleep(MusicBridgeOptions.Current.Apple.PageReadyPollInterval);
			if (intPtr2 != IntPtr.Zero)
			{
				UiaNative.Release(intPtr2);
				intPtr2 = IntPtr.Zero;
			}
			intPtr2 = UiaNative.FindByClassName(winEl, "ListView");
			if (intPtr2 == IntPtr.Zero)
			{
				continue;
			}
			List<UiaNode> list = UiaNative.Children(intPtr2, 40);
			try
			{
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = false;
				foreach (UiaNode item in list)
				{
					if (item.ClassName == "ListViewItem")
					{
						flag5 = true;
					}
					else if (!string.IsNullOrEmpty(item.Name))
					{
						if (IsEmptyMarker(item.Name))
						{
							flag6 = true;
						}
						if (!flag4 && item.Name.IndexOf(pl.Name, StringComparison.Ordinal) >= 0)
						{
							flag4 = true;
							flag3 = true;
							pl.Summary = item.Name;
							pl.DeclaredCount = ParseDeclaredCount(item.Name);
						}
					}
				}
				if (flag4 && flag6 && pl.DeclaredCount < 0)
				{
					pl.DeclaredCount = 0;
				}
				if (flag4 && (pl.DeclaredCount == 0 || flag5))
				{
					flag2 = true;
				}
			}
			finally
			{
				UiaNative.ReleaseAll(list);
			}
		}
		if (!flag2)
		{
			if (intPtr2 != IntPtr.Zero)
			{
				List<UiaNode> list2 = UiaNative.Children(intPtr2, 10);
				try
				{
					StringBuilder stringBuilder = new StringBuilder();
					foreach (UiaNode item2 in list2)
					{
						if (item2.ClassName == "ListViewItem")
						{
							stringBuilder.Append("[行]");
							break;
						}
						stringBuilder.Append('「').Append(item2.Name).Append("」");
					}
					BridgeLog.Warn("[AM] 『" + BridgeLog.Redact(pl.Name) + "』" + (flag3 ? "页头认出来了但一直等不到内容" : "页面没切过来") + "。当前页头 = " + stringBuilder);
				}
				finally
				{
					UiaNative.ReleaseAll(list2);
				}
				UiaNative.Release(intPtr2);
			}
			else
			{
				BridgeLog.Warn("[AM] 『" + BridgeLog.Redact(pl.Name) + "』页面没切过来：内容区里根本没找到 ListView。");
			}
			pl.TracksLoading = false;
			pl.TrackState = AmTrackState.Failed;
			pl.TracksError = "内容区没有切到这个歌单";
			_lastOpenedPlaylistId = null;
			return false;
		}
		_lastOpenedPlaylistId = pl.PersistentId;
		try
		{
			string text = EnumerateTracks(intPtr2, pl);
			if (text != null)
			{
				pl.TracksLoading = false;
				pl.TracksComplete = false;
				pl.TrackState = AmTrackState.Incomplete;
				pl.TracksError = text;
				BridgeLog.Warn("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』枚举未完成：" + text);
				result = false;
				goto IL_08d5;
			}
			if (pl.DeclaredCount > 0 && pl.Tracks.Count < pl.DeclaredCount)
			{
				BridgeLog.Info("[AM] 『" + BridgeLog.Redact(pl.Name) + "』首轮读到 " + pl.Tracks.Count + " / " + pl.DeclaredCount + " 首，重试一次。");
				Thread.Sleep(MusicBridgeOptions.Current.Apple.EnumerationRetryDelay);
				pl.Tracks.Clear();
				text = EnumerateTracks(intPtr2, pl);
				if (text != null)
				{
					pl.TracksLoading = false;
					pl.TracksComplete = false;
					pl.TrackState = AmTrackState.Incomplete;
					pl.TracksError = text;
					result = false;
					goto IL_08d5;
				}
			}
			if (pl.DeclaredCount < 0 && pl.Tracks.Count > 0)
			{
				int count = pl.Tracks.Count;
				List<AmTrack> list3 = new List<AmTrack>(pl.Tracks);
				Thread.Sleep(MusicBridgeOptions.Current.Apple.EnumerationRetryDelay);
				pl.Tracks.Clear();
				text = EnumerateTracks(intPtr2, pl);
				if (text != null)
				{
					pl.TracksLoading = false;
					pl.TracksComplete = false;
					pl.TrackState = AmTrackState.Incomplete;
					pl.TracksError = text;
					result = false;
					goto IL_08d5;
				}
				if (pl.Tracks.Count != count)
				{
					pl.TracksLoading = false;
					pl.TracksComplete = false;
					pl.TrackState = AmTrackState.Incomplete;
					pl.TracksError = "两次枚举数量不一致（" + count + " / " + pl.Tracks.Count + "），未读全";
					BridgeLog.Warn("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』没有页头数量，且两次枚举不一致（" + count + " -> " + pl.Tracks.Count + "），判为未读全。");
					result = false;
					goto IL_08d5;
				}
				pl.DeclaredCount = pl.Tracks.Count;
				BridgeLog.Info("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』页头无数量，两次枚举一致（" + count + " 首），按实测数量认定完整。");
				list3.Clear();
			}
		}
		finally
		{
			UiaNative.Release(intPtr2);
		}
		pl.TracksLoading = false;
		if (pl.DeclaredCount < 0)
		{
			pl.TracksComplete = false;
			pl.TrackState = AmTrackState.Incomplete;
			pl.TracksError = "没有读到歌单声明数量，也没有读到任何曲目";
			BridgeLog.Warn("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』页头无数量且曲目为空，禁止标记完整。");
			return false;
		}
		if (pl.Tracks.Count > 0)
		{
			if (pl.DeclaredCount > 0 && pl.Tracks.Count != pl.DeclaredCount)
			{
				pl.TracksComplete = false;
				pl.TrackState = AmTrackState.Incomplete;
				pl.TracksError = "读到 " + pl.Tracks.Count + " / " + pl.DeclaredCount + " 首，不完整";
				BridgeLog.Warn("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』不完整：声明 " + pl.DeclaredCount + " 首，只读到 " + pl.Tracks.Count + " 首。");
				return false;
			}
			pl.TracksComplete = true;
			pl.TrackState = AmTrackState.Loaded;
			BridgeLog.Info("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』读到 " + pl.Tracks.Count + " 首。");
		}
		else
		{
			if (pl.DeclaredCount != 0)
			{
				pl.TrackState = AmTrackState.Failed;
				pl.TracksError = "曲目读取失败";
				BridgeLog.Warn("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』一首都没读到，标记为读取失败（不当作空歌单）。");
				return false;
			}
			pl.TracksComplete = true;
			pl.TrackState = AmTrackState.Empty;
			BridgeLog.Info("[AM] 歌单『" + BridgeLog.Redact(pl.Name) + "』确实是空的。");
		}
		return true;
		IL_08d5:
		return result;
	}

	private static string EnumerateTracks(IntPtr list, AmPlaylist pl)
	{
		if (!UiaNative.SupportsItemContainer(list))
		{
			BridgeLog.Warn("[AM] 该版本不支持 ItemContainer，退回可见行遍历，长歌单可能读不全。");
			if (pl.DeclaredCount > 100)
			{
				return "当前 Apple Music 版本不提供 ItemContainer，超过 100 首的歌单不能安全完整读取";
			}
			List<UiaNode> list2 = UiaNative.Children(list, 600);
			try
			{
				int num = 0;
				foreach (UiaNode item in list2)
				{
					if (!(item.ClassName != "ListViewItem"))
					{
						AmTrack amTrack = ParseTrackRow(item.Handle);
						if (amTrack != null)
						{
							amTrack.RowIndex = num++;
							pl.Tracks.Add(amTrack);
						}
					}
				}
			}
			finally
			{
				UiaNative.ReleaseAll(list2);
			}
			return null;
		}
		IntPtr intPtr = IntPtr.Zero;
		int num2 = 0;
		int itemContainerMaximumItems = MusicBridgeOptions.Current.Apple.ItemContainerMaximumItems;
		try
		{
			while (num2 < itemContainerMaximumItems)
			{
				IntPtr intPtr2 = UiaNative.NextItem(list, intPtr);
				if (intPtr != IntPtr.Zero)
				{
					UiaNative.Release(intPtr);
				}
				if (intPtr2 == IntPtr.Zero)
				{
					intPtr = IntPtr.Zero;
					return null;
				}
				intPtr = intPtr2;
				UiaNative.Realize(intPtr2);
				UiaNode uiaNode = UiaNative.Snapshot(intPtr2);
				int num3 = num2++;
				if (uiaNode != null && !(uiaNode.ClassName != "ListViewItem"))
				{
					AmTrack amTrack2 = ParseTrackRow(intPtr2);
					if (amTrack2 == null)
					{
						UiaNative.ScrollIntoView(intPtr2);
						Thread.Sleep(MusicBridgeOptions.Current.Apple.TrackParseAfterScrollDelay);
						amTrack2 = ParseTrackRow(intPtr2);
					}
					if (amTrack2 != null)
					{
						amTrack2.RowIndex = num3;
						pl.Tracks.Add(amTrack2);
						continue;
					}
					BridgeLog.Warn("[AM] 『" + BridgeLog.Redact(pl.Name) + "』第 " + (num3 + 1) + " 行实体化后仍读不出内容。");
				}
			}
			return "ItemContainer 枚举达到 " + itemContainerMaximumItems + " 项安全上限，结果明确标记为未完成";
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				UiaNative.Release(intPtr);
			}
		}
	}

	private static int ParseDeclaredCount(string summary)
	{
		if (string.IsNullOrEmpty(summary))
		{
			return -1;
		}
		if (IsEmptyMarker(summary))
		{
			return 0;
		}
		for (int i = 0; i < summary.Length; i++)
		{
			if (char.IsDigit(summary[i]))
			{
				int j;
				for (j = i; j < summary.Length && char.IsDigit(summary[j]); j++)
				{
				}
				string text = summary.Substring(j).TrimStart();
				if ((text.StartsWith("项") || text.StartsWith("item", StringComparison.OrdinalIgnoreCase) || text.StartsWith("song", StringComparison.OrdinalIgnoreCase)) && int.TryParse(summary.Substring(i, j - i), out var result))
				{
					return result;
				}
				i = j;
			}
		}
		return -1;
	}

	private static bool IsEmptyMarker(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		if (s.IndexOf("没有项目", StringComparison.Ordinal) < 0 && s.IndexOf("空播放列表", StringComparison.Ordinal) < 0 && s.IndexOf("No items", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return s.IndexOf("Empty playlist", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static AmTrack ParseTrackRow(IntPtr row)
	{
		List<UiaNode> list = UiaNative.Children(row, 24);
		try
		{
			List<string> list2 = new List<string>();
			foreach (UiaNode item in list)
			{
				if (item.ControlType == 50020 && !string.IsNullOrWhiteSpace(item.Name))
				{
					list2.Add(item.Name.Trim());
				}
			}
			if (list2.Count == 0)
			{
				return null;
			}
			AmTrack amTrack = new AmTrack();
			amTrack.Name = list2[0];
			int num = -1;
			for (int num2 = list2.Count - 1; num2 >= 1; num2--)
			{
				if (DurationText.LooksLikeDuration(list2[num2]))
				{
					amTrack.DurationText = list2[num2];
					num = num2;
					break;
				}
			}
			int num3 = ((num > 0) ? num : list2.Count);
			List<string> list3 = new List<string>();
			for (int i = 1; i < num3; i++)
			{
				if (list3.Count <= 0 || !string.Equals(list3[list3.Count - 1], list2[i], StringComparison.Ordinal))
				{
					list3.Add(list2[i]);
				}
			}
			if (list3.Count > 0)
			{
				amTrack.Artists = list3[0];
			}
			if (list3.Count > 1)
			{
				amTrack.Album = list3[list3.Count - 1];
			}
			return amTrack;
		}
		finally
		{
			UiaNative.ReleaseAll(list);
		}
	}

	public static bool SelectTrack(IntPtr winEl, AmPlaylist pl, int rowIndex)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, pl.PersistentId);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			UiaNative.Realize(intPtr);
			UiaNative.ScrollIntoView(intPtr);
			Thread.Sleep(MusicBridgeOptions.Current.Apple.ItemRealizeDelay);
			UiaNative.Select(intPtr);
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
		Thread.Sleep(MusicBridgeOptions.Current.Apple.PlaylistNavigationSettleDelay);
		IntPtr intPtr2 = UiaNative.FindByClassName(winEl, "ListView");
		if (intPtr2 == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			List<UiaNode> list = UiaNative.Children(intPtr2, 600);
			try
			{
				int num = 0;
				foreach (UiaNode item in list)
				{
					if (!(item.ClassName != "ListViewItem") && num++ == rowIndex)
					{
						bool result = UiaNative.Select(item.Handle);
						BridgeLog.Info("[AM] 在 Apple Music 中选中第 " + (rowIndex + 1) + " 首 -> " + result);
						return result;
					}
				}
			}
			finally
			{
				UiaNative.ReleaseAll(list);
			}
		}
		finally
		{
			UiaNative.Release(intPtr2);
		}
		return false;
	}

	public static bool PlayTrack(IntPtr winEl, IntPtr mainHwnd, AmPlaylist pl, AmTrack target)
	{
		if (target == null)
		{
			return false;
		}
		LastInteractionOpenedPane = false;
		int rowIndex = target.RowIndex;
		string name = target.Name;
		IntPtr originalWindow = UiaNative.CaptureForegroundWindow();
		try
		{
			IntPtr intPtr = FindInputSite(mainHwnd);
			if (intPtr == IntPtr.Zero)
			{
				BridgeLog.Warn("[AM] 找不到 InputSiteWindowClass，无法投递按键。");
				return false;
			}
			if (_lastOpenedPlaylistId != pl.PersistentId)
			{
				IntPtr intPtr2 = FindSidebarNode(winEl, pl.PersistentId);
				if (intPtr2 == IntPtr.Zero && pl.AncestorIds != null && pl.AncestorIds.Count > 0)
				{
					ExpandPathTo(winEl, pl);
					intPtr2 = FindSidebarNode(winEl, pl.PersistentId);
				}
				if (intPtr2 == IntPtr.Zero && EnsurePaneOpen(winEl, allowHamburger: true))
				{
					if (pl.AncestorIds != null && pl.AncestorIds.Count > 0)
					{
						ExpandPathTo(winEl, pl);
					}
					intPtr2 = FindSidebarNode(winEl, pl.PersistentId);
				}
				if (intPtr2 == IntPtr.Zero)
				{
					BridgeLog.Warn("[AM] 侧栏里找不到这个歌单，无法起播：" + BridgeLog.Redact(pl.Name));
					return false;
				}
				try
				{
					UiaNative.Select(intPtr2);
				}
				finally
				{
					UiaNative.Release(intPtr2);
				}
				Thread.Sleep(MusicBridgeOptions.Current.Apple.PointPlayNavigationSettleDelay);
				_lastOpenedPlaylistId = pl.PersistentId;
			}
			IntPtr intPtr3 = UiaNative.FindByClassName(winEl, "ListView");
			if (intPtr3 == IntPtr.Zero)
			{
				BridgeLog.Warn("[AM] 内容区没有曲目列表。");
				return false;
			}
			bool flag;
			try
			{
				flag = SelectTrackRow(intPtr3, pl, target);
			}
			finally
			{
				UiaNative.Release(intPtr3);
			}
			if (!flag)
			{
				BridgeLog.Warn("[AM] 未能选中第 " + (rowIndex + 1) + " 行。");
				return false;
			}
			Thread.Sleep(MusicBridgeOptions.Current.Apple.SelectedRowSettleDelay);
			SmtcNative.Stop();
			Thread.Sleep(MusicBridgeOptions.Current.Apple.QueueStopSettleDelay);
			PostCtrlSpace(intPtr);
			BridgeLog.Info("[AM] 已发起播放：" + BridgeLog.Redact(name));
			return true;
		}
		finally
		{
			UiaNative.RestoreForegroundIfTargetOwnsIt(originalWindow, mainHwnd, "点歌流程");
		}
	}

	private static bool SelectTrackRow(IntPtr list, AmPlaylist playlist, AmTrack target)
	{
		int rowIndex = target.RowIndex;
		if (UiaNative.SupportsItemContainer(list))
		{
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				for (int i = 0; i <= rowIndex; i++)
				{
					IntPtr intPtr2 = UiaNative.NextItem(list, intPtr);
					if (intPtr != IntPtr.Zero)
					{
						UiaNative.Release(intPtr);
					}
					intPtr = intPtr2;
					if (intPtr2 == IntPtr.Zero)
					{
						break;
					}
					if (i == rowIndex)
					{
						UiaNative.Realize(intPtr2);
						UiaNative.ScrollIntoView(intPtr2);
						Thread.Sleep(MusicBridgeOptions.Current.Apple.ItemRealizeDelay);
						AmTrack amTrack = ParseTrackRow(intPtr2);
						if (!TrackIdentityMatches(amTrack, target))
						{
							BridgeLog.Warn("[AM] 第 " + (rowIndex + 1) + " 行元数据与缓存不一致，拒绝选错歌。期望『" + BridgeLog.Redact(target.Name) + "』，实际『" + ((amTrack == null) ? "(读不到)" : BridgeLog.Redact(amTrack.Name)) + "』。");
							return false;
						}
						if (UiaNative.Select(intPtr2))
						{
							return true;
						}
					}
				}
			}
			finally
			{
				if (intPtr != IntPtr.Zero)
				{
					UiaNative.Release(intPtr);
				}
			}
		}
		if (!HasDuplicateIdentity(playlist, target) && !string.IsNullOrEmpty(target.Name))
		{
			IntPtr intPtr3 = UiaNative.FindFirstByName(list, target.Name);
			if (intPtr3 != IntPtr.Zero)
			{
				try
				{
					UiaNative.Realize(intPtr3);
					UiaNative.ScrollIntoView(intPtr3);
					Thread.Sleep(MusicBridgeOptions.Current.Apple.ItemRealizeDelay);
					if (TrackIdentityMatches(ParseTrackRow(intPtr3), target) && UiaNative.Select(intPtr3))
					{
						return true;
					}
				}
				finally
				{
					UiaNative.Release(intPtr3);
				}
			}
		}
		return false;
	}

	private static bool HasDuplicateIdentity(AmPlaylist playlist, AmTrack target)
	{
		if (playlist == null)
		{
			return false;
		}
		int num = 0;
		foreach (AmTrack track in playlist.Tracks)
		{
			if (TrackIdentityMatches(track, target) && ++num > 1)
			{
				return true;
			}
		}
		return false;
	}

	internal static bool TrackIdentityMatches(AmTrack actual, AmTrack expected)
	{
		if (actual == null || expected == null)
		{
			return false;
		}
		if (!MetadataEquals(actual.Name, expected.Name))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(expected.Artists) && !MetadataEquals(actual.Artists, expected.Artists))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(expected.Album) && !MetadataEquals(actual.Album, expected.Album))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(expected.DurationText) && !MetadataEquals(actual.DurationText, expected.DurationText))
		{
			return false;
		}
		return true;
	}

	internal static bool MetadataEquals(string a, string b)
	{
		return TextMatch.Equals(a, b, MatchStrength.Exact);
	}

	public static int ToggleShuffle(IntPtr winEl)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "ShuffleButton");
		if (intPtr == IntPtr.Zero)
		{
			BridgeLog.Warn("[AM] 找不到随机播放键。");
			return -1;
		}
		try
		{
			if (!UiaNative.Toggle(intPtr))
			{
				BridgeLog.Warn("[AM] 随机播放切换失败。");
				return -1;
			}
			Thread.Sleep(MusicBridgeOptions.Current.Apple.ToggleStateSettleDelay);
			int num = UiaNative.ToggleState(intPtr);
			BridgeLog.Info("[AM] 随机播放 -> " + num switch
			{
				0 => "关", 
				1 => "开", 
				_ => "未知", 
			});
			return num;
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	public static int ReadShuffleState(IntPtr winEl)
	{
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "ShuffleButton");
		if (intPtr == IntPtr.Zero)
		{
			return -1;
		}
		try
		{
			return UiaNative.ToggleState(intPtr);
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}

	public static List<AmPlaylist> ReadLibrarySections(IntPtr winEl)
	{
		List<AmPlaylist> list = new List<AmPlaylist>();
		if (!EnsurePaneOpen(winEl))
		{
			BridgeLog.Warn("[AM] 导航面板未展开，读不到资料库入口。");
			return list;
		}
		IntPtr intPtr = UiaNative.FindByAutomationId(winEl, "Sidebar_Header_Library");
		if (intPtr == IntPtr.Zero)
		{
			return list;
		}
		try
		{
			if (UiaNative.ExpandState(intPtr) != 1)
			{
				BridgeLog.Info("[AM] 资料库在 Apple Music 里是折叠的，按约定不自动展开。");
				return list;
			}
			List<UiaNode> list2 = UiaNative.Children(intPtr);
			try
			{
				foreach (UiaNode item in list2)
				{
					if (item.ControlType == 50007)
					{
						list.Add(new AmPlaylist
						{
							Name = item.Name,
							PersistentId = item.AutomationId
						});
					}
				}
				return list;
			}
			finally
			{
				UiaNative.ReleaseAll(list2);
			}
		}
		finally
		{
			UiaNative.Release(intPtr);
		}
	}
}
