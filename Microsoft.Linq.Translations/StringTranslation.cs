using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Linq.Translations
{
  internal static class StringTranslation
  {
    /// <summary>
    ///  Concatenate a list of string values for the specified entity
    /// </summary>
    /// <remarks>Returns a lambda that can be consumed as part of a LINQ-to-Entities query</remarks>
    internal static Expression<System.Func<T, String>> BuildExpressionForJoin<T, TResult>(String strDelimiter,
                                                                                          IList<Expression> arguments)
    {
      String strNullReplace = String.Empty;
      MethodInfo miConcat = null;
      MethodCallExpression exprCall = null;
      // Creating a parameter expression.
      ParameterExpression value = Expression.Parameter(typeof(T), "value");

      if ((strDelimiter == null))
      {
        strDelimiter = string.Empty;
      }

      miConcat = typeof(string).GetMethod("Concat", new Type[] { typeof(String), typeof(String), typeof(String) });
      exprCall = (MethodCallExpression)ConcatRecurse(arguments, 0, miConcat, value, strDelimiter);
      return Expression.Lambda<Func<T, String>>(exprCall, value); 
    }

    private static Expression ConcatRecurse(IList<Expression> arguments,
                                            Int32 iIndex,
                                            MethodInfo mi,
                                            ParameterExpression value,
                                            String strDelimiter)
    {
      Expression exprMember = null;
      if (iIndex < arguments.Count)
      {
        exprMember = arguments[iIndex];
        if (iIndex == arguments.Count - 1)
        {
          strDelimiter = String.Empty;
        }
        return Expression.Call(mi,
                               TransformMemberExpressionForValue(exprMember, value),
                               Expression.Constant(strDelimiter),
                               ConcatRecurse(arguments, iIndex + 1, mi, value, strDelimiter));
      }
      return Expression.Constant(String.Empty);
    }

    /// <summary>
    ///  If the passed in expression is a member expression return the underlying prop info object
    /// </summary>
    private static Expression TransformMemberExpressionForValue(Expression expr, Expression value)
    {
      if (expr != null && expr.NodeType == ExpressionType.MemberAccess)
      {
        MemberExpression exprMember = (MemberExpression)expr;
        PropertyInfo prop = exprMember.Member as PropertyInfo;
        if (prop != null)
        {
          return Expression.Property(value, prop);
        }
      }
      return expr;
    }
  }
}
