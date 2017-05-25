﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders.MapperContexts;

namespace UltraMapper.MappingExpressionBuilders
{
    public class ReferenceMapper : IMappingExpressionBuilder, IMemberMappingExpression
    {
        protected readonly Mapper _mapper;
        public readonly Configuration MapperConfiguration;

        public ReferenceMapper( Configuration configuration )
        {
            this.MapperConfiguration = configuration;
            _mapper = new Mapper( configuration );
        }

#if DEBUG
        private static void debug( object o ) => Console.WriteLine( o );

        public static readonly Expression<Action<object>> debugExp =
            ( o ) => debug( o );
#endif

        public static Func<ReferenceTracking, object, Type, object> refTrackingLookup =
         ( referenceTracker, sourceInstance, targetType ) =>
         {
             object targetInstance;
             referenceTracker.TryGetValue( sourceInstance, targetType, out targetInstance );

             return targetInstance;
         };

        public static Action<ReferenceTracking, object, Type, object> addToTracker =
            ( referenceTracker, sourceInstance, targetType, targetInstance ) =>
        {
            referenceTracker.Add( sourceInstance, targetType, targetInstance );
        };

        public virtual bool CanHandle( Type source, Type target )
        {
            bool builtInTypes = source.IsBuiltInType( false )
                && target.IsBuiltInType( false );

            return !target.IsValueType && !builtInTypes;
        }

        protected virtual ReferenceMapperContext GetMapperContext( Type source, Type target, IMappingOptions options )
        {
            return new ReferenceMapperContext( source, target, options );
        }

        public virtual LambdaExpression GetMappingExpression( Type source, Type target, IMappingOptions options )
        {
            var context = this.GetMapperContext( source, target, options );

            var typeMapping = MapperConfiguration[ context.SourceInstance.Type, context.TargetInstance.Type ];
            var memberMappings = this.GetMemberMappings( typeMapping )
                .ReplaceParameter( context.Mapper, context.Mapper.Name )
                .ReplaceParameter( context.ReferenceTracker, context.ReferenceTracker.Name )
                .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name )
                .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name );

