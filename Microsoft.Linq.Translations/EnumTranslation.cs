using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Microsoft.Linq.Translations
{
  public static class EnumTranslation
  {
    // cache the expressions to avoid having to generate them more than once
    private static ConcurrentDictionary<Type, CompiledExpression> enumExpressions;
  
    static EnumTranslation()
    {
      enumExpressions = new ConcurrentDictionary<Type, CompiledExpression>();
    }

    public static Expression BuildExpression(Type theEnumType, Func<Object, String> formatter = null)
    {
      if (theEnumType != null && theEnumType.IsEnum)
      {
          var values = Enum.GetValues(theEnumType);
          var operand = Expression.Parameter(theEnumType, "o");
          Expression condition = Expression.Constant("Invalid enum value", typeof(String));
          foreach (var value in values)
          {
            var anEnumValue = value;
            String anEnumStringValue;
            if (formatter == null)
            {
              anEnumStringValue = anEnumValue.ToString();
            }
            else
            {
              anEnumStringValue = formatter(anEnumValue); 
            }
            var constant = Expression.Constant(anEnumValue, theEnumType);
            var test = Expression.Equal(operand, constant);
            var ifTrue = Expression.Constant(anEnumStringValue, typeof(String));
            condition = Expression.Condition(test, ifTrue, condition);
          }
          return Expression.Lambda(condition, operand);
      }
      return null;
    }

    /// <summary>
    /// Try to construct an Enum expression for the given enum type
    /// </summary>
    /// <param name="enumType">Enum type to create the expression for</param>
    /// <param name="formatter">Optional delegate function that formats the enum to a string</param>
    /// <returns>The constructed expression or null</returns>
    public static CompiledExpression SafeGetEnumCompiledExpression(Type enumType, Delegate formatter = null)
    {
      try
      {
        if (!enumExpressions.ContainsKey(enumType))
        {        
          Type genericClass = typeof(CompiledExpression<,>);
          Type constructedClass = genericClass.MakeGenericType(enumType, typeof(String));
          Func<Object, String> func = null;
          if (formatter != null)
          {
            func = (Object o) => (String)formatter.DynamicInvoke(o);
          }
          Expression enumExpr = EnumTranslation.BuildExpression(enumType, func);
          var cp = Activator.CreateInstance(constructedClass, enumExpr) as CompiledExpression;
          if (cp != null)
          {
            enumExpressions[enumType] = cp;
          }
          return cp;
        }
        return enumExpressions[enumType];
      }
      catch (Exception ex)
      {
        Debug.WriteLine("Cannot construct the Enum expression for {0}, error occured: {1}", enumType.FullName, ex.Message);
      }
      return null;
    }

  }
}
