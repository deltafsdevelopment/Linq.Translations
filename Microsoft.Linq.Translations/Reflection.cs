using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Microsoft.Linq.Translations
{

  /// <summary>
  /// Reflection static helpers to reduce noise in other methods.
  /// </summary>
  [DebuggerStepThrough]
  public static class Reflection
  {
    /// <summary>
    /// Try to find the unique public method on the type
    /// </summary>
    /// <param name="type">Type to search</param>
    /// <param name="methodName">name of the method to find</param>
    /// <returns>The found method or null</returns>
    public static MethodInfo SafeGetPublicMethod(this Type type, String methodName)
    {
      try
      {
        return type.GetMethod(methodName, Type.EmptyTypes);
      }
      catch (Exception ex)
      {
        Debug.WriteLine("Cannot find unique public method {0} on type {1}, error occured: {2}", methodName, type.FullName, ex.Message);
      }
      return null;
    }
  }
}
