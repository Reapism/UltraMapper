﻿using System;
using System.Linq.Expressions;
using UltraMapper.Internals;

namespace UltraMapper.MappingExpressionBuilders
{
    public class CollectionMapperContext : ReferenceMapperContext
    {
        public Type SourceCollectionElementType { get; }
        public Type TargetCollectionElementType { get; }

        public bool IsSourceElementTypeBuiltIn { get; }
        public bool IsTargetElementTypeBuiltIn { get; }

        public ParameterExpression SourceCollectionLoopingVar { get; set; }

        public CollectionMapperContext( Type source, Type target, IMappingOptions options )
            : base( source, target, options )
        {
            SourceCollectionElementType = SourceInstance.Type.GetCollectionGenericType();
            TargetCollectionElementType = TargetInstance.Type.GetCollectionGenericType();

            IsSourceElementTypeBuiltIn = SourceCollectionElementType.IsBuiltIn( true );
            IsTargetElementTypeBuiltIn = TargetCollectionElementType.IsBuiltIn( true );

            SourceCollectionLoopingVar = Expression.Parameter( SourceCollectionElementType, "loopVar" );
        }
    }
}
