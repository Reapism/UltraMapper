﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace UltraMapper.Internals
{
    //Generating getter/setter expression from MemberInfo does not preserve the entry instance type
    //if the member is extracted from a complex expression chain 
    //(for example in the expression 'a => a.PropertyA.PropertyB.PropertyC'
    //the ReflectedType info of PropertyC (that should be of type 'a') is lost (and will be of the type of 'PropertyC'))
    public static class GetterSetterLambdaExpressionBuilders
    {
        public static LambdaExpression GetGetterLambdaExpression( this MemberInfo memberInfo )
        {
            if( memberInfo is FieldInfo )
                return GetGetterLambdaExpression( (FieldInfo)memberInfo );

            if( memberInfo is PropertyInfo )
                return GetGetterLambdaExpression( (PropertyInfo)memberInfo );

            if( memberInfo is MethodInfo )
                return GetGetterLambdaExpression( (MethodInfo)memberInfo );

            throw new ArgumentException( $"'{memberInfo}' is not supported." );
        }

        public static LambdaExpression GetSetterLambdaExpression( this MemberInfo memberInfo )
        {
            if( memberInfo is Type type )
            {
                // (target, value) => target.field;

                var targetInstance = Expression.Parameter( type, "target" );
                var value = Expression.Parameter( type, "value" );

                var body = Expression.Assign( targetInstance, value );
                var delegateType = typeof( Action<,> ).MakeGenericType( type, type );

                return LambdaExpression.Lambda( delegateType, body, targetInstance, value );
            }

            if( memberInfo is FieldInfo fieldInfo )
                return GetSetterLambdaExpression( fieldInfo );

            if( memberInfo is PropertyInfo propertyInfo )
                return GetSetterLambdaExpression( propertyInfo );

            if( memberInfo is MethodInfo methodInfo )
                return GetSetterLambdaExpression( methodInfo );

            throw new ArgumentException( $"'{memberInfo}' is not supported." );
        }

        public static LambdaExpression GetGetterLambdaExpression( this FieldInfo fieldInfo )
        {
            // (target) => target.field;

            var targetInstance = Expression.Parameter( fieldInfo.ReflectedType, "target" );
            var body = Expression.Field( targetInstance, fieldInfo );

            var delegateType = typeof( Func<,> ).MakeGenericType(
                fieldInfo.ReflectedType, fieldInfo.FieldType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance );
        }

        public static LambdaExpression GetSetterLambdaExpression( this FieldInfo fieldInfo )
        {
            // (target, value) => target.field = value;
            var targetInstance = Expression.Parameter( fieldInfo.ReflectedType, "target" );
            var value = Expression.Parameter( fieldInfo.FieldType, "value" );

            var fieldExp = Expression.Field( targetInstance, fieldInfo );
            var body = Expression.Assign( fieldExp, value );

            var delegateType = typeof( Action<,> ).MakeGenericType(
                fieldInfo.ReflectedType, fieldInfo.FieldType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance, value );
        }

        public static LambdaExpression GetGetterLambdaExpression( this PropertyInfo propertyInfo )
        {
            // (target) => target.get_Property()
            var targetType = propertyInfo.ReflectedType;
            var methodInfo = propertyInfo.GetGetMethod( true );

            var targetInstance = Expression.Parameter( targetType, "target" );
            var body = Expression.Call( targetInstance, methodInfo );

            var delegateType = typeof( Func<,> ).MakeGenericType(
                targetType, propertyInfo.PropertyType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance );
        }

        public static LambdaExpression GetSetterLambdaExpression( this PropertyInfo propertyInfo )
        {
            // (target, value) => target.set_Property( value )
            var methodInfo = propertyInfo.GetSetMethod();
            if( methodInfo == null )
                throw new ArgumentException( $"'{propertyInfo}' does not provide a setter method." );

            var targetInstance = Expression.Parameter( propertyInfo.ReflectedType, "target" );
            var value = Expression.Parameter( propertyInfo.PropertyType, "value" );

            var body = Expression.Call( targetInstance, methodInfo, value );

            var delegateType = typeof( Action<,> ).MakeGenericType(
                propertyInfo.ReflectedType, propertyInfo.PropertyType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance, value );
        }

        public static LambdaExpression GetGetterLambdaExpression( this MethodInfo methodInfo )
        {
            if( methodInfo.GetParameters().Length > 0 )
                throw new NotImplementedException( "Only parameterless methods are supported" );

            var targetType = methodInfo.ReflectedType;

            var targetInstance = Expression.Parameter( targetType, "target" );
            var body = Expression.Call( targetInstance, methodInfo );

            var delegateType = typeof( Func<,> ).MakeGenericType(
                methodInfo.ReflectedType, methodInfo.ReturnType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance );
        }

        public static LambdaExpression GetSetterLambdaExpression( this MethodInfo methodInfo )
        {
            if( methodInfo.GetParameters().Length != 1 )
                throw new NotImplementedException( $"Only methods taking as input exactly one parameter are supported." );

            var targetInstance = Expression.Parameter( methodInfo.ReflectedType, "target" );
            var value = Expression.Parameter( methodInfo.GetParameters()[ 0 ].ParameterType, "value" );

            var body = Expression.Call( targetInstance, methodInfo, value );

            var delegateType = typeof( Action<,> ).MakeGenericType(
                methodInfo.ReflectedType, methodInfo.GetParameters()[ 0 ].ParameterType );

            return LambdaExpression.Lambda( delegateType, body, targetInstance, value );
        }

        public static LambdaExpression GetSetterLambdaExpressionInstantiateNullInstances( this MemberAccessPath memberAccessPath )
        {
            var instanceType = memberAccessPath.First().ReflectedType;
            var valueType = memberAccessPath.Last().GetMemberType();
            var value = Expression.Parameter( valueType, "value" );

            var entryInstance = Expression.Parameter( instanceType, "instance" );

            Expression accessPath = entryInstance;
            var memberAccesses = new List<Expression>();

            foreach( var memberAccess in memberAccessPath )
            {
                if( memberAccess is MethodInfo methodInfo )
                {
                    if( methodInfo.IsGetterMethod() )
                        accessPath = Expression.Call( accessPath, methodInfo );
                    else
                        accessPath = Expression.Call( accessPath, methodInfo, value );
                }
                else
                    accessPath = Expression.MakeMemberAccess( accessPath, memberAccess );

                memberAccesses.Add( accessPath );
            }

            if( !(accessPath is MethodCallExpression) )
                accessPath = Expression.Assign( accessPath, value );

            var nullConstant = Expression.Constant( null );
            var nullChecks = memberAccesses.Take( memberAccesses.Count - 1 ).Select( ( memberAccess, i ) =>
            {
                if( memberAccessPath[ i ] is MethodInfo methodInfo )
                {
                    //nested method calls like GetCustomer().SetName() include non-writable member (GetCustomer).
                    //Assigning a new instance in that case is more difficult.
                    //In that case 'by convention' we should look for:
                    // - A property named Customer
                    // - A method named SetCustomer(argument type = getter return type) 
                    //      (also take into account Set, Set_, set, set_) as for convention.

                    var bindingAttributes = BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic;

                    string setterMethodName = null;
                    if( methodInfo.Name.StartsWith( "Get" ) )
                        setterMethodName = methodInfo.Name.Replace( "Get", "Set" );
                    else if( methodInfo.Name.StartsWith( "get" ) )
                        setterMethodName = methodInfo.Name.Replace( "get", "set" );
                    else if( methodInfo.Name.StartsWith( "Get_" ) )
                        setterMethodName = methodInfo.Name.Replace( "Get_", "Set_" );
                    else if( methodInfo.Name.StartsWith( "get_" ) )
                        setterMethodName = methodInfo.Name.Replace( "get_", "set_" );

                    var setterMethod = methodInfo.ReflectedType.GetMethod( setterMethodName, bindingAttributes );

                    Expression setterAccessPath = entryInstance;
                    for( int j = 0; j < i; j++ )
                    {
                        if( memberAccessPath[ j ] is MethodInfo mi )
                        {
                            if( mi.IsGetterMethod() )
                                setterAccessPath = Expression.Call( accessPath, mi );
                            else
                                setterAccessPath = Expression.Call( accessPath, mi, value );
                        }
                        else
                            setterAccessPath = Expression.MakeMemberAccess( setterAccessPath, memberAccessPath[ j ] );
                    }

                    setterAccessPath = Expression.Call( setterAccessPath, setterMethod, Expression.New( memberAccess.Type ) );
                    var equalsNull = Expression.Equal( memberAccess, nullConstant );
                    return (Expression)Expression.IfThen( equalsNull, setterAccessPath );
                }
                else
                {
                    var createInstance = Expression.Assign( memberAccess, Expression.New( memberAccess.Type ) );
                    var equalsNull = Expression.Equal( memberAccess, nullConstant );
                    return (Expression)Expression.IfThen( equalsNull, createInstance );
                }

            } ).Where( nc => nc != null ).ToList();

            var exp = Expression.Block
            (
                nullChecks.Any() ? Expression.Block( nullChecks.ToArray() )
                    : (Expression)Expression.Empty(),

                accessPath
            );

            var delegateType = typeof( Action<,> ).MakeGenericType( instanceType, valueType );
            return LambdaExpression.Lambda( delegateType, exp, entryInstance, value );
        }
    }
}
