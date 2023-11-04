using System.Diagnostics;

namespace CCPrint.UnitTest.Models;

public class Test
{
  public static void ToWork(ref CCPrint.Print pint)
  {
    pint.Message(new StackFrame(), System.Reflection.Assembly.GetExecutingAssembly(), CCPrint.LogTimeType.Local, CCPrint.LogPrintType.Information, "within a different class and method");

  }
}

public class SubTest
{
    public static void ToWork(ref CCPrint.Print pint)
  {
    pint.Message(new StackFrame(), System.Reflection.Assembly.GetExecutingAssembly(), CCPrint.LogTimeType.Local, CCPrint.LogPrintType.Information, "Within the SubTest within file");

  }
}