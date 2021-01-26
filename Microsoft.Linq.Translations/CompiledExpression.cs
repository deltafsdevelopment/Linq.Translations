// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)
namespace Microsoft.Linq.Translations
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;

  public abstract class CompiledExpression
  {
    public abstract LambdaExpression UnderlyingLambda { get; }
    public abstract LambdaExpression OverrideLambda { get; }
    public Boolean UseOverride { get; set; }
    public abstract void ProcessOverrides(KeyValuePair<MemberInfo, CompiledExpression>[] overrides);
    internal abstract Boolean IsAuto { get; }
    public static CompiledExpression MakeCompiledExpression(Type type,
                                                            Type returnType,
                                                            LambdaExpression lambda,
                                                            Boolean isOverride = false,
                                                            Boolean isAuto = false)
    {
      var cetypeopen = typeof(CompiledExpression<,>);
      Type[] typeArgs = { type, returnType };
      var cetypeclosed = cetypeopen.MakeGenericType(typeArgs);
      var ce = Activator.CreateInstance(cetypeclosed, new Object[] { lambda, isOverride, isAuto }) as CompiledExpression;
      return ce;
    }
  }

  /// <summary>
  /// Represents an expression and its compiled function.
  /// </summary>
  /// <typeparam name="T">Class the expression relates to.</typeparam>
  /// <typeparam name="TResult">Return type of the expression.</typeparam>
  public sealed class CompiledExpression<T, TResult> : CompiledExpression
  {
    private Expression<Func<T, TResult>> expression;
    private Func<T, TResult> function;
    private Expression<Func<T, TResult>> overrideExpression;
    private readonly Boolean isAuto;

    public CompiledExpression()
    {
    }

    public CompiledExpression(Expression<Func<T, TResult>> expression)
    {
      this.expression = expression ?? throw new ArgumentNullException(nameof(expression));
      this.function = ExpressiveExtensions.WithTranslations(expression).Compile();
    }

    public CompiledExpression(LambdaExpression expression, Boolean isOverride = false, Boolean isAuto = false)
    {
      if (expression == null) throw new ArgumentNullException(nameof(expression));
      this.expression = expression as Expression<Func<T, TResult>>;
      this.function = ExpressiveExtensions.WithTranslations(this.expression).Compile();
      if (isOverride)
      {
        this.overrideExpression = this.expression;
      }
      this.isAuto = true;
    }
    /// <summary>
    /// Evaluate a compiled expression against a specific instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instance">Specific instance of <typeparamref name="T"/> to evaluate this
    /// compiled expresion against.</param>
    /// <returns><typeparamref name="TResult"/> result from evaluating this compiled expression against <paramref name="instance"/>.</returns>
    public TResult Evaluate(T instance)
    {
      if (instance == null) throw new ArgumentNullException(nameof(instance));
      return this.function(instance);
    }

    public override LambdaExpression UnderlyingLambda
    {
      get { return expression; }
    }

    public override LambdaExpression OverrideLambda
    {
      get { return this.overrideExpression; }
    }

    internal override Boolean IsAuto => this.isAuto;

    public override void ProcessOverrides(KeyValuePair<MemberInfo, CompiledExpression>[] overrides)
    {
      if (overrides.Length > 0)
      {
        Array.Sort(overrides, (x, y) => x.Key.DeclaringType == y.Key.DeclaringType ? 0 :
                                        (x.Key.DeclaringType.IsAssignableFrom(y.Key.DeclaringType) ? -1 :
                                        (y.Key.DeclaringType.IsAssignableFrom(x.Key.DeclaringType) ? 1 : -1)));
        var param = this.expression.Parameters.Single();
        var ifelse = (this.overrideExpression ?? this.expression).Body;
        foreach (var other in overrides)
        {
          var typeIs = Expression.TypeIs(param, other.Key.DeclaringType);
          var otherParam = other.Value.UnderlyingLambda.Parameters.Single();
          var otherLambda = new ExpressionRebinder(otherParam, param).Visit(other.Value.UnderlyingLambda) as LambdaExpression;
          var ifso = otherLambda.Body;
          if (ifso.Type != ifelse.Type)
          {
            ifso = Expression.Convert(otherLambda.Body, ifelse.Type);
          }
          ifelse = Expression.Condition(typeIs, ifso, ifelse);
        }
        var lambda = Expression.Lambda<Func<T, TResult>>(ifelse, param);
        this.overrideExpression = lambda;
      }
    }

    public override String ToString()
    {
      return this.expression.ToString();
    }

  }

  public class ExpressionRebinder : ExpressionVisitor
  {
    private ParameterExpression old;
    private Expression replace;

    public ExpressionRebinder(ParameterExpression old, Expression replace)
    {
      this.old = old;
      this.replace = replace;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
      var type = node.Member.ReflectedType;
      if (replace != null && type != replace.Type && replace.Type.IsAssignableFrom(type))
      {
        var cast = Expression.TypeAs(replace, type);
        node = Expression.MakeMemberAccess(cast, node.Member);
      }
      return base.VisitMember(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
      if (node == this.old)
      {
        return replace;
      }
      return base.VisitParameter(node);
    }
  }
}