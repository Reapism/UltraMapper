﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using TypeMapper.Internals;

namespace TypeMapper.Mappers
{
    /*NOTES:
    * 
    *- Stack<T> and other LIFO collections require the list to be read in reverse  
    * to preserve order and have a specular clone. This is done with Stack<T> by
    * creating the list two times: 'new Stack( new Stack( sourceCollection ) )'
    * 
    */
    public class StackMapper : CollectionMapper
    {
        public StackMapper( MapperConfiguration configuration )
            : base( configuration ) { }

        public override bool CanHandle( Type source, Type target )
        {
            return base.CanHandle( source, target ) &&
                target.GetGenericTypeDefinition() == typeof( Stack<> );
        }

        protected override MethodInfo GetTargetCollectionAddMethod( CollectionMapperContext context )
        {
            return context.TargetInstance.Type.GetMethod( "Push" );
        }

        protected override Expression GetSimpleTypeInnerBody( CollectionMapperContext context )
        {
            var constructorInfo = this.GetTargetCollectionConstructorFromCollection( context );

            return Expression.Block
            (
                base.GetSimpleTypeInnerBody( context ),
                Expression.Assign( context.TargetInstance, Expression.New( constructorInfo, context.TargetInstance ) )
            );
        }

        protected override Expression GetComplexTypeInnerBody( CollectionMapperContext context )
        {
            var addMethod = this.GetTargetCollectionAddMethod( context );
            var constructorInfo = this.GetTargetCollectionConstructorFromCollection( context );

            //avoids add calls on the Stack because the source and target collection
            //will not be identical (one will be reversed).
            //avoids add calls by creating a temporary list.

            var tempCollectionType = typeof( Stack<> ).MakeGenericType( context.TargetCollectionElementType );
            var tempCollection = Expression.Parameter( tempCollectionType, "tempCollection" );

            var constructorWithCapacity = tempCollectionType.GetConstructor( new Type[] { typeof( int ) } );
            var getCountMethod = context.SourceInstance.Type.GetProperty( "Count" ).GetGetMethod();

            var newTempCollectionExp = Expression.New( constructorWithCapacity,
                Expression.Call( context.SourceInstance, getCountMethod ) );

            return Expression.Block
            (
                new[] { tempCollection },

                Expression.Assign( tempCollection, newTempCollectionExp ),
                CollectionLoopWithReferenceTracking( context, tempCollection, addMethod ),
                Expression.Assign( context.TargetInstance, Expression.New( constructorInfo, tempCollection ) )
            );
        }
    }
}