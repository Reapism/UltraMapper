﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using TypeMapper.Internals;

namespace TypeMapper.Mappers
{
    public class CollectionMapperContext : ReferenceMapperContext
    {
        public Type SourceCollectionElementType { get; set; }
        public Type TargetCollectionElementType { get; set; }

        public bool IsSourceElementTypeBuiltIn { get; set; }
        public bool IsTargetElementTypeBuiltIn { get; set; }

        public ParameterExpression SourceCollectionLoopingVar { get; set; }
        public MethodInfo AddToReturnList { get; internal set; }

        public CollectionMapperContext( Type source, Type target )
            : base( source, target ) { Initialize(); }

        private void Initialize()
        {
            var returnType = typeof( List<ObjectPair> );
            ReturnTypeConstructor = returnType.GetConstructor( new[] { typeof( int ) } );
            ReturnObject = Expression.Variable( returnType, "returnObject" );
            AddToReturnList = returnType.GetMethod( "Add" );

            SourceCollectionElementType = SourceInstance.Type.GetCollectionGenericType();
            TargetCollectionElementType = TargetInstance.Type.GetCollectionGenericType();

            IsSourceElementTypeBuiltIn = SourceCollectionElementType.IsBuiltInType( true );
            IsTargetElementTypeBuiltIn = TargetCollectionElementType.IsBuiltInType( true );

            SourceCollectionLoopingVar = Expression.Parameter( SourceCollectionElementType, "loopVar" );
        }
    }
}
