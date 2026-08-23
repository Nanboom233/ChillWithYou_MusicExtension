using System;

namespace MusicBridge;

internal sealed class UiaNode
{
	public string Name = "";

	public string AutomationId = "";

	public string ClassName = "";

	public int ControlType;

	public IntPtr Handle;

	public override string ToString()
	{
		return "[" + ControlType + "] '" + Name + "' aid=" + AutomationId + " cls=" + ClassName;
	}
}
