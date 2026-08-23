using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicBridge;

internal static class SessionStore
{
	public enum LoadResult
	{
		NotFound,
		Ok,
		Corrupted,
		Obsolete
	}

	private struct DataBlob
	{
		public int cbData;

		public IntPtr pbData;
	}

	private const string FileName = "netease_session.dat";

	private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MBNE");

	private const byte FormatVersion = 2;

	private const byte LegacyFormatVersion = 1;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MusicBridge/NetEase/v2");

	private const int CryptprotectUiForbidden = 1;

	public static string SessionFilePath => BridgePaths.Resolve("config", "netease_session.dat");

	public static bool Exists()
	{
		try
		{
			return File.Exists(SessionFilePath);
		}
		catch
		{
			return false;
		}
	}

	public static LoadResult TryLoad(out string json)
	{
		json = null;
		string sessionFilePath;
		try
		{
			sessionFilePath = SessionFilePath;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("会话路径解析失败：" + ex.Message);
			return LoadResult.Corrupted;
		}
		if (!File.Exists(sessionFilePath))
		{
			return LoadResult.NotFound;
		}
		try
		{
			if (new FileInfo(sessionFilePath).Length > MusicBridgeOptions.Current.Netease.SessionMaximumFileBytes)
			{
				BridgeLog.Warn("会话文件超过安全大小上限，拒绝读取。");
				return LoadResult.Corrupted;
			}
			byte[] array = File.ReadAllBytes(sessionFilePath);
			if (array.Length < 5 || array[0] != Magic[0] || array[1] != Magic[1] || array[2] != Magic[2] || array[3] != Magic[3] || (array[4] != 2 && array[4] != 1))
			{
				BridgeLog.Warn("会话文件头无法识别，视为已损坏。");
				return LoadResult.Corrupted;
			}
			if (array[4] == 1)
			{
				BridgeLog.Info("检测到旧格式会话文件，本版本已不再支持其解密，请重新扫码登录。");
				return LoadResult.Obsolete;
			}
			byte[] array2 = new byte[array.Length - 5];
			Buffer.BlockCopy(array, 5, array2, 0, array2.Length);
			byte[] array3 = Unprotect(array2, Entropy);
			if (array3 == null)
			{
				BridgeLog.Warn("会话文件 DPAPI 解密失败。");
				return LoadResult.Corrupted;
			}
			json = Encoding.UTF8.GetString(array3);
			Array.Clear(array3, 0, array3.Length);
			return LoadResult.Ok;
		}
		catch (Exception ex2)
		{
			BridgeLog.Warn("读取会话文件失败：" + ex2.GetType().Name);
			return LoadResult.Corrupted;
		}
	}

	public static bool Save(string json)
	{
		try
		{
			string sessionFilePath = SessionFilePath;
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			byte[] array = Protect(bytes);
			Array.Clear(bytes, 0, bytes.Length);
			if (array == null)
			{
				BridgeLog.Error("会话 DPAPI 加密失败，未写入任何文件。");
				return false;
			}
			byte[] array2 = new byte[5 + array.Length];
			Buffer.BlockCopy(Magic, 0, array2, 0, 4);
			array2[4] = 2;
			Buffer.BlockCopy(array, 0, array2, 5, array.Length);
			AtomicFile.WriteAllBytes(sessionFilePath, array2);
			BridgeLog.Info("会话已加密保存（DPAPI CurrentUser，" + array2.Length + " 字节）。");
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("保存会话失败：" + ex.GetType().Name + " " + ex.Message);
			return false;
		}
	}

	public static bool Delete()
	{
		try
		{
			string fullPath = Path.GetFullPath(SessionFilePath);
			string fullPath2 = Path.GetFullPath(Path.Combine(BridgePaths.Root, "config", "netease_session.dat"));
			if (!string.Equals(fullPath, fullPath2, StringComparison.OrdinalIgnoreCase))
			{
				BridgeLog.Error("拒绝删除：解析出的路径与预期不符。");
				return false;
			}
			if (!File.Exists(fullPath))
			{
				BridgeLog.Info("会话文件本就不存在，无需删除。");
				return true;
			}
			File.Delete(fullPath);
			BridgeLog.Info("会话文件已删除：" + fullPath);
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("删除会话文件失败：" + ex.GetType().Name);
			return false;
		}
	}

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptProtectData(ref DataBlob pDataIn, string szDataDescr, ref DataBlob pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr ppszDataDescr, ref DataBlob pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr hMem);

	private static byte[] Protect(byte[] plain)
	{
		return Transform(plain, protect: true, Entropy);
	}

	private static byte[] Unprotect(byte[] blob, byte[] entropy)
	{
		return Transform(blob, protect: false, entropy);
	}

	private static byte[] Transform(byte[] input, bool protect, byte[] entropy)
	{
		DataBlob pDataIn = default(DataBlob);
		DataBlob pOptionalEntropy = default(DataBlob);
		DataBlob pDataOut = default(DataBlob);
		try
		{
			pDataIn.cbData = input.Length;
			pDataIn.pbData = Marshal.AllocHGlobal(input.Length);
			Marshal.Copy(input, 0, pDataIn.pbData, input.Length);
			pOptionalEntropy.cbData = entropy.Length;
			pOptionalEntropy.pbData = Marshal.AllocHGlobal(entropy.Length);
			Marshal.Copy(entropy, 0, pOptionalEntropy.pbData, entropy.Length);
			if (!(protect ? CryptProtectData(ref pDataIn, "MusicBridge NetEase session", ref pOptionalEntropy, IntPtr.Zero, IntPtr.Zero, 1, out pDataOut) : CryptUnprotectData(ref pDataIn, IntPtr.Zero, ref pOptionalEntropy, IntPtr.Zero, IntPtr.Zero, 1, out pDataOut)))
			{
				return null;
			}
			byte[] array = new byte[pDataOut.cbData];
			Marshal.Copy(pDataOut.pbData, array, 0, pDataOut.cbData);
			return array;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("DPAPI 调用异常：" + ex.GetType().Name);
			return null;
		}
		finally
		{
			if (pDataIn.pbData != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(pDataIn.pbData);
			}
			if (pOptionalEntropy.pbData != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(pOptionalEntropy.pbData);
			}
			if (pDataOut.pbData != IntPtr.Zero)
			{
				LocalFree(pDataOut.pbData);
			}
		}
	}
}
