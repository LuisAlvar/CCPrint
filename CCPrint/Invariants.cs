namespace CCPrint;

internal class Invariants
{
  public string BaseLogFileName { get; set; } = "LogFile_";
  public string BaseLogFileExtension { get; set; } = ".txt";
  public char LogDelimited { get;  set; } = '|';
  public string ProgramFileType { get; set; } = "cs";
  public string LogFlushDir { get; set; } = "";
  public bool DebugMode { get; set; } = false;
}