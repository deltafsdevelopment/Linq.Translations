// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)
namespace Microsoft.Linq.Translations
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;
  using System.Runtime.CompilerServices;

  /// <summary>
  /// Extension methods over IQueryable to turn on expression translation via a
  /// specified or default TranslationMap.
  /// </summary>
  public static class ExpressiveExtensions
  {
    private static readonly ConcurrentBag<Type> types = new ConcurrentBag<Type>();
    private static readonly ConcurrentBag<Type> exportedTypes = new ConcurrentBag<Type>();

    /// <summary>
    ///  Fluent hint to WithTranslations to not translate this member
    /// </summary>
    /// <remarks>Useful when overriding a persisted field with a calculated expression to
    ///  prevent circular references</remarks>
    public static T NoTranslate<T>(this T item)
    {
      return item;
    }

    /// <summary>
    /// Create a new <see cref="IQueryable{T}"/> based upon the
    /// <paramref name="source"/> with the translatable properties decomposed back
    /// into their expression trees ready for translation to a remote provider using
    /// the default <see cref="TranslationMap"/>.
    /// </summary>
    /// <typeparam name="T">Result type of the query.</typeparam>
    /// <param name="source">Source query to translate.</param>
    /// <returns><see cref="IQueryable{T}"/> containing translated query.</returns>
    public static IQueryable<T> WithTranslations<T>(this IQueryable<T> source)
    {
      if (source == null) throw new ArgumentNullException(nameof(source));
      return source.Provider.CreateQuery<T>(WithTranslations(source.Expression));
    }

    /// <summary>
    /// Create a new <see cref="IQueryable{T}"/> based upon the
    /// <paramref name="source"/> with the translatable properties decomposed back
    /// into their expression trees ready for translation to a remote provider using
    /// a specific <paramref name="map"/>.
    /// </summary>
    /// <typeparam name="T">Result type of the query.</typeparam>
    /// <param name="source">Source query to translate.</param>
    /// <param name="map"><see cref="TranslationMap"/> used to translate property accesses.</param>
    /// <returns><see cref="IQueryable{T}"/> containing translated query.</returns>
    public static IQueryable<T> WithTranslations<T>(this IQueryable<T> source, TranslationMap map)
    {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (map == null) throw new ArgumentNullException(nameof(map));

      return source.Provider.CreateQuery<T>(WithTranslations(source.Expression, map));
    }

    internal static Expression<T> WithTranslations<T>(Expression<T> expression)
    {
      if (expression == null) throw new ArgumentNullException(nameof(expression));

      return WithTranslations(expression, TranslationMap.EvaluateMap) as Expression<T>;
    }

    /// <summary>
    /// Create a new <see cref="Expression"/> tree based upon the
    /// <paramref name="expression"/> with translatable properties decomposed back
    /// into their expression trees ready for translation to a remote provider using
    /// the default <see cref="TranslationMap"/>.
    /// </summary>
    /// <param name="expression">Expression tree to translate.</param>
    /// <returns><see cref="Expression"/> tree with translatable expressions translated.</returns>
    public static Expression WithTranslations(Expression expression)
    {
      if (expression == null) throw new ArgumentNullException(nameof(expression));

      return WithTranslations(expression, TranslationMap.DefaultMap);
    }

    /// <summary>
    /// Create a new <see cref="Expression"/> tree based upon the
    /// <paramref name="expression"/> with translatable properties decomposed back
    /// into their expression trees ready for translation to a remote provider using
    /// the default <see cref="TranslationMap"/>.
    /// </summary>
    /// <param name="expression">Expression tree to translate.</param>
    /// <param name="map"><see cref="TranslationMap"/> used to translate property accesses.</param>
    /// <returns><see cref="Expression"/> tree with translatable expressions translated.</returns>
    public static Expression WithTranslations(Expression expression, TranslationMap map)
    {
      if (expression == null) throw new ArgumentNullException(nameof(expression));
      if (map == null) throw new ArgumentNullException(nameof(map));

      var result = new TranslatingVisitor(map).Visit(expression);
      return result;
    }

    internal static void EnsureTypeInitialized(Type type, Boolean initialiseSubTypes = true)
    {
      if (type == null) throw new ArgumentNullException(nameof(type));
      if (!types.Contains(type))
      {
        try
        {
          // Ensure the static members are accessed class' ctor
          RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
        catch (TypeInitializationException ex)
        {
          Console.WriteLine(ex.Message);
        }
        types.Add(type);
        if (initialiseSubTypes)
        {
          EnsureSubTypesInitialized(type);
        }
      }
    }

    private static void EnsureSubTypesInitialized(Type type)
    {
      if (type == null) throw new ArgumentNullException(nameof(type));
      if (type == typeof(Enum))
      {
        return;
      }
      foreach (var subType in exportedTypes.Where(t => type.IsAssignableFrom(t)))
      {
        EnsureTypeInitialized(subType, false);
      }
    }

    private static void EnsureBaseTranslationsInitialized()
    {
      foreach (var type in exportedTypes.Where(t => t.GetCustomAttribute<BaseTranslationsAttribute>(false) != null))
      {
        EnsureTypeInitialized(type);
      }
    }

    private static void EnsureTypesInitialized(Assembly assem)
    {
      try
      {
        if (assem != null && !assem.IsDynamic)
        {
          foreach (var type in assem.ExportedTypes.Where(t => !t.Namespace?.StartsWith("System") ?? false))
          {
            if (!exportedTypes.Contains(type))
            {
              exportedTypes.Add(type);
            }
          }
        }
      }
      catch (Exception ex)
      {
        //swallow exceptions as some assemblies may not export any types
        Debug.WriteLine("Unable to access assembly {0} to check for translations, error: {1}", assem.FullName, ex.Message);
      }
      EnsureBaseTranslationsInitialized();
    }

    /// <summary>
    /// Extends the expression visitor to translate properties to expressions
    /// according to the provided translation map.
    /// </summary>
    private class TranslatingVisitor : ExpressionVisitor
    {
      private readonly Stack<Expression> skipped = new Stack<Expression>();
      private readonly Stack<CompiledExpression> visited = new Stack<CompiledExpression>();
      private readonly Stack<KeyValuePair<ParameterExpression, Expression>> bindings = new Stack<KeyValuePair<ParameterExpression, Expression>>();
      private readonly TranslationMap map;

      static TranslatingVisitor()
      {
        // initialise the base translations if any are defined
        foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
        {
          EnsureTypesInitialized(assem);
        }
        // ensure any subsequent assemblies loaded are parsed for translations too
        AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
        {
          EnsureTypesInitialized(args.LoadedAssembly);
        };
      }

      internal TranslatingVisitor(TranslationMap map)
      {
        this.map = map ?? throw new ArgumentNullException(nameof(map));
      }

      /// <summary>
      ///  Walk up the inheritance heirarchy searching for a compiled expression attached to a
      ///  property of the given name
      /// </summary>
      /// <param name="memberName">Name of the property to search for</param>
      /// <param name="type">Type of the property to search against</param>
      /// <returns>Compiled expression if found or null if not</returns>
      private CompiledExpression FindCompiledExpressionMember(String memberName, Type type)
      {
        Boolean useOverride = true;
        while (type != typeof(Object) && type != null)
        {
          MemberInfo[] mis = type.GetMember(memberName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
          if (mis != null && mis.Length > 0)
          {
            EnsureTypeInitialized(type);
            if (mis[0] != null && map.TryGetValue(mis[0], out CompiledExpression cp))
            {
              cp.UseOverride = useOverride;
              return cp;
            }
          }
          type = type.BaseType;
          useOverride = false;
        }
        return null;
      }

      /// <summary>
      ///  Search for a compiled expression mapped to the given parameterless method
      /// </summary>
      /// <param name="methodName">Name of the method to search for</param>
      /// <param name="type">Type to search against</param>
      /// <returns>Compiled expression if found or null if not</returns>
      private CompiledExpression FindCompiledExpressionMethod(String methodName, Type type, Expression expression)
      {
        Type enumType = null;
        while (type != typeof(Object) && type != null)
        {
          MethodInfo mi = type.SafeGetPublicMethod(methodName);
          if (mi != null)
          {
            switch (methodName)
            {
              case "ToString":
                var memberExp = expression as MemberExpression;
                if (memberExp != null)
                {
                  var pi = memberExp.Member as PropertyInfo;
                  if (pi != null && pi.PropertyType.IsEnum)
                  {
                    if (enumType == null)
                    {
                      type = pi.PropertyType;
                      enumType = type;
                      var expressionType = pi.PropertyType.Assembly.GetType(type.FullName + "Expressions");
                      if (expressionType != null)
                      {
                        EnsureTypeInitialized(expressionType);
                      }
                    }
                  }

                }
                break;

              default:
                break;
            }

            EnsureTypeInitialized(type);
            if (this.map.TryGetValue(mi, out CompiledExpression cp))
            {
              if (enumType != null)
              {
                cp = EnumTranslation.SafeGetEnumCompiledExpression(enumType, cp.UnderlyingLambda.Compile());
              }
              return cp;
            }
          }
          type = type.BaseType;
        }

        return null;
      }

      protected override Expression VisitLambda<T>(Expression<T> node)
      {
        var result = base.VisitLambda(node);
        return result;
      }

      protected override Expression VisitMember(MemberExpression node)
      {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (skipped.Contains(node))
        {
          // elected to not translate this expression
          return base.VisitMember(node);
        }
        if (node.Expression != null)
        {
          Type type = node.Expression.Type;
          String propName = node.Member.Name;
          CompiledExpression cp = this.FindCompiledExpressionMember(propName, type);
          if (cp != null)
          {
            return this.VisitCompiledExpression(cp, node.Expression);
          }
        }
        return base.VisitMember(node);
      }

      private Expression VisitCompiledExpression(CompiledExpression ce, Expression expression)
      {
        if (this.visited.Contains(ce))
        {
          throw new CircularReferenceException(ce);
        }
        this.visited.Push(ce);
        ParameterExpression param;
        LambdaExpression lambda;
        Expression body;
        if (ce.UseOverride && ce.OverrideLambda != null)
        {
          lambda = ce.OverrideLambda;
        }
        else
        {
          lambda = ce.UnderlyingLambda;
        }
        param = lambda.Parameters.Single();
        this.bindings.Push(new KeyValuePair<ParameterExpression, Expression>(param, expression));
        body = this.Visit(lambda.Body);
        this.bindings.Pop();
        this.visited.Pop();
        return body;
      }

      protected override Expression VisitParameter(ParameterExpression p)
      {
        var binding = bindings.FirstOrDefault(b => b.Key == p);
        return binding.Value == null ? base.VisitParameter(p) : Visit(binding.Value);
      }

      protected override Expression VisitMethodCall(MethodCallExpression node)
      {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.Method != null)
        {
          String methodName = node.Method.Name;
          if (node.Object != null)
          {
            Type type = node.Object.Type;
            CompiledExpression cp = FindCompiledExpressionMethod(methodName, type, node.Object);
            if (cp != null)
            {
              return VisitCompiledExpression(cp, node.Object);
            }
          }
          else
          {
            Expression result = TranslateStaticMethod(methodName, node);
            if (result != null)
            {
              return result;
            }
          }
        }
        return base.VisitMethodCall(node);
      }

      private Expression TranslateStaticMethod(String methodName, MethodCallExpression node)
      {
        try
        {
          if (node.Method.DeclaringType == typeof(ExpressiveExtensions))
          {
            switch (methodName)
            {
              case "NoTranslate":
                skipped.Push(node.Arguments[0]);
                var expression = Visit(node.Arguments[0]);
                skipped.Pop();
                return expression;
              default:
                break;
            }
          }
          else if (node.Method.DeclaringType == typeof(String))
          {
            switch (methodName)
            {
              case "Join":
                {
                  MethodInfo method = typeof(StringTranslation).GetMethod("BuildExpressionForJoin", BindingFlags.NonPublic | BindingFlags.Static);
                  ConstantExpression separator = (ConstantExpression)node.Arguments[0];
                  NewArrayExpression exp = (NewArrayExpression)node.Arguments[1];
                  IList<Expression> arr = exp.Expressions;
                  MemberExpression member = arr.OfType<MemberExpression>().FirstOrDefault();
                  MethodInfo generic = method.MakeGenericMethod(member.Expression.Type, arr.GetType());
                  LambdaExpression lambda = (LambdaExpression)generic.Invoke(null, new Object[] { separator.Value, arr });
                  bindings.Push(new KeyValuePair<ParameterExpression, Expression>(lambda.Parameters.Single(), member.Expression));
                  Expression result = Visit(lambda.Body);
                  bindings.Pop();
                  return result;
                }
              default:
                break;
            }
          }
        }
        catch (Exception ex)
        {
          //swallow exceptions and fail over to the default Not Supported in Linq-to-Entities message
          Debug.WriteLine("Unable to translate the call to method {0} - {1}", methodName, ex.Message);
        }
        return null;
      }

      protected override Expression VisitTypeBinary(TypeBinaryExpression node)
      {
        if (!node.TypeOperand.IsAssignableFrom(node.Expression.Type))
        {
          Type baseType = null;
          var param = node.Expression as ParameterExpression;
          if (param != null)
          {
            // attempt to fix polymorphic expression by casting to supertype
            baseType = bindings.FirstOrDefault().Key?.Type ?? typeof(Object);
            var cast = Expression.TypeAs(node.Expression, baseType);
            node = Expression.TypeIs(cast, node.TypeOperand);
          }
        }
        return base.VisitTypeBinary(node);
      }

    }
  }
  
  public class CircularReferenceException : Exception
  {
    public CircularReferenceException(CompiledExpression ce): base($"Circular reference detected for '{ce.UnderlyingLambda.ToString()}")
    {}
  }
}
