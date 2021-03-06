﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace UltraMapper.Tests
{
    /// <summary>
    /// Test all the possible kinds of member selection combinations
    /// (fields, properties and method calls ).
    /// </summary>
    [TestClass]
    public class ValueAccessTests
    {
        private class TestType
        {
            public string FieldA;

            public string PropertyA
            {
                get { return FieldA; }
                set { FieldA = value; }
            }

            public string GetFieldA() { return FieldA; }
            public void SetFieldA( string value ) { FieldA = value; }
        }

        [TestMethod]
        public void PropertyToProperty()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.PropertyA, t => t.PropertyA )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void PropertyToField()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.PropertyA, t => t.FieldA )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void PropertyToSetterMethod()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.PropertyA, t => t.GetFieldA(), ( t, val ) => t.SetFieldA( val ) )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void FieldToField()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg =>
                {
                    //cfg.GlobalConfiguration.IgnoreConventions = true;

                    cfg.MapTypes<TestType, TestType>()
                            .MapMember( s => s.FieldA, t => t.FieldA );
                }
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void FieldToProperty()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.FieldA, t => t.PropertyA )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void FieldToSetterMethod()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.FieldA, t => t.GetFieldA(), ( t, val ) => t.SetFieldA( val ) )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void GetterMethodToSetterMethod()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.GetFieldA(), t => t.GetFieldA(), ( t, val ) => t.SetFieldA( val ) )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void GetterMethodToProperty()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.GetFieldA(), t => t.PropertyA )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }

        [TestMethod]
        public void GetterMethodToField()
        {
            var source = new TestType() { PropertyA = "test" };
            var target = new TestType() { PropertyA = "overwrite this" };

            var ultraMapper = new Mapper
            (
                cfg => cfg.MapTypes<TestType, TestType>()
                    .MapMember( s => s.GetFieldA(), t => t.FieldA )
            );
            ultraMapper.Map( source, target );

            Assert.IsTrue( !Object.ReferenceEquals( source, target ) );
            Assert.IsTrue( source.PropertyA == target.PropertyA );
        }
    }
}
