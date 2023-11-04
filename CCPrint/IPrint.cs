using System;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;

namespace CCPrint;

public interface IPrint
{
    public void Message(StackFrame Frame, Assembly Ably, int CodeLine, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage, object DataObject);

    public void Message(StackFrame Frame, Assembly Ably, int CodeLine, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage);

    public void Message(StackFrame Frame, Assembly Ably, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage);

    public void Message(StackFrame Frame, string folder, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage);

    public void Message(StackFrame Frame, Assembly Ably, LogPrintType LogType, string ConsoleMessage);

    public void Message(StackFrame Frame, Assembly Ably, LogPrintType LogType, string ConsoleMessage, object DataObject);

    public void Flush(string EndOfStageName);
}