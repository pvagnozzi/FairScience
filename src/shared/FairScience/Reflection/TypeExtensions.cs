using System.Linq.Expressions;

namespace FairScience.Reflection;

public static class TypeExtensions
{
	public static (Expression NewExpression, ParameterExpression[] ParameterExpressions) BuildNewExpression(
		this Type type, Type[] parameterTypes)
	{
		var ctor = type.GetConstructor(parameterTypes);

		if (ctor is null)
		{
			throw new ArgumentException(
				$"{type.FullName} has no contructor for {string.Join(", ", parameterTypes.Select(x => x.FullName))}");
		}

		var parameterExpressions = parameterTypes.Select(Expression.Parameter).ToArray();
		var newExpression = Expression.New(ctor, parameterExpressions.Cast<Expression>());

		return (newExpression, parameterExpressions);
	}

	public static Func<T1, TResult> BuildConstructor<T1, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}

	public static Func<T1, T2, TResult> BuildConstructor<T1, T2, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1), typeof(T2) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, T2, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}

	public static Func<T1, T2, T3, TResult> BuildConstructor<T1, T2, T3, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1), typeof(T2), typeof(T3) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, T2, T3, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}

	public static Func<T1, T2, T3, T4, TResult> BuildConstructor<T1, T2, T3, T4, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, T2, T3, T4, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}

	public static Func<T1, T2, T3, T4, T5, TResult> BuildConstructor<T1, T2, T3, T4, T5, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, T2, T3, T4, T5, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}
	public static Func<T1, T2, T3, T4, T5, T6, TResult> BuildConstructor<T1, T2, T3, T4, T5, T6, TResult>(this Type type)
	{
		var parameterTypes = new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) };
		var (newExpression, parameterExpressions) = type.BuildNewExpression(parameterTypes);
		var lambda = Expression.Lambda<Func<T1, T2, T3, T4, T5, T6, TResult>>(newExpression, parameterExpressions);
		return lambda.Compile();
	}
}
