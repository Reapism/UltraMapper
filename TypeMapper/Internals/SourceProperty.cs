﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TypeMapper.Internals
{
    public class SourceProperty : PropertyBase
    {
        public LambdaExpression ValueGetter { get; set; }

        internal SourceProperty( PropertyInfo propertyInfo )
            : base( propertyInfo )
        {
            //((MemberExpression)propertySelector.Body).Member
            this.ValueGetter = base.PropertyInfo.GetGetterLambdaExpression();
        }
    }
}