            var expression = Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( _mapper ) ),

                memberMappings,
                this.GetExpressionBody( context )
            );

            var delegateType = typeof( Action<,,> ).MakeGenericType(
                context.ReferenceTracker.Type, context.SourceInstance.Type,
                context.TargetInstance.Type );

            return Expression.Lambda( delegateType, expression,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }

        protected virtual Expression GetExpressionBody( ReferenceMapperContext contextObj )
        {
            return Expression.Empty();
        }

        public virtual Expression GetMemberAssignment( MemberMappingContext context )
        {
            Expression newInstanceExp = this.GetMemberNewInstance( context );

            bool isCreateNewInstance = context.Options.ReferenceBehavior ==
                ReferenceBehaviors.CREATE_NEW_INSTANCE;

            if( isCreateNewInstance || context.TargetMemberValueGetter == null )
                return Expression.Assign( context.TargetMember, newInstanceExp );

            return Expression.Block
            (
                Expression.Assign( context.TargetMember, context.TargetMemberValueGetter ),

                Expression.IfThen
                (
                    Expression.Equal( context.TargetMember, context.TargetMemberNullValue ),
                    Expression.Assign( context.TargetMember, newInstanceExp )
                )
            );
        }

        protected virtual Expression GetMemberNewInstance( MemberMappingContext context )
        {
            if( context.Options.CustomTargetConstructor != null )
                return Expression.Invoke( context.Options.CustomTargetConstructor );

            if( context.TargetMember.Type.IsInterface && context.TargetMember.Type.IsAssignableFrom( context.SourceMember.Type ) )
            {
                var createInstanceMethodInfo = typeof( Activator )
                    .GetMethods( BindingFlags.Static | BindingFlags.Public )
                    .Where( method => method.Name == nameof( Activator.CreateInstance ) )
                    .Select( method => new
                    {
                        Method = method,
                        Params = method.GetParameters(),
                        Args = method.GetGenericArguments()
                    } )
                    .Where( x => x.Params.Length == 0 && x.Args.Length == 1 )
                    .Select( x => x.Method )
                    .First();

                MethodInfo getType = typeof( object ).GetMethod( nameof( object.GetType ) );

                var getSourceType = Expression.Call( context.SourceMemberValueGetter, getType );
                var makeGenericMethodInfo = typeof( MethodInfo ).GetMethod( nameof( MethodInfo.MakeGenericMethod ) );
                var arrayParameter = Expression.Parameter( typeof( List<Type> ), "pararray" );
                var parArray = Expression.Call( null, typeof( Enumerable ).GetMethod( nameof( Enumerable.ToArray ) ).MakeGenericMethod( new[] { typeof( Type ) } ), arrayParameter );
                var createInstance = Expression.Call( Expression.Constant( createInstanceMethodInfo ), makeGenericMethodInfo, parArray );

                return Expression.Block
                (
                    new[] { arrayParameter },
                    Expression.Assign( arrayParameter, Expression.New( typeof( List<Type> ) ) ),
                    Expression.Call( arrayParameter, typeof( List<Type> ).GetMethod( nameof( List<Type>.Add ) ), getSourceType ),
                    Expression.Convert(
                       Expression.Call( createInstance, typeof( MethodInfo ).GetMethod( nameof( MethodInfo.Invoke ), new[] { typeof( object ), typeof( object[] ) } ),
                       Expression.Constant( null ), Expression.Constant( null, typeof( object[] ) ) ), context.TargetMember.Type )
                );
            }

            ////If we are mapping on the same type we prefer to use exactly the 
            ////same runtime type used in the source. SHOULD WE?! (By the way not enough. The mapping
            ////still inspects a class declaration) 
            //if( context.SourceMember.Type == context.TargetMember.Type )
            //{
            //    MethodInfo getTypeMethodInfo = typeof( object ).GetMethod( nameof( object.GetType ) );
            //    var getSourceType = Expression.Call( context.SourceMemberValueGetter, getTypeMethodInfo );

            //    return Expression.Convert( Expression.Call( null, typeof( InstanceFactory ).GetMethods()[ 1 ],
            //        getSourceType, Expression.Constant( null, typeof( object[] ) ) ), context.TargetMember.Type );
            //}

            return Expression.New( context.TargetMember.Type );
        }

        #region MemberMapping
        private static MemberMappingComparer _memberComparer = new MemberMappingComparer();

        private class MemberMappingComparer : IComparer<MemberMapping>
        {
            public int Compare( MemberMapping x, MemberMapping y )
            {
                int xCount = x.TargetMember.MemberAccessPath.Count;
                int yCount = y.TargetMember.MemberAccessPath.Count;

                if( xCount > yCount ) return 1;
                if( xCount < yCount ) return -1;

                return 0;
            }
        }

        protected Expression GetMemberMappings( TypeMapping typeMapping )
        {
            //since nested selectors are supported, we sort membermappings to grant
            //that we assign outer objects first
            var memberMappings = typeMapping.MemberMappings.Values.ToList();
            if( typeMapping.IgnoreMemberMappingResolvedByConvention )
            {
                memberMappings = memberMappings.Where( mapping =>
                    mapping.MappingResolution != MappingResolution.RESOLVED_BY_CONVENTION ).ToList();
            }

            var memberMappingExps = memberMappings
                .Where( mapping => !mapping.Ignore )
                .Where( mapping => !mapping.SourceMember.Ignore )
                .Where( mapping => !mapping.TargetMember.Ignore )
                .OrderBy( mapping => mapping, _memberComparer )
                .Select( mapping =>
                {
                    if( mapping.Mapper is ReferenceMapper )
                        return GetComplexMemberExpression( mapping );

                    return GetSimpleMemberExpression( mapping );
                } ).ToList();

            return !memberMappingExps.Any() ? (Expression)Expression.Empty() : Expression.Block( memberMappingExps );
        }

        private Expression GetComplexMemberExpression( MemberMapping mapping )
        {
            /* SOURCE (NULL) -> TARGET = NULL
             * 
             * SOURCE (NOT NULL / VALUE ALREADY TRACKED) -> TARGET (NULL) = ASSIGN TRACKED OBJECT
             * SOURCE (NOT NULL / VALUE ALREADY TRACKED) -> TARGET (NOT NULL) = ASSIGN TRACKED OBJECT (the priority is to map identically the source to the target)
             * 
             * SOURCE (NOT NULL / VALUE UNTRACKED) -> TARGET (NULL) = ASSIGN NEW OBJECT 
             * SOURCE (NOT NULL / VALUE UNTRACKED) -> TARGET (NOT NULL) = KEEP USING INSTANCE OR CREATE NEW INSTANCE
             */

            var memberContext = new MemberMappingContext( mapping );

            var mapMethod = MemberMappingContext.RecursiveMapMethodInfo.MakeGenericMethod(
                memberContext.SourceMember.Type, memberContext.TargetMember.Type );

            Expression itemLookupCall = Expression.Call
            (
                Expression.Constant( refTrackingLookup.Target ),
                refTrackingLookup.Method,
                memberContext.ReferenceTracker,
                memberContext.SourceMember,
                Expression.Constant( memberContext.TargetMember.Type )
            );

            Expression itemCacheCall = Expression.Call
            (
                Expression.Constant( addToTracker.Target ),
                addToTracker.Method,
                memberContext.ReferenceTracker,
                memberContext.SourceMember,
                Expression.Constant( memberContext.TargetMember.Type ),
                memberContext.TargetMember
            );

            return Expression.Block
            (
                new[] { memberContext.TrackedReference, memberContext.SourceMember, memberContext.TargetMember },

                Expression.Assign( memberContext.SourceMember, memberContext.SourceMemberValueGetter ),

                Expression.IfThenElse
                (
                     Expression.Equal( memberContext.SourceMember, memberContext.SourceMemberNullValue ),

                     Expression.Assign( memberContext.TargetMember, memberContext.TargetMemberNullValue ),

                     Expression.Block
                     (
                        //object lookup. An intermediate variable (TrackedReference) is needed in order to deal with ReferenceMappingStrategies
                        Expression.Assign( memberContext.TrackedReference,
                            Expression.Convert( itemLookupCall, memberContext.TargetMember.Type ) ),

                        Expression.IfThenElse
                        (
                            Expression.NotEqual( memberContext.TrackedReference, memberContext.TargetMemberNullValue ),
                            Expression.Assign( memberContext.TargetMember, memberContext.TrackedReference ),
                            Expression.Block
                            (
                                ((IMemberMappingExpression)mapping.Mapper)
                                    .GetMemberAssignment( memberContext ),

                                //cache reference
                                itemCacheCall,

                                memberContext.InitializationComplete ? (Expression)Expression.Empty() :
                                    Expression.Call( memberContext.Mapper, mapMethod, memberContext.SourceMember,
                                    memberContext.TargetMember, memberContext.ReferenceTracker, Expression.Constant( mapping ) )
                            )
                        )
                    )
                ),

                memberContext.TargetMemberValueSetter
            );
        }

        private Expression GetSimpleMemberExpression( MemberMapping mapping )
        {
            var memberContext = new MemberMappingContext( mapping );

            var targetSetterInstanceParamName = mapping.TargetMember.ValueSetter.Parameters[ 0 ].Name;
            var targetSetterValueParamName = mapping.TargetMember.ValueSetter.Parameters[ 1 ].Name;

            var valueReaderExp = mapping.MappingExpression.Body.ReplaceParameter(
                memberContext.SourceMemberValueGetter, mapping.MappingExpression.Parameters[ 0 ].Name );

            return mapping.TargetMember.ValueSetter.Body
                .ReplaceParameter( memberContext.TargetInstance, targetSetterInstanceParamName )
                .ReplaceParameter( valueReaderExp, targetSetterValueParamName );
        }
        #endregion
    }
}
