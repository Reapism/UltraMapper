﻿using System;
using System.Linq;
using System.Linq.Expressions;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Internals
{
    public class MemberMapping : IMemberOptions, IMapping
    {
        public readonly TypeMapping InstanceTypeMapping;
        public readonly MappingSource SourceMember;
        public readonly MappingTarget TargetMember;
        private string _toString;

        public MemberMapping( TypeMapping typeMapping,
            MappingSource sourceMember, MappingTarget targetMember )
        {
            this.InstanceTypeMapping = typeMapping;

            this.SourceMember = sourceMember;
            this.TargetMember = targetMember;
        }

        public MappingResolution MappingResolution { get; internal set; }
        public bool Ignore { get; set; }

        public LambdaExpression CollectionItemEqualityComparer { get; set; }

        private TypeMapping _memberTypeMapping;
        public TypeMapping MemberTypeMapping
        {
            get
            {
                if( _memberTypeMapping == null )
                {
                    _memberTypeMapping = InstanceTypeMapping.GlobalConfiguration[
                           SourceMember.MemberType, TargetMember.MemberType ];
                }

                return _memberTypeMapping;
            }
        }

        private LambdaExpression _customConverter;
        public LambdaExpression CustomConverter
        {
            get { return _customConverter ?? MemberTypeMapping.CustomConverter; }
            set { _customConverter = value; }
        }

        private LambdaExpression _customTargetConstructor;
        public LambdaExpression CustomTargetConstructor
        {
            get { return _customTargetConstructor ?? MemberTypeMapping.CustomTargetConstructor; }
            set { _customTargetConstructor = value; }
        }

        public ReferenceBehaviors ReferenceBehavior { get; set; } = ReferenceBehaviors.INHERIT;
        public CollectionBehaviors CollectionBehavior { get; set; } = CollectionBehaviors.INHERIT;

        private IMappingExpressionBuilder _mapper;
        public IMappingExpressionBuilder Mapper
        {
            get
            {
                if( _mapper == null )
                {
                    _mapper = InstanceTypeMapping.GlobalConfiguration
                        .Mappers.FirstOrDefault( mapper => mapper.CanHandle(
                            this.MemberTypeMapping.TypePair.SourceType,
                            this.MemberTypeMapping.TypePair.TargetType ) );

                    if( _mapper == null && this.CustomConverter == null )
                        throw new Exception( $"No object mapper can handle {this}" );
                }

                return _mapper;
            }
        }

        private LambdaExpression _mappingExpression;
        public LambdaExpression MappingExpression
        {
            get
            {
                if( this.CustomConverter != null )
                    return this.CustomConverter;

                if( _mappingExpression != null ) return _mappingExpression;

                var sourceType = this.MemberTypeMapping.TypePair.SourceType;
                var targetType = this.MemberTypeMapping.TypePair.TargetType;

                return _mappingExpression = this.Mapper.GetMappingExpression(
                    sourceType, targetType, this );
            }
        }

        private Action<ReferenceTracker, object, object> _mappingFunc;
        public Action<ReferenceTracker, object, object> MappingFunc
        {
            get
            {
                if( _mappingFunc != null ) return _mappingFunc;

                var sourceType = this.MemberTypeMapping.TypePair.SourceType;
                var targetType = this.MemberTypeMapping.TypePair.TargetType;

                return _mappingFunc = MappingExpressionBuilder.GetMappingFunc(
                   sourceType, targetType, this.MappingExpression );
            }
        }

        public override string ToString()
        {
            if( _toString == null )
                _toString = $"{this.SourceMember} -> {this.TargetMember}";

            return _toString;
        }
    }
}
