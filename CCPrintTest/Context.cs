using System.Diagnostics;
using System.Reflection;
using CCPrint;

namespace CCPrint.EFCore
{
  /// <summary>
  /// 
  /// </summary>
  public class AppEFCoreDbContext
  {
    public static void DoWork(ref CCPrint.Print pint)
    {

    }
  }

  /// <summary>
  /// For .NET 6 and above EF Core require class
  /// </summary>
  public class AppEFCoreDbContextFactor
  {
    public AppEFCoreDbContextFactor(ref CCPrint.Print pint)
    {
      pint.Message(new StackFrame(), Assembly.GetExecutingAssembly(), CCPrint.LogPrintType.Information, $"Env Target Variable");
    } 
  }


}
